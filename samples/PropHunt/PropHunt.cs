using System;
using System.Collections.Generic;
using ASL.Api;
using Il2CppInterop.Runtime;
using Metater;
using Mirror;
using UnityEngine;

namespace PropHunt
{
    /// <summary>
    /// Multiplayer Prop Hunt. Aim at an object and press <b>G</b> to become it — your choice replicates
    /// through an ASL synced store (the object's Mirror net id, or a scene-hierarchy path for static
    /// objects), so EVERY client hides your model and rebuilds the same object on you, sitting on the
    /// floor. The host runs a timed round: one hunter, the rest are props; the hunter finds props by
    /// walking into them; props win if the timer runs out.
    /// </summary>
    public sealed class PropHunt : AslMod
    {
        private const string StoreId = "com.asl.prophunt/state";
        private const float CatchRange = 2.5f;
        private const float RoundSeconds = 150f;

        private IModContext _ctx;
        private IAslSync _state;
        private int _frame;

        // host-only round bookkeeping
        private float _roundEndsAt;
        private int _lastMilestone;
        private int _msgCounter;

        private sealed class Disguise { public GameObject Obj; public string Value; public float YOffset; }
        private readonly Dictionary<uint, Disguise> _disguises = new Dictionary<uint, Disguise>();

        public override void OnLoad(IModContext ctx)
        {
            _ctx = ctx;
            _state = ctx.Net.GetSync(StoreId);
            _state.Changed += OnStateChanged;

            ctx.Menu.AddLabel("Aim at an object and press G to become it (everyone sees it).");
            ctx.Menu.AddButton("Start Prop Hunt (host)", StartGame);
            ctx.Menu.AddButton("Stop Prop Hunt (host)", StopGame);
            ctx.Menu.AddButton("Become what I'm looking at", ToggleLookDisguise);

            ctx.Events.Update += OnUpdate;
            ctx.Events.SceneChanged += _ => _disguises.Clear();
            ctx.Log.Info("ASL Prop Hunt loaded. Aim at an object and press G to become it.");
        }

        private void OnUpdate()
        {
            _frame++;

            if (_ctx.Net.IsOnline)
            {
                try { if (Input.GetKeyDown(KeyCode.G)) ToggleLookDisguise(); } catch { }
            }

            // keep each disguise glued to its player, sitting on the floor
            if (_disguises.Count > 0 && _frame % 2 == 0)
            {
                foreach (var kv in _disguises)
                {
                    var mp = PlayerByNetId(kv.Key);
                    if (mp != null && kv.Value.Obj != null)
                    {
                        try { kv.Value.Obj.transform.position = mp.transform.position + new Vector3(0f, kv.Value.YOffset, 0f); } catch { }
                    }
                }
            }

            if (_frame % 20 == 0) ReconcileDisguises();
            if (_ctx.Net.IsServer && _frame % 10 == 0) HostGameTick();
        }

        // ---- disguise control ----

        private void ToggleLookDisguise()
        {
            var me = _ctx.Net.LocalPlayer;
            var mp = me != null ? me.Player : null;
            if (mp == null) { Announce("Get into a level first."); return; }

            string key = "d:" + me.NetId;
            if (!string.IsNullOrEmpty(_state.Get(key))) { _state.Set(key, ""); Announce("Back to normal."); return; }

            string value = "box";
            string what = "box";
            try
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    RaycastHit hit;
                    if (Physics.Raycast(cam.transform.position, cam.transform.forward, out hit, 6f))
                    {
                        what = hit.transform.name;
                        var nic = hit.transform.GetComponentInParent(Il2CppType.Of<NetworkIdentity>());
                        var ni = nic != null ? nic.TryCast<NetworkIdentity>() : null;
                        if (ni != null && ni.netId != 0) { value = "n:" + ni.netId; what = ni.name; }
                        else { var path = PathOf(hit.transform); if (path != null) { value = "p:" + path; } }   // static object -> hierarchy path
                    }
                }
            }
            catch (Exception ex) { _ctx.Log.Warning($"PropHunt: raycast failed: {ex.Message}"); }

            _state.Set(key, value);
            Announce(value == "box" ? "You're a box." : $"You are now a '{what}'!");
        }

        // ---- reconcile: each client applies per-player disguise from synced state ----

        private void ReconcileDisguises()
        {
            foreach (var p in _ctx.Net.Players)
            {
                string desired = _state.Get("d:" + p.NetId) ?? "";
                string applied = _disguises.TryGetValue(p.NetId, out var d) ? d.Value : "";
                if (desired == applied) continue;
                RemoveDisguise(p.NetId);
                if (!string.IsNullOrEmpty(desired)) ApplyDisguise(p.Player, p.NetId, desired);
            }

            var gone = new List<uint>();
            foreach (var kv in _disguises)
            {
                bool present = false;
                foreach (var p in _ctx.Net.Players) if (p.NetId == kv.Key) { present = true; break; }
                if (!present) gone.Add(kv.Key);
            }
            foreach (var nid in gone) RemoveDisguise(nid);
        }

        private void ApplyDisguise(MetaPlayer mp, uint netId, string value)
        {
            if (mp == null) return;
            SetPlayerRenderers(mp, false);

            var d = new Disguise { Value = value };
            try
            {
                Transform root = null;
                if (value != "box")
                {
                    if (value.StartsWith("n:")) { uint.TryParse(value.Substring(2), out var on); root = FindSpawnedTransform(on); }
                    else if (value.StartsWith("p:")) { root = FindByPath(mp, value.Substring(2)); }
                }
                d.Obj = root != null ? CopyMeshes(root, mp) : MakeBox(mp);
                d.YOffset = FloorAlign(d.Obj, mp);   // lift so it rests on the floor
            }
            catch (Exception ex) { _ctx.Log.Warning($"PropHunt: apply disguise failed: {ex.Message}"); }

            _disguises[netId] = d;
        }

        private void RemoveDisguise(uint netId)
        {
            if (!_disguises.TryGetValue(netId, out var d)) return;
            _disguises.Remove(netId);
            SetPlayerRenderers(PlayerByNetId(netId), true);
            if (d.Obj != null) { try { UnityEngine.Object.Destroy(d.Obj); } catch { } }
        }

        // ---- visuals ----

        private GameObject MakeBox(MetaPlayer mp)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "ASL_Prop";
            box.transform.localScale = new Vector3(1.1f, 1.1f, 1.1f);
            try { var col = box.GetComponent<Collider>(); if (col != null) col.enabled = false; } catch { }
            try { box.transform.position = mp.transform.position; } catch { }
            return box;
        }

        private GameObject CopyMeshes(Transform root, MetaPlayer mp)
        {
            var parts = new List<MeshFilter>();
            try
            {
                var mfs = root.GetComponentsInChildren(Il2CppType.Of<MeshFilter>(), false);
                if (mfs != null)
                    foreach (var c in mfs)
                    {
                        var mf = c != null ? c.TryCast<MeshFilter>() : null;
                        if (mf != null && mf.sharedMesh != null) parts.Add(mf);
                    }
            }
            catch (Exception ex) { _ctx.Log.Warning($"PropHunt: gather meshes failed: {ex.Message}"); }

            if (parts.Count == 0) return MakeBox(mp);

            var D = new GameObject("ASL_Prop");
            D.transform.position = root.position;
            D.transform.rotation = root.rotation;
            D.transform.localScale = root.lossyScale;

            foreach (var p in parts)
            {
                try
                {
                    var C = new GameObject("part");
                    var nmf = C.AddComponent(Il2CppType.Of<MeshFilter>()).TryCast<MeshFilter>();
                    var nmr = C.AddComponent(Il2CppType.Of<MeshRenderer>()).TryCast<MeshRenderer>();
                    if (nmf != null) nmf.sharedMesh = p.sharedMesh;
                    var prc = p.GetComponent(Il2CppType.Of<MeshRenderer>());
                    var pr = prc != null ? prc.TryCast<MeshRenderer>() : null;
                    if (nmr != null && pr != null) nmr.sharedMaterials = pr.sharedMaterials;
                    C.transform.position = p.transform.position;
                    C.transform.rotation = p.transform.rotation;
                    C.transform.localScale = p.transform.lossyScale;
                    C.transform.SetParent(D.transform, true);
                }
                catch (Exception ex) { _ctx.Log.Warning($"PropHunt: part copy failed: {ex.Message}"); }
            }

            try { D.transform.position = mp.transform.position; } catch { }
            return D;
        }

        // Lift the disguise so its lowest point sits at the player's feet (stops it sinking through the floor).
        private float FloorAlign(GameObject obj, MetaPlayer mp)
        {
            if (obj == null) return 0f;
            try
            {
                var rends = obj.GetComponentsInChildren(Il2CppType.Of<Renderer>(), false);
                bool any = false;
                Bounds b = new Bounds();
                if (rends != null)
                    foreach (var c in rends)
                    {
                        var r = c != null ? c.TryCast<Renderer>() : null;
                        if (r == null) continue;
                        if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                    }
                if (any)
                {
                    float yOffset = mp.transform.position.y - b.min.y;
                    var pos = obj.transform.position; pos.y += yOffset; obj.transform.position = pos;
                    return yOffset;
                }
            }
            catch (Exception ex) { _ctx.Log.Warning($"PropHunt: floor align failed: {ex.Message}"); }
            return 0f;
        }

        private Transform FindSpawnedTransform(uint netId)
        {
            try
            {
                NetworkIdentity ni = null;
                try { if (NetworkServer.active && NetworkServer.spawned != null && NetworkServer.spawned.ContainsKey(netId)) ni = NetworkServer.spawned[netId]; } catch { }
                if (ni == null) { try { if (NetworkClient.spawned != null && NetworkClient.spawned.ContainsKey(netId)) ni = NetworkClient.spawned[netId]; } catch { } }
                if (ni != null) return ni.transform;
            }
            catch (Exception ex) { _ctx.Log.Warning($"PropHunt: find spawned failed: {ex.Message}"); }
            return null;
        }

        // Scene-hierarchy path of sibling indices (deterministic across clients for static scene objects).
        private string PathOf(Transform t)
        {
            try
            {
                var idx = new List<int>();
                var cur = t;
                while (cur != null) { idx.Insert(0, cur.GetSiblingIndex()); cur = cur.parent; }
                return string.Join("/", idx.ToArray());
            }
            catch { return null; }
        }

        private Transform FindByPath(MetaPlayer scopePlayer, string path)
        {
            try
            {
                var parts = path.Split('/');
                if (parts.Length == 0) return null;
                var roots = scopePlayer.gameObject.scene.GetRootGameObjects();
                int r0 = int.Parse(parts[0]);
                if (roots == null || r0 < 0 || r0 >= roots.Length) return null;
                var cur = roots[r0].transform;
                for (int i = 1; i < parts.Length; i++)
                {
                    int ci = int.Parse(parts[i]);
                    if (ci < 0 || ci >= cur.childCount) return null;
                    cur = cur.GetChild(ci);
                }
                return cur;
            }
            catch (Exception ex) { _ctx.Log.Warning($"PropHunt: find by path failed: {ex.Message}"); return null; }
        }

        private void SetPlayerRenderers(MetaPlayer mp, bool enabled)
        {
            if (mp == null) return;
            try
            {
                var comps = mp.GetComponentsInChildren(Il2CppType.Of<Renderer>(), true);
                if (comps != null)
                    foreach (var c in comps)
                    {
                        var r = c != null ? c.TryCast<Renderer>() : null;
                        if (r != null) { try { r.enabled = enabled; } catch { } }
                    }
            }
            catch (Exception ex) { _ctx.Log.Warning($"PropHunt: set renderers failed: {ex.Message}"); }
        }

        // ---- game mode (host-authoritative) ----

        private void StartGame()
        {
            if (!_ctx.Net.IsServer) { Announce("Only the host can start Prop Hunt."); return; }
            var players = _ctx.Net.Players;
            if (players.Count == 0) { Announce("No players in the session."); return; }

            uint hunter = players[_frame % players.Count].NetId;
            _state.Set("hunter", hunter.ToString());
            _state.Set("found", "");
            _state.Set("active", "1");
            foreach (var p in players) _state.Set("d:" + p.NetId, p.NetId == hunter ? "" : "box");

            _roundEndsAt = Now() + RoundSeconds;
            _lastMilestone = int.MaxValue;
            int propCount = players.Count - 1;
            HostAnnounce($"Prop Hunt! Hunter: {NameOf(hunter)}. {propCount} prop(s) hiding. {(int)RoundSeconds}s.");
        }

        private void StopGame()
        {
            if (!_ctx.Net.IsServer) return;
            _state.Set("active", "0");
            foreach (var p in _ctx.Net.Players) _state.Set("d:" + p.NetId, "");
        }

        private void HostGameTick()
        {
            if (_state.Get("active") != "1") return;

            uint hunter = ParseId(_state.Get("hunter"));
            var hp = PlayerByNetId(hunter);
            var found = ParseIds(_state.Get("found"));

            // catching: hunter walks into a prop
            if (hp != null)
            {
                Vector3 hpos;
                bool haveHpos = true;
                try { hpos = hp.transform.position; } catch { hpos = Vector3.zero; haveHpos = false; }
                if (haveHpos)
                {
                    int props = 0;
                    foreach (var p in _ctx.Net.Players)
                    {
                        if (p.NetId == hunter) continue;
                        props++;
                        if (found.Contains(p.NetId) || p.Player == null) continue;
                        Vector3 pos; try { pos = p.Player.transform.position; } catch { continue; }
                        if (Vector3.Distance(hpos, pos) <= CatchRange)
                        {
                            found.Add(p.NetId);
                            _state.Set("found", JoinIds(found));
                            _state.Set("d:" + p.NetId, "");
                            HostAnnounce($"{NameOf(p.NetId)} was found! {Math.Max(0, props - found.Count)} left.");
                            break;
                        }
                    }
                    if (props > 0 && found.Count >= props) { _state.Set("active", "0"); HostAnnounce("Hunter wins - all props found!"); return; }
                }
            }

            // timer
            int left = (int)Math.Ceiling(_roundEndsAt - Now());
            if (left <= 0) { _state.Set("active", "0"); HostAnnounce("Time's up - props win!"); return; }
            int bucket = left <= 10 ? left : (left <= 30 ? 30 : (left <= 60 ? 60 : 999));
            if (bucket < _lastMilestone) { _lastMilestone = bucket; if (bucket <= 60) HostAnnounce($"{left}s left."); }
        }

        // Broadcast an announcement to every client (the synced "msg" key triggers it on each peer).
        private void HostAnnounce(string text)
        {
            _state.Set("msg", (++_msgCounter) + "|" + text);
            Announce(text);
        }

        private void OnStateChanged(string key, string value)
        {
            if (key == "active" && value == "1")
            {
                uint hunter = ParseId(_state.Get("hunter"));
                var me = _ctx.Net.LocalPlayer;
                if (me != null)
                    Announce(me.NetId == hunter ? "You are the HUNTER - walk into props to find them!" : "You are a PROP - aim at an object and press G to hide!");
            }
            else if (key == "msg")
            {
                int bar = value.IndexOf('|');
                if (bar >= 0) Announce(value.Substring(bar + 1));
            }
        }

        // ---- helpers ----

        private MetaPlayer PlayerByNetId(uint netId)
        {
            foreach (var p in _ctx.Net.Players) if (p.NetId == netId) return p.Player;
            return null;
        }

        private string NameOf(uint netId)
        {
            foreach (var p in _ctx.Net.Players)
                if (p.NetId == netId) return string.IsNullOrEmpty(p.Name) ? "player#" + netId : p.Name;
            return "player#" + netId;
        }

        private static float Now() => Time.realtimeSinceStartup;
        private static uint ParseId(string s) { uint.TryParse(s, out var n); return n; }

        private static HashSet<uint> ParseIds(string csv)
        {
            var set = new HashSet<uint>();
            if (!string.IsNullOrEmpty(csv))
                foreach (var part in csv.Split(','))
                    if (uint.TryParse(part, out var n)) set.Add(n);
            return set;
        }

        private static string JoinIds(HashSet<uint> ids)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var id in ids) { if (sb.Length > 0) sb.Append(','); sb.Append(id); }
            return sb.ToString();
        }

        private void Announce(string text)
        {
            try
            {
                var banner = LocalAnnouncementText.Instance;
                if (banner != null) banner.DisplayForSeconds(text, 2.5f);
                else _ctx.Log.Info($"[prophunt] {text}");
            }
            catch (Exception ex) { _ctx.Log.Warning($"announce failed: {ex.Message}"); }
        }
    }
}

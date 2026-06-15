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
    /// Prop Hunt as a real multiplayer mode. A player aims at an object and presses <b>G</b> to become
    /// it; that choice is replicated through an ASL synced store as the object's Mirror net id, so
    /// EVERY client hides that player's model and rebuilds the same object on them. A late joiner gets
    /// the current disguises on arrival. The host also runs a hunter that "finds" props by proximity.
    /// </summary>
    public sealed class PropHunt : AslMod
    {
        private const string StoreId = "com.asl.prophunt/state";
        private const float CatchRange = 3.0f;

        private IModContext _ctx;
        private IAslSync _state;
        private int _frame;

        // What we've actually built locally for each player: the visual object + the synced value it
        // was built from (so we rebuild only when the value changes).
        private sealed class Disguise { public GameObject Obj; public string Value; }
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
            ctx.Events.SceneChanged += _ => _disguises.Clear();   // scene objects gone; drop stale refs
            ctx.Log.Info("ASL Prop Hunt loaded. Aim at an object and press G to become it.");
        }

        private void OnUpdate()
        {
            _frame++;

            if (_ctx.Net.IsOnline)
            {
                try { if (Input.GetKeyDown(KeyCode.G)) ToggleLookDisguise(); } catch { }
            }

            // keep each disguise object glued to its player
            if (_disguises.Count > 0 && _frame % 2 == 0)
            {
                foreach (var kv in _disguises)
                {
                    var mp = PlayerByNetId(kv.Key);
                    if (mp != null && kv.Value.Obj != null) { try { kv.Value.Obj.transform.position = mp.transform.position; } catch { } }
                }
            }

            if (_frame % 20 == 0) ReconcileDisguises();
            if (_ctx.Net.IsServer && _frame % 10 == 0) HostCatchLogic();
        }

        // ---- disguise control ----

        // Toggle MY disguise: become the networked object I'm aiming at (replicated to everyone), or revert.
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
                        if (ni != null && ni.netId != 0) { value = ni.netId.ToString(); what = ni.name; }
                    }
                }
            }
            catch (Exception ex) { _ctx.Log.Warning($"PropHunt: raycast failed: {ex.Message}"); }

            _state.Set(key, value);   // replicate; every client rebuilds from this
            Announce(value == "box" ? "You're a box (that wasn't a networked prop)." : $"You are now a '{what}'!");
        }

        // ---- reconcile: every client applies the per-player disguise from the synced state ----

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

            // players who left: revert + drop
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
                if (value == "box") d.Obj = MakeBox(mp);
                else { uint objNetId; uint.TryParse(value, out objNetId); d.Obj = MakePropFromNetId(mp, objNetId); }
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

        private GameObject MakePropFromNetId(MetaPlayer mp, uint objNetId)
        {
            var root = FindSpawnedTransform(objNetId);
            return root != null ? CopyMeshes(root, mp) : MakeBox(mp);   // not spawned here -> box
        }

        // Rebuild a pure-visual copy of every mesh part under the prop root, keeping each part's pose.
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

        // Find a spawned networked object by its net id (works on host and client — same id everywhere).
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

        // ---- game mode ----

        private void StartGame()
        {
            if (!_ctx.Net.IsServer) { Announce("Only the host can start Prop Hunt."); return; }
            var players = _ctx.Net.Players;
            if (players.Count == 0) { Announce("No players in the session."); return; }

            uint hunter = players[_frame % players.Count].NetId;
            _state.Set("hunter", hunter.ToString());
            _state.Set("found", "");
            _state.Set("active", "1");
            foreach (var p in players) _state.Set("d:" + p.NetId, p.NetId == hunter ? "" : "box");   // props start boxed
            Announce($"Prop Hunt! Hunter is {NameOf(hunter)}. Props: aim + G to hide as objects!");
        }

        private void StopGame()
        {
            if (!_ctx.Net.IsServer) return;
            _state.Set("active", "0");
            foreach (var p in _ctx.Net.Players) _state.Set("d:" + p.NetId, "");
        }

        private void HostCatchLogic()
        {
            if (_state.Get("active") != "1") return;
            uint hunter = ParseId(_state.Get("hunter"));
            var hp = PlayerByNetId(hunter);
            if (hp == null) return;
            Vector3 hpos; try { hpos = hp.transform.position; } catch { return; }

            var found = ParseIds(_state.Get("found"));
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
                    _state.Set("d:" + p.NetId, "");   // reveal the found prop everywhere
                    Announce($"{NameOf(p.NetId)} was found!");
                    break;
                }
            }
            if (props > 0 && found.Count >= props) { _state.Set("active", "0"); Announce("Hunter wins - all props found!"); }
        }

        private void OnStateChanged(string key, string value)
        {
            if (key == "active" && value == "1")
            {
                uint hunter = ParseId(_state.Get("hunter"));
                var me = _ctx.Net.LocalPlayer;
                if (me != null)
                    Announce(me.NetId == hunter ? "You are the HUNTER - find the props!" : "You are a PROP - aim at an object and press G!");
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

using System;
using System.Collections.Generic;
using ASL.Api;
using Il2CppInterop.Runtime;
using Metater;
using UnityEngine;

namespace PropHunt
{
    /// <summary>
    /// Multiplayer Prop Hunt, and a showcase for ASL's mod-facing API. Almost everything the mode needs
    /// goes through the library now:
    /// <list type="bullet">
    /// <item>input via <c>ctx.Input.RegisterKey</c> — the keys show up in the F8 menu, can be rebound, and
    ///   are conflict-checked;</item>
    /// <item>"what am I aiming at" via <c>ctx.Net.LocalPlayer.GetLookedAt()</c>;</item>
    /// <item>freeze / collider sizing via <c>ctx.Net.LocalPlayer.Freeze()</c> / <c>SetColliderSize()</c>;</item>
    /// <item>object lookup via <c>ctx.Net.FindObject(netId)</c>; on-screen text via <c>ctx.Ui.Announce</c>;</item>
    /// <item>replication via the synced store (<c>ctx.Net.GetSync</c>).</item>
    /// </list>
    /// What stays in the mod is the niche bit — rebuilding the prop's meshes on the player — because that
    /// is game-art-specific, not framework material.
    /// </summary>
    public sealed class PropHunt : AslMod
    {
        private const string StoreId = "com.asl.prophunt/state";
        private const float CatchRange = 2.5f;
        private const float RoundSeconds = 150f;

        // A prop the player can plausibly become is human-scale; reject anything outside this range.
        private const float MinPropSize = 0.05f;
        private const float MaxPropSize = 5.0f;

        private IModContext _ctx;
        private IAslSync _state;
        private IAslKeybind _disguiseKey;
        private IAslKeybind _freezeKey;
        private int _frame;
        private Material _fallbackMat;

        // host-only round bookkeeping
        private float _roundEndsAt;
        private int _lastMilestone;
        private int _msgCounter;

        // local collider target while disguised (re-applied each frame); <0 = not disguised
        private float _propRadius = -1f, _propHeight = -1f;

        private sealed class Disguise { public GameObject Obj; public string Value; public float YOffset; }
        private readonly Dictionary<uint, Disguise> _disguises = new Dictionary<uint, Disguise>();

        public override void OnLoad(IModContext ctx)
        {
            _ctx = ctx;
            _state = ctx.Net.GetSync(StoreId);
            _state.Changed += OnStateChanged;

            // Named keybinds: they appear under "ASL Prop Hunt" in the F8 menu and are rebindable.
            _disguiseKey = ctx.Input.RegisterKey("disguise", "Disguise / undisguise", KeyCode.G);
            _freezeKey = ctx.Input.RegisterKey("freeze", "Freeze in place", KeyCode.F);

            ctx.Menu.AddLabel("Aim at an object and press the disguise key to become it.");
            ctx.Menu.AddButton("Start Prop Hunt (host)", StartGame);
            ctx.Menu.AddButton("Stop Prop Hunt (host)", StopGame);
            ctx.Menu.AddButton("Become what I'm looking at", ToggleLookDisguise);
            ctx.Menu.AddButton("Freeze / unfreeze", ToggleFreeze);

            ctx.Events.Update += OnUpdate;
            ctx.Events.SceneChanged += _ => OnSceneChanged();
            ctx.Log.Info("ASL Prop Hunt loaded.");
        }

        private void OnSceneChanged()
        {
            _disguises.Clear();
            _propRadius = _propHeight = -1f;
        }

        private void OnUpdate()
        {
            _frame++;

            if (_ctx.Net.IsOnline)
            {
                if (_disguiseKey != null && _disguiseKey.WasPressed) ToggleLookDisguise();
                if (_freezeKey != null && _freezeKey.WasPressed) ToggleFreeze();
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

            // hold my collider down to the prop's size (the controller can re-apply its own radius)
            if (_propRadius > 0f) _ctx.Net.LocalPlayer?.SetColliderSize(_propRadius, _propHeight);

            if (_frame % 20 == 0) ReconcileDisguises();
            if (_ctx.Net.IsServer && _frame % 10 == 0) HostGameTick();
        }

        // ---- disguise control ----

        private void ToggleLookDisguise()
        {
            var me = _ctx.Net.LocalPlayer;
            if (me == null) { _ctx.Ui.Announce("Get into a level first."); return; }

            string key = "d:" + me.NetId;
            if (!string.IsNullOrEmpty(_state.Get(key))) { _state.Set(key, ""); _ctx.Ui.Announce("Back to normal."); return; }

            string value = "box";
            string what = "box";
            var look = me.GetLookedAt(6f);          // library raycast — gives the object + its net id
            if (look.Hit)
            {
                what = look.Transform != null ? look.Transform.name : "box";
                if (look.NetId != 0) { value = "n:" + look.NetId; }
                else { var path = PathOf(look.Transform); if (path != null) value = "p:" + path; }   // static object -> name path
            }

            _state.Set(key, value);
            _ctx.Ui.Announce(value == "box" ? "You're a box." : $"You are now a '{what}'!");
        }

        // ---- freeze (real prop-hunt "stick in place"), via the library ----

        private void ToggleFreeze()
        {
            var lp = _ctx.Net.LocalPlayer;
            if (lp == null) { _ctx.Ui.Announce("Get into a level first."); return; }
            if (lp.IsFrozen) { lp.Unfreeze(); _ctx.Ui.Announce("Unfrozen."); }
            else { lp.Freeze(); _ctx.Ui.Announce("Frozen — hold still."); }
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
            SetPlayerHidden(mp, true);

            var d = new Disguise { Value = value };
            try
            {
                Transform root = null;
                if (value != "box")
                {
                    if (value.StartsWith("n:")) { uint.TryParse(value.Substring(2), out var on); var go = _ctx.Net.FindObject(on); root = go != null ? go.transform : null; }
                    else if (value.StartsWith("p:")) { root = FindByPath(mp, value.Substring(2)); }
                }

                // Reject a root that isn't human-scale (a mis-resolved path can grab a giant pillar).
                if (root != null && !ReasonableSource(root)) root = null;

                d.Obj = root != null ? CopyMeshes(root, mp) : MakeBox(mp);

                if (ComputeBounds(d.Obj, out var b))
                {
                    float yOffset = mp.transform.position.y - b.min.y;
                    var pos = d.Obj.transform.position; pos.y += yOffset; d.Obj.transform.position = pos;
                    d.YOffset = yOffset;

                    if (IsLocal(netId))
                    {
                        _propRadius = Mathf.Clamp(Mathf.Max(b.extents.x, b.extents.z), 0.12f, 1.5f);
                        _propHeight = Mathf.Clamp(b.size.y, 0.3f, 3f);
                    }
                }
            }
            catch (Exception ex) { _ctx.Log.Warning($"PropHunt: apply disguise failed: {ex.Message}"); }

            _disguises[netId] = d;
        }

        private void RemoveDisguise(uint netId)
        {
            if (!_disguises.TryGetValue(netId, out var d)) return;
            _disguises.Remove(netId);
            SetPlayerHidden(PlayerByNetId(netId), false);
            if (IsLocal(netId)) { _ctx.Net.LocalPlayer?.ResetCollider(); _propRadius = _propHeight = -1f; }
            if (d.Obj != null) { try { UnityEngine.Object.Destroy(d.Obj); } catch { } }
        }

        // ---- visuals (game-art-specific; stays in the mod) ----

        // A material that is guaranteed to render in this URP build, cached. Used for fallback boxes and
        // to repair any copied material that lost its shader (which renders magenta).
        private Material FallbackMaterial()
        {
            if (_fallbackMat != null) return _fallbackMat;
            try
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null) sh = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                if (sh != null)
                {
                    _fallbackMat = new Material(sh);
                    var grey = new Color(0.55f, 0.55f, 0.58f, 1f);
                    try { _fallbackMat.SetColor("_BaseColor", grey); } catch { }
                    try { _fallbackMat.color = grey; } catch { }
                    _fallbackMat.hideFlags = HideFlags.HideAndDontSave;
                }
            }
            catch (Exception ex) { _ctx.Log.Warning($"PropHunt: fallback material failed: {ex.Message}"); }
            return _fallbackMat;
        }

        private GameObject MakeBox(MetaPlayer mp)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "ASL_Prop";
            box.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
            try { var col = box.GetComponent<Collider>(); if (col != null) col.enabled = false; } catch { }
            try { var mr = box.GetComponent<MeshRenderer>(); var fm = FallbackMaterial(); if (mr != null && fm != null) mr.sharedMaterial = fm; } catch { }
            try { box.transform.position = mp.transform.position; } catch { }
            return box;
        }

        private GameObject CopyMeshes(Transform root, MetaPlayer mp)
        {
            // Gather static (MeshFilter+MeshRenderer) and skinned (SkinnedMeshRenderer) geometry.
            // sharedMesh is a reference — safe to read; never touch vertices (that crashes).
            var meshes = new List<Mesh>();
            var mats = new List<Material[]>();
            var xforms = new List<Transform>();

            try
            {
                var mfs = root.GetComponentsInChildren(Il2CppType.Of<MeshFilter>(), false);
                if (mfs != null)
                    foreach (var c in mfs)
                    {
                        var mf = c != null ? c.TryCast<MeshFilter>() : null;
                        if (mf == null || mf.sharedMesh == null) continue;
                        var prc = mf.GetComponent(Il2CppType.Of<MeshRenderer>());
                        var pr = prc != null ? prc.TryCast<MeshRenderer>() : null;
                        meshes.Add(mf.sharedMesh); mats.Add(pr != null ? pr.sharedMaterials : null); xforms.Add(mf.transform);
                    }
            }
            catch (Exception ex) { _ctx.Log.Warning($"PropHunt: gather mesh filters failed: {ex.Message}"); }

            try
            {
                var smrs = root.GetComponentsInChildren(Il2CppType.Of<SkinnedMeshRenderer>(), false);
                if (smrs != null)
                    foreach (var c in smrs)
                    {
                        var smr = c != null ? c.TryCast<SkinnedMeshRenderer>() : null;
                        if (smr == null || smr.sharedMesh == null) continue;
                        meshes.Add(smr.sharedMesh); mats.Add(smr.sharedMaterials); xforms.Add(smr.transform);
                    }
            }
            catch (Exception ex) { _ctx.Log.Warning($"PropHunt: gather skinned meshes failed: {ex.Message}"); }

            if (meshes.Count == 0) return MakeBox(mp);

            var D = new GameObject("ASL_Prop");
            D.transform.position = root.position;
            D.transform.rotation = root.rotation;

            for (int i = 0; i < meshes.Count; i++)
            {
                try
                {
                    var C = new GameObject("part");
                    C.transform.SetParent(D.transform, false);
                    var src = xforms[i];
                    C.transform.position = src.position; C.transform.rotation = src.rotation; C.transform.localScale = src.lossyScale;
                    var nmf = C.AddComponent(Il2CppType.Of<MeshFilter>()).TryCast<MeshFilter>();
                    var nmr = C.AddComponent(Il2CppType.Of<MeshRenderer>()).TryCast<MeshRenderer>();
                    if (nmf != null) nmf.sharedMesh = meshes[i];
                    if (nmr != null) nmr.sharedMaterials = ScrubMaterials(mats[i]);
                }
                catch (Exception ex) { _ctx.Log.Warning($"PropHunt: part copy failed: {ex.Message}"); }
            }

            try { D.transform.position = mp.transform.position; } catch { }
            return D;
        }

        // Replace any material whose shader vanished in this build (renders magenta) with the fallback.
        private Material[] ScrubMaterials(Material[] src)
        {
            if (src == null || src.Length == 0) return new Material[] { FallbackMaterial() };
            try
            {
                for (int i = 0; i < src.Length; i++)
                {
                    var m = src[i];
                    bool broken = m == null || m.shader == null;
                    if (!broken) { try { var n = m.shader.name; broken = string.IsNullOrEmpty(n) || n.Contains("InternalErrorShader"); } catch { broken = true; } }
                    if (broken) src[i] = FallbackMaterial();
                }
            }
            catch { }
            return src;
        }

        private bool ReasonableSource(Transform root)
        {
            try
            {
                var rends = root.GetComponentsInChildren(Il2CppType.Of<Renderer>(), false);
                bool any = false; Bounds b = new Bounds();
                if (rends != null)
                    foreach (var c in rends)
                    {
                        var r = c != null ? c.TryCast<Renderer>() : null;
                        if (r == null) continue;
                        if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                    }
                if (!any) return false;
                float m = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                return m >= MinPropSize && m <= MaxPropSize;
            }
            catch { return false; }
        }

        private bool ComputeBounds(GameObject obj, out Bounds bounds)
        {
            bounds = new Bounds();
            if (obj == null) return false;
            try
            {
                var rends = obj.GetComponentsInChildren(Il2CppType.Of<Renderer>(), false);
                bool any = false;
                if (rends != null)
                    foreach (var c in rends)
                    {
                        var r = c != null ? c.TryCast<Renderer>() : null;
                        if (r == null) continue;
                        if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
                    }
                return any;
            }
            catch { return false; }
        }

        // Scene-hierarchy path by transform NAMES (stable across clients, unlike sibling indices).
        private string PathOf(Transform t)
        {
            try
            {
                var names = new List<string>();
                var cur = t;
                while (cur != null) { names.Insert(0, Sanitize(cur.name)); cur = cur.parent; }
                return string.Join("/", names.ToArray());
            }
            catch { return null; }
        }

        private static string Sanitize(string s) => string.IsNullOrEmpty(s) ? "?" : s.Replace('/', '_');

        private Transform FindByPath(MetaPlayer scopePlayer, string path)
        {
            try
            {
                var parts = path.Split('/');
                if (parts.Length == 0) return null;

                Transform cur = null;
                var roots = scopePlayer.gameObject.scene.GetRootGameObjects();
                if (roots != null)
                    foreach (var go in roots)
                        if (go != null && Sanitize(go.name) == parts[0]) { cur = go.transform; break; }
                if (cur == null) return null;

                for (int i = 1; i < parts.Length; i++)
                {
                    Transform next = null;
                    int n = cur.childCount;
                    for (int ci = 0; ci < n; ci++)
                    {
                        var child = cur.GetChild(ci);
                        if (child != null && Sanitize(child.name) == parts[i]) { next = child; break; }
                    }
                    if (next == null) return null;
                    cur = next;
                }
                return cur;
            }
            catch (Exception ex) { _ctx.Log.Warning($"PropHunt: find by path failed: {ex.Message}"); return null; }
        }

        // Hide/show a player's body via forceRenderingOff (the game re-asserts .enabled every frame).
        private void SetPlayerHidden(MetaPlayer mp, bool hidden)
        {
            if (mp == null) return;
            try
            {
                var comps = mp.GetComponentsInChildren(Il2CppType.Of<Renderer>(), true);
                if (comps != null)
                    foreach (var c in comps)
                    {
                        var r = c != null ? c.TryCast<Renderer>() : null;
                        if (r != null) { try { r.forceRenderingOff = hidden; } catch { } }
                    }
            }
            catch (Exception ex) { _ctx.Log.Warning($"PropHunt: hide player failed: {ex.Message}"); }
        }

        // ---- game mode (host-authoritative) ----

        private void StartGame()
        {
            if (!_ctx.Net.IsServer) { _ctx.Ui.Announce("Only the host can start Prop Hunt."); return; }
            var players = _ctx.Net.Players;
            if (players.Count == 0) { _ctx.Ui.Announce("No players in the session."); return; }

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

            int left = (int)Math.Ceiling(_roundEndsAt - Now());
            if (left <= 0) { _state.Set("active", "0"); HostAnnounce("Time's up - props win!"); return; }
            int bucket = left <= 10 ? left : (left <= 30 ? 30 : (left <= 60 ? 60 : 999));
            if (bucket < _lastMilestone) { _lastMilestone = bucket; if (bucket <= 60) HostAnnounce($"{left}s left."); }
        }

        private void HostAnnounce(string text)
        {
            _state.Set("msg", (++_msgCounter) + "|" + text);
            _ctx.Ui.Announce(text);
        }

        private void OnStateChanged(string key, string value)
        {
            if (key == "active" && value == "1")
            {
                uint hunter = ParseId(_state.Get("hunter"));
                var me = _ctx.Net.LocalPlayer;
                if (me != null)
                    _ctx.Ui.Announce(me.NetId == hunter ? "You are the HUNTER - walk into props to find them!" : "You are a PROP - aim at an object and press the disguise key!");
            }
            else if (key == "msg")
            {
                int bar = value.IndexOf('|');
                if (bar >= 0) _ctx.Ui.Announce(value.Substring(bar + 1));
            }
        }

        // ---- helpers ----

        private bool IsLocal(uint netId)
        {
            var lp = _ctx.Net.LocalPlayer;
            return lp != null && lp.NetId == netId;
        }

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
    }
}

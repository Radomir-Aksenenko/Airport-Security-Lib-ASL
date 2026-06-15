using System;
using System.Collections.Generic;
using ASL.Api;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Metater;
using UnityEngine;

namespace PropHunt
{
    /// <summary>
    /// A new multiplayer game mode the base game doesn't have: <b>Prop Hunt</b>. When a round starts,
    /// one player is the Hunter and the rest are Props. A Prop's character model is hidden on every
    /// client and a box is shown in its place — so to everyone else they look like a box. The Hunter
    /// finds Props by getting close. All state (who's hunter, who's been found) lives in an ASL synced
    /// store, so it replicates to every client and a late joiner gets it on arrival.
    /// </summary>
    public sealed class PropHunt : AslMod
    {
        private const string StoreId = "com.asl.prophunt/state";
        private const float CatchRange = 3.0f;

        private IModContext _ctx;
        private IAslSync _state;
        private int _frame;
        private bool _testDisguised;

        private sealed class Disguise { public GameObject Box; }
        private readonly Dictionary<uint, Disguise> _disguises = new Dictionary<uint, Disguise>();

        public override void OnLoad(IModContext ctx)
        {
            _ctx = ctx;
            _state = ctx.Net.GetSync(StoreId);
            _state.Changed += OnStateChanged;

            ctx.Menu.AddLabel("Prop Hunt: props turn into boxes, the hunter finds them.");
            ctx.Menu.AddButton("Start Prop Hunt (host)", StartGame);
            ctx.Menu.AddButton("Stop Prop Hunt (host)", StopGame);
            ctx.Menu.AddButton("Become what I'm looking at", ToggleLookDisguise);

            ctx.Events.Update += OnUpdate;
            ctx.Events.SceneChanged += _ => ForgetDisguises();   // scene objects are gone; drop stale refs
            ctx.Log.Info("ASL Prop Hunt loaded. F8 -> ASL Prop Hunt (needs 2+ players for the full game).");
        }

        // ---- round control (host) ----

        private void StartGame()
        {
            if (!_ctx.Net.IsServer) { Announce("Only the host can start Prop Hunt."); return; }
            var players = _ctx.Net.Players;
            if (players.Count == 0) { Announce("No players in the session."); return; }

            uint hunter = players[_frame % players.Count].NetId;   // cheap pseudo-random pick
            _state.Set("found", "");
            _state.Set("hunter", hunter.ToString());
            _state.Set("active", "1");
            Announce($"Prop Hunt! Hunter is {NameOf(hunter)}. Props, hide!");
            _ctx.Log.Info($"PropHunt: started, hunter = {NameOf(hunter)}.");
        }

        private void StopGame()
        {
            if (!_ctx.Net.IsServer) return;
            _state.Set("active", "0");
        }

        // ---- per-frame ----

        private void OnUpdate()
        {
            _frame++;

            // Press G while in a level: become whatever object you're looking at (press again to revert).
            if (_ctx.Net.IsOnline)
            {
                try { if (Input.GetKeyDown(KeyCode.G)) ToggleLookDisguise(); } catch { }
            }

            // keep each box glued to its player
            if (_disguises.Count > 0 && _frame % 2 == 0)
            {
                foreach (var kv in _disguises)
                {
                    var mp = PlayerByNetId(kv.Key);
                    if (mp != null && kv.Value.Box != null)
                    {
                        try { kv.Value.Box.transform.position = mp.transform.position; } catch { }
                    }
                }
            }

            if (_frame % 20 == 0) ReconcileDisguises();                 // ~3x/sec
            if (_ctx.Net.IsServer && _frame % 10 == 0) HostCatchLogic(); // host runs the rules
        }

        // Make the local set of disguises match the game state: active props that haven't been found.
        private void ReconcileDisguises()
        {
            if (_testDisguised) return;   // manual test mode owns the disguises

            bool active = _state.Get("active") == "1";
            uint hunter = ParseId(_state.Get("hunter"));
            var found = ParseIds(_state.Get("found"));

            var desired = new HashSet<uint>();
            if (active)
                foreach (var p in _ctx.Net.Players)
                    if (p.NetId != hunter && !found.Contains(p.NetId)) desired.Add(p.NetId);

            foreach (var nid in desired)
                if (!_disguises.ContainsKey(nid)) { var mp = PlayerByNetId(nid); if (mp != null) ApplyDisguise(mp, nid); }

            var stale = new List<uint>();
            foreach (var kv in _disguises) if (!desired.Contains(kv.Key)) stale.Add(kv.Key);
            foreach (var nid in stale) RemoveDisguise(nid);
        }

        private void HostCatchLogic()
        {
            if (_state.Get("active") != "1") return;
            uint hunter = ParseId(_state.Get("hunter"));
            var hp = PlayerByNetId(hunter);
            if (hp == null) return;
            Vector3 hpos; try { hpos = hp.transform.position; } catch { return; }

            var found = ParseIds(_state.Get("found"));
            int propCount = 0;
            foreach (var p in _ctx.Net.Players)
            {
                if (p.NetId == hunter) continue;
                propCount++;
                if (found.Contains(p.NetId) || p.Player == null) continue;
                Vector3 pos; try { pos = p.Player.transform.position; } catch { continue; }
                if (Vector3.Distance(hpos, pos) <= CatchRange)
                {
                    found.Add(p.NetId);
                    _state.Set("found", JoinIds(found));
                    Announce($"{NameOf(p.NetId)} was found!");
                    _ctx.Log.Info($"PropHunt: hunter found {NameOf(p.NetId)}.");
                    break;
                }
            }
            if (propCount > 0 && found.Count >= propCount)
            {
                _state.Set("active", "0");
                Announce("Hunter wins - all props found!");
            }
        }

        // ---- disguise (visual) ----

        // Enable/disable all of a player's renderers. Always fetched fresh from the (live) player, so we
        // never touch a renderer that the game already destroyed — that would be an uncatchable crash.
        private void SetPlayerRenderers(MetaPlayer mp, bool enabled)
        {
            if (mp == null) return;
            try
            {
                var comps = mp.GetComponentsInChildren(Il2CppType.Of<Renderer>(), true);
                if (comps != null)
                    foreach (var c in comps)
                    {
                        var r = c == null ? null : c.TryCast<Renderer>();
                        if (r != null) { try { r.enabled = enabled; } catch { } }
                    }
            }
            catch (Exception ex) { _ctx.Log.Warning($"PropHunt: set renderers failed: {ex.Message}"); }
        }

        private void ApplyDisguise(MetaPlayer mp, uint netId)
        {
            if (mp == null || _disguises.ContainsKey(netId)) return;
            SetPlayerRenderers(mp, false);                    // hide the model

            var d = new Disguise();
            try
            {
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = "ASL_PropHunt_Box";
                box.transform.localScale = new Vector3(1.1f, 1.1f, 1.1f);
                try { var col = box.GetComponent<Collider>(); if (col != null) col.enabled = false; } catch { }
                try { box.transform.position = mp.transform.position; } catch { }
                d.Box = box;
            }
            catch (Exception ex) { _ctx.Log.Warning($"PropHunt: create box failed: {ex.Message}"); }

            _disguises[netId] = d;
            _ctx.Log.Info($"PropHunt: disguised netId={netId} (box={(d.Box != null)}).");
        }

        private void RemoveDisguise(uint netId)
        {
            if (!_disguises.TryGetValue(netId, out var d)) return;
            _disguises.Remove(netId);
            SetPlayerRenderers(PlayerByNetId(netId), true);   // re-fetch live player; show its model again
            if (d.Box != null) { try { UnityEngine.Object.Destroy(d.Box); } catch { } }
        }

        // Game stopped (same scene): properly revert each disguise.
        private void ClearAllDisguises()
        {
            var ids = new List<uint>(_disguises.Keys);
            foreach (var nid in ids) RemoveDisguise(nid);
            _testDisguised = false;
        }

        // Scene changed: the scene's objects (players + our boxes) are already gone. Just drop our
        // references without touching them — re-disguising happens fresh in the new scene.
        private void ForgetDisguises()
        {
            _disguises.Clear();
            _testDisguised = false;
        }

        // Toggle: become the object you're aiming at, or revert if already disguised.
        private void ToggleLookDisguise()
        {
            var me = _ctx.Net.LocalPlayer;
            var mp = me != null ? me.Player : null;
            if (mp == null) { Announce("Get into a level first."); return; }

            if (_testDisguised || _disguises.ContainsKey(me.NetId))
            {
                RemoveDisguise(me.NetId);
                _testDisguised = false;
                Announce("Back to normal.");
                return;
            }

            _testDisguised = true;   // keep the round-reconcile from clearing a manual disguise
            DisguiseAsLookedAt(mp, me.NetId);
        }

        // Raycast from the camera, find the WHOLE object you're aiming at (its prop root), and rebuild a
        // pure-visual copy: every mesh part with its materials and its pose relative to the root, so a
        // multi-part prop (a flower + its pot, a whole dynamite bundle) comes across complete and rightly
        // oriented. We only reference shared meshes/materials (never read vertices), so it stays safe.
        private void DisguiseAsLookedAt(MetaPlayer me, uint netId)
        {
            var cam = Camera.main;
            if (cam == null) { Announce("No camera found."); _testDisguised = false; return; }

            RaycastHit hit;
            bool got;
            try { got = Physics.Raycast(cam.transform.position, cam.transform.forward, out hit, 6f); }
            catch (Exception ex) { _ctx.Log.Warning($"PropHunt: raycast failed: {ex.Message}"); _testDisguised = false; return; }

            if (!got) { Announce("Aim at an object, then press G."); _testDisguised = false; return; }

            // Find the prop root: the rigidbody/networked-item root that owns the whole object, so we copy
            // all of its parts (not just the one collider we hit).
            Transform root = hit.transform;
            string what = root.name;
            try
            {
                var rb = root.GetComponentInParent(Il2CppType.Of<Rigidbody>());
                var rbc = rb != null ? rb.TryCast<Rigidbody>() : null;
                if (rbc != null) { root = rbc.transform; what = root.name; }
                else
                {
                    var ni = root.GetComponentInParent(Il2CppType.Of<Mirror.NetworkIdentity>());
                    var nic = ni != null ? ni.TryCast<Mirror.NetworkIdentity>() : null;
                    if (nic != null) { root = nic.transform; what = root.name; }
                }
            }
            catch { }

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

            if (parts.Count == 0)
            {
                _ctx.Log.Info($"PropHunt: '{what}' has no mesh; using a box.");
                ApplyDisguise(me, netId);
                Announce("Couldn't copy that - you're a box.");
                return;
            }

            SetPlayerRenderers(me, false);   // hide the player's model

            var d = new Disguise();
            try
            {
                // D snapshots the prop's world pose; each part keeps its world transform via
                // SetParent(worldPositionStays), so the assembly is identical to the original.
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
                        C.transform.SetParent(D.transform, true);   // keep world pose -> relative to root
                    }
                    catch (Exception ex) { _ctx.Log.Warning($"PropHunt: part copy failed: {ex.Message}"); }
                }

                try { D.transform.position = me.transform.position; } catch { }   // move the whole prop to the player
                d.Box = D;
            }
            catch (Exception ex) { _ctx.Log.Warning($"PropHunt: build prop failed: {ex.Message}"); }

            _disguises[netId] = d;
            _ctx.Log.Info($"PropHunt: disguised netId={netId} as '{what}' ({parts.Count} part(s)).");
            Announce($"You are now a '{what}'!");
        }

        // ---- state + helpers ----

        private void OnStateChanged(string key, string value)
        {
            if (key == "active")
            {
                if (value == "1")
                {
                    uint hunter = ParseId(_state.Get("hunter"));
                    var me = _ctx.Net.LocalPlayer;
                    if (me != null)
                        Announce(me.NetId == hunter ? "You are the HUNTER - find the props!" : "You are a PROP - you look like a box. Hide!");
                }
                else
                {
                    ClearAllDisguises();
                }
            }
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

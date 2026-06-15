using System;
using System.Collections.Generic;
using ASL.Api;
using Il2CppInterop.Runtime;
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
        private int _selfTestState, _selfTestFrames;   // one-shot disguise validation

        private sealed class Disguise
        {
            public GameObject Box;
            public readonly List<Renderer> Hidden = new List<Renderer>();
        }
        private readonly Dictionary<uint, Disguise> _disguises = new Dictionary<uint, Disguise>();

        public override void OnLoad(IModContext ctx)
        {
            _ctx = ctx;
            _state = ctx.Net.GetSync(StoreId);
            _state.Changed += OnStateChanged;

            ctx.Menu.AddLabel("Prop Hunt: props turn into boxes, the hunter finds them.");
            ctx.Menu.AddButton("Start Prop Hunt (host)", StartGame);
            ctx.Menu.AddButton("Stop Prop Hunt (host)", StopGame);
            ctx.Menu.AddButton("Disguise test (me as a box)", ToggleSelfDisguise);

            ctx.Events.Update += OnUpdate;
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
            MaybeDisguiseSelfTest();
        }

        // One-shot: once the local player exists, briefly disguise it and log whether renderer-hiding +
        // the box worked. Proves the core trick from the log, no button press needed.
        private void MaybeDisguiseSelfTest()
        {
            if (_selfTestState == 2) return;
            var me = _ctx.Net.LocalPlayer;
            var mp = me != null ? me.Player : null;
            if (mp == null) return;

            if (_selfTestState == 0)
            {
                ApplyDisguise(mp, me.NetId);
                _disguises.TryGetValue(me.NetId, out var d);
                _ctx.Log.Info($"DISGUISE SELF-TEST: hid {(d != null ? d.Hidden.Count : 0)} renderers, box={(d != null && d.Box != null)} (you should look like a box for ~3s).");
                _testDisguised = true;          // stop reconcile clearing it during the test
                _selfTestState = 1; _selfTestFrames = 0;
                return;
            }
            if (++_selfTestFrames > 180)        // ~3s
            {
                RemoveDisguise(me.NetId);
                _testDisguised = false;
                _ctx.Log.Info("DISGUISE SELF-TEST: reverted to normal.");
                _selfTestState = 2;
            }
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

        private void ApplyDisguise(MetaPlayer mp, uint netId)
        {
            if (_disguises.ContainsKey(netId)) return;
            var d = new Disguise();

            // hide the player's renderers
            try
            {
                var comps = mp.GetComponentsInChildren(Il2CppType.Of<Renderer>(), true);
                if (comps != null)
                    foreach (var c in comps)
                    {
                        var r = c == null ? null : c.TryCast<Renderer>();
                        if (r != null) { try { r.enabled = false; d.Hidden.Add(r); } catch { } }
                    }
            }
            catch (Exception ex) { _ctx.Log.Warning($"PropHunt: hide renderers failed: {ex.Message}"); }

            // show a box where they are
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
            _ctx.Log.Info($"PropHunt: disguised netId={netId} (hid {d.Hidden.Count} renderers, box={(d.Box != null)}).");
        }

        private void RemoveDisguise(uint netId)
        {
            if (!_disguises.TryGetValue(netId, out var d)) return;
            _disguises.Remove(netId);
            foreach (var r in d.Hidden) { try { if (r != null) r.enabled = true; } catch { } }
            if (d.Box != null) { try { UnityEngine.Object.Destroy(d.Box); } catch { } }
        }

        private void ClearAllDisguises()
        {
            var ids = new List<uint>(_disguises.Keys);
            foreach (var nid in ids) RemoveDisguise(nid);
            _testDisguised = false;
        }

        private void ToggleSelfDisguise()
        {
            var me = _ctx.Net.LocalPlayer;
            var mp = me != null ? me.Player : null;
            if (mp == null) { Announce("Join / start a game first."); return; }

            if (_testDisguised) { RemoveDisguise(me.NetId); _testDisguised = false; Announce("Back to normal."); }
            else { ApplyDisguise(mp, me.NetId); _testDisguised = true; Announce("You are a box now! (test)"); }
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

using System;
using ASL.Api;
using Metater;
using UnityEngine;

namespace ASLTag
{
    /// <summary>
    /// A new multiplayer game mode the base game has no idea about: <b>Tag / You're It</b>. The host
    /// runs the rules (reads everyone's position, passes "It" on contact), the current state lives in an
    /// ASL synced store so every client sees who's It, and a player who joins mid-game gets the state on
    /// arrival. Nothing here is in the game — it's all built on ASL's identity + synced state + transport.
    /// </summary>
    public sealed class ASLTag : AslMod
    {
        private const string StoreId = "com.asl.tag/state";
        private const float TagRange = 3.5f;        // metres
        private const float TagCooldown = 3f;       // seconds before "It" can pass again

        private IModContext _ctx;
        private IAslSync _state;
        private uint _itNetId;                       // host's working copy
        private bool _active;
        private float _cooldownUntil;
        private int _frame;

        public override void OnLoad(IModContext ctx)
        {
            _ctx = ctx;
            _state = ctx.Net.GetSync(StoreId);
            _state.Changed += OnStateChanged;

            ctx.Menu.AddLabel("Tag / You're It - a new game mode. Host starts; touch to pass It.");
            ctx.Menu.AddButton("Start Tag (host)", StartTag);
            ctx.Menu.AddButton("Stop Tag (host)", StopTag);
            ctx.Menu.AddButton("Who's It?", () =>
            {
                var name = CurrentItName();
                Announce(name != null ? $"{name} is It!" : "No tag game running.");
            });

            ctx.Events.Update += OnUpdate;
            ctx.Log.Info("ASL Tag loaded. Press F8 -> ASL Tag (needs 2+ players for the full game).");
        }

        private void StartTag()
        {
            if (!_ctx.Net.IsServer) { Announce("Only the host can start Tag."); return; }
            var players = _ctx.Net.Players;
            if (players.Count == 0) { Announce("No players in the session."); return; }

            var pick = players[_frame % players.Count];   // cheap pseudo-random seed
            _active = true;
            _state.Set("active", "1");
            SetIt(pick.NetId);
            _ctx.Log.Info($"TAG: started, It = {NameOf(pick.NetId)}.");
        }

        private void StopTag()
        {
            if (!_ctx.Net.IsServer) return;
            _active = false;
            _state.Set("active", "0");
        }

        private void SetIt(uint netId)
        {
            _itNetId = netId;
            _cooldownUntil = Now() + TagCooldown;
            _state.Set("it", netId.ToString());   // replicates; OnStateChanged announces on every peer
        }

        // Host-only rules: while active, pass "It" to anyone the current It is standing next to.
        private void OnUpdate()
        {
            _frame++;
            if (!_ctx.Net.IsServer || !_active) return;
            if ((_frame % 10) != 0) return;        // ~6 checks/sec is plenty
            if (Now() < _cooldownUntil) return;

            var players = _ctx.Net.Players;
            MetaPlayer it = null;
            for (int i = 0; i < players.Count; i++) if (players[i].NetId == _itNetId) { it = players[i].Player; break; }
            if (it == null) return;

            Vector3 itPos;
            try { itPos = it.transform.position; } catch { return; }

            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p.NetId == _itNetId || p.Player == null) continue;
                Vector3 pos;
                try { pos = p.Player.transform.position; } catch { continue; }
                if (Vector3.Distance(itPos, pos) <= TagRange)
                {
                    _ctx.Log.Info($"TAG: {NameOf(_itNetId)} tagged {NameOf(p.NetId)}.");
                    SetIt(p.NetId);
                    break;
                }
            }
        }

        // Runs on every peer when the synced state changes (host on Set, clients on receive/snapshot).
        private void OnStateChanged(string key, string value)
        {
            if (key == "it")
            {
                uint.TryParse(value, out var nid);
                Announce($"{NameOf(nid)} is now It!");

                // If that's me, get a short chase boost so being It is playable.
                var me = _ctx.Net.LocalPlayer;
                if (me != null && me.NetId == nid && me.Player != null)
                {
                    try { me.Player.MultiplySpeed(1.4f, 4f); } catch { /* server may override; fine */ }
                }
            }
            else if (key == "active" && value == "0")
            {
                Announce("Tag is over.");
            }
        }

        private string NameOf(uint netId)
        {
            foreach (var p in _ctx.Net.Players)
                if (p.NetId == netId) return string.IsNullOrEmpty(p.Name) ? "player#" + netId : p.Name;
            return "player#" + netId;
        }

        private string CurrentItName()
        {
            var v = _state.Get("it");
            if (string.IsNullOrEmpty(v) || _state.Get("active") != "1") return null;
            uint.TryParse(v, out var n);
            return NameOf(n);
        }

        private static float Now() => Time.realtimeSinceStartup;

        private void Announce(string text)
        {
            try
            {
                var banner = LocalAnnouncementText.Instance;
                if (banner != null) banner.DisplayForSeconds(text, 2.5f);
                else _ctx.Log.Info($"[tag] {text}");
            }
            catch (Exception ex) { _ctx.Log.Warning($"announce failed: {ex.Message}"); }
        }
    }
}

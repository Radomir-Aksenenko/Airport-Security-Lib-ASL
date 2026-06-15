using System;
using ASL.Api;
using Metater;

namespace FunPanel
{
    /// <summary>
    /// Interactive showcase mod. Adds an F8 menu page whose controls drive the <b>real</b> game player
    /// (<see cref="MetaPlayer"/>, reached through ASL's player identity) and pop messages on the game's
    /// own on-screen announcement UI — so you can see a mod doing real, game-native things immediately.
    /// </summary>
    public sealed class FunPanel : AslMod
    {
        private IModContext _ctx;
        private float _speed = 3f;

        public override void OnLoad(IModContext ctx)
        {
            _ctx = ctx;
            ctx.Log.Info("Fun Panel loaded. Press F8 -> Fun Panel.");

            ctx.Menu.AddLabel("Try these on yourself (best inside a game / Level).");

            // Interactive: a slider feeds the speed-boost button.
            ctx.Menu.AddSlider("Speed multiplier", 1.5f, 6f, _speed, v => _speed = v);
            ctx.Menu.AddButton("Apply speed boost (10s)", () => Do(me =>
            {
                me.MultiplySpeed(_speed, 10f);
                Announce($"Speed x{_speed:0.#} for 10 seconds!");
            }));

            ctx.Menu.AddButton("Coffee rush", () => Do(me =>
            {
                me.ConsumeCoffee(12f, 3f, 1.6f);   // (duration, maxStackMultiplier, speedMultiplier)
                Announce("Coffee rush! Zoom zoom.");
            }));

            ctx.Menu.AddButton("Jump", () => Do(me => me.Jump()));

            ctx.Menu.AddButton("Pass out (ragdoll)", () => Do(me =>
            {
                me.TriggerAlcoholPassOut();
                Announce("Night night...");
            }));

            ctx.Menu.AddButton("Say hi on screen", () => Announce("Hello from ASL! This is a mod."));

            ctx.Menu.AddButton("Who's here?", () =>
            {
                var players = ctx.Net.Players;
                string names = "";
                foreach (var p in players)
                    names += (names.Length > 0 ? ", " : "") + (string.IsNullOrEmpty(p.Name) ? "player#" + p.NetId : p.Name);
                Announce($"{players.Count} player(s): {names}");
                ctx.Log.Info($"FunPanel players: {names}");
            });
        }

        // Run an action on the local player, with guards so a missing player or interop hiccup can't
        // take the menu down.
        private void Do(Action<MetaPlayer> action)
        {
            var local = _ctx.Net.LocalPlayer;
            var me = local != null ? local.Player : null;
            if (me == null) { Announce("Join / start a game first."); _ctx.Log.Info("FunPanel: no local player yet."); return; }
            try { action(me); }
            catch (Exception ex) { _ctx.Log.Error($"FunPanel action failed: {ex.Message}"); }
        }

        // Pop text on the game's own announcement banner (the thing it uses for in-game messages).
        private void Announce(string text)
        {
            try
            {
                var banner = LocalAnnouncementText.Instance;
                if (banner != null) banner.DisplayForSeconds(text, 2.5f);
                else _ctx.Log.Info($"[announce] {text}");
            }
            catch (Exception ex) { _ctx.Log.Warning($"announce failed: {ex.Message}"); }
        }
    }
}

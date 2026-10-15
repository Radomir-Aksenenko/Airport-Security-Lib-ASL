using System;
using ASL.Api;
using Metater;

namespace ASL
{
    /// <summary>
    /// Per-mod <see cref="IAslEvents"/> proxy handed out as <c>ctx.Events</c>. For now it simply forwards
    /// add/remove to the shared <see cref="EventBus"/>; the Core agent will make it track each mod's
    /// subscriptions (to release them on unload), isolate Update handlers per mod and measure per-mod time.
    /// </summary>
    internal sealed class ModEvents : IAslEvents
    {
        private readonly string _modId;
        private readonly IAslEvents _bus;

        public ModEvents(string modId, IAslEvents bus)
        {
            _modId = modId;
            _bus = bus;
        }

        public string ModId => _modId;

        public event Action Update { add => _bus.Update += value; remove => _bus.Update -= value; }
        public event Action<string> SceneChanged { add => _bus.SceneChanged += value; remove => _bus.SceneChanged -= value; }
        public event Action<MetaPlayer> LocalPlayerChanged { add => _bus.LocalPlayerChanged += value; remove => _bus.LocalPlayerChanged -= value; }
        public event Action LateUpdate { add => _bus.LateUpdate += value; remove => _bus.LateUpdate -= value; }
        public event Action<bool> MenuVisibilityChanged { add => _bus.MenuVisibilityChanged += value; remove => _bus.MenuVisibilityChanged -= value; }
        public event Action AllModsLoaded { add => _bus.AllModsLoaded += value; remove => _bus.AllModsLoaded -= value; }
        public event Action<string> LanguageChanged { add => _bus.LanguageChanged += value; remove => _bus.LanguageChanged -= value; }
        public event Action GameQuitting { add => _bus.GameQuitting += value; remove => _bus.GameQuitting -= value; }
    }
}

using ASL.Api;
using Metater;

namespace HelloMod
{
    /// <summary>
    /// Sample / template ASL mod. Reference ASL.API, subclass <see cref="AslMod"/>, subscribe to
    /// events and register menu controls in <see cref="OnLoad"/>. Build it, then drop
    /// <c>HelloMod.dll</c> + <c>manifest.json</c> into <c>mods/HelloMod/</c>.
    /// </summary>
    public sealed class HelloMod : AslMod
    {
        private IModContext _ctx;
        private bool _firstFrameLogged;
        private bool _logScenes = true;

        public override void OnLoad(IModContext ctx)
        {
            _ctx = ctx;
            ctx.Log.Info($"Hello from {ctx.ModName}! Wiring up ASL events and menu.");

            ctx.Events.Update += OnUpdate;
            ctx.Events.SceneChanged += OnSceneChanged;
            ctx.Events.LocalPlayerChanged += OnLocalPlayerChanged;

            // Demo controls in the shared F8 menu, grouped under this mod's name.
            ctx.Menu.AddLabel("A sample mod wired to ASL events.");
            ctx.Menu.AddToggle("Log scene changes", _logScenes, on => _logScenes = on);
            ctx.Menu.AddButton("Say hello in the log", () => ctx.Log.Info("Hello from the menu button!"));
            ctx.Menu.AddButton("Log network status", () =>
                ctx.Log.Info($"Net: online={ctx.Net.IsOnline} host={ctx.Net.IsHost} client={ctx.Net.IsConnectedClient} conns={ctx.Net.ConnectionCount}"));

            // Networking awareness: log when the connection count changes (server side).
            ctx.Net.ConnectionsChanged += n => ctx.Log.Info($"Connections changed -> {n}");
        }

        private void OnUpdate()
        {
            if (_firstFrameLogged) return;       // log only the first tick to keep the log clean
            _firstFrameLogged = true;
            _ctx.Log.Info("First Update tick received - the event bus is live. Press F8 for the menu.");
        }

        private void OnSceneChanged(string sceneName)
        {
            if (_logScenes) _ctx.Log.Info($"Scene changed -> {sceneName}");
        }

        private void OnLocalPlayerChanged(MetaPlayer player)
        {
            _ctx.Log.Info(player != null ? "Local player is ready!" : "Local player cleared.");
        }
    }
}

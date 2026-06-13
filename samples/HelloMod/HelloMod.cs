using ASL.Api;
using Metater;

namespace HelloMod
{
    /// <summary>
    /// Sample / template ASL mod. Reference ASL.API, subclass <see cref="AslMod"/>, and subscribe
    /// to whatever game events you care about in <see cref="OnLoad"/>. Build it, then drop
    /// <c>HelloMod.dll</c> + <c>manifest.json</c> into <c>mods/HelloMod/</c>.
    /// </summary>
    public sealed class HelloMod : AslMod
    {
        private IModContext _ctx;
        private bool _firstFrameLogged;

        public override void OnLoad(IModContext ctx)
        {
            _ctx = ctx;
            ctx.Log.Info($"Hello from {ctx.ModName}! Wiring up ASL events.");

            ctx.Events.Update += OnUpdate;
            ctx.Events.SceneChanged += OnSceneChanged;
            ctx.Events.LocalPlayerChanged += OnLocalPlayerChanged;
        }

        private void OnUpdate()
        {
            if (_firstFrameLogged) return;       // log only the first tick to keep the log clean
            _firstFrameLogged = true;
            _ctx.Log.Info("First Update tick received - the event bus is live.");
        }

        private void OnSceneChanged(string sceneName)
        {
            _ctx.Log.Info($"Scene changed -> {sceneName}");
        }

        private void OnLocalPlayerChanged(MetaPlayer player)
        {
            _ctx.Log.Info(player != null ? "Local player is ready!" : "Local player cleared.");
        }
    }
}

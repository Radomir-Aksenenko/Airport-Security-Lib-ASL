using System;
using ASL.Api;
using BepInEx.Logging;
using Metater;

namespace ASL
{
    /// <summary>
    /// Concrete event bus. ASL's internals call the Raise* methods; mods subscribe through the
    /// <see cref="IAslEvents"/> face.
    ///
    /// <see cref="RaiseUpdate"/> runs every frame, so it is allocation-free (direct invoke). The
    /// low-frequency events isolate each subscriber so one misbehaving mod cannot break the rest.
    /// </summary>
    internal sealed class EventBus : IAslEvents
    {
        private readonly ManualLogSource _log;

        public EventBus(ManualLogSource log) => _log = log;

        public event Action Update;
        public event Action<string> SceneChanged;
        public event Action<MetaPlayer> LocalPlayerChanged;

        // Hot path: invoked every frame. No GetInvocationList() allocation.
        public void RaiseUpdate()
        {
            try { Update?.Invoke(); }
            catch (Exception ex) { _log.LogError($"Event 'Update' handler threw: {ex.Message}"); }
        }

        public void RaiseSceneChanged(string sceneName) => Dispatch(SceneChanged, sceneName, "SceneChanged");

        public void RaiseLocalPlayerChanged(MetaPlayer player) => Dispatch(LocalPlayerChanged, player, "LocalPlayerChanged");

        // Per-subscriber isolation for the infrequent events.
        private void Dispatch<T>(Action<T> ev, T arg, string name)
        {
            if (ev == null) return;
            foreach (var h in ev.GetInvocationList())
            {
                try { ((Action<T>)h)(arg); }
                catch (Exception ex) { _log.LogError($"Event '{name}' handler threw: {ex.Message}"); }
            }
        }
    }
}

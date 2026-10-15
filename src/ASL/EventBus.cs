using System;
using ASL.Api;
using BepInEx.Logging;
using Metater;
using UnityEngine;

namespace ASL
{
    /// <summary>Something the bus ticks every frame (a mod's <see cref="ModEvents"/>, its <see cref="ModTimers"/>, the loader's hot-reload poll).</summary>
    internal interface IFrameParticipant
    {
        /// <summary>Once per frame, from Unity Update.</summary>
        void FrameUpdate(float deltaTime, float unscaledDeltaTime);
        /// <summary>Once per frame, from Unity LateUpdate.</summary>
        void FrameLateUpdate();
    }

    /// <summary>Receives the infrequent bus events, one sink per mod (so each mod's handlers are isolated and releasable).</summary>
    internal interface IEventSink
    {
        void OnSceneChanged(string sceneName);
        void OnLocalPlayerChanged(MetaPlayer player);
        void OnMenuVisibilityChanged(bool visible);
        void OnAllModsLoaded();
        void OnLanguageChanged(string language);
        void OnGameQuitting();
    }

    /// <summary>
    /// Concrete event bus. ASL's internals call the Raise* methods and may subscribe to the plain events;
    /// mods never touch the bus directly — each mod gets a <see cref="ModEvents"/> proxy that registers here as
    /// a frame participant + event sink.
    ///
    /// <see cref="RaiseUpdate"/> / <see cref="RaiseLateUpdate"/> run every frame, so they are allocation-free:
    /// the participant list is a copy-on-write array (changed only on mod load/unload) and a mod's handlers run
    /// inside the mod's own try/catch (see <see cref="ModEvents"/>), so one mod throwing never stops another.
    /// </summary>
    internal sealed class EventBus : IAslEvents
    {
        private readonly ManualLogSource _log;
        private IFrameParticipant[] _frame = Array.Empty<IFrameParticipant>();
        private IEventSink[] _sinks = Array.Empty<IEventSink>();

        public EventBus(ManualLogSource log) => _log = log;

        // Plain events: for ASL's own subsystems (keybinds, content, menu, ...). Mods use ModEvents.
        public event Action Update;
        public event Action<string> SceneChanged;
        public event Action<MetaPlayer> LocalPlayerChanged;
        public event Action LateUpdate;
        public event Action<bool> MenuVisibilityChanged;
        public event Action AllModsLoaded;
        public event Action<string> LanguageChanged;
        public event Action GameQuitting;

        // ---- registration (rare; copy-on-write so a running frame loop is never disturbed) -------

        public void Register(IFrameParticipant p) => _frame = With(_frame, p);
        public void Unregister(IFrameParticipant p) => _frame = Without(_frame, p);
        public void Register(IEventSink s) => _sinks = With(_sinks, s);
        public void Unregister(IEventSink s) => _sinks = Without(_sinks, s);

        private static T[] With<T>(T[] cur, T item) where T : class
        {
            if (item == null) return cur;
            for (int i = 0; i < cur.Length; i++) if (ReferenceEquals(cur[i], item)) return cur;
            var next = new T[cur.Length + 1];
            Array.Copy(cur, next, cur.Length);
            next[cur.Length] = item;
            return next;
        }

        private static T[] Without<T>(T[] cur, T item) where T : class
        {
            int at = -1;
            for (int i = 0; i < cur.Length; i++) if (ReferenceEquals(cur[i], item)) { at = i; break; }
            if (at < 0) return cur;
            var next = new T[cur.Length - 1];
            if (at > 0) Array.Copy(cur, 0, next, 0, at);
            if (at < cur.Length - 1) Array.Copy(cur, at + 1, next, at, cur.Length - at - 1);
            return next;
        }

        // ---- hot path ---------------------------------------------------------------------------

        public void RaiseUpdate()
        {
            float dt = Time.deltaTime, udt = Time.unscaledDeltaTime;

            try { Update?.Invoke(); }
            catch (Exception ex) { _log.LogError($"Event 'Update' handler threw: {ex.Message}"); }

            var parts = _frame;   // snapshot: registrations during the loop apply from the next frame
            for (int i = 0; i < parts.Length; i++)
            {
                try { parts[i].FrameUpdate(dt, udt); }
                catch (Exception ex) { _log.LogError($"Frame participant threw in Update: {ex.Message}"); }
            }

            Profiler.EndFrame(udt);
        }

        public void RaiseLateUpdate()
        {
            try { LateUpdate?.Invoke(); }
            catch (Exception ex) { _log.LogError($"Event 'LateUpdate' handler threw: {ex.Message}"); }

            var parts = _frame;
            for (int i = 0; i < parts.Length; i++)
            {
                try { parts[i].FrameLateUpdate(); }
                catch (Exception ex) { _log.LogError($"Frame participant threw in LateUpdate: {ex.Message}"); }
            }
        }

        // ---- infrequent events ---------------------------------------------------------------------

        public void RaiseMenuVisibilityChanged(bool visible)
        {
            Dispatch(MenuVisibilityChanged, visible, "MenuVisibilityChanged");
            var sinks = _sinks;
            for (int i = 0; i < sinks.Length; i++) { try { sinks[i].OnMenuVisibilityChanged(visible); } catch (Exception ex) { SinkFailed("MenuVisibilityChanged", ex); } }
        }

        public void RaiseAllModsLoaded()
        {
            Dispatch(AllModsLoaded, "AllModsLoaded");
            var sinks = _sinks;
            for (int i = 0; i < sinks.Length; i++) { try { sinks[i].OnAllModsLoaded(); } catch (Exception ex) { SinkFailed("AllModsLoaded", ex); } }
        }

        public void RaiseLanguageChanged(string language)
        {
            Dispatch(LanguageChanged, language, "LanguageChanged");
            var sinks = _sinks;
            for (int i = 0; i < sinks.Length; i++) { try { sinks[i].OnLanguageChanged(language); } catch (Exception ex) { SinkFailed("LanguageChanged", ex); } }
        }

        public void RaiseGameQuitting()
        {
            Dispatch(GameQuitting, "GameQuitting");
            var sinks = _sinks;
            for (int i = 0; i < sinks.Length; i++) { try { sinks[i].OnGameQuitting(); } catch (Exception ex) { SinkFailed("GameQuitting", ex); } }
        }

        public void RaiseSceneChanged(string sceneName)
        {
            Dispatch(SceneChanged, sceneName, "SceneChanged");
            var sinks = _sinks;
            for (int i = 0; i < sinks.Length; i++) { try { sinks[i].OnSceneChanged(sceneName); } catch (Exception ex) { SinkFailed("SceneChanged", ex); } }
        }

        public void RaiseLocalPlayerChanged(MetaPlayer player)
        {
            Dispatch(LocalPlayerChanged, player, "LocalPlayerChanged");
            var sinks = _sinks;
            for (int i = 0; i < sinks.Length; i++) { try { sinks[i].OnLocalPlayerChanged(player); } catch (Exception ex) { SinkFailed("LocalPlayerChanged", ex); } }
        }

        private void SinkFailed(string name, Exception ex) => _log.LogError($"Event '{name}' dispatch threw: {ex.Message}");

        // Per-subscriber isolation for ASL's own infrequent subscribers.
        private void Dispatch(Action ev, string name)
        {
            if (ev == null) return;
            foreach (var h in ev.GetInvocationList())
            {
                try { ((Action)h)(); }
                catch (Exception ex) { _log.LogError($"Event '{name}' handler threw: {ex.Message}"); }
            }
        }

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

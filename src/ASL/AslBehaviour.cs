using System;
using Metater;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ASL
{
    /// <summary>
    /// Injected MonoBehaviour that drives the event bus. The per-frame cost is deliberately tiny:
    /// only the (allocation-free) Update event fires every frame. Scene and local-player changes
    /// are detected by a throttled poll a few times per second, which keeps IL2CPP interop and
    /// GC pressure off the hot path. Created once, kept alive across scenes.
    /// </summary>
    internal sealed class AslBehaviour : MonoBehaviour
    {
        // Required ctor for IL2CPP-injected MonoBehaviours.
        public AslBehaviour(IntPtr ptr) : base(ptr) { }

        private const float PollInterval = 0.5f;
        private float _pollTimer;
        private string _lastScene;
        private IntPtr _lastLocalPlayerPtr = IntPtr.Zero;
        private IntPtr _candidatePlayerPtr = IntPtr.Zero;
        private int _candidateHits;

        private void Update()
        {
            var bus = AslPlugin.Bus;
            if (bus == null) return;

            bus.RaiseUpdate();   // per-frame, allocation-free

            _pollTimer += Time.unscaledDeltaTime;
            if (_pollTimer < PollInterval) return;
            _pollTimer = 0f;

            // Active-scene change (polled, not per-frame).
            try
            {
                var scene = SceneManager.GetActiveScene().name;
                if (scene != _lastScene)
                {
                    _lastScene = scene;
                    bus.RaiseSceneChanged(scene);
                }
            }
            catch { /* scene manager not ready yet */ }

            // Local-player change, debounced: the game briefly flaps LocalPlayerInstance during
            // transitions, so only fire once a new value has held across two consecutive polls.
            try
            {
                var lp = MetaPlayer.LocalPlayerInstance;
                var ptr = lp != null ? lp.Pointer : IntPtr.Zero;
                if (ptr != _lastLocalPlayerPtr)
                {
                    if (ptr != _candidatePlayerPtr)
                    {
                        _candidatePlayerPtr = ptr;
                        _candidateHits = 1;
                    }
                    else if (++_candidateHits >= 2)
                    {
                        _lastLocalPlayerPtr = ptr;
                        _candidateHits = 0;
                        bus.RaiseLocalPlayerChanged(lp);
                    }
                }
            }
            catch { /* player subsystem not ready yet */ }
        }
    }
}

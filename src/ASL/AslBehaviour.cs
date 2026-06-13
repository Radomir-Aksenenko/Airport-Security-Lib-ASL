using System;
using System.Runtime.InteropServices;
using Metater;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ASL
{
    /// <summary>
    /// Injected MonoBehaviour that drives the framework: the per-frame event tick, the throttled
    /// scene/local-player polling, the F8 menu toggle, and the IMGUI menu render. Per-frame cost is
    /// deliberately tiny (only the alloc-free Update event + a key check run every frame). Created
    /// once, kept alive across scenes.
    /// </summary>
    internal sealed class AslBehaviour : MonoBehaviour
    {
        // Required ctor for IL2CPP-injected MonoBehaviours.
        public AslBehaviour(IntPtr ptr) : base(ptr) { }

        // Win32 key state — reliable regardless of the game's input system (same approach BepInEx
        // mods commonly use on this build). F8 toggles the mod menu.
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
        private const int VK_F8 = 0x77;
        private bool _wasToggleDown;

        private const float PollInterval = 0.5f;
        private float _pollTimer;
        private string _lastScene;
        private IntPtr _lastLocalPlayerPtr = IntPtr.Zero;
        private IntPtr _candidatePlayerPtr = IntPtr.Zero;
        private int _candidateHits;

        private void Update()
        {
            // Menu toggle — checked every frame for responsiveness.
            var menu = AslPlugin.Menu;
            if (menu != null)
            {
                bool down = (GetAsyncKeyState(VK_F8) & 0x8000) != 0;
                if (down && !_wasToggleDown) menu.Visible = !menu.Visible;
                _wasToggleDown = down;

                if (menu.Visible)
                {
                    // Free the cursor so the menu is clickable during gameplay.
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.None;
                }
            }

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

            // Networking awareness (connection-count changes), polled on the same throttle.
            AslPlugin.Net?.Poll();

            // Try to add the "Mods" button to the main menu (bounded retries after scene changes).
            MainMenuInjector.Tick();
        }
    }
}

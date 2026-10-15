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
            // UI toolkit driver first, so text-focus state is fresh for the F8 check and the keybinds below.
            UiRuntime.Tick();

            // Menu toggle — checked every frame for responsiveness. Suppressed while typing in an ASL text field.
            var menu = AslPlugin.Menu;
            if (menu != null)
            {
                bool down = (GetAsyncKeyState(VK_F8) & 0x8000) != 0;
                if (down && !_wasToggleDown && !UiRuntime.IsTextInputFocused) menu.Visible = !menu.Visible;
                _wasToggleDown = down;

                if (menu.Visible) AslPlugin.MenuUi?.TickBackground();   // scroll the animated backdrop
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
                    UiRuntime.SetReady();   // first scene = the UI runtime may build / open (idempotent)
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

            // Keep the "Mods" button on the main menu (self-healing; stays out of the in-game pause menu).
            MainMenuInjector.Tick();
            // Re-localize the Mods button if the player switched language.
            MainMenuInjector.PollLanguage();
        }

        // Freeing the cursor / blocking player input is done by CursorManager (reason based: the F8 menu,
        // open toolkit windows, a focused text field) with the exact mechanism the F8 menu always used:
        // register this behaviour as a MetaCursor "cursor user" (the game's own RequireCursor way — writing
        // Cursor.lockState ourselves loses a per-frame race with MetaCursor.LateUpdate) and block the local
        // controller's input so the camera doesn't spin while you use the UI.
        private void LateUpdate()
        {
            AslPlugin.Bus?.RaiseLateUpdate();   // per-frame, allocation-free

            var menu = AslPlugin.Menu;
            CursorManager.Apply(this, menu != null && menu.Visible);
        }

        // Safety: never leave ourselves registered (a stale entry could wedge the cursor free forever).
        private void OnDisable() => CursorManager.Release(this);
        private void OnDestroy() => CursorManager.Release(this);

        // BasePlugin.Unload is not reliably called on IL2CPP exit; this is the best-effort GameQuitting / OnUnload trigger.
        private void OnApplicationQuit() { try { AslPlugin.Instance?.Shutdown(); } catch { } }
    }
}

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

        private bool _menuWasVisible;
        private bool _registeredCursorUser;
        private bool _inputBlocked;

        // Freeing the cursor for the F8 menu the WRONG way (writing Cursor.lockState ourselves) loses a
        // per-frame race: the game's cursor is owned by the singleton Metater.MetaCursor, whose own
        // LateUpdate re-asserts the state from its CursorUsers set. So we do it the game's OWN way —
        // register this behaviour as a "cursor user" while the menu is open (exactly like the game's
        // RequireCursor component) and let MetaCursor free the cursor. We also block player look/move so
        // the camera doesn't spin while you use the menu.
        private void LateUpdate()
        {
            var menu = AslPlugin.Menu;
            bool visible = menu != null && menu.Visible;

            if (visible)
            {
                if (!_registeredCursorUser)
                {
                    try
                    {
                        var mc = MetaCursor.Instance;                 // null on the main menu (cursor already free there)
                        var users = mc != null ? mc.CursorUsers : null;
                        if (users != null) { users.Add(this); _registeredCursorUser = true; }
                    }
                    catch { }
                }
                if (SetLocalInputBlocked(true)) _inputBlocked = true;
            }
            else
            {
                UnregisterCursorUser();
                if (_inputBlocked && SetLocalInputBlocked(false)) _inputBlocked = false;   // retry until a player exists
            }
            _menuWasVisible = visible;
        }

        private void UnregisterCursorUser()
        {
            if (!_registeredCursorUser) return;
            try { MetaCursor.Instance?.CursorUsers?.Remove(this); } catch { }
            _registeredCursorUser = false;
        }

        // Safety: never leave ourselves registered (a stale entry could wedge the cursor free forever).
        private void OnDisable() => UnregisterCursorUser();
        private void OnDestroy() => UnregisterCursorUser();

        // Returns true if the local controller was found and the flag was set — callers retry until then.
        private static bool SetLocalInputBlocked(bool blocked)
        {
            try
            {
                var lp = MetaPlayer.LocalPlayerInstance;
                if (lp == null) return false;
                return PlayerControl.SetInputBlocked(lp, blocked);
            }
            catch { return false; }
        }
    }
}

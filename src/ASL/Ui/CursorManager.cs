using System;
using Metater;
using UnityEngine;

namespace ASL
{
    /// <summary>
    /// Decides whether the game cursor must be free and player input blocked, from a set of reasons: the F8
    /// menu, open toolkit windows (<c>CaptureCursor</c> / <c>BlockGameInput</c>) and a focused text field.
    /// Applied once per LateUpdate by <see cref="AslBehaviour"/> using EXACTLY the mechanism the F8 menu always
    /// used: register the behaviour as a <c>MetaCursor.CursorUsers</c> entry (the game's own RequireCursor way —
    /// writing Cursor.lockState ourselves loses a per-frame race) and set the local controller's
    /// <c>debugBlockInput</c> via <see cref="PlayerControl.SetInputBlocked"/>, retrying until a player exists.
    /// </summary>
    internal static class CursorManager
    {
        private static bool _windowCursor, _windowBlock, _textInput;
        private static bool _registered, _inputBlocked;
        private static MonoBehaviour _owner;

        /// <summary>Reason "open windows": any open window wants the cursor / wants input blocked.</summary>
        public static void SetWindows(bool wantCursor, bool wantBlock) { _windowCursor = wantCursor; _windowBlock = wantBlock; }

        /// <summary>Reason "text input": a toolkit text field has keyboard focus.</summary>
        public static void SetTextInput(bool focused) { _textInput = focused; }

        public static void Apply(MonoBehaviour owner, bool menuVisible)
        {
            _owner = owner;
            bool wantCursor = menuVisible || _windowCursor || _textInput;
            bool wantBlock = menuVisible || _windowBlock || _textInput;

            if (wantCursor)
            {
                if (!_registered)
                {
                    try
                    {
                        var mc = MetaCursor.Instance;                 // null on the main menu (cursor already free there)
                        var users = mc != null ? mc.CursorUsers : null;
                        if (users != null) { users.Add(owner); _registered = true; }
                    }
                    catch { }
                }
            }
            else Unregister(owner);

            if (wantBlock)
            {
                if (SetLocalInputBlocked(true)) _inputBlocked = true;
            }
            else if (_inputBlocked && SetLocalInputBlocked(false)) _inputBlocked = false;   // retry until a player exists
        }

        /// <summary>Safety: never leave the behaviour registered (a stale entry could wedge the cursor free forever).</summary>
        public static void Release(MonoBehaviour owner) => Unregister(owner ?? _owner);

        private static void Unregister(MonoBehaviour owner)
        {
            if (!_registered) return;
            try { MetaCursor.Instance?.CursorUsers?.Remove(owner); } catch { }
            _registered = false;
        }

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

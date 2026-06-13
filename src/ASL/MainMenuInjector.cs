using System;
using ASL.Api;
using BepInEx.Logging;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ASL
{
    /// <summary>
    /// Adds a "Mods" button to the game's main menu (alongside Play / Settings / Quit) that opens the
    /// ASL menu. Works by cloning the existing <b>Settings</b> button, relabelling it, and rewiring its
    /// click — no dependency on the game's menu class (buttons are matched by their TMP label). Only
    /// the main menu is targeted (it has a "Play" button), and the inject is retried for a short window
    /// after each scene change so it catches the menu once its UI exists.
    /// </summary>
    internal static class MainMenuInjector
    {
        private const string CloneName = "ASL_ModsButton";
        private static ManualLogSource _log;
        private static int _attempts;

        public static void Init(ManualLogSource log, IAslEvents events)
        {
            _log = log;
            _attempts = 12;
            events.SceneChanged += _ => _attempts = 12;   // re-try when (re)entering the menu
        }

        // Called from the framework's throttled tick; cheap no-op once injected or off the menu.
        public static void Tick()
        {
            if (_attempts <= 0) return;
            _attempts--;
            try { if (TryInject()) _attempts = 0; }
            catch (Exception ex) { _log.LogError($"[menu] main-menu inject failed: {ex.Message}"); _attempts = 0; }
        }

        private static bool TryInject()
        {
            var buttons = Resources.FindObjectsOfTypeAll<Button>();

            Button settingsBtn = null;
            bool hasPlay = false;

            for (int i = 0; i < buttons.Length; i++)
            {
                var b = buttons[i];
                if (b == null) continue;
                if (b.gameObject.name == CloneName) return true;        // already injected this menu
                if (!b.gameObject.activeInHierarchy) continue;

                var label = b.GetComponentInChildren<TMP_Text>(true);
                if (label == null) continue;

                var text = (label.text ?? string.Empty).Trim();
                if (text.Equals("Play", StringComparison.OrdinalIgnoreCase)) hasPlay = true;
                else if (text.Equals("Settings", StringComparison.OrdinalIgnoreCase)) settingsBtn = b;
            }

            // Only inject on the MAIN menu (it has a Play button) and only if we can clone Settings.
            if (!hasPlay || settingsBtn == null) return false;

            var parent = settingsBtn.transform.parent;
            var clone = UnityEngine.Object.Instantiate(settingsBtn.gameObject, parent);
            clone.name = CloneName;

            var cloneLabel = clone.GetComponentInChildren<TMP_Text>(true);
            if (cloneLabel != null) cloneLabel.text = "Mods";

            var btn = clone.GetComponent<Button>();
            var click = btn.onClick;
            int persistent = click.GetPersistentEventCount();
            for (int i = 0; i < persistent; i++) click.SetPersistentListenerState(i, UnityEventCallState.Off);
            click.RemoveAllListeners();
            click.AddListener(DelegateSupport.ConvertDelegate<UnityAction>(new Action(OpenMenu)));

            // Slot it right after Settings.
            clone.transform.SetSiblingIndex(settingsBtn.transform.GetSiblingIndex() + 1);

            _log.LogInfo("[menu] Added 'Mods' button to the main menu.");
            return true;
        }

        private static void OpenMenu()
        {
            if (AslPlugin.Menu != null) AslPlugin.Menu.Visible = !AslPlugin.Menu.Visible;
        }
    }
}

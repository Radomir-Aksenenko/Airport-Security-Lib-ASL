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
    /// ASL menu, and captures a copy of a real menu button as the template the native menu clones for
    /// its rows. Buttons are matched by their TMP label (no dependency on the game's menu class); only
    /// the main menu is targeted (it has a "Play" button). Retried for a short window after each scene
    /// change so it catches the menu once its UI exists.
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

            // Only on the MAIN menu (it has a Play button) and only if we can clone Settings.
            if (!hasPlay || settingsBtn == null) return false;

            // Capture a template for the native menu (a separate, stored, deactivated clone).
            AslPlugin.Ui?.SetTemplate(UnityEngine.Object.Instantiate(settingsBtn.gameObject));

            // Add the visible "Mods" button next to Settings.
            var clone = UnityEngine.Object.Instantiate(settingsBtn.gameObject, settingsBtn.transform.parent);
            clone.name = CloneName;

            UiUtil.SetLabel(clone, "Mods");   // strips the localizer so it stays "Mods", not "Settings"

            var btn = clone.GetComponent<Button>();
            var click = btn.onClick;
            int persistent = click.GetPersistentEventCount();
            for (int i = 0; i < persistent; i++) click.SetPersistentListenerState(i, UnityEventCallState.Off);
            click.RemoveAllListeners();
            click.AddListener(DelegateSupport.ConvertDelegate<UnityAction>(new Action(OpenMenu)));

            clone.transform.SetSiblingIndex(settingsBtn.transform.GetSiblingIndex() + 1);

            // Re-run the button's enable hooks so its entrance/idle animation plays like the others.
            clone.SetActive(false);
            clone.SetActive(true);

            _log.LogInfo("[menu] Added 'Mods' button to the main menu.");
            return true;
        }

        private static void OpenMenu()
        {
            if (AslPlugin.Menu != null) AslPlugin.Menu.Visible = !AslPlugin.Menu.Visible;
        }
    }
}

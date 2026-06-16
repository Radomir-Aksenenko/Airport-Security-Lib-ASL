using System;
using ASL.Api;
using BepInEx.Logging;
using Il2CppInterop.Runtime;
using Metater;
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
        private static GameObject _modsButton;   // the live "Mods" button, for re-localizing
        private static string _lastLang;

        public static void Init(ManualLogSource log, IAslEvents events)
        {
            _log = log;
        }

        // Self-healing: called on the throttled poll. The main menu can be rebuilt without a scene-name
        // change (it's a persistent scene), which is why the button used to vanish after a game — so we
        // re-inject whenever our button isn't present and we're back on the main menu. The
        // no-local-player gate keeps us OUT of the in-game pause/settings menu entirely.
        public static void Tick()
        {
            try
            {
                if (ButtonAlive()) return;            // our button is up → nothing to do
                if (InGame()) return;                 // in a game / pause menu → never inject (keeps it out of there)
                TryInject();                          // (re)inject once the main menu exists
            }
            catch (Exception ex) { _log.LogError($"[menu] main-menu inject failed: {ex.Message}"); }
        }

        private static bool ButtonAlive()
        {
            try { return _modsButton != null && _modsButton.activeInHierarchy; }
            catch { return false; }
        }

        private static bool InGame()
        {
            try { return MetaPlayer.LocalPlayerInstance != null; }
            catch { return false; }
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
            AslPlugin.MenuUi?.SetTemplate(UnityEngine.Object.Instantiate(settingsBtn.gameObject));

            // Add the visible "Mods" button next to Settings.
            var clone = UnityEngine.Object.Instantiate(settingsBtn.gameObject, settingsBtn.transform.parent);
            clone.name = CloneName;

            UiUtil.SetLabel(clone, Loc.Mods());   // localized; also strips the game's localizer so it stays put
            ApplyModsIcon(clone);

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

            _modsButton = clone;
            _lastLang = Loc.CurrentLanguage();

            _log.LogInfo("[menu] Added 'Mods' button to the main menu.");
            return true;
        }

        private static void OpenMenu()
        {
            if (AslPlugin.Menu != null) AslPlugin.Menu.Visible = !AslPlugin.Menu.Visible;
        }

        /// <summary>Re-localizes the Mods button when the player changes language. Cheap; polled.</summary>
        public static void PollLanguage()
        {
            try
            {
                var cur = Loc.CurrentLanguage();
                if (cur == _lastLang) return;
                _lastLang = cur;
                if (_modsButton != null) UiUtil.SetLabel(_modsButton, Loc.Mods());
            }
            catch { }
        }

        // Replace the cloned button's icon (a gear) with the custom Mods icon. Targets a child icon
        // image only — never the button's own background panel.
        private static void ApplyModsIcon(GameObject buttonGo)
        {
            try
            {
                var spr = UiUtil.ModsIcon();
                if (spr == null) return;

                Image iconImg = null;
                var ih = buttonGo.transform.Find("IconHolder");
                if (ih != null) iconImg = ih.GetComponentInChildren<Image>(true);
                if (iconImg == null)
                {
                    var im = buttonGo.transform.Find("Image");
                    if (im != null) iconImg = im.GetComponent<Image>();
                }
                if (iconImg != null) iconImg.sprite = spr;
            }
            catch (Exception ex) { _log.LogWarning($"[menu] set mods icon failed: {ex.Message}"); }
        }
    }
}

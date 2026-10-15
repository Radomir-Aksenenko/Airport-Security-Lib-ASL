using System;
using ASL.Api;
using BepInEx.Logging;

namespace ASL
{
    /// <summary>
    /// Shared (one instance for all mods) helper behind <c>IAslUi.Announce</c> — the announcement banner is a
    /// single global widget. Wraps the game's <c>LocalAnnouncementText</c> so mods don't each re-derive it.
    /// The per-mod <see cref="IAslUi"/> implementation is <see cref="ModUi"/>, which delegates here.
    /// </summary>
    internal sealed class UiManager
    {
        private readonly ManualLogSource _log;

        public UiManager(ManualLogSource log) => _log = log;

        public void Announce(string text, float seconds = 2.5f)
        {
            try
            {
                var banner = LocalAnnouncementText.Instance;
                if (banner != null) banner.DisplayForSeconds(text, seconds);
                else _log.LogInfo($"[announce] {text}");
            }
            catch (Exception ex) { _log.LogWarning($"Ui.Announce failed: {ex.Message}"); }
        }
    }
}

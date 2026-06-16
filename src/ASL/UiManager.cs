using System;
using ASL.Api;
using BepInEx.Logging;

namespace ASL
{
    /// <summary>
    /// Implements <see cref="IAslUi"/>. One shared instance for all mods — the announcement banner is a
    /// single global widget. Wraps the game's <c>LocalAnnouncementText</c> so mods don't each re-derive it.
    /// </summary>
    internal sealed class UiManager : IAslUi
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

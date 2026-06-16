namespace ASL.Api
{
    /// <summary>
    /// On-screen UI helpers that drive the game's own widgets, so a mod's messages look native.
    /// Reached through <c>ctx.Ui</c>. (Before this existed, every mod re-implemented the announcement
    /// banner by hand — now it's one call.)
    /// </summary>
    public interface IAslUi
    {
        /// <summary>
        /// Show <paramref name="text"/> on the game's on-screen announcement banner (the same widget the
        /// game uses for its own messages) for <paramref name="seconds"/>. Safe to call before a level
        /// loads — it logs instead if the banner isn't up yet.
        /// </summary>
        void Announce(string text, float seconds = 2.5f);
    }
}

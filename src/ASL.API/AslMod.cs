namespace ASL.Api
{
    /// <summary>
    /// Base class for code and script mods. The "easy" path: subclass it, override
    /// <see cref="OnLoad"/>, build a DLL (or — later — write a .cs script), then drop the
    /// result plus a <c>manifest.json</c> into the game's <c>mods/&lt;YourMod&gt;/</c> folder.
    /// ASL discovers it, instantiates this class, and calls <see cref="OnLoad"/> once.
    /// </summary>
    public abstract class AslMod
    {
        /// <summary>
        /// Called once when ASL loads the mod. Do your setup here: read assets from
        /// <see cref="IModContext.ModDirectory"/>, log via <see cref="IModContext.Log"/>, and
        /// (from later phases) subscribe to game events or register content.
        /// </summary>
        /// <param name="ctx">Per-mod services provided by ASL.</param>
        public abstract void OnLoad(IModContext ctx);

        /// <summary>Called when the mod is unloaded (e.g. on game shutdown). Optional.</summary>
        public virtual void OnUnload() { }
    }
}

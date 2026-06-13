using System;

namespace ASL.Api
{
    /// <summary>
    /// Register simple controls into ASL's shared in-game menu (toggle it in-game with <b>F8</b>).
    /// Call these in <see cref="AslMod.OnLoad"/> via <see cref="IModContext.Menu"/>; your controls
    /// appear grouped under your mod's name. All callbacks run on the main thread.
    /// </summary>
    public interface IModMenu
    {
        /// <summary>A static text line.</summary>
        void AddLabel(string text);

        /// <summary>A checkbox. <paramref name="onChanged"/> fires with the new value when toggled.</summary>
        void AddToggle(string label, bool initial, Action<bool> onChanged);

        /// <summary>A button. <paramref name="onClick"/> fires when pressed.</summary>
        void AddButton(string label, Action onClick);

        /// <summary>A slider in [min, max]. <paramref name="onChanged"/> fires as the value moves.</summary>
        void AddSlider(string label, float min, float max, float initial, Action<float> onChanged);
    }
}

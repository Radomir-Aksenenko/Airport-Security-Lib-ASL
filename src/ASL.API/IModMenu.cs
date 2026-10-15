using System;

namespace ASL.Api
{
    /// <summary>
    /// Register simple controls into ASL's shared in-game menu (open it in-game with <b>F8</b> or the
    /// <b>Mods</b> button ASL adds to the main menu).
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

        /// <summary>
        /// A row on your mod's page that opens a rich sub-page. <paramref name="build"/> is called each
        /// time the sub-page is shown, with a container to fill using the UI toolkit.
        /// <code>ctx.Menu.AddPage("Advanced", page => { page.AddHeader("Advanced"); page.AddToggle("Debug", false, v => debug = v); });</code>
        /// </summary>
        void AddPage(string title, Action<IUiContainer> build);

        /// <summary>A row that opens/toggles <paramref name="window"/> (created with <c>ctx.Ui.CreateWindow</c>).</summary>
        void AddWindowButton(string label, IUiWindow window);

        /// <summary>A visual group header row. <paramref name="title"/> null = a plain divider.</summary>
        void AddSeparator(string title = null);
    }
}

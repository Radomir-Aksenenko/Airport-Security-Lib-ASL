using System;
using System.Collections.Generic;
using ASL.Api;

namespace ASL
{
    /// <summary>
    /// Registry for the shared mod menu. Mods add controls through a per-mod <see cref="IModMenu"/>
    /// (via <c>ctx.Menu</c>); the actual rendering is done natively (uGUI) by <see cref="NativeMenu"/>,
    /// which reads these sections. <see cref="Visible"/> drives show/hide and raises
    /// <see cref="VisibleChanged"/> (F8 and the main-menu "Mods" button flip it).
    /// </summary>
    internal sealed class MenuManager
    {
        private readonly List<Section> _sections = new();
        private readonly Dictionary<string, Section> _byMod = new();
        private bool _visible;

        public IReadOnlyList<Section> Sections => _sections;
        public event Action<bool> VisibleChanged;

        public bool Visible
        {
            get => _visible;
            set
            {
                if (_visible == value) return;
                _visible = value;
                VisibleChanged?.Invoke(value);
            }
        }

        public IModMenu For(string modName)
        {
            if (!_byMod.TryGetValue(modName, out var sec))
            {
                sec = new Section { ModName = modName };
                _byMod[modName] = sec;
                _sections.Add(sec);
            }
            return new ModMenu(sec);
        }

        internal sealed class Section
        {
            public string ModName;
            public readonly List<MenuControl> Controls = new();
        }
    }

    // ---- Control data (rendered by NativeMenu) -----------------------------------------------

    internal abstract class MenuControl { public string Label; }
    internal sealed class LabelControl  : MenuControl { }
    internal sealed class ButtonControl : MenuControl { public Action OnClick; }
    internal sealed class ToggleControl : MenuControl { public bool Value; public Action<bool> OnChanged; }
    internal sealed class SliderControl : MenuControl { public float Min, Max, Value; public Action<float> OnChanged; }

    /// <summary>Per-mod <see cref="IModMenu"/> that appends to one section.</summary>
    internal sealed class ModMenu : IModMenu
    {
        private readonly MenuManager.Section _sec;
        public ModMenu(MenuManager.Section sec) => _sec = sec;

        public void AddLabel(string text) => _sec.Controls.Add(new LabelControl { Label = text });
        public void AddButton(string label, Action onClick) => _sec.Controls.Add(new ButtonControl { Label = label, OnClick = onClick });
        public void AddToggle(string label, bool initial, Action<bool> onChanged) => _sec.Controls.Add(new ToggleControl { Label = label, Value = initial, OnChanged = onChanged });
        public void AddSlider(string label, float min, float max, float initial, Action<float> onChanged) => _sec.Controls.Add(new SliderControl { Label = label, Min = min, Max = max, Value = initial, OnChanged = onChanged });
    }
}

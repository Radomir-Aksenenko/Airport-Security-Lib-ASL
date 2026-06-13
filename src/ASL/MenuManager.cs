using System;
using System.Collections.Generic;
using ASL.Api;
using BepInEx.Logging;
using UnityEngine;

namespace ASL
{
    /// <summary>
    /// The shared in-game mod menu (IMGUI). Mods register controls through a per-mod
    /// <see cref="IModMenu"/> (via <c>ctx.Menu</c>); ASL draws them grouped by mod in a draggable
    /// window toggled with F8. Each control's callback is isolated so one mod can't break the menu.
    /// </summary>
    internal sealed class MenuManager
    {
        private readonly ManualLogSource _log;
        private readonly List<Section> _sections = new();
        private readonly Dictionary<string, Section> _byMod = new();
        private Rect _window = new Rect(40f, 40f, 360f, 500f);
        private Vector2 _scroll;

        public bool Visible;

        public MenuManager(ManualLogSource log) => _log = log;

        /// <summary>Returns a per-mod menu facade; controls added through it group under <paramref name="modName"/>.</summary>
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

        public void Draw()
        {
            if (!Visible) return;
            _window = GUI.Window(0x0A51, _window, (GUI.WindowFunction)DrawWindow, "ASL — Mods (F8)");
        }

        private void DrawWindow(int id)
        {
            _scroll = GUILayout.BeginScrollView(_scroll);
            if (_sections.Count == 0)
                GUILayout.Label("No mod controls yet. Mods add controls via ctx.Menu in OnLoad.");

            foreach (var sec in _sections)
            {
                GUILayout.Space(8);
                GUILayout.Label($"<b>{sec.ModName}</b>");
                var controls = sec.Controls;
                for (int i = 0; i < controls.Count; i++)
                {
                    try { controls[i].Draw(); }
                    catch (Exception ex) { _log.LogError($"menu control ('{sec.ModName}') threw: {ex.Message}"); }
                }
            }

            GUILayout.EndScrollView();
            GUILayout.Space(6);
            GUILayout.Label("<i>Press F8 to close</i>");
            GUI.DragWindow();
        }

        internal sealed class Section
        {
            public string ModName;
            public readonly List<MenuControl> Controls = new();
        }
    }

    internal abstract class MenuControl
    {
        public abstract void Draw();
    }

    internal sealed class LabelControl : MenuControl
    {
        public string Text;
        public override void Draw() => GUILayout.Label(Text);
    }

    internal sealed class ToggleControl : MenuControl
    {
        public string Label;
        public bool Value;
        public Action<bool> OnChanged;
        public override void Draw()
        {
            bool v = GUILayout.Toggle(Value, " " + Label);
            if (v != Value) { Value = v; OnChanged?.Invoke(v); }
        }
    }

    internal sealed class ButtonControl : MenuControl
    {
        public string Label;
        public Action OnClick;
        public override void Draw()
        {
            if (GUILayout.Button(Label)) OnClick?.Invoke();
        }
    }

    internal sealed class SliderControl : MenuControl
    {
        public string Label;
        public float Min, Max, Value;
        public Action<float> OnChanged;
        public override void Draw()
        {
            GUILayout.Label($"{Label}: {Value:0.##}");
            float v = GUILayout.HorizontalSlider(Value, Min, Max);
            if (Mathf.Abs(v - Value) > 0.0001f) { Value = v; OnChanged?.Invoke(v); }
        }
    }

    /// <summary>Per-mod <see cref="IModMenu"/> that appends to one section.</summary>
    internal sealed class ModMenu : IModMenu
    {
        private readonly MenuManager.Section _sec;
        public ModMenu(MenuManager.Section sec) => _sec = sec;

        public void AddLabel(string text) => _sec.Controls.Add(new LabelControl { Text = text });
        public void AddToggle(string label, bool initial, Action<bool> onChanged) => _sec.Controls.Add(new ToggleControl { Label = label, Value = initial, OnChanged = onChanged });
        public void AddButton(string label, Action onClick) => _sec.Controls.Add(new ButtonControl { Label = label, OnClick = onClick });
        public void AddSlider(string label, float min, float max, float initial, Action<float> onChanged) => _sec.Controls.Add(new SliderControl { Label = label, Min = min, Max = max, Value = initial, OnChanged = onChanged });
    }
}

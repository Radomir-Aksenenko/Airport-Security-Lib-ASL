using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ASL.Api;
using UnityEngine;

namespace ASL
{
    /// <summary>
    /// The auto-generated "Settings" page of a mod's F8 menu (<see cref="IAslConfig.ShowInMenu"/>): one box per config
    /// section with a control per entry, generated from <see cref="ConfigEntryInfo"/> - bool: toggle, ranged
    /// int/float: slider, enum: dropdown, string: text field (Enter saves), KeyCode: click-to-capture button
    /// (Esc cancels), anything else: read-only text. Descriptions become tooltips. Controls follow the entry both
    /// ways: a change made here is saved at once, a change made elsewhere (the .cfg file, code, JSON layouts)
    /// updates the control while the page is open. Built with the public toolkit, inside the F8 menu.
    /// </summary>
    internal static class UiConfigPage
    {
        /// <summary>True if the F8 page of this config should offer a Settings row.</summary>
        public static bool HasPage(ModConfig cfg) => cfg != null && cfg.ShowInMenu && cfg.Entries.Count > 0;

        /// <summary>Fills <paramref name="page"/> with the controls for <paramref name="cfg"/>.</summary>
        public static void Build(ModConfig cfg, string modName, IUiContainer page)
        {
            var owner = (page as UiElement)?.Host.Owner ?? new UiOwner(cfg.ModId, modName, AslPlugin.Logger);
            string file = SafeFileName(cfg);
            page.AddLabel("Changes apply immediately and are saved to BepInEx/config/" + file + ".", UiTextStyle.Muted);

            // Group by section, in bind order.
            var sections = new List<string>();
            foreach (var e in cfg.Entries) if (!sections.Contains(e.Section)) sections.Add(e.Section);

            foreach (var section in sections)
            {
                var box = page.AddGroup(section);
                foreach (var e in cfg.Entries)
                {
                    if (e.Section != section) continue;
                    try { AddEntry(box, e, modName, owner); }
                    catch (Exception ex) { owner.Report("settings control for " + e.Key, ex); }
                }
            }

            page.AddSpacer(8f);
            var reset = page.AddButton("Reset to defaults", () =>
            {
                foreach (var e in cfg.Entries)
                {
                    try { e.ResetToDefault(); }
                    catch (Exception ex) { owner.Report("reset " + e.Key, ex); }
                }
            }, UiButtonStyle.Danger.With(size: UiSize.Medium));
            reset.Tooltip = "Set every setting of this mod back to its default value.";
        }

        private static string SafeFileName(ModConfig cfg)
        {
            try { return Path.GetFileName(cfg.FilePath); } catch { return cfg.ModId + ".cfg"; }
        }

        private static void AddEntry(IUiContainer box, ConfigEntryInfo e, string modName, UiOwner owner)
        {
            var type = e.ValueType;
            string tip = string.IsNullOrWhiteSpace(e.Description) ? null : e.Description;
            bool busy = false;

            if (type == typeof(bool))
            {
                IUiToggle t = null;
                t = box.AddToggle(e.Key, (bool)e.BoxedValue, v => { busy = true; try { Set(e, v, owner); } finally { busy = false; } });
                t.Tooltip = tip;
                Follow(e, t, owner, () => { if (!busy) t.Value = (bool)e.BoxedValue; });
            }
            else if (e.HasRange && (type == typeof(int) || type == typeof(float)))
            {
                bool isInt = type == typeof(int);
                float min = ToFloat(e.Min), max = ToFloat(e.Max);
                if (max <= min) max = min + 1f;
                IUiSlider s = null;
                s = box.AddSlider(e.Key, min, max, Mathf.Clamp(ToFloat(e.BoxedValue), min, max), v =>
                {
                    busy = true;
                    try { Set(e, isInt ? (object)(int)Math.Round(v) : v, owner); } finally { busy = false; }
                }, isInt ? 1f : 0f, isInt ? "0" : "0.##");
                s.Tooltip = tip;
                Follow(e, s, owner, () => { if (!busy) s.Value = ToFloat(e.BoxedValue); });
            }
            else if (e.IsEnum && e.EnumNames != null && !e.IsKeyCode)
            {
                string[] names = e.EnumNames;
                IUiDropdown d = null;
                d = box.AddDropdown(e.Key, names, IndexOf(names, e.BoxedValue), i =>
                {
                    if (i < 0 || i >= names.Length) return;
                    busy = true;
                    try { Set(e, names[i], owner); } finally { busy = false; }
                });
                d.Tooltip = tip;
                Follow(e, d, owner, () => { if (!busy) d.Index = IndexOf(names, e.BoxedValue); });
            }
            else if (type == typeof(string))
            {
                var lbl = box.AddLabel(e.Key, UiTextStyle.Body);
                lbl.Tooltip = tip;
                IUiInput inp = null;
                inp = box.AddInput(e.Key, (string)e.BoxedValue ?? string.Empty, s => Set(e, s ?? string.Empty, owner));
                inp.Tooltip = tip;
                Follow(e, inp, owner, () => { if (!inp.IsFocused) inp.Text = (string)e.BoxedValue ?? string.Empty; });
            }
            else if (e.IsKeyCode)
            {
                AddKeyRow(box, e, modName, tip, owner);
            }
            else
            {
                // Numbers without a range, doubles, ...: shown, edited in the .cfg file.
                IUiText lbl = null;
                lbl = box.AddLabel(ReadOnlyText(e), UiTextStyle.Muted);
                lbl.Tooltip = tip;
                Follow(e, lbl, owner, () => lbl.Text = ReadOnlyText(e));
            }
        }

        private static void AddKeyRow(IUiContainer box, ConfigEntryInfo e, string modName, string tip, UiOwner owner)
        {
            var row = box.AddRow(14f);
            var name = row.AddLabel(e.Key);
            name.SetFlexible(1f);
            name.Tooltip = tip;

            IUiButton btn = null;
            btn = row.AddButton(KeyText(e), () =>
            {
                var keys = AslPlugin.Keybinds;
                if (keys == null || keys.IsRebinding) return;
                // A throw-away bind: KeybindManager's capture flow (any key, Esc cancels, clashes with other mods' keys are
                // refused) assigns the pressed key to it; we copy the result into the config entry when it finishes.
                var probe = new AslKeybind("config:" + e.Section + "." + e.Key, e.Key, modName, (KeyCode)e.DefaultBoxed, (KeyCode)e.BoxedValue);
                btn.Text = "press a key...";
                keys.BeginRebind(probe, () =>
                {
                    if (((UiElement)btn).Dead) return;
                    if (probe.Key != (KeyCode)e.BoxedValue) Set(e, probe.Key, owner);
                    btn.Text = KeyText(e);
                });
            }, new UiButtonStyle { Variant = UiVariant.Secondary, Shape = UiShape.Rounded, Size = UiSize.Small, Width = 170f });
            btn.Tooltip = tip != null ? tip + "\n(click, then press a key; Esc cancels)" : "Click, then press a key; Esc cancels.";
            Follow(e, btn, owner, () => { if (!AslPlugin.Keybinds.IsRebinding) btn.Text = KeyText(e); });
        }

        // ---- helpers ---------------------------------------------------------------------------

        private static void Set(ConfigEntryInfo e, object value, UiOwner owner)
        {
            try { e.BoxedValue = value; }
            catch (Exception ex) { owner.Report("set " + e.Key, ex); }
        }

        // Updates a control when the entry changes elsewhere; removes itself once the control is gone (page closed).
        private static void Follow(ConfigEntryInfo e, IUiElement el, UiOwner owner, Action refresh)
        {
            Action handler = null;
            handler = () =>
            {
                try
                {
                    if (el is UiElement ue && ue.Dead) { e.ValueChanged -= handler; return; }
                    refresh();
                }
                catch (Exception ex) { owner.Report("settings refresh", ex); }
            };
            e.ValueChanged += handler;
        }

        private static string KeyText(ConfigEntryInfo e)
        {
            var k = (KeyCode)e.BoxedValue;
            return k == KeyCode.None ? "-" : k.ToString();
        }

        private static string ReadOnlyText(ConfigEntryInfo e) =>
            e.Key + ": " + Convert.ToString(e.BoxedValue, CultureInfo.InvariantCulture) + "   (edit in the .cfg file)";

        private static int IndexOf(string[] names, object value)
        {
            int i = Array.IndexOf(names, Convert.ToString(value, CultureInfo.InvariantCulture));
            return i < 0 ? 0 : i;
        }

        private static float ToFloat(object o)
        {
            try { return Convert.ToSingle(o, CultureInfo.InvariantCulture); } catch { return 0f; }
        }
    }
}

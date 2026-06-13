using System;
using BepInEx.Logging;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ASL
{
    /// <summary>
    /// Renders the mod menu natively (uGUI), in the game's own style — no IMGUI. A dark backdrop, a
    /// centred panel that borrows the Settings screen's sprite, and rows that are clones of a real
    /// game menu button (captured by <see cref="MainMenuInjector"/>) so they inherit the LED-panel
    /// look, font, and hover animation.
    ///
    /// Two pages: the root lists one button per mod; selecting a mod opens its settings page (its
    /// registered controls) with a Back button. Toggles/sliders render as click-to-change rows.
    /// </summary>
    internal sealed class NativeMenu
    {
        private const float RowW = 620f, RowH = 76f, RowGap = 10f, TopPad = 70f;

        private readonly ManualLogSource _log;
        private readonly MenuManager _registry;

        private GameObject _template;
        private GameObject _canvasGo;
        private RectTransform _root;     // the panel; rows are its children
        private RectTransform _panelRT;
        private Image _panelImg;
        private bool _built;
        private string _currentMod;

        public NativeMenu(ManualLogSource log, MenuManager registry)
        {
            _log = log;
            _registry = registry;
            registry.VisibleChanged += OnVisibleChanged;
        }

        public void SetTemplate(GameObject buttonClone)
        {
            if (_template != null || buttonClone == null) return;
            _template = buttonClone;
            UnityEngine.Object.DontDestroyOnLoad(_template);
            _template.SetActive(false);
            _log.LogInfo("[menu] captured native button template.");
            try { Build(); }
            catch (Exception ex) { _log.LogError($"[menu] native build failed: {ex}"); }
        }

        private void OnVisibleChanged(bool visible)
        {
            try
            {
                if (visible)
                {
                    _currentMod = null;
                    if (!_built) Build(); else Rebuild();
                    if (_canvasGo != null) _canvasGo.SetActive(true);
                }
                else if (_canvasGo != null)
                {
                    _canvasGo.SetActive(false);
                }
            }
            catch (Exception ex) { _log.LogError($"[menu] native toggle failed: {ex.Message}"); }
        }

        private void Build()
        {
            if (_template == null) { _log.LogWarning("[menu] no button template yet (open the main menu once)."); return; }

            _canvasGo = new GameObject("ASL_MenuCanvas");
            UnityEngine.Object.DontDestroyOnLoad(_canvasGo);
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;
            var scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _canvasGo.AddComponent<GraphicRaycaster>();

            // Full-screen dark backdrop that also blocks clicks to the game behind.
            var bg = new GameObject("Backdrop");
            bg.transform.SetParent(_canvasGo.transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.6f);
            var bgrt = bg.GetComponent<RectTransform>();
            bgrt.anchorMin = Vector2.zero; bgrt.anchorMax = Vector2.one;
            bgrt.offsetMin = Vector2.zero; bgrt.offsetMax = Vector2.zero;

            // Centred panel (borrows the Settings sprite). Rows live inside it.
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_canvasGo.transform, false);
            _panelImg = panel.AddComponent<Image>();
            _panelRT = panel.GetComponent<RectTransform>();
            _panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            _panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            _panelRT.pivot = new Vector2(0.5f, 0.5f);
            _panelRT.sizeDelta = new Vector2(RowW + 60f, 600f);
            _panelRT.anchoredPosition = Vector2.zero;
            _root = _panelRT;

            EnsureEventSystem();
            Rebuild();
            _canvasGo.SetActive(false);
            _built = true;
            _log.LogInfo("[menu] native menu built.");
        }

        private void ApplyPanelStyle()
        {
            if (_panelImg == null) return;
            var sp = UiUtil.SettingsPanelSprite();
            if (sp != null)
            {
                _panelImg.sprite = sp;
                _panelImg.type = Image.Type.Sliced;
                _panelImg.color = Color.white;
            }
            else
            {
                _panelImg.sprite = null;
                _panelImg.color = new Color(0.04f, 0.04f, 0.05f, 0.97f);
            }
        }

        private void Rebuild()
        {
            if (_root == null) return;

            for (int i = _root.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_root.GetChild(i).gameObject);

            int idx = 0;

            if (_currentMod == null)
            {
                AddRow("— Mods —", false, null, idx++);
                foreach (var sec in _registry.Sections)
                {
                    var modName = sec.ModName;
                    AddRow(modName, true, () => { _currentMod = modName; Rebuild(); }, idx++);
                }
                if (_registry.Sections.Count == 0)
                    AddRow("(no mods loaded)", false, null, idx++);
                AddRow("Close", true, () => _registry.Visible = false, idx++);
            }
            else
            {
                AddRow(_currentMod, false, null, idx++);
                AddRow("< Back", true, () => { _currentMod = null; Rebuild(); }, idx++);

                MenuManager.Section section = null;
                foreach (var s in _registry.Sections) if (s.ModName == _currentMod) { section = s; break; }

                if (section != null)
                {
                    var controls = section.Controls;
                    for (int c = 0; c < controls.Count; c++)
                    {
                        switch (controls[c])
                        {
                            case LabelControl lc:
                                AddRow(lc.Label, false, null, idx++);
                                break;
                            case ButtonControl bc:
                                AddRow(bc.Label, true, () => Safe(bc.OnClick), idx++);
                                break;
                            case ToggleControl tc:
                            {
                                GameObject row = null;
                                row = AddRow(ToggleText(tc), true, () =>
                                {
                                    tc.Value = !tc.Value;
                                    Safe(() => tc.OnChanged?.Invoke(tc.Value));
                                    SetRowText(row, ToggleText(tc));
                                }, idx++);
                                break;
                            }
                            case SliderControl sc:
                            {
                                GameObject row = null;
                                row = AddRow(SliderText(sc), true, () =>
                                {
                                    Step(sc);
                                    Safe(() => sc.OnChanged?.Invoke(sc.Value));
                                    SetRowText(row, SliderText(sc));
                                }, idx++);
                                break;
                            }
                        }
                    }
                }

                AddRow("Close", true, () => _registry.Visible = false, idx++);
            }

            // Size the panel to wrap the rows, and (re)apply the settings-style background.
            if (_panelRT != null)
                _panelRT.sizeDelta = new Vector2(RowW + 60f, TopPad + idx * (RowH + RowGap) + 30f);
            ApplyPanelStyle();
        }

        private GameObject AddRow(string text, bool interactable, Action onClick, int index)
        {
            var go = UnityEngine.Object.Instantiate(_template, _root);
            go.name = "ASL_Row";
            go.SetActive(true);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.localScale = Vector3.one;
            rt.sizeDelta = new Vector2(RowW, RowH);
            rt.anchoredPosition = new Vector2(0f, -(TopPad + index * (RowH + RowGap)));

            UiUtil.SetLabel(go, text);

            var icon = rt.Find("IconHolder");
            if (icon != null) icon.gameObject.SetActive(false);

            var btn = go.GetComponent<Button>();
            if (btn != null)
            {
                var ev = btn.onClick;
                int pc = ev.GetPersistentEventCount();
                for (int i = 0; i < pc; i++) ev.SetPersistentListenerState(i, UnityEventCallState.Off);
                ev.RemoveAllListeners();
                btn.interactable = interactable;
                if (interactable && onClick != null)
                    ev.AddListener(DelegateSupport.ConvertDelegate<UnityAction>(onClick));
            }
            return go;
        }

        private void SetRowText(GameObject row, string text) => UiUtil.SetLabel(row, text);

        private static string ToggleText(ToggleControl t) => $"{t.Label}: {(t.Value ? "ON" : "OFF")}";
        private static string SliderText(SliderControl s) => $"{s.Label}: {s.Value:0.##}";

        private static void Step(SliderControl s)
        {
            float step = (s.Max - s.Min) / 10f;
            if (step <= 0f) return;
            s.Value += step;
            if (s.Value > s.Max + 0.0001f) s.Value = s.Min;
        }

        private void EnsureEventSystem()
        {
            try
            {
                if (UnityEngine.EventSystems.EventSystem.current != null) return;
                var es = new GameObject("ASL_EventSystem");
                UnityEngine.Object.DontDestroyOnLoad(es);
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                _log.LogInfo("[menu] created an EventSystem (none was present).");
            }
            catch (Exception ex) { _log.LogWarning($"[menu] EventSystem check failed: {ex.Message}"); }
        }

        private void Safe(Action a)
        {
            try { a?.Invoke(); }
            catch (Exception ex) { _log.LogError($"[menu] control callback threw: {ex.Message}"); }
        }
    }
}

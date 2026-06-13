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
    /// Renders the mod menu natively (uGUI), in the game's own style — no IMGUI. Each row is a clone
    /// of a real game menu button (captured by <see cref="MainMenuInjector"/>), so rows inherit the
    /// game's LED-panel look, font, and hover animation. Toggles and sliders are rendered as
    /// click-to-change buttons. Reads its content from <see cref="MenuManager"/>.
    /// </summary>
    internal sealed class NativeMenu
    {
        private const float RowW = 640f, RowH = 78f, RowGap = 10f, TopPad = 64f;

        private readonly ManualLogSource _log;
        private readonly MenuManager _registry;

        private GameObject _template;     // inactive clone of a game button
        private GameObject _canvasGo;
        private RectTransform _root;
        private bool _built;

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
            try { Build(); }   // build hidden now so any error surfaces immediately
            catch (Exception ex) { _log.LogError($"[menu] native build failed: {ex}"); }
        }

        private void OnVisibleChanged(bool visible)
        {
            try
            {
                if (visible)
                {
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
            _root = _canvasGo.GetComponent<RectTransform>();

            // Dark backdrop that also blocks clicks to the game behind.
            var bg = new GameObject("Backdrop");
            bg.transform.SetParent(_canvasGo.transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.82f);
            var bgrt = bg.GetComponent<RectTransform>();
            bgrt.anchorMin = Vector2.zero;
            bgrt.anchorMax = Vector2.one;
            bgrt.offsetMin = Vector2.zero;
            bgrt.offsetMax = Vector2.zero;

            EnsureEventSystem();
            Rebuild();
            _canvasGo.SetActive(false);
            _built = true;
            _log.LogInfo("[menu] native menu built.");
        }

        private void Rebuild()
        {
            if (_root == null) return;

            // Clear previous rows (keep the backdrop at child 0).
            for (int i = _root.childCount - 1; i >= 1; i--)
                UnityEngine.Object.Destroy(_root.GetChild(i).gameObject);

            int idx = 0;
            AddRow("— ASL  Mods —", false, null, idx++);

            foreach (var sec in _registry.Sections)
            {
                AddRow(sec.ModName, false, null, idx++);
                var controls = sec.Controls;
                for (int c = 0; c < controls.Count; c++)
                {
                    var ctrl = controls[c];
                    switch (ctrl)
                    {
                        case LabelControl lc:
                            AddRow("  " + lc.Label, false, null, idx++);
                            break;
                        case ButtonControl bc:
                            AddRow(bc.Label, true, () => Safe(bc.OnClick), idx++);
                            break;
                        case ToggleControl tc:
                        {
                            int rowIndex = idx++;
                            GameObject row = null;
                            row = AddRow(ToggleText(tc), true, () =>
                            {
                                tc.Value = !tc.Value;
                                Safe(() => tc.OnChanged?.Invoke(tc.Value));
                                SetRowText(row, ToggleText(tc));
                            }, rowIndex);
                            break;
                        }
                        case SliderControl sc:
                        {
                            int rowIndex = idx++;
                            GameObject row = null;
                            row = AddRow(SliderText(sc), true, () =>
                            {
                                Step(sc);
                                Safe(() => sc.OnChanged?.Invoke(sc.Value));
                                SetRowText(row, SliderText(sc));
                            }, rowIndex);
                            break;
                        }
                    }
                }
            }

            AddRow("Close", true, () => _registry.Visible = false, idx++);
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

            var tmp = go.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) tmp.text = text;

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

        private void SetRowText(GameObject row, string text)
        {
            if (row == null) return;
            var tmp = row.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) tmp.text = text;
        }

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

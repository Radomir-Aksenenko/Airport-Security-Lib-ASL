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
        private readonly KeybindManager _keys;

        private GameObject _template;
        private GameObject _canvasGo;
        private RectTransform _root;     // the panel; rows are its children
        private RectTransform _panelRT;
        private Image _panelImg;
        private bool _built;
        private string _currentMod;

        // Animated backdrop that mimics the Settings screen's flowing background.
        private RawImage _animBg;
        private Vector2 _bgUv, _bgSpeed;
        private bool _bgReady;

        public NativeMenu(ManualLogSource log, MenuManager registry, KeybindManager keys)
        {
            _log = log;
            _registry = registry;
            _keys = keys;
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
                    if (!_built) Build();
                    else
                    {
                        if (!_bgReady) TrySetupAnimatedBackground();   // re-resolve: the menu bg may not have existed at Build()
                        Rebuild();
                    }
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

            // Animated, Settings-style backdrop (a scrolling texture). Child of the canvas (not the
            // panel), so Rebuild — which clears the panel's children — never destroys it.
            TrySetupAnimatedBackground();

            // Centred panel (borrows the Settings sprite). Rows live inside it.
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_canvasGo.transform, false);
            _panelImg = panel.AddComponent<Image>();
            _panelRT = panel.GetComponent<RectTransform>();
            _panelRT.anchorMin = Vector2.zero;       // full-screen background, like the Settings screen
            _panelRT.anchorMax = Vector2.one;
            _panelRT.offsetMin = Vector2.zero;
            _panelRT.offsetMax = Vector2.zero;
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

            // If the scrolling backdrop is up, keep the panel transparent so it shows through (like the
            // Settings screen, where controls sit directly over the moving background).
            if (_bgReady) { _panelImg.sprite = null; _panelImg.color = new Color(0f, 0f, 0f, 0f); return; }

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

        // Build a full-screen RawImage that scrolls the Settings background texture. Best-effort: if the
        // texture isn't found, we simply fall back to the static panel style.
        private void TrySetupAnimatedBackground()
        {
            try
            {
                UiUtil.LogScrollers(_log);   // one-time diagnostic: which scrollers are live + conveyor flags

                if (!UiUtil.SettingsScrollTexture(out var tex, out var speed) || tex == null) { _bgReady = false; return; }

                if (_animBg == null)
                {
                    var go = new GameObject("ASL_AnimatedBg");
                    go.transform.SetParent(_canvasGo.transform, false);
                    go.transform.SetSiblingIndex(1);   // above the backdrop, below the panel
                    _animBg = go.AddComponent<RawImage>();
                    _animBg.raycastTarget = false;
                    var rt = go.GetComponent<RectTransform>();
                    rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                }
                _animBg.texture = tex;
                _animBg.color = new Color(1f, 1f, 1f, 0.5f);   // dim it over the dark backdrop for readability
                try { tex.wrapMode = TextureWrapMode.Repeat; } catch { }   // so uvRect tiling scrolls cleanly
                _animBg.uvRect = new Rect(0f, 0f, 2f, 2f);

                _bgSpeed = speed;
                _bgUv = Vector2.zero;
                _bgReady = true;
                _log.LogInfo("[menu] animated Settings-style backdrop attached.");
            }
            catch (Exception ex) { _log.LogWarning($"[menu] animated bg setup failed: {ex.Message}"); _bgReady = false; }
        }

        /// <summary>Advances the scrolling backdrop one frame. Called while the menu is visible.</summary>
        public void TickBackground()
        {
            if (!_bgReady || _animBg == null) return;
            try
            {
                _bgUv += _bgSpeed * Time.unscaledDeltaTime;
                var r = _animBg.uvRect;
                _animBg.uvRect = new Rect(_bgUv.x, _bgUv.y, r.width, r.height);
            }
            catch { }
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
                            case KeybindControl kc:
                            {
                                GameObject row = null;
                                row = AddRow(KeybindText(kc.Bind), true, () =>
                                {
                                    if (_keys == null) return;
                                    _keys.BeginRebind(kc.Bind, () => SetRowText(row, KeybindText(kc.Bind)));
                                    SetRowText(row, $"{kc.Bind.DisplayName}: press a key…  (Esc = cancel)");
                                }, idx++);
                                break;
                            }
                        }
                    }
                }

                AddRow("Close", true, () => _registry.Visible = false, idx++);
            }

            // (Re)apply the settings-style background; the panel fills the whole screen.
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

        private static string KeybindText(AslKeybind b)
        {
            string key = b.Key == KeyCode.None ? "—" : b.Key.ToString();
            return $"{b.DisplayName}: [{key}]{(b.HasConflict ? "  (!)" : "")}";
        }

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

// Stub-surface self-test: exercises the Unity/uGUI/TMP API surface the stubs are expected to provide, so a
// regression in the stubs (or a wrong assumption) shows up here and not in the middle of real code.
// It is compiled (never run) by build.sh as "smoke:UiSurface". Mirrors the way IL2CPP mods call the API.
using System;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CompileCheck.Smoke
{
    internal sealed class SmokeBehaviour : MonoBehaviour, IPointerClickHandler
    {
        public SmokeBehaviour(IntPtr ptr) : base(ptr) { }
        public void OnPointerClick(PointerEventData eventData) { var p = eventData.position; var b = eventData.button == PointerEventData.InputButton.Left; }
        private void Update() { }
    }

    internal static class UiSurface
    {
        public static void Run()
        {
            // ---- GameObject / Transform / RectTransform
            var go = new GameObject("Root");
            go.SetActive(true);
            bool a = go.activeSelf, b = go.activeInHierarchy;
            go.name = "x"; go.layer = 5; var t = go.transform;
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10; canvas.overrideSorting = true;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            var cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 0.5f; cg.interactable = false; cg.blocksRaycasts = false;

            var child = new GameObject("Child");
            child.transform.SetParent(t, false);
            child.transform.SetParent(t);
            child.transform.SetAsLastSibling(); child.transform.SetAsFirstSibling(); child.transform.SetSiblingIndex(2);
            int sib = child.transform.GetSiblingIndex();
            var found = t.Find("Child"); var c0 = t.GetChild(0); int cc = t.childCount;
            child.transform.localScale = Vector3.one; child.transform.localPosition = Vector3.zero; child.transform.position = Vector3.up;
            var par = child.transform.parent;
            var rt = child.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(10f, 10f); rt.anchoredPosition = Vector2.zero; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100f);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 100f);
            Rect r = rt.rect; float w = r.width, h = r.height;
            var beh = go.GetComponent<Behaviour>(); beh.enabled = true;
            var comps = go.GetComponents<MonoBehaviour>(); var inChild = go.GetComponentInChildren<Image>(true);
            var inChild2 = go.GetComponentsInChildren<Image>(true); var inPar = child.GetComponentInParent<Canvas>();
            var anyComps = go.GetComponentsInChildren(Il2CppType.Of<Image>(), true);
            var cmp = go.GetComponent(Il2CppType.Of<Image>()); var img0 = cmp != null ? cmp.TryCast<Image>() : null;

            // ---- Graphics
            var img = child.AddComponent<Image>();
            img.sprite = null; img.color = Color.white; img.raycastTarget = false; img.material = null; var grt = img.rectTransform;
            img.type = Image.Type.Filled; img.fillAmount = 0.5f; img.fillMethod = Image.FillMethod.Horizontal; img.fillOrigin = 0;
            img.preserveAspect = true; img.pixelsPerUnitMultiplier = 2f;
            var raw = child.AddComponent<RawImage>(); raw.texture = Texture2D.whiteTexture; raw.uvRect = new Rect(0f, 0f, 1f, 1f);

            // ---- Selectables
            var btn = child.AddComponent<Button>();
            btn.interactable = true; btn.transition = Selectable.Transition.ColorTint; btn.targetGraphic = img;
            var cb = btn.colors;
            cb.normalColor = Color.white; cb.highlightedColor = Color.grey; cb.pressedColor = Color.black; cb.selectedColor = Color.cyan;
            cb.disabledColor = Color.clear; cb.colorMultiplier = 1f; cb.fadeDuration = 0.1f; btn.colors = cb;
            btn.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>(new Action(() => { })));
            btn.onClick.AddListener(new Action(() => { }));
            btn.onClick.RemoveAllListeners(); btn.onClick.Invoke();
            int pc = btn.onClick.GetPersistentEventCount(); btn.onClick.SetPersistentListenerState(0, UnityEventCallState.Off);
            var tg = child.AddComponent<Toggle>(); tg.isOn = true; tg.graphic = img; tg.group = null;
            tg.onValueChanged.AddListener(new Action<bool>(v => { })); tg.onValueChanged.RemoveAllListeners();
            var sl = child.AddComponent<Slider>();
            sl.minValue = 0f; sl.maxValue = 1f; sl.value = 0.5f; sl.wholeNumbers = false; sl.fillRect = rt; sl.handleRect = rt;
            sl.direction = Slider.Direction.LeftToRight; sl.onValueChanged.AddListener(new Action<float>(v => { }));
            var sb = child.AddComponent<Scrollbar>(); sb.value = 1f; sb.size = 0.2f; sb.handleRect = rt; sb.direction = Scrollbar.Direction.BottomToTop;
            var sr = child.AddComponent<ScrollRect>();
            sr.content = rt; sr.viewport = rt; sr.horizontal = false; sr.vertical = true; sr.scrollSensitivity = 20f;
            sr.movementType = ScrollRect.MovementType.Clamped; sr.verticalNormalizedPosition = 1f; sr.verticalScrollbar = sb; sr.inertia = true;
            var mk = child.AddComponent<Mask>(); mk.showMaskGraphic = false;
            child.AddComponent<RectMask2D>();

            // ---- Layout
            var hl = child.AddComponent<HorizontalLayoutGroup>();
            var vl = child.AddComponent<VerticalLayoutGroup>();
            HorizontalOrVerticalLayoutGroup hv = vl; LayoutGroup lg = hv;
            lg.padding = new RectOffset(1, 2, 3, 4); lg.padding.left = 1; lg.padding.right = 2; lg.padding.top = 3; lg.padding.bottom = 4;
            hv.spacing = 4f; lg.childAlignment = TextAnchor.UpperLeft;
            hv.childControlWidth = true; hv.childControlHeight = true; hv.childForceExpandWidth = false; hv.childForceExpandHeight = false;
            hv.childScaleWidth = false; hv.childScaleHeight = false;
            var gl = child.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(10f, 10f); gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 3;
            gl.startCorner = GridLayoutGroup.Corner.UpperLeft; gl.startAxis = GridLayoutGroup.Axis.Horizontal; gl.spacing = Vector2.one;
            var csf = child.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var le = child.AddComponent<LayoutElement>();
            le.minWidth = 1f; le.minHeight = 1f; le.preferredWidth = 1f; le.preferredHeight = 1f; le.flexibleWidth = 1f; le.flexibleHeight = 1f; le.ignoreLayout = false;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            var ol = child.AddComponent<Outline>(); ol.effectColor = Color.black; ol.effectDistance = new Vector2(1f, -1f);
            var sh = child.AddComponent<Shadow>(); sh.effectColor = Color.black; sh.effectDistance = Vector2.one;

            // ---- TextMeshPro
            var tmp = child.AddComponent<TextMeshProUGUI>();
            TMP_Text tx = tmp;
            tx.text = "hi"; tx.fontSize = 24f; tx.color = Color.white; tx.alignment = TextAlignmentOptions.Center; tx.font = TMP_Settings.defaultFontAsset;
            tx.enableWordWrapping = false; tx.overflowMode = TextOverflowModes.Ellipsis; tx.fontStyle = FontStyles.Bold | FontStyles.Italic;
            tx.raycastTarget = false; tx.enableAutoSizing = true; tx.fontSizeMin = 8f; tx.fontSizeMax = 40f; tx.margin = new Vector4(1f, 1f, 1f, 1f);
            tx.richText = true; float ph = tx.preferredHeight, pw = tx.preferredWidth; tx.ForceMeshUpdate();
            var inp = child.AddComponent<TMP_InputField>();
            inp.text = "x"; inp.onValueChanged.AddListener(new Action<string>(s => { })); inp.onEndEdit.AddListener(new Action<string>(s => { }));
            inp.onSubmit.AddListener(new Action<string>(s => { }));
            inp.textComponent = tmp; inp.textViewport = rt; inp.placeholder = tmp; inp.characterLimit = 20;
            inp.contentType = TMP_InputField.ContentType.Standard; inp.lineType = TMP_InputField.LineType.SingleLine;
            bool foc = inp.isFocused; inp.ActivateInputField(); inp.DeactivateInputField();
            inp.caretColor = Color.white; inp.selectionColor = Color.blue; inp.pointSize = 18f; inp.fontAsset = TMP_Settings.defaultFontAsset; inp.richText = false;

            // ---- Events / EventSystem
            var ev = new UnityEvent(); ev.AddListener(new Action(() => { })); ev.RemoveListener(null); ev.RemoveAllListeners(); ev.Invoke();
            var ev1 = new UnityEvent<float>(); ev1.AddListener(new Action<float>(f => { })); ev1.Invoke(1f);
            UnityEventBase eb = ev; int n = eb.GetPersistentEventCount(); eb.SetPersistentListenerState(0, UnityEventCallState.Off);
            var es = EventSystem.current; var sel = es.currentSelectedGameObject; es.SetSelectedGameObject(null); bool over = es.IsPointerOverGameObject();
            var esGo = new GameObject("ES"); esGo.AddComponent<EventSystem>(); esGo.AddComponent<StandaloneInputModule>();

            // ---- Textures / sprites / math
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Point; int tw = tex.width, th = tex.height; string tn = tex.name;
            tex.LoadRawTextureData(new Il2CppStructArray<byte>(16)); tex.Apply(false); tex.Apply(false, false); tex.SetPixels32(new Il2CppStructArray<Color32>(4));
            var px = tex.GetPixels32(); bool rd = tex.isReadable;
            var spr = Sprite.Create(tex, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 100f);
            var spr2 = Sprite.Create(tex, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, new Vector4(1f, 1f, 1f, 1f));
            var st = spr.texture; var sr2 = spr.rect; var sbd = spr.border; string spn = spr.name;
            var col = new Color(1f, 0.5f, 0.2f, 1f); col = Color.Lerp(Color.red, Color.blue, 0.5f) * 0.5f; col.a = 0.5f; float cr = col.r + col.g + col.b;
            var c32 = new Color32(1, 2, 3, 4);
            float m = Mathf.Clamp(1f, 0f, 2f) + Mathf.Lerp(0f, 1f, 0.5f) + Mathf.Max(1f, 2f) + Mathf.Min(1, 2) + Mathf.Abs(-1f) + Mathf.Clamp01(2f);
            float dt = Time.deltaTime + Time.unscaledDeltaTime + Time.time + Time.unscaledTime + Time.realtimeSinceStartup + Time.frameCount + Time.timeScale;
            int sw = Screen.width, sh2 = Screen.height;
            var mp = Input.mousePosition; bool k1 = Input.GetKey(KeyCode.A), k2 = Input.GetKeyDown(KeyCode.Mouse0), k3 = Input.GetKeyUp(KeyCode.F8);
            bool m1 = Input.GetMouseButton(0), m2 = Input.GetMouseButtonDown(1), m3 = Input.GetMouseButtonUp(2);
            var msd = Input.mouseScrollDelta; string ins = Input.inputString; bool ak = Input.anyKeyDown;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            string pdp = Application.persistentDataPath, ver = Application.version; bool foc2 = Application.isFocused;
            Debug.Log("x"); Debug.LogWarning("x"); Debug.LogError("x");

            // ---- Objects
            var cloned = UnityEngine.Object.Instantiate(go, t); var cloned2 = UnityEngine.Object.Instantiate(go, t, false);
            var cloned3 = UnityEngine.Object.Instantiate(go); var cloned4 = UnityEngine.Object.Instantiate(go, Vector3.zero, Quaternion.identity);
            UnityEngine.Object.Destroy(cloned); UnityEngine.Object.DestroyImmediate(cloned2); UnityEngine.Object.DontDestroyOnLoad(go);
            var f1 = UnityEngine.Object.FindObjectOfType<Canvas>(); var f2 = UnityEngine.Object.FindObjectsOfType<Canvas>();
            var f3 = Resources.FindObjectsOfTypeAll(Il2CppType.Of<Texture2D>()); var f4 = Resources.FindObjectsOfTypeAll<Texture2D>();
            go.hideFlags = HideFlags.HideAndDontSave; int iid = go.GetInstanceID(); bool alive = go; bool alive2 = go != null;
            var scene = SceneManager.GetActiveScene(); string sn = scene.name; int bi = scene.buildIndex;

            // ---- audio / bundles
            var clip = AudioClip.Create("c", 100, 1, 44100, false); clip.SetData(new Il2CppStructArray<float>(100), 0);
            float len = clip.length; int smp = clip.samples, chn = clip.channels, frq = clip.frequency; string cn = clip.name;
            var src = go.AddComponent<AudioSource>(); src.clip = clip; src.Play(); src.Stop(); src.PlayOneShot(clip, 1f); src.volume = 1f; src.pitch = 1f;
            src.loop = false; bool pl = src.isPlaying; src.spatialBlend = 0f;
            var ab = AssetBundle.LoadFromFile("x"); var asset = ab.LoadAsset("n", Il2CppType.Of<Texture2D>()); var all = ab.LoadAllAssets();
            ab.Unload(false); var names = ab.GetAllAssetNames(); bool has = ab.Contains("x");
            var agent = go.GetComponent<NavMeshAgent>();

            // ---- injected behaviour
            var inj = go.AddComponent<SmokeBehaviour>();
        }
    }
}

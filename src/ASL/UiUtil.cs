using System;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ASL
{
    internal static class UiUtil
    {
        // If non-empty, SettingsScrollTexture prefers a material whose texture/shader/material name
        // CONTAINS this (case-insensitive) over the default "first controller material". Set this once
        // the F8 LogScrollers dump tells us the real menu-bg texture name, to lock onto it precisely.
        // Leave empty to use the controller-first heuristic below.
        private const string PreferredTextureNameContains = "";

        /// <summary>
        /// Best-effort: the scrolling texture + scroll speed the game's animated menu background uses
        /// (the flowing X-ray-style backdrop). Strong prior (confirmed by the live LogScrollers dump:
        /// exactly 1 <c>MaterialOffsetController</c> vs 17 <c>MaterialOffsetScroller</c>s that are all
        /// world geometry — belts, cubes, 25x30 walls): the backdrop is driven by the single
        /// <c>MaterialOffsetController</c> (which animates a material's <c>_ST</c> offset every frame),
        /// NOT by the world scrollers. We read the controller first and only fall back to a strictly
        /// non-belt, non-wall scroller. Returns false if nothing usable is found yet.
        /// </summary>
        public static bool SettingsScrollTexture(out Texture tex, out Vector2 speed)
        {
            tex = null; speed = Vector2.zero;

            // 1) PRIMARY path: the MaterialOffsetController (the actual menu backdrop animator).
            try
            {
                var ctrls = Resources.FindObjectsOfTypeAll(Il2CppType.Of<MaterialOffsetController>());
                if (ctrls != null)
                    foreach (var o in ctrls)
                    {
                        var ctrl = o != null ? o.TryCast<MaterialOffsetController>() : null;
                        if (ctrl == null) continue;
                        if (TryControllerTexture(ctrl, out tex, out speed) && tex != null) return true;
                    }
            }
            catch { }

            // 2) LAST-RESORT fallback: a non-belt MaterialOffsetScroller. Belts/conveyors also carry a
            // scroller, so we reject by BOTH the conveyor-component check AND name (Belt/Cube/Conveyer/
            // Conveyor). We ALSO reject "Wall" by name: the only other non-conveyor scrollers in the
            // live scene are the giant 2x25x30 lobby walls, which are world geometry, not the backdrop —
            // so if the controller path ever returns nothing we still must not grab a wall texture.
            try
            {
                var arr = Resources.FindObjectsOfTypeAll(Il2CppType.Of<MaterialOffsetScroller>());
                if (arr != null)
                    foreach (var o in arr)
                    {
                        var sc = o != null ? o.TryCast<MaterialOffsetScroller>() : null;
                        if (sc == null) continue;
                        if (IsConveyor(sc)) continue;                       // component-based reject
                        var path = PathOf(sc.transform);
                        if (LooksLikeBelt(path)) continue;                  // name reject: Belt/Cube/Conveyer/Conveyor
                        if (Contains(path, "Wall")) continue;               // name reject: lobby walls (world geometry)

                        Material mat = null;
                        try { mat = sc.targetMaterial; } catch { }
                        if (mat == null) { try { var r = sc.targetRenderer; if (r != null) mat = r.sharedMaterial; } catch { } }
                        if (mat == null) continue;

                        Texture t = null;
                        try
                        {
                            var prop = sc.resolvedTextureProperty;
                            if (!string.IsNullOrEmpty(prop) && mat.HasProperty(prop)) t = mat.GetTexture(prop);
                            if (t == null) t = mat.mainTexture;
                        }
                        catch { }
                        if (t == null) continue;

                        tex = t;
                        try { speed = sc.offsetSpeed; } catch { }
                        return true;
                    }
            }
            catch { }

            return false;
        }

        // Resolve the controller's animated texture + scroll speed. Walks targetMats (preferring
        // materialIndex when valid, else the first material that yields a usable texture), resolves the
        // base-texture property from baseSTProperty (StPropertyToTextureProperty), then the static
        // BaseTexturePropertyCandidates, then mainTexture. If PreferredTextureNameContains is set we
        // require a material whose texture/shader/material name matches it.
        // Il2Cpp List<Material>: .Count + indexer [i] (Material is a managed-wrapped Unity type, no
        // TryCast); every hop guarded since this runs while the menu UI is being built.
        private static bool TryControllerTexture(MaterialOffsetController ctrl, out Texture tex, out Vector2 speed)
        {
            tex = null; speed = Vector2.zero;

            Il2CppSystem.Collections.Generic.List<Material> mats = null;
            try { mats = ctrl.targetMats; } catch { }
            if (mats == null) return false;

            int count = 0;
            try { count = mats.Count; } catch { return false; }
            if (count <= 0) return false;

            try { speed = ctrl.offsetSpeed; } catch { }

            // Preferred index order: materialIndex first (if valid), then 0..count-1.
            int prefIndex = -1;
            try { prefIndex = ctrl.materialIndex; } catch { }

            for (int pass = 0; pass < count + 1; pass++)
            {
                // pass 0 = materialIndex (when valid); subsequent passes = 0..count-1 in order
                int i = pass == 0 ? prefIndex : pass - 1;
                if (i < 0 || i >= count) continue;

                Material mat = null;
                try { mat = mats[i]; } catch { continue; }   // managed-wrapped Material; null-guard
                if (mat == null) continue;

                string prop;
                Texture t = ResolveControllerTexture(ctrl, mat, out prop);
                if (t == null) continue;

                // Optional precise lock-on once we know the real menu-bg name from the F8 dump.
                if (PreferredTextureNameContains.Length > 0 && !NameMatchesPreferred(mat, t))
                    continue;

                tex = t;
                return true;
            }

            return false;
        }

        // Resolve the texture a controller material animates: try the baseSTProperty -> texture-property
        // mapping, then the static candidate list, then mainTexture. Reports which property name we used
        // (for the diagnostic log). All reads are SAFE on materials.
        private static Texture ResolveControllerTexture(MaterialOffsetController ctrl, Material mat, out string usedProp)
        {
            usedProp = null;
            if (mat == null) return null;

            try
            {
                // a) baseSTProperty (e.g. "_BaseMap_ST") -> texture property (e.g. "_BaseMap")
                string st = null;
                try { st = ctrl != null ? ctrl.baseSTProperty : null; } catch { }
                if (!string.IsNullOrEmpty(st))
                {
                    string p = null;
                    try { p = MaterialOffsetController.StPropertyToTextureProperty(st); } catch { }
                    var t = TexByProp(mat, p, ref usedProp);
                    if (t != null) return t;
                }

                // b) static BaseTexturePropertyCandidates (the names the controller itself probes)
                Il2CppStringArray cands = null;
                try { cands = MaterialOffsetController.BaseTexturePropertyCandidates; } catch { }
                if (cands != null)
                {
                    int len = 0; try { len = cands.Length; } catch { }
                    for (int i = 0; i < len; i++)
                    {
                        string p = null; try { p = cands[i]; } catch { }
                        var t = TexByProp(mat, p, ref usedProp);
                        if (t != null) return t;
                    }
                }

                // c) common URP/legacy fallbacks, in case the static array isn't populated this early
                //    (it is filled in MaterialOffsetController.Awake, which may not have run yet).
                var t2 = TexByProp(mat, "_BaseMap", ref usedProp);
                if (t2 != null) return t2;
                t2 = TexByProp(mat, "_MainTex", ref usedProp);
                if (t2 != null) return t2;

                // d) mainTexture as the final per-material fallback
                try
                {
                    var mt = mat.mainTexture;
                    if (mt != null) { usedProp = "<mainTexture>"; return mt; }
                }
                catch { }
            }
            catch { }
            return null;
        }

        // Read a material's texture by property name iff the material actually has it and it is usable.
        // Sets usedProp to the property name when the material HAS it (so the diagnostic reports the true
        // _ST-derived property even if GetTexture momentarily returns null), but only RETURNS a non-null
        // texture — so selection is unaffected by the reporting tweak.
        private static Texture TexByProp(Material mat, string prop, ref string usedProp)
        {
            if (mat == null || string.IsNullOrEmpty(prop)) return null;
            try
            {
                bool has = false;
                try { has = mat.HasProperty(prop); } catch { }
                if (!has) return null;

                // HasUsableTexture is the game's own "is there a real texture here" check — prefer it,
                // but it is a static interop method, so guard it and fall back to a direct GetTexture.
                bool usable = true;
                try { usable = MaterialOffsetController.HasUsableTexture(mat, prop); } catch { usable = true; }
                if (!usable) return null;

                // The material has the property: report it for the log even if the read comes back null.
                if (string.IsNullOrEmpty(usedProp)) usedProp = prop;

                var t = mat.GetTexture(prop);
                if (t != null) { usedProp = prop; return t; }
            }
            catch { }
            return null;
        }

        // True if any of the material/shader/texture names contain PreferredTextureNameContains.
        private static bool NameMatchesPreferred(Material mat, Texture tex)
        {
            if (PreferredTextureNameContains.Length == 0) return true;
            string needle = PreferredTextureNameContains;
            try { if (Contains(mat != null ? mat.name : null, needle)) return true; } catch { }
            try { if (mat != null && mat.shader != null && Contains(mat.shader.name, needle)) return true; } catch { }
            try { if (Contains(tex != null ? tex.name : null, needle)) return true; } catch { }
            return false;
        }

        private static bool Contains(string s, string needle)
        {
            return !string.IsNullOrEmpty(s) && !string.IsNullOrEmpty(needle)
                && s.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Name-based belt reject for the fallback scroller path (belongs alongside IsConveyor).
        private static bool LooksLikeBelt(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return Contains(name, "Belt") || Contains(name, "Cube")
                || Contains(name, "Conveyer") || Contains(name, "Conveyor");
        }

        // True if this scroller sits on (or under) an in-world conveyor belt — those carry a
        // MaterialOffsetScroller too, and there is no separate menu scene, so we must reject them.
        private static bool IsConveyor(MaterialOffsetScroller sc)
        {
            try
            {
                if (sc.GetComponentInParent(Il2CppType.Of<ConveyorMover>()) != null) return true;
                if (sc.GetComponentInParent(Il2CppType.Of<ConveyorInteractable>()) != null) return true;
            }
            catch { }
            return false;
        }

        private static bool _scrollersLogged;

        /// <summary>One-time diagnostic. For BOTH the MaterialOffsetController(s) and every
        /// MaterialOffsetScroller, logs the transform path AND, for every candidate material, the
        /// material name + shader name + resolved texture property + texture name + scroll speed. Each
        /// controller line also reports what the PRIMARY path would actually CHOOSE, and the chosen
        /// material in targetMats is marked with '*'. With these names in the in-game log, a single F8
        /// session tells us BY NAME which texture is the true menu background, so SettingsScrollTexture
        /// can be locked onto it via PreferredTextureNameContains.</summary>
        public static void LogScrollers(BepInEx.Logging.ManualLogSource log)
        {
            if (_scrollersLogged || log == null) return;
            _scrollersLogged = true;
            try
            {
                // --- Controllers (the strong prior for the menu backdrop) ---
                var ctrls = Resources.FindObjectsOfTypeAll(Il2CppType.Of<MaterialOffsetController>());
                int m = 0;
                if (ctrls != null)
                    foreach (var o in ctrls)
                    {
                        var ctrl = o != null ? o.TryCast<MaterialOffsetController>() : null;
                        if (ctrl == null) continue;
                        m++;
                        int idx = -1; Vector2 spd = Vector2.zero; string st = null;
                        try { idx = ctrl.materialIndex; } catch { }
                        try { spd = ctrl.offsetSpeed; } catch { }
                        try { st = ctrl.baseSTProperty; } catch { }

                        // What the PRIMARY path would actually pick for this controller.
                        Texture chosen = null; Vector2 chosenSpd = Vector2.zero;
                        try { TryControllerTexture(ctrl, out chosen, out chosenSpd); } catch { }
                        log.LogInfo($"[menu] CONTROLLER '{PathOf(ctrl.transform)}' materialIndex={idx} baseST='{st}' speed=({spd.x},{spd.y}) -> CHOSEN tex='{SafeTexName(chosen)}'");

                        Il2CppSystem.Collections.Generic.List<Material> mats = null;
                        try { mats = ctrl.targetMats; } catch { }
                        int cnt = 0; if (mats != null) { try { cnt = mats.Count; } catch { } }
                        if (cnt == 0) { log.LogInfo("[menu]   (controller has no targetMats)"); }
                        for (int i = 0; i < cnt; i++)
                        {
                            Material mat = null;
                            try { mat = mats[i]; } catch { }
                            string prop; var tex = ResolveControllerTexture(ctrl, mat, out prop);
                            log.LogInfo($"[menu]   ctrlMat[{i}]{(i == idx ? "*" : " ")} {DescribeMaterial(mat, tex, prop)}");
                        }
                    }
                log.LogInfo($"[menu] MaterialOffsetController live count={m}");

                // --- Scrollers (world geometry; belts/walls excluded from selection but still logged) ---
                var arr = Resources.FindObjectsOfTypeAll(Il2CppType.Of<MaterialOffsetScroller>());
                int n = 0;
                if (arr != null)
                    foreach (var o in arr)
                    {
                        var sc = o != null ? o.TryCast<MaterialOffsetScroller>() : null;
                        if (sc == null) continue;
                        n++;
                        var path = PathOf(sc.transform);
                        bool conv = IsConveyor(sc);
                        bool belt = LooksLikeBelt(path);
                        bool wall = Contains(path, "Wall");

                        Material mat = null;
                        try { mat = sc.targetMaterial; } catch { }
                        if (mat == null) { try { var r = sc.targetRenderer; if (r != null) mat = r.sharedMaterial; } catch { } }

                        string prop = null; Texture tex = null;
                        try
                        {
                            prop = sc.resolvedTextureProperty;
                            if (mat != null && !string.IsNullOrEmpty(prop) && mat.HasProperty(prop)) tex = mat.GetTexture(prop);
                            if (tex == null && mat != null) { tex = mat.mainTexture; if (tex != null) prop = "<mainTexture>"; }
                        }
                        catch { }

                        Vector2 spd = Vector2.zero; try { spd = sc.offsetSpeed; } catch { }
                        log.LogInfo($"[menu] scroller '{path}' conveyor={conv} beltName={belt} wallName={wall} speed=({spd.x},{spd.y}) {DescribeMaterial(mat, tex, prop)}");
                    }
                log.LogInfo($"[menu] MaterialOffsetScroller live count={n}");
            }
            catch (Exception ex) { log.LogWarning($"[menu] scroller diag failed: {ex.Message}"); }
        }

        // Compact, crash-safe "material -> shader/texture/property" descriptor for the diagnostic log.
        // Material/shader/texture name reads are all SAFE in IL2CPP; everything is still guarded.
        private static string DescribeMaterial(Material mat, Texture tex, string prop)
        {
            string matName = "<null mat>", shaderName = "?", texName = "<null tex>";
            try { if (mat != null) matName = mat.name; } catch { matName = "<err>"; }
            try { if (mat != null && mat.shader != null) shaderName = mat.shader.name; } catch { shaderName = "<err>"; }
            try { if (tex != null) texName = tex.name; } catch { texName = "<err>"; }
            return $"mat='{matName}' shader='{shaderName}' prop='{prop}' tex='{texName}'";
        }

        private static string SafeTexName(Texture t)
        {
            try { return t != null ? t.name : "<null>"; } catch { return "<err>"; }
        }

        private static string PathOf(Transform t)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                var cur = t;
                while (cur != null) { if (sb.Length > 0) sb.Insert(0, '/'); sb.Insert(0, cur.name); cur = cur.parent; }
                return sb.ToString();
            }
            catch { return "?"; }
        }

        /// <summary>
        /// Best-effort: the sprite of the game's Settings panel, so the mod menu can wear the same
        /// background. Returns null if the Settings screen isn't available yet.
        /// </summary>
        public static Sprite SettingsPanelSprite()
        {
            try
            {
                var inst = SettingsUi.Instance;
                if (inst == null) return null;

                var t = inst.contentsGo != null ? inst.contentsGo.transform : inst.transform;
                for (int up = 0; up < 3 && t != null; up++)
                {
                    var img = t.GetComponent<Image>();
                    if (img != null && img.sprite != null) return img.sprite;
                    t = t.parent;
                }
                var any = inst.GetComponentInChildren<Image>(true);
                return any != null && any.sprite != null ? any.sprite : null;
            }
            catch { return null; }
        }

        /// <summary>
        /// Sets a cloned button's TMP label, first removing any extra script on the label object
        /// (e.g. a localizer) that would otherwise overwrite the text back to the original — which
        /// is why a relabelled clone was still showing "Settings".
        /// </summary>
        public static void SetLabel(GameObject root, string text)
        {
            if (root == null) return;
            var tmp = root.GetComponentInChildren<TMP_Text>(true);
            if (tmp == null) return;

            var labelGo = tmp.gameObject;
            var monos = labelGo.GetComponents<MonoBehaviour>();
            for (int i = 0; i < monos.Length; i++)
            {
                var m = monos[i];
                if (m == null) continue;
                if (m.TryCast<TMP_Text>() == null) UnityEngine.Object.Destroy(m);  // drop localizer / overrides
            }

            tmp.text = text;
        }

        private static Sprite _modsIcon;

        /// <summary>The custom Mods-button icon sprite (built once from the embedded PNG).</summary>
        public static Sprite ModsIcon()
        {
            if (_modsIcon != null) return _modsIcon;
            try { _modsIcon = SpriteFromPng(Convert.FromBase64String(IconAsset.ModsIconPngBase64)); }
            catch { _modsIcon = null; }
            return _modsIcon;
        }

        /// <summary>Decodes a PNG (managed) and builds a readable RGBA32 sprite — the marshal-safe path.</summary>
        public static Sprite SpriteFromPng(byte[] png)
        {
            int w, h;
            var rgba = PngDecoder.Decode(png, out w, out h);     // top-down RGBA32

            int stride = w * 4;
            var px = new Il2CppStructArray<byte>(rgba.Length);
            for (int row = 0; row < h; row++)
            {
                int src = (h - 1 - row) * stride, dst = row * stride;   // flip to Unity bottom-up
                for (int b = 0; b < stride; b++) px[dst + b] = rgba[src + b];
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.LoadRawTextureData(px);
            tex.Apply(false);
            tex.hideFlags = HideFlags.HideAndDontSave;

            var spr = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
            spr.hideFlags = HideFlags.HideAndDontSave;
            return spr;
        }
    }
}

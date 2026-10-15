using ASL.Api;
using UnityEngine;

namespace UiShowcase
{
    /// <summary>
    /// A HUD is a window without a title bar that does not capture the cursor and is click-through, so it is
    /// ideal for counters and status lines. This one shows a live FPS-ish number and the time.
    /// </summary>
    internal sealed class ShowcaseHud
    {
        private readonly IModContext _ctx;
        private readonly IConfigEntry<bool> _show;
        private readonly IConfigEntry<float> _scale;
        private IUiWindow _hud;
        private IUiText _label;

        public ShowcaseHud(IModContext ctx, IConfigEntry<bool> show, IConfigEntry<float> scale)
        {
            _ctx = ctx;
            _show = show;
            _scale = scale;
        }

        /// <summary>(Re)create the HUD. Creating a HUD with an existing id replaces the old one.</summary>
        public void Build()
        {
            _hud = _ctx.Ui.CreateHud("hud", UiAnchor.TopRight, new UiWindowOptions { Width = 300, OffsetX = -16, OffsetY = -16 });

            // AddLiveLabel re-evaluates the lambda while visible, at most every 0.5 s here. Cheap and alloc-light:
            // no per-frame work in our own code.
            _label = _hud.AddLiveLabel(() =>
            {
                float dt = Time.unscaledDeltaTime;
                float fps = dt > 0f ? 1f / dt : 0f;
                return _ctx.Loc.Get("hud.line", fps, System.DateTime.Now.ToString("HH:mm:ss"));
            }, 0.5f);

            Apply();
        }

        /// <summary>Apply the current config: visibility and text size.</summary>
        public void Apply()
        {
            if (_hud == null) return;
            if (_label != null) _label.FontSize = 22f * _scale.Value;   // 22 = the default theme font size
            if (_show.Value) { if (!_hud.IsOpen) _hud.Open(); }
            else if (_hud.IsOpen) _hud.Close();
        }
    }
}

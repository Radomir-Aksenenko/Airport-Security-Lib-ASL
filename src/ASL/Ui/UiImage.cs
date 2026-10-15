using ASL.Api;
using UnityEngine;
using UnityEngine.UI;

namespace ASL
{
    /// <summary>A sprite. Width/height 0 means "use the sprite's own size" (or stretch).</summary>
    internal sealed class UiImage : UiElement, IUiImage
    {
        private readonly Image _img;

        public UiImage(UiHost host, RectTransform parent, Sprite sprite, float width, float height) : base(host)
        {
            var rt = UiBuild.Rect("ASL_Image", parent);
            Init(rt);
            _img = UiBuild.Img(rt, sprite, Color.white, false, false);
            _img.preserveAspect = true;
            float w = width, h = height;
            if (sprite != null && w <= 0f && h <= 0f)
            {
                try { var r = sprite.rect; w = r.width; h = r.height; } catch { }
            }
            var le = Le;
            if (w > 0f) { le.preferredWidth = w; le.minWidth = w; }
            if (h > 0f) { le.preferredHeight = h; le.minHeight = h; }
            FlexInRow = 0f;
            FlexInColumn = 0f;
        }

        public Sprite Sprite { get => _img.sprite; set { _img.sprite = value; Host.MarkDirty(); } }
        public Color Color { get => _img.color; set => _img.color = value; }
        public bool PreserveAspect { get => _img.preserveAspect; set => _img.preserveAspect = value; }
    }
}

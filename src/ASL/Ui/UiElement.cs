using System;
using ASL.Api;
using UnityEngine;
using UnityEngine.UI;

namespace ASL
{
    /// <summary>
    /// Base of every toolkit element: owns the GameObject/RectTransform, the shared <see cref="IUiElement"/>
    /// behaviour (size, tooltip, visibility, interactability) and the optional per-frame <see cref="Poll"/>
    /// that <see cref="UiHost"/> drives for elements that registered via <see cref="NeedPoll"/>.
    /// </summary>
    internal abstract class UiElement : IUiElement
    {
        public readonly UiHost Host;
        public UiContainer Parent;
        public GameObject Go { get; protected set; }
        public RectTransform Rt { get; protected set; }
        public bool Dead;

        /// <summary>Layout weight when added to a row / to a column (see <see cref="UiContainer"/>).</summary>
        public float FlexInRow = 0f, FlexInColumn = 1f;

        /// <summary>True once the width weight was set explicitly (SetSize/SetFlexible), so adding to a container keeps it.</summary>
        public bool FlexLocked;

        private LayoutElement _le;
        private string _id;
        private string _tooltip;
        private bool _interactable = true;
        private CanvasGroup _group;

        protected UiElement(UiHost host) { Host = host; }

        protected UiOwner Owner => Host.Owner;
        protected UiTheme Theme => Host.Theme;

        protected void Init(RectTransform rt)
        {
            Rt = rt;
            Go = rt.gameObject;
            _le = null;
            _group = null;
        }

        public LayoutElement Le => _le != null ? _le : (_le = UiBuild.Le(Go));

        protected void NeedPoll() => Host.AddPoller(this);

        /// <summary>Per-frame work; only called while the owning host is active.</summary>
        public virtual void Poll() { }

        public bool IsActiveInHierarchy()
        {
            try { return Go != null && Go.activeInHierarchy; } catch { return false; }
        }

        // ---- IUiElement ----------------------------------------------------------------------

        public string Id { get => _id; set => _id = value; }

        GameObject IUiElement.GameObject => Go;

        public virtual bool Visible
        {
            get => Go != null && Go.activeSelf;
            set
            {
                if (Go == null || Go.activeSelf == value) return;
                Go.SetActive(value);
                Host.MarkDirty();
            }
        }

        public virtual bool Interactable
        {
            get => _interactable;
            set
            {
                if (_interactable == value) return;
                _interactable = value;
                if (_group == null) _group = Go.AddComponent<CanvasGroup>();
                _group.interactable = value;
                _group.alpha = value ? 1f : 0.5f;
                OnInteractableChanged();
            }
        }

        protected virtual void OnInteractableChanged() { }

        public string Tooltip
        {
            get => _tooltip;
            set
            {
                bool had = !string.IsNullOrEmpty(_tooltip);
                _tooltip = value;
                if (!had && !string.IsNullOrEmpty(value)) Host.Tipped.Add(this);
            }
        }

        public IUiElement SetSize(float width, float height)
        {
            var le = Le;
            le.preferredWidth = width > 0f ? width : -1f;
            le.minWidth = width > 0f ? width : -1f;
            le.preferredHeight = height > 0f ? height : -1f;
            le.minHeight = height > 0f ? height : -1f;
            if (width > 0f) { le.flexibleWidth = 0f; FlexLocked = true; }
            Host.MarkDirty();
            return this;
        }

        public IUiElement SetFlexible(float flexibleWidth)
        {
            Le.flexibleWidth = flexibleWidth;
            FlexLocked = true;
            Host.MarkDirty();
            return this;
        }

        public virtual void Destroy()
        {
            if (Dead) return;
            MarkDead();
            if (Parent != null) Parent.RemoveChild(this);
            try { if (Go != null) UnityEngine.Object.Destroy(Go); } catch { }
            Host.MarkDirty();
        }

        /// <summary>Flags this element (and descendants) dead so pollers drop it; does not touch Unity objects.</summary>
        public virtual void MarkDead() { Dead = true; }
    }
}

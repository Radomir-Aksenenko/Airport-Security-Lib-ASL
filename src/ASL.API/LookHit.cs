using UnityEngine;

namespace ASL.Api
{
    /// <summary>
    /// What the local player is aiming at — the result of a camera raycast (see
    /// <see cref="IAslPlayer.GetLookedAt"/>). Check <see cref="Hit"/> first; the other fields are only
    /// meaningful when it is true.
    /// </summary>
    public struct LookHit
    {
        /// <summary>True if the ray hit something within range.</summary>
        public bool Hit;

        /// <summary>The hit object.</summary>
        public GameObject Object;

        /// <summary>The hit object's transform.</summary>
        public Transform Transform;

        /// <summary>World-space point where the ray hit.</summary>
        public Vector3 Point;

        /// <summary>Distance from the camera to the hit point.</summary>
        public float Distance;

        /// <summary>
        /// Mirror net id of the hit object (looked up on it or its parents), or <c>0</c> if it isn't a
        /// networked object. Hand this to <see cref="IAslNet.FindObject"/> on any peer to get the same object.
        /// </summary>
        public uint NetId;
    }
}

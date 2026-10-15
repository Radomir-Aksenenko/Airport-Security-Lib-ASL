// STUB: Assembly-CSharp = the game's own code. Every member here was INFERRED FROM USAGE in ASL/samples
// (the real game dump was not available). Treat each signature as a guess; see README "Risk points".
using System;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Mirror;
using UnityEngine;

// ---- global namespace -----------------------------------------------------------------------------

public class SettingsUi : MonoBehaviour
{
    public SettingsUi(IntPtr pointer) : base(pointer) { }
    public static SettingsUi Instance { get; }
    public GameObject contentsGo { get; set; }
}

public class LocalAnnouncementText : MonoBehaviour
{
    public LocalAnnouncementText(IntPtr pointer) : base(pointer) { }
    public static LocalAnnouncementText Instance { get; }
    public void DisplayForSeconds(string text, float seconds) => throw null;
}

public static class LanguageManager
{
    /// <summary>GUESS: a static string (language code or name) read via LanguageManager.CurrentLanguage.</summary>
    public static string CurrentLanguage { get; }
}

public class MaterialOffsetController : MonoBehaviour
{
    public MaterialOffsetController(IntPtr pointer) : base(pointer) { }
    public Il2CppSystem.Collections.Generic.List<Material> targetMats { get; set; }
    public Vector2 offsetSpeed { get; set; }
    public int materialIndex { get; set; }
    public string baseSTProperty { get; set; }
    public static Il2CppStringArray BaseTexturePropertyCandidates { get; }
    public static string StPropertyToTextureProperty(string stProperty) => throw null;
    public static bool HasUsableTexture(Material material, string property) => throw null;
}

public class MaterialOffsetScroller : MonoBehaviour
{
    public MaterialOffsetScroller(IntPtr pointer) : base(pointer) { }
    public Material targetMaterial { get; set; }
    public Renderer targetRenderer { get; set; }
    public string resolvedTextureProperty { get; }
    public Vector2 offsetSpeed { get; set; }
}

public class ConveyorMover : MonoBehaviour { public ConveyorMover(IntPtr pointer) : base(pointer) { } }
public class ConveyorInteractable : MonoBehaviour { public ConveyorInteractable(IntPtr pointer) : base(pointer) { } }

// ---- Metater ----------------------------------------------------------------------------------------

namespace Metater
{
    /// <summary>GUESS: a Mirror NetworkBehaviour (netId / isLocalPlayer / connectionToClient are used on it).</summary>
    public class MetaPlayer : NetworkBehaviour
    {
        public MetaPlayer(IntPtr pointer) : base(pointer) { }
        public static MetaPlayer LocalPlayerInstance { get; }
        /// <summary>GUESS: Il2Cpp List (only .Count and [i] are used).</summary>
        public static Il2CppSystem.Collections.Generic.List<MetaPlayer> Instances { get; }
        public void MultiplySpeed(float multiplier, float seconds) => throw null;
        public void ConsumeCoffee(float duration, float maxStackMultiplier, float speedMultiplier) => throw null;
        public void Jump() => throw null;
        public void TriggerAlcoholPassOut() => throw null;
    }

    public class MetaPlayerController : MonoBehaviour
    {
        public MetaPlayerController(IntPtr pointer) : base(pointer) { }
        public bool debugBlockSimulation { get; set; }
        public bool debugBlockMovement { get; set; }
        public bool debugBlockJump { get; set; }
        public bool debugBlockInput { get; set; }
        public float velocityY { get; set; }
        public float appliedRadius { get; set; }
        public CharacterController characterController { get; }
        public void Teleport(Vector3 position, bool resetVelocity) => throw null;
    }

    public class MetaCursor : MonoBehaviour
    {
        public MetaCursor(IntPtr pointer) : base(pointer) { }
        public static MetaCursor Instance { get; }
        /// <summary>GUESS: HashSet of the behaviours currently wanting a free cursor (MonoBehaviour added/removed).</summary>
        public Il2CppSystem.Collections.Generic.HashSet<MonoBehaviour> CursorUsers { get; }
    }
}

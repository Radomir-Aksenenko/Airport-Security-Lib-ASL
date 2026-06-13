using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace ASL
{
    /// <summary>
    /// Core entry point for ASL (Airport Security Lib) — the shared modding framework for
    /// <i>Airport Security Sucks!</i>. On boot it wires the event bus, then scans the game's
    /// <c>mods/</c> folder and loads every mod it finds (see <see cref="ModLoader"/>).
    /// </summary>
    [BepInPlugin(AslInfo.Guid, AslInfo.Name, AslInfo.Version)]
    public sealed class AslPlugin : BasePlugin
    {
        /// <summary>The running plugin instance. Null until <see cref="Load"/> has run.</summary>
        public static AslPlugin Instance { get; private set; }

        /// <summary>Shared framework log source.</summary>
        public static ManualLogSource Logger { get; private set; }

        /// <summary>Framework-owned Harmony instance, reserved for future opt-in hook patches.</summary>
        public Harmony Harmony { get; private set; }

        /// <summary>The event bus. Surfaced to mods through <c>IModContext.Events</c>.</summary>
        internal static EventBus Bus { get; private set; }

        /// <summary>Opt-in hook manager. Surfaced to mods through <c>IModContext.Hooks</c>.</summary>
        internal static HookManager Hooks { get; private set; }

        /// <summary>Applies no-code content mods (texture swaps, ...).</summary>
        internal static ContentRegistry Content { get; private set; }

        /// <summary>The shared in-game mod menu (F8). Surfaced to mods through <c>IModContext.Menu</c>.</summary>
        internal static MenuManager Menu { get; private set; }

        /// <summary>Networking awareness. Surfaced to mods through <c>IModContext.Net</c>.</summary>
        internal static NetState Net { get; private set; }

        private ModLoader _loader;

        public override void Load()
        {
            Instance = this;
            Logger = Log;                       // BasePlugin.Log -> the per-plugin ManualLogSource
            Harmony = new Harmony(AslInfo.Guid);
            Bus = new EventBus(Logger);
            Hooks = new HookManager(Harmony, Logger);
            Content = new ContentRegistry(Logger, Bus);
            Menu = new MenuManager(Logger);
            Net = new NetState(Logger);

            Logger.LogInfo($"{AslInfo.Name} v{AslInfo.Version} - booting.");

            AttachEventDriver();

            // mods/ lives next to the game executable so it is easy for players to find.
            var modsRoot = Path.Combine(Paths.GameRootPath, "mods");
            _loader = new ModLoader(Logger, modsRoot, Bus, Hooks, Content, Menu, Net);
            _loader.DiscoverAndLoad();

            Logger.LogInfo("Core online.");
        }

        public override bool Unload()
        {
            _loader?.UnloadAll();
            return true;
        }

        /// <summary>Injects the MonoBehaviour that drives Update / SceneChanged / LocalPlayerChanged.</summary>
        private void AttachEventDriver()
        {
            try
            {
                ClassInjector.RegisterTypeInIl2Cpp<AslBehaviour>();
                var go = new GameObject("ASL.Runtime");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.HideAndDontSave;
                go.AddComponent<AslBehaviour>();
                Logger.LogInfo("Event driver attached (Update / SceneChanged / LocalPlayerChanged).");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to attach event driver: {ex.Message}");
            }
        }
    }

    /// <summary>Single source of truth for the plugin's identity. Referenced by dependent mods.</summary>
    public static class AslInfo
    {
        public const string Guid = "com.radomir.asl";
        public const string Name = "ASL - Airport Security Lib";
        public const string Version = "0.1.0";
    }
}

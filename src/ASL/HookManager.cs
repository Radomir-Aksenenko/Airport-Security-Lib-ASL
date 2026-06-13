using System;
using System.Collections.Generic;
using System.Reflection;
using ASL.Api;
using BepInEx.Logging;
using HarmonyLib;

namespace ASL
{
    /// <summary>
    /// Implements <see cref="IModHooks"/>. Each target method is Harmony-patched at most once with
    /// a single shared postfix (<see cref="DispatchPostfix"/>); per-mod callbacks are kept in a
    /// table keyed by the original method and dispatched from there. Patch failures (common on
    /// IL2CPP, or when another plugin has poisoned the same method) are caught and reported, so a
    /// failed hook never takes down the framework or other mods.
    /// </summary>
    internal sealed class HookManager : IModHooks
    {
        private readonly Harmony _harmony;
        private readonly ManualLogSource _log;

        private static readonly Dictionary<MethodBase, List<Action<object>>> Callbacks = new();
        private static readonly HashSet<MethodBase> Patched = new();
        private static ManualLogSource _staticLog;

        public HookManager(Harmony harmony, ManualLogSource log)
        {
            _harmony = harmony;
            _log = log;
            _staticLog = log;
        }

        public bool TryPostfix(Type targetType, string methodName, Action<object> after)
        {
            if (targetType == null || string.IsNullOrEmpty(methodName) || after == null)
            {
                _log.LogError("TryPostfix: null/empty argument.");
                return false;
            }

            MethodInfo target;
            try
            {
                target = AccessTools.Method(targetType, methodName);
            }
            catch (Exception ex)
            {
                _log.LogError($"TryPostfix: lookup failed for {targetType.Name}.{methodName}: {ex.Message}");
                return false;
            }

            if (target == null)
            {
                _log.LogWarning($"TryPostfix: method {targetType.Name}.{methodName} not found.");
                return false;
            }

            if (!Callbacks.TryGetValue(target, out var list))
            {
                list = new List<Action<object>>();
                Callbacks[target] = list;
            }
            list.Add(after);

            if (!Patched.Contains(target))
            {
                try
                {
                    var postfix = new HarmonyMethod(
                        typeof(HookManager).GetMethod(nameof(DispatchPostfix),
                            BindingFlags.Static | BindingFlags.NonPublic));
                    _harmony.Patch(target, postfix: postfix);
                    Patched.Add(target);
                    _log.LogInfo($"Hook installed on {targetType.Name}.{methodName}.");
                }
                catch (Exception ex)
                {
                    _log.LogError($"Hook on {targetType.Name}.{methodName} failed to install (non-fatal): {ex.Message}");
                    list.Remove(after);
                    return false;
                }
            }

            return true;
        }

        // Single shared postfix for every hooked method; dispatches by the original method.
        private static void DispatchPostfix(object __instance, MethodBase __originalMethod)
        {
            if (__originalMethod == null) return;
            if (!Callbacks.TryGetValue(__originalMethod, out var list)) return;

            for (int i = 0; i < list.Count; i++)
            {
                try { list[i](__instance); }
                catch (Exception ex) { _staticLog?.LogError($"Hook callback for {__originalMethod.Name} threw: {ex.Message}"); }
            }
        }
    }
}

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
    ///
    /// Callbacks are tagged with their owning mod (see <see cref="ModHooks"/>) so they can be removed when the
    /// mod unloads / hot-reloads; the Harmony patch itself stays (it is shared) and simply dispatches to an
    /// empty list. The callback lists are copy-on-write arrays, so adding/removing during a dispatch is safe.
    /// </summary>
    internal sealed class HookManager : IModHooks
    {
        private readonly Harmony _harmony;
        private readonly ManualLogSource _log;

        private struct Entry
        {
            public string Owner;
            public Action<object> Callback;
        }

        private static readonly Dictionary<MethodBase, Entry[]> Callbacks = new();
        private static readonly HashSet<MethodBase> Patched = new();
        private static ManualLogSource _staticLog;

        public HookManager(Harmony harmony, ManualLogSource log)
        {
            _harmony = harmony;
            _log = log;
            _staticLog = log;
        }

        public bool TryPostfix(Type targetType, string methodName, Action<object> after)
            => TryPostfix(null, targetType, methodName, after);

        /// <summary>Same as the public overload, but the callback is owned by <paramref name="ownerModId"/> and removed by <see cref="RemoveOwner"/>.</summary>
        internal bool TryPostfix(string ownerModId, Type targetType, string methodName, Action<object> after)
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

            var entry = new Entry { Owner = ownerModId, Callback = after };
            Callbacks.TryGetValue(target, out var cur);
            Callbacks[target] = Append(cur, entry);

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
                    Callbacks[target] = Remove(Callbacks[target], after);
                    return false;
                }
            }

            return true;
        }

        /// <summary>Remove every callback that <paramref name="ownerModId"/> registered. Returns how many were removed.</summary>
        internal int RemoveOwner(string ownerModId)
        {
            if (ownerModId == null) return 0;
            int removed = 0;
            var keys = new List<MethodBase>(Callbacks.Keys);
            foreach (var key in keys)
            {
                var cur = Callbacks[key];
                int keep = 0;
                for (int i = 0; i < cur.Length; i++) if (cur[i].Owner != ownerModId) keep++;
                if (keep == cur.Length) continue;
                var next = new Entry[keep];
                int w = 0;
                for (int i = 0; i < cur.Length; i++) if (cur[i].Owner != ownerModId) next[w++] = cur[i];
                removed += cur.Length - keep;
                Callbacks[key] = next;
            }
            return removed;
        }

        private static Entry[] Append(Entry[] cur, Entry e)
        {
            int n = cur?.Length ?? 0;
            var next = new Entry[n + 1];
            if (n > 0) Array.Copy(cur, next, n);
            next[n] = e;
            return next;
        }

        private static Entry[] Remove(Entry[] cur, Action<object> cb)
        {
            if (cur == null) return null;
            int at = Array.FindIndex(cur, x => ReferenceEquals(x.Callback, cb));
            if (at < 0) return cur;
            var next = new Entry[cur.Length - 1];
            if (at > 0) Array.Copy(cur, 0, next, 0, at);
            if (at < cur.Length - 1) Array.Copy(cur, at + 1, next, at, cur.Length - at - 1);
            return next;
        }

        // Single shared postfix for every hooked method; dispatches by the original method.
        private static void DispatchPostfix(object __instance, MethodBase __originalMethod)
        {
            if (__originalMethod == null) return;
            if (!Callbacks.TryGetValue(__originalMethod, out var list)) return;

            for (int i = 0; i < list.Length; i++)
            {
                try { list[i].Callback(__instance); }
                catch (Exception ex)
                {
                    var who = list[i].Owner != null ? $" (mod '{list[i].Owner}')" : "";
                    _staticLog?.LogError($"Hook callback for {__originalMethod.Name}{who} threw: {ex.Message}");
                }
            }
        }
    }

    /// <summary>Per-mod <see cref="IModHooks"/> facade: tags every callback with the mod id so unload can remove them.</summary>
    internal sealed class ModHooks : IModHooks
    {
        private readonly HookManager _hooks;
        private readonly string _modId;

        public ModHooks(HookManager hooks, string modId)
        {
            _hooks = hooks;
            _modId = modId;
        }

        public bool TryPostfix(Type targetType, string methodName, Action<object> after) => _hooks.TryPostfix(_modId, targetType, methodName, after);

        /// <summary>Remove this mod's callbacks (called on unload / hot reload).</summary>
        public void Release() => _hooks.RemoveOwner(_modId);
    }
}

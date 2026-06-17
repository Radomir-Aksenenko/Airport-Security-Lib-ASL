using System;
using System.Collections.Generic;
using ASL.Api;
using BepInEx.Logging;
using Il2CppInterop.Runtime;
using Metater;
using Mirror;
using UnityEngine;

namespace ASL
{
    /// <summary>
    /// Live look + control over a game player, used by <see cref="AslPlayer"/> to back the control half
    /// of <see cref="IAslPlayer"/>. Freezing reuses the game's own simulation blockers (so gravity stops
    /// too); collider sizing drives the movement <see cref="CharacterController"/> and remembers the
    /// originals so <see cref="ResetCollider"/> can put them back.
    /// </summary>
    internal static class PlayerControl
    {
        private static ManualLogSource _log;
        public static void Init(ManualLogSource log) => _log = log;

        private struct ColliderState { public float Radius, Height; public Vector3 Center; }
        private static readonly Dictionary<uint, ColliderState> _savedColliders = new Dictionary<uint, ColliderState>();

        // ---- look ----

        public static LookHit GetLookedAt(float maxDistance)
        {
            var result = new LookHit();
            try
            {
                var cam = Camera.main;
                if (cam == null) return result;
                RaycastHit hit;
                if (!Physics.Raycast(cam.transform.position, cam.transform.forward, out hit, maxDistance)) return result;

                result.Hit = true;
                result.Transform = hit.transform;
                result.Object = hit.transform != null ? hit.transform.gameObject : null;
                result.Point = hit.point;
                result.Distance = hit.distance;
                try
                {
                    var nic = hit.transform.GetComponentInParent(Il2CppType.Of<NetworkIdentity>());
                    var ni = nic != null ? nic.TryCast<NetworkIdentity>() : null;
                    if (ni != null) result.NetId = ni.netId;
                }
                catch { }
            }
            catch (Exception ex) { Warn($"GetLookedAt failed: {ex.Message}"); }
            return result;
        }

        // ---- freeze ----

        public static void Freeze(MetaPlayer mp, bool frozen)
        {
            var c = Controller(mp);
            if (c == null) return;
            try
            {
                c.debugBlockSimulation = frozen;
                c.debugBlockMovement = frozen;
                c.debugBlockJump = frozen;
                if (frozen) { try { c.velocityY = 0f; } catch { } }
            }
            catch (Exception ex) { Warn($"Freeze failed: {ex.Message}"); }
        }

        public static bool IsFrozen(MetaPlayer mp)
        {
            var c = Controller(mp);
            try { return c != null && c.debugBlockSimulation; } catch { return false; }
        }

        // Block/unblock all player input (used while the mod menu is open so the camera doesn't spin and
        // the player doesn't move while you click). Orthogonal to Freeze (different flag). Returns true if
        // the controller was found and the flag was applied (so callers can retry until a player exists).
        public static bool SetInputBlocked(MetaPlayer mp, bool blocked)
        {
            var c = Controller(mp);
            if (c == null) return false;
            try { c.debugBlockInput = blocked; return true; } catch { return false; }
        }

        // ---- teleport ----

        public static void Teleport(MetaPlayer mp, Vector3 position)
        {
            var c = Controller(mp);
            try
            {
                if (c != null) { c.Teleport(position, true); return; }
            }
            catch { }
            try { if (mp != null) mp.transform.position = position; } catch (Exception ex) { Warn($"Teleport failed: {ex.Message}"); }
        }

        // ---- collider sizing ----

        public static void SetColliderSize(MetaPlayer mp, uint netId, float radius, float height)
        {
            var c = Controller(mp);
            CharacterController cc = null;
            try { if (c != null) cc = c.characterController; } catch { }
            if (cc == null) return;
            try
            {
                if (!_savedColliders.ContainsKey(netId))
                    _savedColliders[netId] = new ColliderState { Radius = cc.radius, Height = cc.height, Center = cc.center };

                float r = Mathf.Max(0.05f, radius);
                float h = Mathf.Max(height, r * 2f);   // CharacterController requires height >= 2*radius
                cc.radius = r;
                cc.height = h;
                var center = cc.center; center.y = h * 0.5f; cc.center = center;
                try { c.appliedRadius = r; } catch { }
            }
            catch (Exception ex) { Warn($"SetColliderSize failed: {ex.Message}"); }
        }

        public static void ResetCollider(MetaPlayer mp, uint netId)
        {
            if (!_savedColliders.TryGetValue(netId, out var saved)) return;
            _savedColliders.Remove(netId);
            var c = Controller(mp);
            CharacterController cc = null;
            try { if (c != null) cc = c.characterController; } catch { }
            if (cc == null) return;
            try { cc.radius = saved.Radius; cc.height = saved.Height; cc.center = saved.Center; }
            catch (Exception ex) { Warn($"ResetCollider failed: {ex.Message}"); }
        }

        // ---- helpers ----

        private static MetaPlayerController Controller(MetaPlayer mp)
        {
            if (mp == null) return null;
            try
            {
                var comp = mp.GetComponentInChildren(Il2CppType.Of<MetaPlayerController>(), true);
                return comp != null ? comp.TryCast<MetaPlayerController>() : null;
            }
            catch { return null; }
        }

        private static void Warn(string msg) { try { _log?.LogWarning($"[player] {msg}"); } catch { } }
    }
}

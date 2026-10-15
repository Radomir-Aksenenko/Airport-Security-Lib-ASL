// STUB: UnityEngine.AIModule (minimal).
using System;
namespace UnityEngine.AI
{
    public class NavMeshAgent : Behaviour
    {
        public NavMeshAgent(IntPtr pointer) : base(pointer) { }
        public Vector3 destination { get; set; }
        public float speed { get; set; }
        public float stoppingDistance { get; set; }
        public bool isStopped { get; set; }
        public bool isOnNavMesh { get; }
        public Vector3 velocity { get; set; }
        public float remainingDistance { get; }
        public bool SetDestination(Vector3 target) => throw null;
        public void ResetPath() => throw null;
        public bool Warp(Vector3 newPosition) => throw null;
    }
}

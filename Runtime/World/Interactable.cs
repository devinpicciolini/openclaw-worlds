using System;
using System.Collections.Generic;
using UnityEngine;
using OpenClawWorlds;

namespace OpenClawWorlds.World
{
    /// <summary>
    /// Event-based interaction system. Attach to trigger colliders on doors, NPCs, pickups, etc.
    /// Subscribe to static events to handle interactions in your game code.
    /// </summary>
    public class Interactable : MonoBehaviour
    {
        // ─── Static registry (avoids FindObjectsByType) ─────────────
        static readonly List<Interactable> all = new List<Interactable>();
        public static IReadOnlyList<Interactable> All => all;

        // ─── Events ─────────────────────────────────────────────────
        /// <summary>Fired when a door interaction occurs. Args: (interactable, actor).</summary>
        public static event Action<Interactable, GameObject> OnDoorInteract;

        /// <summary>Fired when an NPC interaction occurs. Args: (interactable, actor).</summary>
        public static event Action<Interactable, GameObject> OnNPCInteract;

        /// <summary>Fired when a pickup interaction occurs. Args: (interactable, actor).</summary>
        public static event Action<Interactable, GameObject> OnPickupInteract;

        /// <summary>Fired for any custom interaction type. Args: (interactable, actor).</summary>
        public static event Action<Interactable, GameObject> OnCustomInteract;

        [SerializeField] InteractableType type = InteractableType.Door;
        [SerializeField] string prompt = "Press E";
        [SerializeField] Zone targetZone = Zone.Saloon;
        Vector3 teleportPos;
        float teleportYaw;
        bool hasTeleportOverride;

        public InteractableType Type => type;
        public string Prompt => prompt;
        public Zone TargetZone => targetZone;
        public bool HasTeleport => hasTeleportOverride;
        public Vector3 TeleportPosition => teleportPos;
        public float TeleportYaw => teleportYaw;

        void OnEnable()  => all.Add(this);
        void OnDisable() => all.Remove(this);

        public void Init(InteractableType t, string p, Zone z)
        {
            type = t;
            prompt = p;
            targetZone = z;
        }

        public void SetPrompt(string p) => prompt = p;

        /// <summary>Set exact world position and facing direction for teleport.</summary>
        public void SetTeleport(Vector3 worldPos, float yaw)
        {
            teleportPos = worldPos;
            teleportYaw = yaw;
            hasTeleportOverride = true;
        }

        /// <summary>
        /// Call this to trigger the interaction. Fires the appropriate event.
        /// Your game code should subscribe to the events to handle the actual logic.
        /// </summary>
        public void Interact(GameObject actor)
        {
            switch (type)
            {
                case InteractableType.Door:
                    OnDoorInteract?.Invoke(this, actor);
                    break;
                case InteractableType.NPC:
                    OnNPCInteract?.Invoke(this, actor);
                    break;
                case InteractableType.Pickup:
                    OnPickupInteract?.Invoke(this, actor);
                    break;
                default:
                    OnCustomInteract?.Invoke(this, actor);
                    break;
            }
        }
    }
}

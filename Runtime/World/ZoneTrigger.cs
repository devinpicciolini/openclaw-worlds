using UnityEngine;
using System;
using OpenClawWorlds;

namespace OpenClawWorlds.World
{
    /// <summary>
    /// Fires an event when the player enters a zone collider.
    /// Event-based — subscribe to OnZoneEntered to handle zone changes in your game.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class ZoneTrigger : MonoBehaviour
    {
        /// <summary>Fired when any zone trigger is entered. Args: (zone).</summary>
        public static event Action<Zone> OnZoneEntered;

        /// <summary>
        /// Optional: tag to identify the player object. Defaults to "Player".
        /// </summary>
        public static string PlayerTag = "Player";

        [SerializeField] Zone zone = Zone.MainStreet;

        public void Init(Zone z) => zone = z;

        void Start()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(PlayerTag))
                OnZoneEntered?.Invoke(zone);
        }
    }
}

using UnityEngine;

namespace OpenClawWorlds.World
{
    /// <summary>
    /// Toggles the "Interior" child on/off based on player proximity.
    /// Interiors start inactive (their colliders are 3.5x the building size).
    /// Activates when the player is very close (i.e. inside the building).
    /// </summary>
    public class InteriorActivator : MonoBehaviour
    {
        public static float ActivateDistance = 8f;
        public static float DeactivateDistance = 15f;
        const float CheckInterval = 0.3f;

        Transform interior;
        Transform player;
        float nextCheck;
        bool active;

        void Start()
        {
            interior = transform.Find("Interior");
            if (interior != null)
                interior.gameObject.SetActive(false);
        }

        void Update()
        {
            if (interior == null) return;
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + CheckInterval;

            if (player == null)
            {
                player = GameObject.FindWithTag("Player")?.transform;
                if (player == null) return;
            }

            float dist = Vector3.Distance(player.position, transform.position);

            if (!active && dist < ActivateDistance)
            {
                interior.gameObject.SetActive(true);
                active = true;
            }
            else if (active && dist > DeactivateDistance)
            {
                interior.gameObject.SetActive(false);
                active = false;
            }
        }
    }
}

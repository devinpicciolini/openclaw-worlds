using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace OpenClawWorlds.World
{
    /// <summary>
    /// Distance-based streaming for AI-generated towns (City_* GameObjects).
    /// Three LOD tiers to preserve GPU/CPU budget:
    ///   Close  — full detail: everything enabled.
    ///   Medium — lights off, interior hidden, particles off.
    ///   Far    — entire town deactivated.
    /// </summary>
    public class TownStreamer : MonoBehaviour
    {
        [Header("LOD Ranges")]
        public float CloseRange   = 120f;
        public float MediumRange  = 250f;
        public float FarRange     = 300f;
        public float CheckInterval = 0.5f;

        Transform player;
        float nextCheck;

        struct TownEntry
        {
            public GameObject root;
            public Vector3 center;
            public LODTier tier;
        }

        enum LODTier { Close, Medium, Far, Unknown }

        readonly List<TownEntry> towns = new List<TownEntry>();

        void Update()
        {
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + CheckInterval;

            if (player == null)
            {
                player = GameObject.FindWithTag("Player")?.transform;
                if (player == null) return;
            }

            RefreshTownList();
            ApplyLOD();
        }

        void RefreshTownList()
        {
            for (int i = towns.Count - 1; i >= 0; i--)
                if (towns[i].root == null) towns.RemoveAt(i);

            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var go in roots)
            {
                if (!go.name.StartsWith("City_")) continue;
                bool found = false;
                for (int i = 0; i < towns.Count; i++)
                    if (towns[i].root == go) { found = true; break; }
                if (found) continue;

                towns.Add(new TownEntry
                {
                    root = go,
                    center = go.transform.position,
                    tier = LODTier.Unknown
                });
            }
        }

        void ApplyLOD()
        {
            Vector3 pPos = new Vector3(player.position.x, 0, player.position.z);

            for (int i = 0; i < towns.Count; i++)
            {
                var entry = towns[i];
                if (entry.root == null) continue;

                Vector3 tPos = new Vector3(entry.center.x, 0, entry.center.z);
                float dist = Vector3.Distance(pPos, tPos);

                LODTier newTier;
                if (dist < CloseRange) newTier = LODTier.Close;
                else if (dist < MediumRange) newTier = LODTier.Medium;
                else newTier = LODTier.Far;

                if (newTier == entry.tier) continue;

                switch (newTier)
                {
                    case LODTier.Close:
                        SetTownActive(entry.root, true);
                        SetLightsEnabled(entry.root, true);
                        SetInteriorsVisible(entry.root, true);
                        break;
                    case LODTier.Medium:
                        SetTownActive(entry.root, true);
                        SetLightsEnabled(entry.root, false);
                        SetInteriorsVisible(entry.root, false);
                        break;
                    case LODTier.Far:
                        SetTownActive(entry.root, false);
                        break;
                }

                entry.tier = newTier;
                towns[i] = entry;
            }
        }

        static void SetTownActive(GameObject town, bool active)
        {
            if (town.activeSelf != active)
                town.SetActive(active);
        }

        static void SetLightsEnabled(GameObject town, bool enabled)
        {
            var lights = town.GetComponentsInChildren<Light>(true);
            foreach (var light in lights)
                light.enabled = enabled;
        }

        static void SetInteriorsVisible(GameObject town, bool visible)
        {
            var transforms = town.GetComponentsInChildren<Transform>(true);
            foreach (var t in transforms)
            {
                if (t.name == "Interior")
                    t.gameObject.SetActive(visible);
            }
        }
    }
}

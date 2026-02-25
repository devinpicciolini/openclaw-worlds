using UnityEngine;
using OpenClawWorlds;
using OpenClawWorlds.Agents;

namespace OpenClawWorlds.World
{
    /// <summary>
    /// Spawns NPCs from prefabs or fallback capsule geometry.
    /// Interior shopkeepers and outdoor wandering townsfolk.
    /// NPC data is driven by IAssetMapper — change NPCs by providing a custom mapper.
    /// </summary>
    public static class NPCBuilder
    {
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        static RuntimeAnimatorController _mascController;
        static RuntimeAnimatorController _femnController;

        // ── Interior NPCs ──

        public static void PlaceInteriorNPC(Transform building, BuildingDef def, TownMaterials m)
        {
            var mapper = BuildingBuilder.AssetMapper;
            if (mapper == null) return;

            NPCTemplate tmpl = mapper.GetZoneOverrideNPC(def.zone);
            if (string.IsNullOrEmpty(tmpl.name))
                tmpl = mapper.GetNPCTemplate(def.interior);
            if (string.IsNullOrEmpty(tmpl.name))
                return;

            float d = def.size.z;
            float offsetZ = d * tmpl.zFraction + (tmpl.zFraction < 0 ? -0.8f : 0.5f);
            float offsetX = (def.interior == InteriorStyle.Clinic) ? -def.size.x / 4f : 0f;

            var agent = building.GetComponent<BuildingAgent>();
            string agentId = agent != null ? agent.agentId : null;

            var localPos = V(offsetX, 0, offsetZ);
            SpawnNPC(building, tmpl.prefab, tmpl.name, tmpl.greeting, tmpl.offerings, localPos, def.zone, m, agentId, tmpl.persistent, tmpl.personality);
        }

        // ── Shared Prefab Loading ──

        static RuntimeAnimatorController GetController(bool feminine)
        {
            var mapper = BuildingBuilder.AssetMapper;
            string path = mapper != null
                ? mapper.GetAnimatorController(feminine)
                : (feminine ? "Animations/AC_Polygon_Feminine" : "Animations/AC_Polygon_Masculine");

            if (feminine)
            {
                if (_femnController == null)
                    _femnController = Resources.Load<RuntimeAnimatorController>(path);
                return _femnController;
            }
            if (_mascController == null)
                _mascController = Resources.Load<RuntimeAnimatorController>(path);
            return _mascController;
        }

        static bool IsFeminine(string prefabName)
        {
            var mapper = BuildingBuilder.AssetMapper;
            if (mapper != null) return mapper.IsFeminine(prefabName);
            return prefabName.Contains("Woman") || prefabName.Contains("Girl") || prefabName.Contains("Female");
        }

        static void AssignAnimator(GameObject npc, string prefabName)
        {
            var animator = npc.GetComponentInChildren<Animator>();
            if (animator == null)
                animator = npc.AddComponent<Animator>();

            animator.applyRootMotion = false;
            var ctrl = GetController(IsFeminine(prefabName));
            if (ctrl != null)
                animator.runtimeAnimatorController = ctrl;
        }

        static GameObject LoadOrCreateNPC(string prefabName, string npcName, Vector3 pos, TownMaterials m, Color fallbackColor)
        {
            GameObject prefab = null;
            if (!string.IsNullOrEmpty(prefabName))
                prefab = PrefabLibrary.Find(prefabName);

            GameObject npc;
            if (prefab != null)
            {
                npc = Object.Instantiate(prefab, pos, Quaternion.identity);
                npc.name = npcName;
                PrefabLibrary.FixMaterials(npc, prefabName);
            }
            else
            {
                npc = new GameObject(npcName);
                npc.transform.position = pos;
                var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                capsule.name = "FallbackMesh";
                capsule.transform.SetParent(npc.transform);
                capsule.transform.localPosition = new Vector3(0, 1f, 0);
                capsule.transform.localScale = new Vector3(0.4f, 0.8f, 0.4f);
                var renderer = capsule.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.material = TownMaterials.QuickMat(fallbackColor);
            }
            return npc;
        }

        static void SpawnNPC(Transform building, string prefabName, string npcName,
            string greeting, string[] offerings, Vector3 localPos, Zone zone, TownMaterials m,
            string agentId = null, bool persistent = false, string personality = null)
        {
            var npc = LoadOrCreateNPC(prefabName, npcName, Vector3.zero, m, new Color(0.8f, 0.6f, 0.4f));

            npc.transform.SetParent(building);
            npc.transform.localPosition = localPos;
            npc.transform.localRotation = Quaternion.Euler(0, 180f, 0);

            var col = npc.AddComponent<BoxCollider>();
            col.size = new Vector3(2f, 2.5f, 2f);
            col.center = new Vector3(0, 1f, 0);
            col.isTrigger = true;

            string prompt = $"[E] Talk to {npcName}";
            var interactable = npc.AddComponent<Interactable>();
            interactable.Init(InteractableType.NPC, prompt, zone);

            var data = npc.AddComponent<NPCData>();
            data.Init(npcName, greeting, offerings, agentId, persistent, personality);

            if (!string.IsNullOrEmpty(prefabName))
                AssignAnimator(npc, prefabName);
            npc.AddComponent<NPCIdleAnimator>();
        }

        // ── Wandering Townsfolk ──

        public static void SpawnTownsfolk(Transform root, string prefabName, string npcName,
            Vector3 pos, float speed, float radius, TownMaterials m)
        {
            var npc = LoadOrCreateNPC(prefabName, npcName, pos, m, new Color(0.7f, 0.55f, 0.4f));
            npc.transform.SetParent(root);

            // Remove prefab colliders that may interfere with wandering
            foreach (var col in npc.GetComponentsInChildren<Collider>())
                Object.Destroy(col);

            // Add a trigger collider so the NPC is detectable by raycasts and proximity
            var tc = npc.AddComponent<BoxCollider>();
            tc.size = new Vector3(1f, 2f, 1f);
            tc.center = new Vector3(0, 1f, 0);
            tc.isTrigger = true;

            var wanderer = npc.AddComponent<WanderingNPC>();
            wanderer.Init(speed, radius, 3f, 8f);

            if (!string.IsNullOrEmpty(prefabName))
                AssignAnimator(npc, prefabName);
        }
    }
}

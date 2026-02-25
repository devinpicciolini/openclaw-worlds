using UnityEngine;
using OpenClawWorlds;

namespace OpenClawWorlds.World
{
    /// <summary>
    /// Spawns buildings from prefabs or fallback geometry.
    /// Adds gameplay components: triggers, interactables, teleport, lights.
    /// </summary>
    public static class BuildingBuilder
    {
        /// <summary>
        /// The active asset mapper. Set this to your own IAssetMapper implementation
        /// to use custom prefabs. Defaults to primitive-geometry fallback.
        /// </summary>
        public static IAssetMapper AssetMapper { get; set; } = new DefaultAssetMapper();

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        public static void Build(Transform root, BuildingDef def, TownMaterials m)
        {
            var bld = new GameObject(def.name);
            bld.transform.SetParent(root);
            bld.transform.position = def.position;
            bld.transform.rotation = Quaternion.Euler(0f, def.rotation, 0f);

            float w = def.size.x, h = def.size.y, d = def.size.z;
            float frontZ = d / 2f;

            // Try prefab first via asset mapper
            string bldPrefab = AssetMapper?.GetBuildingPrefab(def);
            GameObject exterior = null;
            bool hasPrefab = false;

            if (!string.IsNullOrEmpty(bldPrefab))
            {
                exterior = PrefabLibrary.Spawn(bldPrefab, bld.transform, Vector3.zero);
                hasPrefab = exterior != null;
            }

            if (hasPrefab)
            {
                exterior.name = "Exterior";
                float s = def.scale > 0.01f ? def.scale : 1f;
                if (s != 1f)
                    exterior.transform.localScale = Vector3.one * s;
                var bounds = GetLocalBounds(exterior);
                w = bounds.size.x;
                h = bounds.size.y;
                d = bounds.size.z;
                frontZ = bounds.max.z;
                def.size = new Vector3(w, h, d);
            }
            else
            {
                // Fallback: visible colored cube shell
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "FallbackShell";
                cube.transform.SetParent(bld.transform);
                cube.transform.localPosition = V(0, h / 2f, 0);
                cube.transform.localScale = V(w, h, d);
                var renderer = cube.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var mat = TownMaterials.QuickMat(
                        def.wallColor != default ? def.wallColor : new Color(0.6f, 0.45f, 0.3f));
                    renderer.material = mat;
                }
            }

            // Sign — only on fallback buildings
            if (!hasPrefab)
                AddBuildingSign(bld, def, h, frontZ);

            // Gameplay triggers
            if (def.hasDoor)
                BuildTriggers(bld, def, frontZ);
            BuildCeilingLight(bld, w, h, d);

            // Interior furniture (only for fallback buildings)
            InteriorBuilder.Furnish(bld, def, m, hasPrefab);

            // Interior NPC
            NPCBuilder.PlaceInteriorNPC(bld.transform, def, m);

            if (!hasPrefab)
                bld.AddComponent<InteriorActivator>();
        }

        // ── Bounds ──

        static Bounds GetLocalBounds(GameObject go)
        {
            var filters = go.GetComponentsInChildren<MeshFilter>();
            if (filters.Length == 0) return new Bounds(Vector3.zero, Vector3.one * 4f);

            var root = go.transform.parent ?? go.transform;
            Bounds b = default;
            bool first = true;

            foreach (var mf in filters)
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                for (int cx = 0; cx < 2; cx++)
                for (int cy = 0; cy < 2; cy++)
                for (int cz = 0; cz < 2; cz++)
                {
                    var corner = mb.min + Vector3.Scale(mb.size, new Vector3(cx, cy, cz));
                    var pt = root.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (first) { b = new Bounds(pt, Vector3.zero); first = false; }
                    else b.Encapsulate(pt);
                }
            }
            return first ? new Bounds(Vector3.zero, Vector3.one * 4f) : b;
        }

        // ── Triggers ──

        static void BuildTriggers(GameObject bld, BuildingDef def, float frontZ)
        {
            // Enter trigger
            var enter = new GameObject("EnterTrigger");
            enter.transform.SetParent(bld.transform);
            enter.transform.localPosition = V(0, 1.5f, frontZ + 2f);
            enter.transform.localRotation = Quaternion.identity;
            var ec = enter.AddComponent<BoxCollider>();
            ec.size = new Vector3(6f, 4f, 8f);
            ec.isTrigger = true;
            var ei = enter.AddComponent<Interactable>();
            ei.Init(InteractableType.Door, $"[E] Enter {def.name}", def.zone);
            ei.SetTeleport(
                bld.transform.TransformPoint(V(0, 0.5f, -1f)),
                def.rotation + 180f);

            // Exit trigger
            var exit = new GameObject("ExitTrigger");
            exit.transform.SetParent(bld.transform);
            exit.transform.localPosition = V(0, 1.5f, 0f);
            exit.transform.localRotation = Quaternion.identity;
            var xc = exit.AddComponent<BoxCollider>();
            xc.size = new Vector3(4f, 3f, 3f);
            xc.isTrigger = true;
            var xi = exit.AddComponent<Interactable>();
            xi.Init(InteractableType.Door, "[E] Exit to Street", Zone.MainStreet);
            xi.SetTeleport(
                bld.transform.TransformPoint(V(0, 0.5f, frontZ + 5f)),
                def.rotation);
        }

        static void BuildCeilingLight(GameObject bld, float w, float h, float d)
        {
            var lt = new GameObject("Light");
            lt.transform.SetParent(bld.transform);
            lt.transform.localPosition = V(0, h - 0.5f, 0);
            var pl = lt.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.color = new Color(1f, 0.85f, 0.6f);
            pl.intensity = 2f;
            pl.range = Mathf.Min(Mathf.Max(w, d) * 1.2f, 15f);
            pl.shadows = LightShadows.None;
        }

        static void AddBuildingSign(GameObject bld, BuildingDef def, float h, float frontZ)
        {
            if (def.zone == Zone.Church || def.zone == Zone.TrainStation) return;

            int variant = Mathf.Abs(def.name.GetHashCode()) % 20 + 1;
            string signName = $"SM_Bld_Sign_{variant:D2}";
            float signY = h * 0.7f;
            PrefabLibrary.Spawn(signName, bld.transform, V(0, signY, frontZ));
        }
    }
}

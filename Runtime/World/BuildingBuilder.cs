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

        public static void Build(Transform root, BuildingDef def, TownMaterials m, string agentId = null)
        {
            var bld = new GameObject(def.name);
            bld.transform.SetParent(root);
            bld.transform.position = def.position;
            bld.transform.rotation = Quaternion.Euler(0f, def.rotation, 0f);

            // Add BuildingAgent BEFORE interior NPCs are placed so they can read the agentId
            if (!string.IsNullOrEmpty(agentId))
                bld.AddComponent<BuildingAgent>().agentId = agentId;

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
                // Fallback: procedural building from primitives — no asset pack needed
                BuildPrimitiveBuilding(bld.transform, def, w, h, d, m);
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

        // ── Procedural Primitive Building ──

        static void BuildPrimitiveBuilding(Transform parent, BuildingDef def, float w, float h, float d, TownMaterials m)
        {
            Color wallColor = def.wallColor != default ? def.wallColor : new Color(0.65f, 0.55f, 0.4f);
            Color roofColor = new Color(wallColor.r * 0.6f, wallColor.g * 0.4f, wallColor.b * 0.25f);
            Color trimColor = new Color(wallColor.r * 0.7f, wallColor.g * 0.7f, wallColor.b * 0.7f);
            Color doorColor = new Color(0.35f, 0.22f, 0.12f);
            Color windowColor = new Color(0.5f, 0.7f, 0.9f, 0.7f);

            Material wallMat = TownMaterials.QuickMat(wallColor);
            Material roofMat = TownMaterials.QuickMat(roofColor);
            Material trimMat = TownMaterials.QuickMat(trimColor);
            Material doorMat = TownMaterials.QuickMat(doorColor);
            Material windowMat = TownMaterials.QuickMat(windowColor);

            var building = new GameObject("PrimitiveBuilding");
            building.transform.SetParent(parent);
            building.transform.localPosition = Vector3.zero;

            // ── Main walls ──
            var walls = GameObject.CreatePrimitive(PrimitiveType.Cube);
            walls.name = "Walls";
            walls.transform.SetParent(building.transform);
            walls.transform.localPosition = V(0, h / 2f, 0);
            walls.transform.localScale = V(w, h, d);
            PrimApplyMat(walls, wallMat);

            // ── Foundation ──
            float baseH = 0.2f;
            var baseTrim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseTrim.name = "Foundation";
            baseTrim.transform.SetParent(building.transform);
            baseTrim.transform.localPosition = V(0, baseH / 2f, 0);
            baseTrim.transform.localScale = V(w + 0.15f, baseH, d + 0.15f);
            PrimApplyMat(baseTrim, trimMat);

            // ── Roof ──
            float roofH = h * 0.2f;
            float roofOverhang = 0.4f;
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Roof";
            roof.transform.SetParent(building.transform);
            roof.transform.localPosition = V(0, h + roofH / 2f, 0);
            roof.transform.localScale = V(w + roofOverhang, roofH, d + roofOverhang);
            PrimApplyMat(roof, roofMat);

            // ── Roof ridge ──
            if (w > 6f)
            {
                var ridge = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ridge.name = "RoofRidge";
                ridge.transform.SetParent(building.transform);
                ridge.transform.localPosition = V(0, h + roofH + 0.15f, 0);
                ridge.transform.localScale = V(w * 0.4f, 0.3f, d + roofOverhang * 0.5f);
                PrimApplyMat(ridge, roofMat);
            }

            // ── Door ──
            float doorW = 1.2f, doorH = 2.2f;
            float frontZ = d / 2f;
            var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.name = "Door";
            door.transform.SetParent(building.transform);
            door.transform.localPosition = V(0, doorH / 2f, frontZ + 0.02f);
            door.transform.localScale = V(doorW, doorH, 0.1f);
            PrimApplyMat(door, doorMat);

            // Door frame
            var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.name = "DoorFrame";
            frame.transform.SetParent(building.transform);
            frame.transform.localPosition = V(0, doorH / 2f, frontZ + 0.03f);
            frame.transform.localScale = V(doorW + 0.3f, doorH + 0.15f, 0.06f);
            PrimApplyMat(frame, trimMat);

            // ── Windows ──
            int numWindows = Mathf.Max(1, Mathf.FloorToInt((w - 3f) / 3f));
            float windowW = 0.8f, windowH = 1.0f;
            float windowY = h * 0.55f;

            for (int side = 0; side < 2; side++)
            {
                float zPos = side == 0 ? frontZ + 0.02f : -(frontZ + 0.02f);
                for (int i = 0; i < numWindows; i++)
                {
                    float xOff = (i - (numWindows - 1) / 2f) * 3f;
                    if (side == 0 && Mathf.Abs(xOff) < doorW + 0.5f) continue;

                    var win = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    win.name = $"Window_{side}_{i}";
                    win.transform.SetParent(building.transform);
                    win.transform.localPosition = V(xOff, windowY, zPos);
                    win.transform.localScale = V(windowW, windowH, 0.06f);
                    PrimApplyMat(win, windowMat);

                    var wf = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wf.name = $"WindowFrame_{side}_{i}";
                    wf.transform.SetParent(building.transform);
                    wf.transform.localPosition = V(xOff, windowY, zPos + (side == 0 ? 0.01f : -0.01f));
                    wf.transform.localScale = V(windowW + 0.2f, windowH + 0.2f, 0.04f);
                    PrimApplyMat(wf, trimMat);
                }
            }

            // ── Side windows ──
            int sideWindows = Mathf.Max(1, Mathf.FloorToInt((d - 2f) / 3f));
            for (int side = 0; side < 2; side++)
            {
                float xPos = side == 0 ? w / 2f + 0.02f : -(w / 2f + 0.02f);
                for (int i = 0; i < sideWindows; i++)
                {
                    float zOff = (i - (sideWindows - 1) / 2f) * 3f;
                    var win = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    win.name = $"SideWindow_{side}_{i}";
                    win.transform.SetParent(building.transform);
                    win.transform.localPosition = V(xPos, windowY, zOff);
                    win.transform.localScale = V(0.06f, windowH, windowW);
                    PrimApplyMat(win, windowMat);
                }
            }

            // ── Awning / porch overhang ──
            if (def.hasDoor)
            {
                var awning = GameObject.CreatePrimitive(PrimitiveType.Cube);
                awning.name = "Awning";
                awning.transform.SetParent(building.transform);
                awning.transform.localPosition = V(0, doorH + 0.3f, frontZ + 1f);
                awning.transform.localScale = V(w * 0.6f, 0.1f, 2f);
                PrimApplyMat(awning, roofMat);

                for (int i = -1; i <= 1; i += 2)
                {
                    var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    post.name = $"AwningPost_{i}";
                    post.transform.SetParent(building.transform);
                    post.transform.localPosition = V(w * 0.28f * i, doorH / 2f + 0.15f, frontZ + 1.8f);
                    post.transform.localScale = V(0.12f, doorH / 2f + 0.15f, 0.12f);
                    PrimApplyMat(post, trimMat);
                }
            }

            // ── Chimney on larger buildings ──
            if (w > 8f || def.zone == Zone.Saloon || def.zone == Zone.GeneralStore)
            {
                var chimney = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chimney.name = "Chimney";
                chimney.transform.SetParent(building.transform);
                chimney.transform.localPosition = V(w * 0.35f, h + roofH + 0.6f, 0);
                chimney.transform.localScale = V(0.7f, 1.2f, 0.7f);
                PrimApplyMat(chimney, trimMat);
            }
        }

        static void PrimApplyMat(GameObject go, Material mat)
        {
            var r = go.GetComponent<Renderer>();
            if (r != null) r.material = mat;
        }

        static void AddBuildingSign(GameObject bld, BuildingDef def, float h, float frontZ)
        {
            if (def.zone == Zone.Church || def.zone == Zone.TrainStation) return;

            // Try prefab sign first
            int variant = Mathf.Abs(def.name.GetHashCode()) % 20 + 1;
            string signName = $"SM_Bld_Sign_{variant:D2}";
            float signY = h * 0.7f;
            var signGo = PrefabLibrary.Spawn(signName, bld.transform, V(0, signY, frontZ));
            if (signGo != null) return;

            // Fallback: simple text mesh sign
            var sign = new GameObject("Sign");
            sign.transform.SetParent(bld.transform);
            sign.transform.localPosition = V(0, signY, frontZ + 0.05f);

            // Sign board
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "SignBoard";
            board.transform.SetParent(sign.transform);
            board.transform.localPosition = Vector3.zero;
            board.transform.localScale = V(Mathf.Min(def.name.Length * 0.25f + 0.5f, 4f), 0.6f, 0.08f);
            var boardR = board.GetComponent<Renderer>();
            if (boardR != null) boardR.material = TownMaterials.QuickMat(new Color(0.3f, 0.2f, 0.1f));

            // Text
            var textGo = new GameObject("SignText");
            textGo.transform.SetParent(sign.transform);
            textGo.transform.localPosition = V(0, 0, 0.06f);
            var tm = textGo.AddComponent<TextMesh>();
            tm.text = def.name;
            tm.fontSize = 24;
            tm.characterSize = 0.08f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.9f, 0.85f, 0.7f);
        }
    }
}

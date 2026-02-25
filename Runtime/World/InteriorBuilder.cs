using UnityEngine;
using OpenClawWorlds;

namespace OpenClawWorlds.World
{
    /// <summary>
    /// Furnishes building interiors with style-appropriate furniture.
    /// Uses prefabs when available, falls back to primitive geometry.
    /// Interiors use a "TARDIS" approach — the interior space is scaled up
    /// significantly so buildings feel spacious from inside.
    /// Only applies to fallback (non-prefab) buildings.
    /// </summary>
    public static class InteriorBuilder
    {
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        /// <summary>
        /// Interior space multiplier. Interiors are this many times wider/deeper
        /// than the physical exterior, creating a "bigger on the inside" effect.
        /// </summary>
        public static float InteriorScale = 3.5f;

        public static void Furnish(GameObject bld, BuildingDef def, TownMaterials m, bool hasPrefab = false)
        {
            // Prefab buildings already have their own interior geometry baked in.
            // Adding furniture on top creates duplicate structures. Skip entirely.
            if (hasPrefab) return;

            // Fallback buildings have no interior detail, so we create
            // a TARDIS-scaled interior with furniture, floor, and walls.
            var interiorRoot = new GameObject("Interior");
            interiorRoot.transform.SetParent(bld.transform);
            interiorRoot.transform.localPosition = Vector3.zero;
            interiorRoot.transform.localRotation = Quaternion.identity;

            float iw = def.size.x * InteriorScale;
            float ih = def.size.y > 0 ? def.size.y * 1.5f : 5f;
            float id = def.size.z * InteriorScale;

            // Floor plane
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "InteriorFloor";
            floor.transform.SetParent(interiorRoot.transform);
            floor.transform.localPosition = V(0, 0.11f, 0);
            floor.transform.localScale = V(iw / 10f, 1, id / 10f);
            var floorCollider = floor.GetComponent<MeshCollider>();
            if (floorCollider != null) Object.Destroy(floorCollider);
            var floorMat = TownMaterials.QuickMat(new Color(0.35f, 0.22f, 0.12f), 0.3f);
            floor.GetComponent<Renderer>().material = floorMat;

            // Invisible walls
            BuildInteriorWall(interiorRoot.transform, "WallLeft",  V(-iw/2, ih/2, 0), V(0.1f, ih, id));
            BuildInteriorWall(interiorRoot.transform, "WallRight", V(iw/2, ih/2, 0),  V(0.1f, ih, id));
            BuildInteriorWall(interiorRoot.transform, "WallBack",  V(0, ih/2, -id/2), V(iw, ih, 0.1f));
            BuildInteriorWall(interiorRoot.transform, "WallFront", V(0, ih/2, id/2),  V(iw, ih, 0.1f));

            // Furniture at scaled dimensions
            var interiorDef = def;
            interiorDef.size = V(iw, ih, id);
            AddFurniture(interiorRoot.transform, interiorDef, m);

            // Start inactive — InteriorActivator toggles on/off by player distance
            interiorRoot.SetActive(false);
        }

        /// <summary>Invisible wall collider to bound the interior space.</summary>
        static void BuildInteriorWall(Transform parent, string name, Vector3 pos, Vector3 size)
        {
            var wall = new GameObject(name);
            wall.transform.SetParent(parent);
            wall.transform.localPosition = pos;
            var col = wall.AddComponent<BoxCollider>();
            col.size = size;
        }

        // ── Style-Specific Furniture ──

        static void AddFurniture(Transform b, BuildingDef def, TownMaterials m)
        {
            float w = def.size.x, d = def.size.z;
            var go = b.gameObject;

            switch (def.interior)
            {
                case InteriorStyle.Saloon:   FurnishSaloon(go, b, w, d, m); break;
                case InteriorStyle.Office:   FurnishOffice(go, def, w, d, m); break;
                case InteriorStyle.Shop:     FurnishShop(go, w, d, m); break;
                case InteriorStyle.Jail:     FurnishJail(go, w, d, m); break;
                case InteriorStyle.Hotel:    FurnishHotel(go, b, w, d, m); break;
                case InteriorStyle.Church:   FurnishChurch(go, w, d, m); break;
                case InteriorStyle.Warehouse:FurnishWarehouse(go, w, d, m); break;
                case InteriorStyle.School:   FurnishSchool(go, w, d, m); break;
                case InteriorStyle.Clinic:   FurnishClinic(go, w, d, m); break;
                case InteriorStyle.Smithy:   FurnishSmithy(go, w, d, m); break;
                case InteriorStyle.Library:  FurnishLibrary(go, w, d, m); break;
            }
        }

        // ── SALOON ──

        static void FurnishSaloon(GameObject go, Transform b, float w, float d, TownMaterials m)
        {
            var t = go.transform;

            PropBuilder.InteriorLantern(go, V(-w/2 + 0.4f, 2.2f, -d/6), true, m);
            PropBuilder.InteriorLantern(go, V(w/2 - 0.4f, 2.2f, -d/6), false, m);
            PropBuilder.InteriorLantern(go, V(-w/2 + 0.4f, 2.2f, d/4), true, m);
            PropBuilder.InteriorLantern(go, V(w/2 - 0.4f, 2.2f, d/4), false, m);

            // Bar counter
            PropBuilder.BoxMat(go, "Bar", V(0, 0.55f, -d/4), V(w*0.7f, 1.1f, 0.5f), m.WoodDark);
            PropBuilder.BoxMat(go, "BarTop", V(0, 1.12f, -d/4), V(w*0.72f, 0.06f, 0.55f),
                m.MakeMat(new Color(0.35f, 0.2f, 0.08f), 0.5f));
            PropBuilder.BoxMat(go, "FootRail", V(0, 0.15f, -d/4 + 0.35f), V(w*0.68f, 0.04f, 0.04f), m.Brass);

            // Back-bar shelf with bottles
            PropBuilder.Shelf(go, V(0, 0, -d/2 + 0.6f), 0f, m);
            for (int i = 0; i < 5; i++)
                PropBuilder.Bottle(go, V(-w*0.25f + i * (w*0.5f/4), 1.3f, -d/2 + 0.6f), m);

            PropBuilder.PLocalRandom(PropBuilder.Cups, t, V(-0.8f, 1.18f, -d/4));
            PropBuilder.PLocalRandom(PropBuilder.Cups, t, V(0.5f, 1.18f, -d/4));

            // Stools at bar
            PropBuilder.Stool(go, V(-1f, 0, -d/4 + 0.7f), 0f, m);
            PropBuilder.Stool(go, V(0f, 0, -d/4 + 0.7f), 0f, m);
            PropBuilder.Stool(go, V(1f, 0, -d/4 + 0.7f), 0f, m);

            // Poker tables with chairs
            PropBuilder.SaloonTable(b, V(-w/4, 0, d/6), m);
            PropBuilder.SaloonTable(b, V(w/4, 0, d/6), m);
            PropBuilder.Chair(go, V(-w/4 - 1, 0, d/6), 90f, m);
            PropBuilder.Chair(go, V(-w/4 + 1, 0, d/6), -90f, m);
            PropBuilder.Chair(go, V(w/4 - 1, 0, d/6), 90f, m);
            PropBuilder.Chair(go, V(w/4 + 1, 0, d/6), -90f, m);

            // Piano
            PropBuilder.PLocal("SM_Prop_Piano_01", t, V(-w/3, 0, -d/4 + 1.5f));
            PropBuilder.PLocal("SM_Prop_PianoSeat_01", t, V(-w/3, 0, -d/4 + 2.5f));
        }

        // ── OFFICE ──

        static void FurnishOffice(GameObject go, BuildingDef def, float w, float d, TownMaterials m)
        {
            var t = go.transform;

            PropBuilder.InteriorLantern(go, V(-w/2 + 0.4f, 2.2f, -d/6), true, m);
            PropBuilder.InteriorLantern(go, V(w/2 - 0.4f, 2.2f, -d/6), false, m);

            PropBuilder.Desk(go, V(0, 0, -d/6), 0f, m);
            PropBuilder.Chair(go, V(0, 0, -d/6 - 0.8f), 0f, m);
            PropBuilder.Chair(go, V(0, 0, d/6), 180f, m);

            PropBuilder.PLocalRandom(PropBuilder.Maps, t, V(-1f, 2f, -d/2 + 0.3f));
            PropBuilder.Shelf(go, V(w/3, 0, -d/2 + 0.5f), 0f, m);
            PropBuilder.Shelf(go, V(-w/3, 0, -d/2 + 0.5f), 0f, m);

            if (def.zone == Zone.Bank)
            {
                PropBuilder.PLocal("SM_Prop_Vault_01", t, V(w/3, 0, d/4), 180f);
                PropBuilder.PLocal("SM_Prop_CashRegister_01", t, V(0.5f, 0.85f, -d/6));
                PropBuilder.GoldBar(go, V(w/3 - 0.5f, 0.1f, d/4 - 0.5f), m);
                PropBuilder.GoldBar(go, V(w/3 - 0.3f, 0.1f, d/4 - 0.4f), m);
            }

            PropBuilder.BoxMat(go, "Rug", V(0, 0.12f, -d/6), V(3f, 0.01f, 2.5f), m.FabricGreen);
        }

        // ── SHOP ──

        static void FurnishShop(GameObject go, float w, float d, TownMaterials m)
        {
            var t = go.transform;

            PropBuilder.InteriorLantern(go, V(-w/2 + 0.4f, 2.2f, -d/6), true, m);
            PropBuilder.InteriorLantern(go, V(w/2 - 0.4f, 2.2f, -d/6), false, m);

            PropBuilder.Shelf(go, V(-w/2 + 0.6f, 0, 0), 90f, m);
            PropBuilder.Shelf(go, V(w/2 - 0.6f, 0, 0), -90f, m);

            // Counter
            PropBuilder.BoxMat(go, "Counter", V(0, 0.55f, d/4), V(w*0.5f, 1.1f, 0.4f), m.WoodDark);
            PropBuilder.BoxMat(go, "CounterTop", V(0, 1.12f, d/4), V(w*0.52f, 0.04f, 0.44f),
                m.MakeMat(new Color(0.3f, 0.18f, 0.08f), 0.4f));
            PropBuilder.PLocal("SM_Prop_CashRegister_01", t, V(0.3f, 1.15f, d/4));

            PropBuilder.PLocalRandom(PropBuilder.Baskets, t, V(-w/2 + 0.6f, 1.2f, -d/6));
            PropBuilder.PLocalRandom(PropBuilder.Sacks, t, V(w/2 - 0.6f, 0, d/6));
            PropBuilder.PLocalRandom(PropBuilder.Maps, t, V(0, 2f, -d/2 + 0.3f));
        }

        // ── JAIL ──

        static void FurnishJail(GameObject go, float w, float d, TownMaterials m)
        {
            var t = go.transform;

            PropBuilder.InteriorLantern(go, V(-w/2 + 0.4f, 2.2f, d/6), true, m);
            PropBuilder.InteriorLantern(go, V(w/2 - 0.4f, 2.2f, -d/4), false, m);

            PropBuilder.Desk(go, V(0, 0, -d/6), 0f, m);
            PropBuilder.Chair(go, V(0, 0, -d/6 - 0.8f), 0f, m);

            PropBuilder.PLocalRandom(PropBuilder.Maps, t, V(-1f, 2f, -d/2 + 0.3f));
            PropBuilder.PLocalRandom(PropBuilder.Maps, t, V(0.5f, 2f, -d/2 + 0.3f));

            // Jail cell bars
            float cellX = w / 4;
            for (int i = 0; i < 6; i++)
                PropBuilder.BoxMat(go, $"Bar{i}", V(cellX, 1.5f, -d/2 + 0.8f + i * 0.4f), V(0.05f, 3f, 0.05f), m.Metal);
            PropBuilder.BoxMat(go, "CellTop", V(cellX, 2.8f, -d/2 + 2.1f), V(0.05f, 0.05f, 2.2f), m.Metal);

            PropBuilder.JailBed(go, V(w/3 + 0.5f, 0, -d/2 + 1.5f), m);

            // Gun rack
            PropBuilder.Shelf(go, V(-w/3, 0, -d/2 + 0.4f), 0f, m);
            PropBuilder.GunRack(go, V(-w/3, 0, -d/2 + 0.5f), 0f, m);
        }

        // ── HOTEL ──

        static void FurnishHotel(GameObject go, Transform b, float w, float d, TownMaterials m)
        {
            var t = go.transform;

            PropBuilder.InteriorLantern(go, V(-w/2 + 0.4f, 2.2f, d/6), true, m);
            PropBuilder.InteriorLantern(go, V(w/2 - 0.4f, 2.2f, -d/6), false, m);

            // Front desk
            PropBuilder.BoxMat(go, "FrontDesk", V(0, 0.55f, d/4), V(3.5f, 1.1f, 0.5f), m.WoodDark);
            PropBuilder.BoxMat(go, "FDTop", V(0, 1.12f, d/4), V(3.6f, 0.05f, 0.55f),
                m.MakeMat(new Color(0.3f, 0.18f, 0.08f), 0.4f));

            PropBuilder.PLocalRandom(PropBuilder.Suitcases, t, V(-w/6, 0, d/4 + 0.6f));

            // Fireplace
            PropBuilder.PLocal("SM_Prop_Fireplace_01", t, V(-w/3, 0, -d/4));

            // Bed + dresser
            PropBuilder.Bed(go, V(w/4, 0, -d/4), 90f, m);
            PropBuilder.PLocal("SM_Prop_Dresser_01", t, V(w/3, 0, -d/3));

            // Stairs
            for (int i = 0; i < 5; i++)
                PropBuilder.BoxMat(go, $"Stair{i}", V(-w/3, 0.2f + i * 0.35f, -d/4 + i * 0.5f), V(2f, 0.15f, 0.5f), m.WoodMed);
        }

        // ── CHURCH ──

        static void FurnishChurch(GameObject go, float w, float d, TownMaterials m)
        {
            var t = go.transform;

            PropBuilder.InteriorLantern(go, V(-w/2 + 0.4f, 2.2f, d/6), true, m);
            PropBuilder.InteriorLantern(go, V(-w/2 + 0.4f, 2.2f, -d/4), true, m);
            PropBuilder.InteriorLantern(go, V(w/2 - 0.4f, 2.2f, d/6), false, m);
            PropBuilder.InteriorLantern(go, V(w/2 - 0.4f, 2.2f, -d/4), false, m);

            // Pews
            for (int i = 0; i < 5; i++)
            {
                float z = d/6 - i * 1.6f;
                PropBuilder.Pew(go, V(-w/4, 0, z), m);
                PropBuilder.Pew(go, V(w/4, 0, z), m);
            }

            // Altar
            PropBuilder.BoxMat(go, "Altar", V(0, 0.5f, -d/2 + 1.5f), V(2.2f, 1f, 0.9f),
                m.MakeMat(new Color(0.85f, 0.8f, 0.7f), 0.3f));
            PropBuilder.BoxMat(go, "AltarCloth", V(0, 1.02f, -d/2 + 1.5f), V(2.3f, 0.02f, 0.95f),
                m.MakeMat(new Color(0.9f, 0.9f, 0.85f)));

            PropBuilder.PLocal("SM_Prop_Church_Podium_01", t, V(0, 0, -d/2 + 2.5f));
            PropBuilder.PLocal("SM_Prop_Church_Cross_01", t, V(0, 3f, -d/2 + 0.3f));
        }

        // ── WAREHOUSE ──

        static void FurnishWarehouse(GameObject go, float w, float d, TownMaterials m)
        {
            var t = go.transform;

            PropBuilder.InteriorLantern(go, V(-w/2 + 0.4f, 2.2f, 0), true, m);
            PropBuilder.InteriorLantern(go, V(w/2 - 0.4f, 2.2f, 0), false, m);

            PropBuilder.StreetCrate(go.transform, V(-w/4, 0, -d/4), 1f, m);
            PropBuilder.StreetCrate(go.transform, V(-w/4, 1f, -d/4), 0.8f, m);
            PropBuilder.StreetCrate(go.transform, V(w/4, 0, d/4), 1.2f, m);
            PropBuilder.StreetCrate(go.transform, V(0, 0, 0), 1f, m);

            PropBuilder.PLocalRandom(PropBuilder.Barrels, t, V(-w/4, 0, d/4));
            PropBuilder.PLocalRandom(PropBuilder.Barrels, t, V(0, 0, d/3));
            PropBuilder.PLocalRandom(PropBuilder.Sacks, t, V(w/4, 0, -d/6));

            PropBuilder.Shelf(go, V(-w/2 + 0.6f, 0, 0), 90f, m);
            PropBuilder.PLocalRandom(PropBuilder.Tins, t, V(-w/2 + 0.6f, 1.5f, 0));

            PropBuilder.BoxMat(go, "Rug", V(0, 0.12f, 0), V(2.5f, 0.01f, 2f), m.FabricGreen);
        }

        // ── SCHOOL ──

        static void FurnishSchool(GameObject go, float w, float d, TownMaterials m)
        {
            var t = go.transform;

            PropBuilder.InteriorLantern(go, V(-w/2 + 0.4f, 2.2f, 0), true, m);
            PropBuilder.InteriorLantern(go, V(w/2 - 0.4f, 2.2f, 0), false, m);

            PropBuilder.Desk(go, V(0, 0, -d/3), 0f, m);
            PropBuilder.Chair(go, V(0, 0, -d/3 - 0.8f), 0f, m);

            // Chalkboard
            PropBuilder.BoxMat(go, "Chalkboard", V(0, 2f, -d/2 + 0.35f), V(3f, 1.5f, 0.06f),
                m.MakeMat(new Color(0.12f, 0.2f, 0.12f), 0.3f));
            PropBuilder.BoxMat(go, "ChalkFrame", V(0, 2f, -d/2 + 0.33f), V(3.15f, 1.65f, 0.03f), m.WoodDark);

            // Student desks
            for (int row = 0; row < 3; row++)
            {
                float rz = d/8 + row * 1.8f;
                PropBuilder.Table(go.transform, V(-w/4, 0, rz), m);
                PropBuilder.Chair(go, V(-w/4, 0, rz + 0.6f), 180f, m);
                PropBuilder.Table(go.transform, V(w/4, 0, rz), m);
                PropBuilder.Chair(go, V(w/4, 0, rz + 0.6f), 180f, m);
            }

            PropBuilder.Shelf(go, V(-w/2 + 0.6f, 0, -d/6), 90f, m);
        }

        // ── CLINIC ──

        static void FurnishClinic(GameObject go, float w, float d, TownMaterials m)
        {
            var t = go.transform;

            PropBuilder.InteriorLantern(go, V(-w/2 + 0.4f, 2.2f, -d/6), true, m);
            PropBuilder.InteriorLantern(go, V(w/2 - 0.4f, 2.2f, d/6), false, m);

            // Patient beds with curtain divider
            PropBuilder.Bed(go, V(w/4, 0, -d/6), 0f, m);
            PropBuilder.Bed(go, V(w/4, 0, -d/3), 0f, m);
            PropBuilder.PLocal("SM_Prop_Curtain_01", t, V(0, 0, -d/6), 90f);

            // Doctor desk area
            PropBuilder.Desk(go, V(-w/4, 0, d/6), 0f, m);
            PropBuilder.Chair(go, V(-w/4, 0, d/6 + 0.7f), 180f, m);

            // Medicine shelf with bottles
            PropBuilder.Shelf(go, V(-w/2 + 0.5f, 0, -d/3), 90f, m);
            for (int i = 0; i < 8; i++)
                PropBuilder.Bottle(go, V(-w/2 + 0.5f, 1.7f, -d/3 + i * 0.12f), m);

            PropBuilder.BoxMat(go, "Rug", V(-w/4, 0.12f, d/6), V(2.5f, 0.01f, 2f), m.FabricGreen);
        }

        // ── SMITHY ──

        static void FurnishSmithy(GameObject go, float w, float d, TownMaterials m)
        {
            var t = go.transform;

            PropBuilder.InteriorLantern(go, V(w/2 - 0.4f, 2.2f, -d/6), false, m);
            PropBuilder.InteriorLantern(go, V(w/2 - 0.4f, 2.2f, d/6), false, m);

            // Forge
            Material forgeMat = m.MakeMat(new Color(0.8f, 0.3f, 0.1f), 0.2f, 0f, new Color(1f, 0.4f, 0.1f) * 1.5f);
            PropBuilder.BoxMat(go, "ForgeBase",  V(-w/4, 0.5f, -d/3),      V(1.5f, 1f, 1.2f), m.Stone);
            PropBuilder.BoxMat(go, "ForgeCoals", V(-w/4, 1.05f, -d/3),     V(1f, 0.1f, 0.8f), forgeMat);
            PropBuilder.BoxMat(go, "ForgeHood",  V(-w/4, 2.5f, -d/3-0.5f), V(1.6f, 1.5f, 0.15f), m.Metal);
            PropBuilder.AddPointLight(go.transform, V(-w/4, 1.5f, -d/3), new Color(1f, 0.5f, 0.15f), 1.5f, 5f);

            // Anvil
            PropBuilder.BoxMat(go, "AnvilBase", V(0, 0.4f, -d/6),      V(0.4f, 0.8f, 0.3f), m.Metal);
            PropBuilder.BoxMat(go, "AnvilTop",  V(0, 0.85f, -d/6),     V(0.6f, 0.1f, 0.3f), m.Metal);
            PropBuilder.BoxMat(go, "AnvilHorn", V(0.35f, 0.85f, -d/6), V(0.2f, 0.08f, 0.15f), m.Metal);

            // Tool rack
            PropBuilder.Shelf(go, V(w/3, 0, -d/2 + 0.4f), 0f, m);

            // Quench barrels
            PropBuilder.PLocalRandom(PropBuilder.Barrels, t, V(0, 0, d/4));
            PropBuilder.PLocalRandom(PropBuilder.Barrels, t, V(0.8f, 0, d/4));
        }

        // ── LIBRARY ──

        static void FurnishLibrary(GameObject go, float w, float d, TownMaterials m)
        {
            var t = go.transform;

            PropBuilder.InteriorLantern(go, V(-w/2 + 0.4f, 2.2f, d/6), true, m);
            PropBuilder.InteriorLantern(go, V(w/2 - 0.4f, 2.2f, d/6), false, m);

            // Bookshelves on three walls
            PropBuilder.Shelf(go, V(-w/2 + 0.6f, 0, 0), 90f, m);
            PropBuilder.Shelf(go, V(w/2 - 0.6f, 0, 0), -90f, m);
            PropBuilder.Shelf(go, V(0, 0, -d/2 + 0.6f), 0f, m);

            // Reading tables with chairs
            PropBuilder.Table(go.transform, V(-w/6, 0, d/6), m);
            PropBuilder.Table(go.transform, V(w/6, 0, d/6), m);
            PropBuilder.Chair(go, V(-w/6, 0, d/6 + 0.7f), 180f, m);
            PropBuilder.Chair(go, V(w/6, 0, d/6 + 0.7f), 180f, m);
            PropBuilder.Chair(go, V(-w/6, 0, d/6 - 0.7f), 0f, m);
            PropBuilder.Chair(go, V(w/6, 0, d/6 - 0.7f), 0f, m);

            PropBuilder.PLocalRandom(PropBuilder.Maps, t, V(-0.5f, 2f, -d/2 + 0.3f));
            PropBuilder.PLocalRandom(PropBuilder.Maps, t, V(0.5f, 2f, -d/2 + 0.3f));

            PropBuilder.BoxMat(go, "Rug", V(0, 0.12f, d/6), V(w*0.5f, 0.01f, d*0.3f), m.FabricGreen);
        }
    }
}

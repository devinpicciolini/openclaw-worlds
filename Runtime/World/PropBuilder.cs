using UnityEngine;
using OpenClawWorlds;

namespace OpenClawWorlds.World
{
    /// <summary>
    /// Spawns props from prefabs or fallback geometry.
    /// Each prop type maps to a prefab name via PrefabLibrary.
    /// Falls back to primitive geometry when prefabs aren't available.
    /// Includes interior furniture helpers used by InteriorBuilder.
    /// </summary>
    public static class PropBuilder
    {
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        // ── Prefab Name Arrays (shared across PropBuilder & InteriorBuilder) ──

        public static readonly string[] Bottles = {
            "SM_Prop_Bottle_01", "SM_Prop_Bottle_02", "SM_Prop_Bottle_03",
            "SM_Prop_Bottle_04", "SM_Prop_Bottle_05", "SM_Prop_Bottle_06", "SM_Prop_Bottle_07"
        };
        public static readonly string[] BrokenBottles = {
            "SM_Prop_Bottle_Broken_01", "SM_Prop_Bottle_Broken_02",
            "SM_Prop_Bottle_Broken_03", "SM_Prop_Bottle_Broken_04"
        };
        public static readonly string[] BottleCandles = { "SM_Prop_Bottle_Candle_01", "SM_Prop_Bottle_Candle_02" };
        public static readonly string[] PokerChips = { "SM_Prop_Poker_Chip_01", "SM_Prop_Poker_Chip_02", "SM_Prop_Poker_Chip_03" };
        public static readonly string[] Cards = { "SM_Prop_Card_01", "SM_Prop_Card_02", "SM_Prop_Card_03" };
        public static readonly string[] Sacks = { "SM_Prop_Sack_01", "SM_Prop_Sack_02", "SM_Prop_Sack_03", "SM_Prop_Sack_04" };
        public static readonly string[] Baskets = { "SM_Prop_Basket_01", "SM_Prop_Basket_02", "SM_Prop_Basket_03" };
        public static readonly string[] Ropes = { "SM_Prop_Rope_01", "SM_Prop_Rope_02", "SM_Prop_Rope_03" };
        public static readonly string[] Tins = { "SM_Prop_Tin_01", "SM_Prop_Tin_02", "SM_Prop_Tin_03" };
        public static readonly string[] FurRolls = { "SM_Prop_Fur_Roll_01", "SM_Prop_Fur_Roll_02", "SM_Prop_Fur_Roll_03" };
        public static readonly string[] Cups = { "SM_Prop_Cup_01", "SM_Prop_Cup_02" };
        public static readonly string[] Maps = { "SM_Prop_Map_01", "SM_Prop_Map_02" };
        public static readonly string[] Barrels = { "SM_Prop_Barrel_01", "SM_Prop_Barrel_02" };
        public static readonly string[] Suitcases = { "SM_Prop_Suitcase_01", "SM_Prop_Suitcase_02" };
        public static readonly string[] Revolvers = { "SM_Wep_Revolver_01", "SM_Wep_Revolver_02" };
        public static readonly string[] Shotguns = { "SM_Wep_Shotgun_01", "SM_Wep_Shotgun_02" };

        // ── Primitive Helpers ──

        /// <summary>Apply a material to a GameObject's renderer.</summary>
        public static void ApplyMat(GameObject go, Material mat)
        {
            var r = go.GetComponent<Renderer>();
            if (r != null) r.material = mat;
        }

        /// <summary>Create a named cube with material at local position inside a parent.</summary>
        public static void BoxMat(GameObject parent, string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            BoxMat(parent, name, localPos, scale, mat, Quaternion.identity);
        }

        /// <summary>Create a named cube with material and rotation at local position.</summary>
        public static void BoxMat(GameObject parent, string name, Vector3 localPos, Vector3 scale, Material mat, Quaternion localRot)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent.transform);
            cube.transform.localPosition = localPos;
            cube.transform.localScale = scale;
            cube.transform.localRotation = localRot;
            ApplyMat(cube, mat);
        }

        /// <summary>Add a point light at a local position under a parent.</summary>
        public static void AddPointLight(Transform parent, Vector3 localPos, Color color, float intensity, float range, bool shadows = false)
        {
            var lt = new GameObject("Light");
            lt.transform.SetParent(parent);
            lt.transform.localPosition = localPos;
            var pl = lt.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.color = color;
            pl.intensity = intensity;
            pl.range = Mathf.Min(range, 12f);
            pl.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        }

        // ── Prefab Spawn Helpers ──

        /// <summary>Spawn prefab at local pos inside a parent object.</summary>
        public static GameObject PLocal(string prefab, Transform parent, Vector3 localPos, float yaw = 0f, float scale = 1f)
        {
            return PrefabLibrary.Spawn(prefab, parent, localPos, Quaternion.Euler(0, yaw, 0), V(scale, scale, scale));
        }

        /// <summary>Spawn one of several random prefabs at local pos.</summary>
        public static GameObject PLocalRandom(string[] prefabs, Transform parent, Vector3 localPos, float yaw = 0f, float scale = 1f)
        {
            return PLocal(prefabs[Random.Range(0, prefabs.Length)], parent, localPos, yaw, scale);
        }

        // ── Interior Furniture Helpers ──

        public static void SaloonTable(Transform parent, Vector3 pos, TownMaterials m)
        { PLocal("SM_Prop_PokerTable_01", parent, pos); }

        public static void Chair(GameObject parent, Vector3 pos, float yaw, TownMaterials m)
        { PLocal("SM_Prop_Chair_01", parent.transform, pos, yaw); }

        public static void Stool(GameObject parent, Vector3 pos, float yaw, TownMaterials m)
        { PLocal("SM_Prop_Stool_01", parent.transform, pos, yaw); }

        public static void Pew(GameObject parent, Vector3 pos, TownMaterials m)
        { PLocal("SM_Prop_Church_Pew_01", parent.transform, pos); }

        public static void Desk(GameObject parent, Vector3 pos, float yaw, TownMaterials m)
        { PLocal("SM_Prop_Desk_01", parent.transform, pos, yaw); }

        public static void Shelf(GameObject parent, Vector3 pos, float yaw, TownMaterials m)
        { PLocal("SM_Prop_Shelf_01", parent.transform, pos, yaw); }

        public static void Bottle(GameObject parent, Vector3 pos, TownMaterials m)
        { PLocalRandom(Bottles, parent.transform, pos); }

        public static void Bed(GameObject parent, Vector3 pos, float yaw, TownMaterials m)
        { PLocal("SM_Prop_Bed_01", parent.transform, pos, yaw); }

        public static void JailBed(GameObject parent, Vector3 pos, TownMaterials m)
        { PLocal("SM_Prop_JailBed_01", parent.transform, pos); }

        public static void Table(Transform parent, Vector3 pos, TownMaterials m)
        { PLocal("SM_Prop_Table_01", parent, pos); }

        public static void InteriorLantern(GameObject bld, Vector3 pos, bool leftWall, TownMaterials m)
        {
            PLocal("SM_Prop_Lantern_01", bld.transform, pos);
            AddPointLight(bld.transform, pos, new Color(1f, 0.82f, 0.5f), 0.6f, 3f, false);
        }

        public static void GunRack(GameObject parent, Vector3 pos, float yaw, TownMaterials m)
        {
            PLocalRandom(Revolvers, parent.transform, pos + V(-0.3f, 1.8f, 0), yaw);
            PLocalRandom(Shotguns, parent.transform, pos + V(0.3f, 1.8f, 0), yaw);
            PLocal("SM_Wep_Rifle_01", parent.transform, pos + V(0f, 1.5f, 0), yaw);
        }

        public static void GoldBar(GameObject g, Vector3 p, TownMaterials m)
        { PLocal("SM_Prop_GoldBar_01", g.transform, p); }

        public static void BarrelIndoor(GameObject g, Vector3 p, TownMaterials m)
        { PLocalRandom(Barrels, g.transform, p); }

        public static void StreetCrate(Transform root, Vector3 pos, float size, TownMaterials m)
        {
            var go = PrefabLibrary.Spawn("SM_Prop_Crate_01", root, pos);
            if (go != null) { go.transform.localScale = Vector3.one * size; return; }
            Crate(root, pos, size, m);
        }

        /// <summary>
        /// Master prop spawner — routes prop type string to the correct builder method.
        /// Returns true if the prop was spawned successfully.
        /// </summary>
        public static bool SpawnProp(Transform root, string type, Vector3 pos, float yaw, float height, float scale, TownMaterials mat)
        {
            switch (type)
            {
                // Simple props
                case "StreetLamp":    StreetLamp(root, pos, mat); break;
                case "Barrel":        Barrel(root, pos, mat); break;
                case "HitchingPost":  HitchingPost(root, pos, mat); break;
                case "WaterTrough":   WaterTrough(root, pos, mat); break;
                case "NoticeBoard":   NoticeBoard(root, pos, mat); break;
                case "Fountain":      Fountain(root, pos, mat); break;
                case "Flagpole":      Flagpole(root, pos, mat); break;
                case "HayBale":       HayBale(root, pos, mat); break;
                case "WoodPile":      WoodPile(root, pos, mat); break;
                case "CampFire":      CampFire(root, pos, mat); break;
                case "WaterTower":    WaterTower(root, pos, mat); break;

                // Props with yaw
                case "Bench":         Bench(root, pos, yaw, mat); break;
                case "Horse":         Horse(root, pos, yaw, mat); break;
                case "Cart":          Cart(root, pos, yaw, mat); break;

                // Trees (use height)
                case "PineTree":      PineTree(root, pos, height, mat); break;
                case "OakTree":       OakTree(root, pos, height, mat); break;

                // Props with scale
                case "Rock":          Rock(root, pos, scale, mat); break;
                case "Crate":         Crate(root, pos, scale, mat); break;

                default:
                    // Try to spawn via PrefabLibrary directly
                    var go = PrefabLibrary.Spawn(type, root, pos);
                    if (go != null) return true;
                    Debug.LogWarning($"[PropBuilder] Unknown prop type '{type}'");
                    return false;
            }

            return true;
        }

        // ── Simple Props ──

        public static void StreetLamp(Transform root, Vector3 pos, TownMaterials mat)
        {
            var go = PrefabLibrary.Spawn("SM_Env_Lantern_Lamp_01", root, pos);
            if (go != null) return;

            // Fallback: pole with glowing top
            var lamp = new GameObject("StreetLamp");
            lamp.transform.SetParent(root);
            lamp.transform.position = pos;

            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.transform.SetParent(lamp.transform);
            pole.transform.localPosition = V(0, 2f, 0);
            pole.transform.localScale = V(0.1f, 2f, 0.1f);
            ApplyMat(pole, mat.Metal);

            var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulb.transform.SetParent(lamp.transform);
            bulb.transform.localPosition = V(0, 4.2f, 0);
            bulb.transform.localScale = V(0.4f, 0.4f, 0.4f);
            ApplyMat(bulb, mat.LanternGlow);

            var light = bulb.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.85f, 0.5f);
            light.intensity = 1.5f;
            light.range = 12f;
            light.shadows = LightShadows.None;
        }

        public static void Barrel(Transform root, Vector3 pos, TownMaterials mat)
        {
            var go = PrefabLibrary.Spawn("SM_Env_Barrel_01", root, pos);
            if (go != null) return;

            var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            barrel.name = "Barrel";
            barrel.transform.SetParent(root);
            barrel.transform.position = pos + V(0, 0.5f, 0);
            barrel.transform.localScale = V(0.5f, 0.5f, 0.5f);
            ApplyMat(barrel, mat.WoodDark);
        }

        public static void HitchingPost(Transform root, Vector3 pos, TownMaterials mat)
        {
            var go = PrefabLibrary.Spawn("SM_Env_HitchingPost_01", root, pos);
            if (go != null) return;

            var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = "HitchingPost";
            post.transform.SetParent(root);
            post.transform.position = pos + V(0, 0.5f, 0);
            post.transform.localScale = V(2f, 0.1f, 0.1f);
            ApplyMat(post, mat.WoodMed);
        }

        public static void WaterTrough(Transform root, Vector3 pos, TownMaterials mat)
        {
            var go = PrefabLibrary.Spawn("SM_Env_WaterTrough_01", root, pos);
            if (go != null) return;

            var trough = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trough.name = "WaterTrough";
            trough.transform.SetParent(root);
            trough.transform.position = pos + V(0, 0.3f, 0);
            trough.transform.localScale = V(1.5f, 0.5f, 0.6f);
            ApplyMat(trough, mat.WoodMed);
        }

        public static void NoticeBoard(Transform root, Vector3 pos, TownMaterials mat)
        {
            var go = PrefabLibrary.Spawn("SM_Env_NoticeBoard_01", root, pos);
            if (go != null) return;

            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "NoticeBoard";
            board.transform.SetParent(root);
            board.transform.position = pos + V(0, 1.2f, 0);
            board.transform.localScale = V(1.2f, 1.5f, 0.1f);
            ApplyMat(board, mat.WoodLight);
        }

        public static void Fountain(Transform root, Vector3 pos, TownMaterials mat)
        {
            var go = PrefabLibrary.Spawn("SM_Env_Fountain_01", root, pos);
            if (go != null) return;

            var fountain = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            fountain.name = "Fountain";
            fountain.transform.SetParent(root);
            fountain.transform.position = pos + V(0, 0.4f, 0);
            fountain.transform.localScale = V(1.5f, 0.4f, 1.5f);
            ApplyMat(fountain, mat.Stone);
        }

        public static void Flagpole(Transform root, Vector3 pos, TownMaterials mat)
        {
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Flagpole";
            pole.transform.SetParent(root);
            pole.transform.position = pos + V(0, 4f, 0);
            pole.transform.localScale = V(0.05f, 4f, 0.05f);
            ApplyMat(pole, mat.Metal);
        }

        public static void HayBale(Transform root, Vector3 pos, TownMaterials mat)
        {
            var go = PrefabLibrary.Spawn("SM_Env_HayBale_01", root, pos);
            if (go != null) return;

            var bale = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bale.name = "HayBale";
            bale.transform.SetParent(root);
            bale.transform.position = pos + V(0, 0.4f, 0);
            bale.transform.localScale = V(1.2f, 0.8f, 0.8f);
            ApplyMat(bale, mat.MakeMat(new Color(0.7f, 0.6f, 0.2f)));
        }

        public static void WoodPile(Transform root, Vector3 pos, TownMaterials mat)
        {
            var pile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pile.name = "WoodPile";
            pile.transform.SetParent(root);
            pile.transform.position = pos + V(0, 0.4f, 0);
            pile.transform.localScale = V(1.5f, 0.8f, 1f);
            ApplyMat(pile, mat.WoodDark);
        }

        public static void CampFire(Transform root, Vector3 pos, TownMaterials mat)
        {
            var fire = new GameObject("CampFire");
            fire.transform.SetParent(root);
            fire.transform.position = pos;

            var light = fire.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.6f, 0.2f);
            light.intensity = 2f;
            light.range = 8f;
        }

        public static void WaterTower(Transform root, Vector3 pos, TownMaterials mat)
        {
            var go = PrefabLibrary.Spawn("SM_Env_WaterTower_01", root, pos);
            if (go != null) return;

            var tower = new GameObject("WaterTower");
            tower.transform.SetParent(root);
            tower.transform.position = pos;

            var tank = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tank.transform.SetParent(tower.transform);
            tank.transform.localPosition = V(0, 5f, 0);
            tank.transform.localScale = V(1.5f, 1.5f, 1.5f);
            ApplyMat(tank, mat.WoodMed);
        }

        // ── Props with Yaw ──

        public static void Bench(Transform root, Vector3 pos, float yaw, TownMaterials mat)
        {
            var go = PrefabLibrary.Spawn("SM_Env_Bench_01", root, pos);
            if (go != null) { go.transform.rotation = Quaternion.Euler(0, yaw, 0); return; }

            var bench = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bench.name = "Bench";
            bench.transform.SetParent(root);
            bench.transform.position = pos + V(0, 0.25f, 0);
            bench.transform.rotation = Quaternion.Euler(0, yaw, 0);
            bench.transform.localScale = V(1.5f, 0.5f, 0.5f);
            ApplyMat(bench, mat.WoodLight);
        }

        public static void Horse(Transform root, Vector3 pos, float yaw, TownMaterials mat)
        {
            var go = PrefabLibrary.Spawn("SM_Env_Horse_01", root, pos);
            if (go != null) { go.transform.rotation = Quaternion.Euler(0, yaw, 0); return; }

            // Fallback: capsule placeholder
            var horse = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            horse.name = "Horse";
            horse.transform.SetParent(root);
            horse.transform.position = pos + V(0, 0.8f, 0);
            horse.transform.rotation = Quaternion.Euler(0, yaw, 0);
            horse.transform.localScale = V(0.5f, 0.8f, 1f);
            ApplyMat(horse, mat.MakeMat(new Color(0.45f, 0.3f, 0.15f)));
        }

        public static void Cart(Transform root, Vector3 pos, float yaw, TownMaterials mat)
        {
            var go = PrefabLibrary.Spawn("SM_Veh_Cart_01", root, pos);
            if (go != null) { go.transform.rotation = Quaternion.Euler(0, yaw, 0); return; }

            var cart = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cart.name = "Cart";
            cart.transform.SetParent(root);
            cart.transform.position = pos + V(0, 0.5f, 0);
            cart.transform.rotation = Quaternion.Euler(0, yaw, 0);
            cart.transform.localScale = V(1.5f, 0.8f, 2.5f);
            ApplyMat(cart, mat.WoodMed);
        }

        // ── Trees ──

        public static void PineTree(Transform root, Vector3 pos, float height, TownMaterials mat)
        {
            var go = PrefabLibrary.Spawn("SM_Env_Tree_Pine_01", root, pos);
            if (go != null) return;

            var tree = new GameObject("PineTree");
            tree.transform.SetParent(root);
            tree.transform.position = pos;

            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.transform.SetParent(tree.transform);
            trunk.transform.localPosition = V(0, height * 0.3f, 0);
            trunk.transform.localScale = V(0.3f, height * 0.3f, 0.3f);
            ApplyMat(trunk, mat.WoodDark);

            var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            canopy.transform.SetParent(tree.transform);
            canopy.transform.localPosition = V(0, height * 0.7f, 0);
            canopy.transform.localScale = V(height * 0.4f, height * 0.5f, height * 0.4f);
            ApplyMat(canopy, mat.FabricGreen);
        }

        public static void OakTree(Transform root, Vector3 pos, float height, TownMaterials mat)
        {
            var go = PrefabLibrary.Spawn("SM_Env_Tree_Oak_01", root, pos);
            if (go != null) return;

            PineTree(root, pos, height, mat); // reuse pine fallback
        }

        // ── Scaled Props ──

        public static void Rock(Transform root, Vector3 pos, float scale, TownMaterials mat)
        {
            var go = PrefabLibrary.Spawn("SM_Env_Rock_01", root, pos);
            if (go != null) { go.transform.localScale = Vector3.one * scale; return; }

            var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Rock";
            rock.transform.SetParent(root);
            rock.transform.position = pos + V(0, scale * 0.4f, 0);
            rock.transform.localScale = Vector3.one * scale * 0.8f;
            ApplyMat(rock, mat.Stone);
        }

        public static void Crate(Transform root, Vector3 pos, float scale, TownMaterials mat)
        {
            var go = PrefabLibrary.Spawn("SM_Env_Crate_01", root, pos);
            if (go != null) { go.transform.localScale = Vector3.one * scale; return; }

            var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = "Crate";
            crate.transform.SetParent(root);
            crate.transform.position = pos + V(0, scale * 0.4f, 0);
            crate.transform.localScale = Vector3.one * scale * 0.8f;
            ApplyMat(crate, mat.WoodLight);
        }

        // ── Bounty Board (special: has gameplay trigger) ──

        public static void BountyBoard(Transform root, Vector3 pos, float streetWidth, TownMaterials mat)
        {
            NoticeBoard(root, pos, mat); // reuse notice board geometry
        }
    }
}

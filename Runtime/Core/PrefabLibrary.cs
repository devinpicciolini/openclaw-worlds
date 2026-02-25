using UnityEngine;
using System.Collections.Generic;

namespace OpenClawWorlds
{
    /// <summary>
    /// Central prefab loading and spawning. Caches Resources.Load results.
    /// Configure SearchPaths for your asset pack's folder structure.
    /// </summary>
    public static class PrefabLibrary
    {
        static readonly Dictionary<string, GameObject> Cache = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, bool> PackCache = new Dictionary<string, bool>();

        /// <summary>
        /// Search paths tried in order when a short prefab name is used.
        /// Override these for your asset pack's folder structure.
        /// Example for POLYGON Western: { "Western/Props/", "Western/Buildings/", "" }
        /// </summary>
        public static string[] SearchPaths = { "" };

        // ─── Material Fix ──────────────────────────────────────────────

        static Material _primaryMat;
        static Material _secondaryMat;

        /// <summary>
        /// Primary texture atlas name (loaded from Resources).
        /// Set this to your asset pack's main texture atlas.
        /// Example for POLYGON Western: "PolygonWestern_Texture_01_A"
        /// </summary>
        public static string PrimaryTextureName = "";
        /// <summary>
        /// Secondary/fallback texture atlas name.
        /// Example for POLYGON Starter: "PolygonStarter_Texture_01"
        /// </summary>
        public static string SecondaryTextureName = "";

        static Material MakeRuntimeMat(Texture2D tex, string name)
        {
            var shader = TownMaterials.LitShader;
            if (shader == null) return null;
            var mat = new Material(shader);
            mat.name = name;
            mat.SetTexture("_BaseMap", tex);
            mat.SetTexture("_MainTex", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetColor("_Color", Color.white);
            mat.SetFloat("_Smoothness", 0.3f);
            mat.SetFloat("_Metallic", 0f);
            return mat;
        }

        static Material GetPrimaryMat()
        {
            if (_primaryMat != null) return _primaryMat;
            var tex = Resources.Load<Texture2D>(PrimaryTextureName);
            if (tex == null) return null;
            _primaryMat = MakeRuntimeMat(tex, "Primary_Runtime");
            return _primaryMat;
        }

        static Material GetSecondaryMat()
        {
            if (_secondaryMat != null) return _secondaryMat;
            var tex = Resources.Load<Texture2D>(SecondaryTextureName);
            if (tex == null) return null;
            _secondaryMat = MakeRuntimeMat(tex, "Secondary_Runtime");
            return _secondaryMat;
        }

        /// <summary>
        /// Fix materials on an instantiated prefab by applying the correct texture atlas.
        /// Override ShouldSkipMaterialFix for custom exclusion logic.
        /// </summary>
        public static void FixMaterials(GameObject go, string prefabName = null)
        {
            if (go == null) return;
            if (prefabName != null && ShouldSkipMaterialFix(prefabName)) return;

            bool usePrimary = true;
            if (prefabName != null)
            {
                if (PackCache.TryGetValue(prefabName, out var p))
                    usePrimary = p;
                else
                    usePrimary = !prefabName.StartsWith("SM_Generic_") &&
                                 !prefabName.StartsWith("SM_PolygonPrototype");
            }

            Material mat = usePrimary ? GetPrimaryMat() : GetSecondaryMat();
            if (mat == null) mat = usePrimary ? GetSecondaryMat() : GetPrimaryMat();
            if (mat == null) return;

            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.materials = mats;
            }
        }

        /// <summary>Override to exclude specific prefab categories from material fix.</summary>
        public static System.Func<string, bool> CustomSkipCheck;

        static bool ShouldSkipMaterialFix(string name)
        {
            if (CustomSkipCheck != null && CustomSkipCheck(name)) return true;
            if (name.StartsWith("FX_")) return true;
            if (name == "SkyDome" || name.StartsWith("SM_SimpleSky")) return true;
            if (name.StartsWith("SM_Env_BackgroundCard")) return true;
            if (name.StartsWith("SM_Env_Cloud")) return true;
            if (name.StartsWith("SM_Generic_Cloud")) return true;
            if (name.StartsWith("SM_Env_Sand_Ground")) return true;
            if (name.StartsWith("SM_Env_DustPile")) return true;
            return false;
        }

        // ─── Loading ───────────────────────────────────────────────────

        public static GameObject Load(string path)
        {
            if (Cache.TryGetValue(path, out var cached)) return cached;
            var prefab = Resources.Load<GameObject>(path);
            Cache[path] = prefab;
            return prefab;
        }

        public static GameObject Find(string name)
        {
            if (Cache.TryGetValue(name, out var cached)) return cached;
            foreach (var prefix in SearchPaths)
            {
                var prefab = Resources.Load<GameObject>(prefix + name);
                if (prefab != null)
                {
                    Cache[name] = prefab;
                    PackCache[name] = SearchPaths.Length > 1 && System.Array.IndexOf(SearchPaths, prefix) == 0;
                    return prefab;
                }
            }
            Cache[name] = null;
            return null;
        }

        // ─── Spawning ──────────────────────────────────────────────────

        public static GameObject Spawn(string name, Transform parent, Vector3 localPos)
            => Spawn(name, parent, localPos, Quaternion.identity, Vector3.one);

        public static GameObject Spawn(string name, Transform parent, Vector3 localPos, Quaternion localRot)
            => Spawn(name, parent, localPos, localRot, Vector3.one);

        public static GameObject Spawn(string name, Transform parent, Vector3 localPos, Quaternion localRot, Vector3 scale)
        {
            var prefab = Find(name);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity, parent);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = scale;
            FixMaterials(go, name);
            return go;
        }

        public static GameObject SpawnWorld(string name, Transform parent, Vector3 worldPos)
            => SpawnWorld(name, parent, worldPos, Quaternion.identity, Vector3.one);

        public static GameObject SpawnWorld(string name, Transform parent, Vector3 worldPos, Quaternion rot)
            => SpawnWorld(name, parent, worldPos, rot, Vector3.one);

        public static GameObject SpawnWorld(string name, Transform parent, Vector3 worldPos, Quaternion rot, Vector3 scale)
        {
            var prefab = Find(name);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, worldPos, rot, parent);
            go.transform.localScale = scale;
            FixMaterials(go, name);
            return go;
        }

        public static bool TrySpawn(string name, Transform parent, Vector3 localPos, out GameObject result)
        {
            result = Spawn(name, parent, localPos);
            return result != null;
        }

        /// <summary>Clear the prefab cache (useful when changing asset packs at runtime).</summary>
        public static void ClearCache()
        {
            Cache.Clear();
            PackCache.Clear();
            _primaryMat = null;
            _secondaryMat = null;
        }
    }
}

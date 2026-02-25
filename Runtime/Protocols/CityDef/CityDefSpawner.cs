using UnityEngine;
using System;
using OpenClawWorlds;
using OpenClawWorlds.World;

namespace OpenClawWorlds.Protocols
{
    /// <summary>
    /// Instantiates a CityDef into the Unity scene.
    /// Bridges parsed CityDef JSON to the builder pipeline:
    /// CityDefParser → CityDefSpawner → BuildingBuilder, PropBuilder, NPCBuilder.
    /// </summary>
    public static class CityDefSpawner
    {
        /// <summary>Fired when a town is built from a response. Args: (summary, rawJson).</summary>
        public static event Action<string, string> OnCityBuilt;

        /// <summary>
        /// Optional: delegate to check if a world position is in a forbidden zone.
        /// Return true to skip placement at that position.
        /// If null, no zone checking is performed (suitable for flat/simple worlds).
        /// </summary>
        public static Func<Vector3, bool> IsForbiddenZone { get; set; }

        /// <summary>
        /// Optional: delegate to nudge an origin out of a forbidden zone.
        /// If null, origins are used as-is.
        /// </summary>
        public static Func<Vector3, Vector3> NudgeOrigin { get; set; }

        /// <summary>Max distance from world origin — safety net for absurd LLM coordinates.</summary>
        public static float MaxWorldRadius = 800f;

        /// <summary>
        /// Check an AI response for ```citydef code blocks.
        /// If found, parse and build each one. Returns summary or null.
        /// </summary>
        public static string ProcessResponse(string response, Vector3 spawnOrigin)
        {
            var blocks = ExtractCityDefBlocks(response);
            if (blocks == null || blocks.Length == 0)
                return null;

            string summary = "";
            foreach (var json in blocks)
            {
                string result = null;
                try
                {
                    result = Build(json, spawnOrigin, out Vector3 townPos);
                    summary += (summary.Length > 0 ? "\n" : "") + result;
                    spawnOrigin = townPos + new Vector3(100f, 0, 0); // offset next town
                }
                catch (Exception e)
                {
                    Debug.LogError($"[CityDef] Failed to build from response: {e.Message}");
                    summary += $"\n[CityDef failed: {e.Message}]";
                }

                // Fire event outside try/catch so subscriber errors don't get blamed on the builder
                if (result != null)
                {
                    try { OnCityBuilt?.Invoke(result, json); }
                    catch (Exception e) { Debug.LogError($"[CityDef] OnCityBuilt subscriber error: {e.Message}"); }
                }
            }

            return summary;
        }

        static string[] ExtractCityDefBlocks(string response)
        {
            var results = new System.Collections.Generic.List<string>();
            int searchFrom = 0;

            while (searchFrom < response.Length)
            {
                // Look for ```citydef or ```json blocks that contain CityDef
                int fenceStart = response.IndexOf("```citydef", searchFrom, StringComparison.OrdinalIgnoreCase);
                if (fenceStart < 0)
                    fenceStart = response.IndexOf("```json", searchFrom, StringComparison.OrdinalIgnoreCase);
                if (fenceStart < 0) break;

                int codeStart = response.IndexOf('\n', fenceStart);
                if (codeStart < 0) break;
                codeStart++;

                int fenceEnd = response.IndexOf("```", codeStart);
                if (fenceEnd < 0) break;

                string json = response.Substring(codeStart, fenceEnd - codeStart).Trim();

                // Only treat as CityDef if it looks like one (has streets or buildings)
                if (json.Length > 20 &&
                    (json.Contains("\"streets\"") || json.Contains("\"buildings\"")) &&
                    json.Contains("\"name\""))
                {
                    results.Add(json);
                }

                searchFrom = fenceEnd + 3;
            }

            return results.Count > 0 ? results.ToArray() : null;
        }

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        /// <summary>Returns true if a world position is outside the playable area boundary.</summary>
        public static bool IsOutOfBounds(Vector3 worldPos)
        {
            return Mathf.Abs(worldPos.x) > MaxWorldRadius || Mathf.Abs(worldPos.z) > MaxWorldRadius;
        }

        /// <summary>Check all forbidden zones (custom + out of bounds).</summary>
        static bool CheckForbidden(Vector3 pos)
        {
            if (IsOutOfBounds(pos)) return true;
            return IsForbiddenZone != null && IsForbiddenZone(pos);
        }

        /// <summary>
        /// Build a town from CityDef JSON. Returns summary string.
        /// townPosition receives the final world origin used.
        /// </summary>
        public static string Build(string json, Vector3 worldOrigin, out Vector3 townPosition)
        {
            var city = CityDefParser.Parse(json);
            string sanitizedJson = CityDefParser.SanitizeJson(json);

            // Use worldX/Z from JSON for absolute positioning, else use passed origin
            if (city.worldX != 0 || city.worldZ != 0)
                worldOrigin = new Vector3(city.worldX, 0, city.worldZ);

            // Nudge if in forbidden zone
            if (NudgeOrigin != null && CheckForbidden(worldOrigin))
                worldOrigin = NudgeOrigin(worldOrigin);

            // Handle existing towns with same name
            string rootName = $"City_{city.name}";
            var existing = GameObject.Find(rootName);
            if (existing != null)
            {
                if (city.edit)
                {
                    Debug.Log($"[CityDef] Edit mode — replacing '{city.name}' at {existing.transform.position}");
                    worldOrigin = existing.transform.position;
                }
                else
                {
                    Debug.Log($"[CityDef] Duplicate prevention — removing old '{city.name}'");
                }
                UnityEngine.Object.Destroy(existing);
            }
            CityDefPersistence.DeleteSavedCity(city.name);

            townPosition = worldOrigin;

            var root = new GameObject($"City_{city.name}").transform;
            root.position = worldOrigin;
            var mat = new TownMaterials();

            BuildGround(root, city, worldOrigin, mat);

            if (city.streets != null)
                foreach (var street in city.streets)
                    BuildStreet(root, street, worldOrigin, mat);

            int bc = 0;
            if (city.buildings != null)
            {
                int max = Mathf.Min(city.buildings.Length, CityDefParser.MaxBuildings);
                for (int i = 0; i < max; i++)
                    if (PlaceBuilding(root, city.buildings[i], city.streets, worldOrigin, mat))
                        bc++;
            }

            int pc = 0;
            if (city.props != null)
            {
                int max = Mathf.Min(city.props.Length, CityDefParser.MaxProps);
                for (int i = 0; i < max; i++)
                    if (PlaceProp(root, city.props[i], worldOrigin, mat))
                        pc++;
            }

            int nc = 0;
            if (city.npcs != null)
            {
                int max = Mathf.Min(city.npcs.Length, CityDefParser.MaxNPCs);
                for (int i = 0; i < max; i++)
                    if (SpawnNPC(root, city.npcs[i], worldOrigin, mat))
                        nc++;
            }

            CityDefPersistence.SaveCity(sanitizedJson, worldOrigin);
            return $"Built {city.name} — {bc} buildings, {pc} props, {nc} NPCs";
        }

        /// <summary>Build without saving (used when loading from disk).</summary>
        public static string BuildNoSave(string json, Vector3 worldOrigin)
        {
            var city = CityDefParser.Parse(json);

            var root = new GameObject($"City_{city.name}").transform;
            root.position = worldOrigin;
            var mat = new TownMaterials();

            BuildGround(root, city, worldOrigin, mat);

            if (city.streets != null)
                foreach (var street in city.streets)
                    BuildStreet(root, street, worldOrigin, mat);

            int bc = 0;
            if (city.buildings != null)
            {
                int max = Mathf.Min(city.buildings.Length, CityDefParser.MaxBuildings);
                for (int i = 0; i < max; i++)
                    if (PlaceBuilding(root, city.buildings[i], city.streets, worldOrigin, mat))
                        bc++;
            }

            int pc = 0;
            if (city.props != null)
            {
                int max = Mathf.Min(city.props.Length, CityDefParser.MaxProps);
                for (int i = 0; i < max; i++)
                    if (PlaceProp(root, city.props[i], worldOrigin, mat))
                        pc++;
            }

            int nc = 0;
            if (city.npcs != null)
            {
                int max = Mathf.Min(city.npcs.Length, CityDefParser.MaxNPCs);
                for (int i = 0; i < max; i++)
                    if (SpawnNPC(root, city.npcs[i], worldOrigin, mat))
                        nc++;
            }

            Debug.Log($"[CityDef] Restored {city.name} — {bc} buildings, {pc} props, {nc} NPCs");
            return city.name;
        }

        /// <summary>Rebuild a saved town at a new origin.</summary>
        public static bool RebuildAtOrigin(string townName, Vector3 newOrigin)
        {
            string cityJson = CityDefPersistence.FindSavedCityJson(townName);
            if (cityJson == null)
            {
                Debug.LogWarning($"[CityDef] RebuildAtOrigin: no saved city found for '{townName}'");
                return false;
            }

            var existing = GameObject.Find($"City_{townName}");
            if (existing != null)
                UnityEngine.Object.Destroy(existing);

            CityDefPersistence.UpdateSavedCityOrigin(townName, newOrigin);
            BuildNoSave(cityJson, newOrigin);
            Debug.Log($"[CityDef] Rebuilt '{townName}' at {newOrigin}");
            return true;
        }

        // ── Ground ──

        static void BuildGround(Transform root, CityDef city, Vector3 origin, TownMaterials mat)
        {
            float minX = 0, maxX = 0, minZ = 0, maxZ = 0;
            if (city.streets != null)
            {
                foreach (var s in city.streets)
                {
                    float halfLen = s.length / 2f;
                    float hw = CityDefParser.StreetWidth / 2f + CityDefParser.SidewalkWidth + 15f;
                    if (s.centerX - hw < minX) minX = s.centerX - hw;
                    if (s.centerX + hw > maxX) maxX = s.centerX + hw;
                    if (s.centerZ - halfLen < minZ) minZ = s.centerZ - halfLen;
                    if (s.centerZ + halfLen > maxZ) maxZ = s.centerZ + halfLen;
                }
            }

            float gw = Mathf.Max(maxX - minX + 30f, 60f);
            float gd = Mathf.Max(maxZ - minZ + 30f, 60f);
            float cx = (minX + maxX) / 2f;
            float cz = (minZ + maxZ) / 2f;

            var g = GameObject.CreatePrimitive(PrimitiveType.Plane);
            g.name = "Ground";
            g.transform.SetParent(root);
            g.transform.position = origin + V(cx, 0, cz);
            g.transform.localScale = V(gw / 10f, 1, gd / 10f);
            PropBuilder.ApplyMat(g, mat.MakeMat(new Color(0.42f, 0.48f, 0.28f), 0.05f));
        }

        // ── Streets ──

        static void BuildStreet(Transform root, CityStreetDef street, Vector3 origin, TownMaterials mat)
        {
            street.length = Mathf.Clamp(street.length > 0 ? street.length : 50f, 30f, 100f);
            street.centerX = Mathf.Clamp(street.centerX, -200f, 200f);
            street.centerZ = Mathf.Clamp(street.centerZ, -200f, 200f);

            Vector3 pos = origin + V(street.centerX, 0.01f, street.centerZ);

            var s = GameObject.CreatePrimitive(PrimitiveType.Cube);
            s.name = street.name;
            s.transform.SetParent(root);
            s.transform.position = pos;
            s.transform.localScale = V(CityDefParser.StreetWidth, 0.05f, street.length);
            PropBuilder.ApplyMat(s, mat.MakeMat(new Color(0.42f, 0.33f, 0.18f), 0.08f));

            // Sidewalks
            BuildSidewalk(root, $"Sidewalk_L_{street.name}",
                origin + V(street.centerX - (CityDefParser.StreetWidth / 2 + CityDefParser.SidewalkWidth / 2), 0.06f, street.centerZ),
                street.length, mat);
            BuildSidewalk(root, $"Sidewalk_R_{street.name}",
                origin + V(street.centerX + (CityDefParser.StreetWidth / 2 + CityDefParser.SidewalkWidth / 2), 0.06f, street.centerZ),
                street.length, mat);

            // Zone trigger
            Zone zone = Zone.MainStreet;
            CityDefParser.TryParseZone(street.zone, out zone);
            var trigger = new GameObject($"{street.name}Zone");
            trigger.transform.SetParent(root);
            trigger.transform.position = origin + V(street.centerX, 2f, street.centerZ);
            var bc = trigger.AddComponent<BoxCollider>();
            bc.size = V(CityDefParser.StreetWidth + CityDefParser.SidewalkWidth * 2 + 20f, 4f, street.length + 5f);
            bc.isTrigger = true;
            trigger.AddComponent<ZoneTrigger>().Init(zone);
        }

        static void BuildSidewalk(Transform root, string name, Vector3 pos, float length, TownMaterials mat)
        {
            var sw = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sw.name = name;
            sw.transform.SetParent(root);
            sw.transform.position = pos;
            sw.transform.localScale = V(CityDefParser.SidewalkWidth, 0.15f, length);
            PropBuilder.ApplyMat(sw, mat.WoodLight);
        }

        // ── Buildings ──

        static bool PlaceBuilding(Transform root, CityBuildingDef bdef, CityStreetDef[] streets,
            Vector3 origin, TownMaterials mat)
        {
            if (streets == null || streets.Length == 0)
            {
                streets = new CityStreetDef[] {
                    new CityStreetDef { name = "Main Street", centerX = 0, centerZ = 0, length = 80 }
                };
                Debug.LogWarning($"[CityDef] No streets defined — using default for '{bdef.name}'");
            }

            int idx = Mathf.Clamp(bdef.streetIndex, 0, streets.Length - 1);
            var street = streets[idx];
            float sx = origin.x + street.centerX;
            float sz = origin.z + street.centerZ;

            float width  = Mathf.Clamp(bdef.width  > 0 ? bdef.width  : 10f, 5f, 30f);
            float height = Mathf.Clamp(bdef.height > 0 ? bdef.height : 6f,  3f, 15f);
            float depth  = Mathf.Clamp(bdef.depth  > 0 ? bdef.depth  : 8f,  4f, 25f);

            float estDepth = CityDefParser.GetEstimatedDepth(bdef.zone);
            float offsetDepth = Mathf.Max(depth, estDepth);

            float halfLen = street.length / 2f;
            float zPos = Mathf.Clamp(bdef.zPos, -halfLen + 3f, halfLen - 3f);

            float offset = CityDefParser.StreetWidth / 2f + CityDefParser.SidewalkWidth + offsetDepth / 2f + 2f;
            Vector3 pos;
            float rot;

            string side = (bdef.side ?? "Left").ToLower();
            switch (side)
            {
                case "right":
                    pos = V(sx + offset, origin.y, sz + zPos);
                    rot = -90f;
                    break;
                case "end":
                    if (zPos >= 0)
                    {
                        pos = V(sx, origin.y, sz + halfLen + depth / 2f + 2f);
                        rot = 180f;
                    }
                    else
                    {
                        pos = V(sx, origin.y, sz - halfLen - depth / 2f - 2f);
                        rot = 0f;
                    }
                    break;
                default:
                    pos = V(sx - offset, origin.y, sz + zPos);
                    rot = 90f;
                    break;
            }

            Zone zone = Zone.Wilderness;
            CityDefParser.TryParseZone(bdef.zone, out zone);

            InteriorStyle interior = InteriorStyle.Empty;
            CityDefParser.TryParseInterior(bdef.interior, out interior);

            Color color = new Color(0.60f, 0.50f, 0.40f);
            if (bdef.color != null && bdef.color.Length >= 3)
                color = new Color(bdef.color[0], bdef.color[1], bdef.color[2]);

            if (CheckForbidden(pos))
            {
                Debug.LogWarning($"[CityDef] Skipping building '{bdef.name}' — overlaps forbidden zone at {pos}");
                return false;
            }

            var def = new BuildingDef
            {
                name = bdef.name,
                zone = zone,
                position = pos,
                rotation = rot,
                size = V(width, height, depth),
                wallColor = color,
                hasDoor = true,
                interior = interior
            };

            BuildingBuilder.Build(root, def, mat);

            if (!string.IsNullOrEmpty(bdef.agentId))
            {
                var bldTransform = root.Find(bdef.name);
                if (bldTransform != null)
                    bldTransform.gameObject.AddComponent<BuildingAgent>().agentId = bdef.agentId;
            }

            return true;
        }

        // ── Props ──

        static bool PlaceProp(Transform root, CityPropDef prop, Vector3 origin, TownMaterials mat)
        {
            if (string.IsNullOrEmpty(prop.type)) return false;

            Vector3 pos = origin + V(prop.x, 0, prop.z);

            if (CheckForbidden(pos))
            {
                Debug.LogWarning($"[CityDef] Skipping prop '{prop.type}' — overlaps forbidden zone at {pos}");
                return false;
            }

            float h = prop.height > 0 ? prop.height : 6f;
            float s = prop.scale > 0 ? prop.scale : 1f;
            float yaw = prop.yaw;

            // Delegate to PropBuilder — it handles all prop types
            return PropBuilder.SpawnProp(root, prop.type, pos, yaw, h, s, mat);
        }

        // ── NPCs ──

        static bool SpawnNPC(Transform root, CityNPCDef npc, Vector3 origin, TownMaterials mat)
        {
            if (string.IsNullOrEmpty(npc.prefab) || string.IsNullOrEmpty(npc.name)) return false;

            Vector3 pos = origin + V(npc.x, 0, npc.z);

            if (CheckForbidden(pos))
            {
                Debug.LogWarning($"[CityDef] Skipping NPC '{npc.name}' — overlaps forbidden zone at {pos}");
                return false;
            }

            float speed = npc.speed > 0 ? npc.speed : 0.8f;
            float radius = npc.radius > 0 ? npc.radius : 10f;

            NPCBuilder.SpawnTownsfolk(root, npc.prefab, npc.name, pos, speed, radius, mat);
            return true;
        }
    }
}

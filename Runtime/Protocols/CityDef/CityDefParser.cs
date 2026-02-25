using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using OpenClawWorlds;

namespace OpenClawWorlds.Protocols
{
    /// <summary>
    /// Parses, sanitizes, and normalizes CityDef JSON from AI agents.
    /// Handles LLM output variants: nested position objects, points arrays,
    /// missing zones, wanderingNpcs, and more.
    /// </summary>
    public static class CityDefParser
    {
        // ── Layout Constants ──
        public const float StreetWidth = 10f;
        public const float SidewalkWidth = 2.5f;
        public const float PlotGutter = 3f;
        public const int MaxBuildings = 40;
        public const int MaxProps = 50;
        public const int MaxNPCs = 10;

        /// <summary>
        /// Fix common LLM JSON mistakes that JsonUtility can't handle:
        /// trailing commas, single-line comments, unescaped newlines in strings,
        /// single-quoted strings, and other common issues.
        /// </summary>
        public static string SanitizeJson(string json)
        {
            // 1. Strip single-line comments (// ...)
            json = Regex.Replace(json, @"//[^\n]*", "");

            // 2. Strip multi-line comments (/* ... */)
            json = Regex.Replace(json, @"/\*.*?\*/", "", RegexOptions.Singleline);

            // 3. Replace single-quoted strings with double-quoted strings
            //    Match 'text' that is not inside double quotes
            json = Regex.Replace(json, @"(?<=[:,\[\{\s])\'([^']*?)\'", "\"$1\"");

            // 4. Remove trailing commas before } or ]
            json = Regex.Replace(json, @",\s*([}\]])", "$1");

            // 5. Fix unescaped newlines inside strings
            //    Walk the string and escape \n that are inside quotes
            var sb = new System.Text.StringBuilder(json.Length);
            bool inStr = false;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"' && (i == 0 || json[i - 1] != '\\'))
                {
                    inStr = !inStr;
                    sb.Append(c);
                }
                else if (inStr && c == '\n')
                {
                    sb.Append("\\n");
                }
                else if (inStr && c == '\r')
                {
                    // skip \r
                }
                else if (inStr && c == '\t')
                {
                    sb.Append("\\t");
                }
                else
                {
                    sb.Append(c);
                }
            }
            json = sb.ToString();

            // 6. Fix unterminated strings — if an odd number of unescaped quotes,
            //    append a closing quote before the next } or ]
            int quoteCount = 0;
            for (int i = 0; i < json.Length; i++)
            {
                if (json[i] == '"' && (i == 0 || json[i - 1] != '\\'))
                    quoteCount++;
            }
            if (quoteCount % 2 != 0)
            {
                // Find the last unmatched quote and close it
                int lastQuote = json.LastIndexOf('"');
                int nextClose = json.IndexOfAny(new[] { '}', ']' }, lastQuote);
                if (nextClose > lastQuote)
                    json = json.Insert(nextClose, "\"");
                else
                    json += "\"";
            }

            return json;
        }

        /// <summary>
        /// Attempt to repair corrupted CityDef JSON by truncating at each }
        /// from the end and closing unclosed arrays/objects until it parses.
        /// Returns repaired JSON or null if unfixable.
        /// </summary>
        public static string TryRepairJson(string json)
        {
            // Walk backwards through each } and try to close unclosed structures
            for (int i = json.Length - 1; i > 20; i--)
            {
                if (json[i] != '}') continue;

                string attempt = json.Substring(0, i + 1);

                // Count unclosed braces and brackets (simple, not string-aware
                // — good enough since we're truncating at known } positions)
                int braces = 0, brackets = 0;
                for (int j = 0; j < attempt.Length; j++)
                {
                    char c = attempt[j];
                    if (c == '{') braces++;
                    else if (c == '}') braces--;
                    else if (c == '[') brackets++;
                    else if (c == ']') brackets--;
                }

                // Build closing suffix
                string suffix = "";
                for (int b = 0; b < brackets; b++) suffix += "]";
                for (int b = 0; b < braces; b++) suffix += "}";

                string repaired = attempt + suffix;

                try
                {
                    string sanitized = SanitizeJson(repaired);
                    var city = JsonUtility.FromJson<CityDef>(sanitized);
                    if (city != null && !string.IsNullOrEmpty(city.name))
                    {
                        Debug.Log($"[CityDef] Repaired JSON by truncating at pos {i}, closing {brackets}x], {braces}x}}");
                        return sanitized;
                    }
                }
                catch { /* keep trying earlier positions */ }
            }

            return null;
        }

        /// <summary>
        /// Parse a CityDef from JSON. Sanitizes, deserializes, normalizes, and auto-packs.
        /// </summary>
        public static CityDef Parse(string json)
        {
            json = SanitizeJson(json);
            var city = JsonUtility.FromJson<CityDef>(json);
            if (city == null || string.IsNullOrEmpty(city.name))
                throw new Exception("Invalid CityDef JSON — missing name");

            NormalizeCityDef(city);
            AutoPackBuildings(city);
            return city;
        }

        /// <summary>
        /// Infer a building zone from its interior style or name.
        /// Handles the case where the LLM omits the "zone" field entirely.
        /// </summary>
        public static string InferZoneFromInteriorOrName(string interior, string buildingName)
        {
            if (!string.IsNullOrEmpty(interior))
            {
                switch (interior.ToLower())
                {
                    case "saloon":    return "Saloon";
                    case "shop":      return "GeneralStore";
                    case "jail":      return "Sheriff";
                    case "hotel":     return "Hotel";
                    case "office":    return "Office";
                    case "church":    return "Church";
                    case "library":   return "TownLibrary";
                    case "clinic":    return "Doctor";
                    case "smithy":    return "Blacksmith";
                    case "warehouse": return "Warehouse";
                    case "theater":   return "Theater";
                    case "empty":     break;
                }
            }

            if (!string.IsNullOrEmpty(buildingName))
            {
                string lower = buildingName.ToLower();
                if (lower.Contains("saloon") || lower.Contains("bar") || lower.Contains("tavern")) return "Saloon";
                if (lower.Contains("sheriff") || lower.Contains("jail") || lower.Contains("marshal")) return "Sheriff";
                if (lower.Contains("hotel") || lower.Contains("inn") || lower.Contains("lodge")) return "Hotel";
                if (lower.Contains("church") || lower.Contains("chapel")) return "Church";
                if (lower.Contains("store") || lower.Contains("shop") || lower.Contains("general")) return "GeneralStore";
                if (lower.Contains("bank")) return "Bank";
                if (lower.Contains("blacksmith") || lower.Contains("forge")) return "Blacksmith";
                if (lower.Contains("stable")) return "Stables";
                if (lower.Contains("library")) return "TownLibrary";
                if (lower.Contains("doctor") || lower.Contains("clinic")) return "Doctor";
                if (lower.Contains("courthouse") || lower.Contains("court")) return "Courthouse";
                if (lower.Contains("post office")) return "PostOffice";
                if (lower.Contains("fire")) return "FireDept";
                if (lower.Contains("barn")) return "Barn";
                if (lower.Contains("warehouse")) return "Warehouse";
                if (lower.Contains("theater") || lower.Contains("theatre")) return "Theater";
                if (lower.Contains("mill")) return "GrainMill";
                if (lower.Contains("market")) return "Marketplace";
            }

            return "GeneralStore";
        }

        /// <summary>
        /// Normalize LLM output variants into canonical format.
        /// </summary>
        public static void NormalizeCityDef(CityDef city)
        {
            // --- Convert wanderingNpcs → npcs if npcs is empty ---
            if (city.wanderingNpcs != null && city.wanderingNpcs.Length > 0
                && (city.npcs == null || city.npcs.Length == 0))
            {
                var npcList = new List<CityNPCDef>();
                float zSpread = 0;
                foreach (var wn in city.wanderingNpcs)
                {
                    if (string.IsNullOrEmpty(wn.prefab)) continue;
                    npcList.Add(new CityNPCDef
                    {
                        prefab = wn.prefab,
                        name = "Townsfolk",
                        x = 0,
                        z = zSpread,
                        speed = 0.8f,
                        radius = 15f
                    });
                    zSpread += 20f;
                }
                city.npcs = npcList.ToArray();
                Debug.Log($"[CityDefParser] Converted {npcList.Count} wanderingNpcs → npcs");
            }

            // --- Normalize streets ---
            if (city.streets != null)
            {
                foreach (var s in city.streets)
                {
                    if (s.points != null && s.points.Length >= 2 && s.length == 0 && s.centerX == 0 && s.centerZ == 0)
                    {
                        float minX = float.MaxValue, maxX = float.MinValue;
                        float minZ = float.MaxValue, maxZ = float.MinValue;
                        foreach (var p in s.points)
                        {
                            if (p.x < minX) minX = p.x;
                            if (p.x > maxX) maxX = p.x;
                            if (p.z < minZ) minZ = p.z;
                            if (p.z > maxZ) maxZ = p.z;
                        }
                        s.centerX = (minX + maxX) / 2f;
                        s.centerZ = (minZ + maxZ) / 2f;
                        float dx = maxX - minX;
                        float dz = maxZ - minZ;
                        s.length = Mathf.Max(dx, dz);
                        Debug.Log($"[CityDefParser] Normalized street '{s.name}' from points → center=({s.centerX},{s.centerZ}), length={s.length}");
                    }
                }
            }

            // --- Normalize buildings ---
            if (city.buildings != null)
            {
                foreach (var b in city.buildings)
                {
                    if (string.IsNullOrEmpty(b.zone))
                    {
                        b.zone = InferZoneFromInteriorOrName(b.interior, b.name);
                        Debug.Log($"[CityDefParser] Inferred zone '{b.zone}' for '{b.name}' (interior={b.interior})");
                    }

                    if (b.position != null && (b.position.x != 0 || b.position.z != 0)
                        && string.IsNullOrEmpty(b.side) && b.zPos == 0)
                    {
                        int bestStreet = 0;
                        float bestDist = float.MaxValue;
                        if (city.streets != null)
                        {
                            for (int i = 0; i < city.streets.Length; i++)
                            {
                                float dist = Mathf.Abs(b.position.x - city.streets[i].centerX);
                                if (dist < bestDist) { bestDist = dist; bestStreet = i; }
                            }
                        }

                        b.streetIndex = bestStreet;
                        var street = city.streets != null && city.streets.Length > 0 ? city.streets[bestStreet] : null;

                        float streetCX = street != null ? street.centerX : 0;
                        if (b.position.x < streetCX - 0.5f) b.side = "Left";
                        else if (b.position.x > streetCX + 0.5f) b.side = "Right";
                        else b.side = "End";

                        float streetCZ = street != null ? street.centerZ : 0;
                        b.zPos = b.position.z - streetCZ;

                        Debug.Log($"[CityDefParser] Normalized building '{b.name}' from position({b.position.x},{b.position.z}) → street {bestStreet}, side={b.side}, zPos={b.zPos}");
                    }
                }
            }

            // --- Normalize NPCs ---
            if (city.npcs != null)
            {
                foreach (var n in city.npcs)
                {
                    if (n.position != null && (n.position.x != 0 || n.position.z != 0) && n.x == 0 && n.z == 0)
                    {
                        n.x = n.position.x;
                        n.z = n.position.z;
                    }
                }
            }

            // --- Normalize Props ---
            if (city.props != null)
            {
                foreach (var p in city.props)
                {
                    if (p.position != null && (p.position.x != 0 || p.position.z != 0) && p.x == 0 && p.z == 0)
                    {
                        p.x = p.position.x;
                        p.z = p.position.z;
                    }
                }
            }
        }

        // ── Plot-Based Auto-Packing ──

        /// <summary>Approximate depth for each building zone (prefab-based).</summary>
        public static float GetEstimatedDepth(string zone)
        {
            switch ((zone ?? "").ToLower())
            {
                case "church": case "barn": case "trainstation": return 14f;
                case "courthouse": case "firedept": case "townlibrary": return 12f;
                case "saloon": case "hotel": case "ranchhouse": return 11f;
                default: return 10f;
            }
        }

        /// <summary>Approximate width along the street for each building zone.</summary>
        public static float GetPlotWidth(string zone)
        {
            switch ((zone ?? "").ToLower())
            {
                case "church": case "courthouse": case "firedept": case "barn": case "trainstation": return 20f;
                case "hotel": case "ranchhouse": case "townlibrary": case "saloon": return 16f;
                default: return 13f;
            }
        }

        /// <summary>
        /// Auto-pack buildings tightly along each street side.
        /// Uses zPos only as an ordering hint, then assigns packed positions.
        /// </summary>
        public static void AutoPackBuildings(CityDef city)
        {
            if (city.buildings == null || city.streets == null || city.streets.Length == 0)
                return;

            var groups = new Dictionary<string, List<int>>();
            var endBuildings = new List<int>();

            for (int i = 0; i < city.buildings.Length; i++)
            {
                var b = city.buildings[i];
                string side = (b.side ?? "Left").ToLower();

                if (side == "end") { endBuildings.Add(i); continue; }

                int si = Mathf.Clamp(b.streetIndex, 0, city.streets.Length - 1);
                string key = $"{si}_{side}";
                if (!groups.ContainsKey(key))
                    groups[key] = new List<int>();
                groups[key].Add(i);
            }

            foreach (var kvp in groups)
            {
                var group = kvp.Value;
                if (group.Count <= 1) continue;

                group.Sort((a, b) => city.buildings[a].zPos.CompareTo(city.buildings[b].zPos));

                float totalWidth = 0;
                var plotWidths = new float[group.Count];
                for (int i = 0; i < group.Count; i++)
                {
                    plotWidths[i] = GetPlotWidth(city.buildings[group[i]].zone);
                    totalWidth += plotWidths[i];
                }
                totalWidth += PlotGutter * (group.Count - 1);

                int streetIdx = Mathf.Clamp(city.buildings[group[0]].streetIndex, 0, city.streets.Length - 1);
                float streetHalfLen = city.streets[streetIdx].length / 2f;

                if (totalWidth > city.streets[streetIdx].length - 4f)
                {
                    city.streets[streetIdx].length = totalWidth + 8f;
                    streetHalfLen = city.streets[streetIdx].length / 2f;
                    Debug.Log($"[CityDefParser] Auto-extended street '{city.streets[streetIdx].name}' to length {city.streets[streetIdx].length:F0} for {group.Count} buildings");
                }

                float cursor = -totalWidth / 2f;
                for (int i = 0; i < group.Count; i++)
                {
                    float halfW = plotWidths[i] / 2f;
                    float newZPos = cursor + halfW;
                    city.buildings[group[i]].zPos = newZPos;
                    cursor += plotWidths[i] + PlotGutter;
                }

                Debug.Log($"[CityDefParser] Auto-packed {group.Count} buildings on {kvp.Key} (total width: {totalWidth:F0})");
            }
        }

        // ── Enum Parsing Helpers ──

        public static bool TryParseZone(string s, out Zone zone)
        {
            zone = Zone.Wilderness;
            if (string.IsNullOrEmpty(s)) return false;
            try { zone = (Zone)Enum.Parse(typeof(Zone), s, true); return true; }
            catch { return false; }
        }

        public static bool TryParseInterior(string s, out InteriorStyle style)
        {
            style = InteriorStyle.Empty;
            if (string.IsNullOrEmpty(s)) return false;
            try { style = (InteriorStyle)Enum.Parse(typeof(InteriorStyle), s, true); return true; }
            catch { return false; }
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using OpenClawWorlds.Protocols;

namespace OpenClawWorlds.Validation
{
    /// <summary>
    /// Validates AI-generated CityDef JSON before building.
    /// Returns a list of errors — empty list means JSON is valid.
    /// </summary>
    public static class AuditPipeline
    {
        static readonly HashSet<string> ValidZones = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "Saloon", "Bank", "Sheriff", "TradingPost", "Hotel", "PostOffice", "Church",
            "Blacksmith", "Doctor", "GeneralStore", "Stables", "Schoolhouse",
            "Courthouse", "TownLibrary", "Newspaper", "FireDept",
            "LumberYard", "GrainMill", "Bakery", "RanchHouse", "Barn", "FeedStore",
            "TrainStation", "Cemetery", "University", "Library", "Theater", "Marketplace",
            "Residential", "Office", "Warehouse"
        };

        static readonly HashSet<string> ValidInteriors = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "Saloon", "Office", "Shop", "Jail", "Hotel", "Church", "Warehouse",
            "School", "Library", "Theater", "Clinic", "Smithy", "Empty"
        };

        static readonly HashSet<string> ValidSides = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "Left", "Right", "End"
        };

        /// <summary>
        /// Optional: set of valid NPC prefab names. If null, prefab validation is skipped.
        /// This allows projects to define their own character prefab set.
        /// </summary>
        public static HashSet<string> ValidPrefabs { get; set; }

        /// <summary>
        /// Optional: additional valid zones specific to your project.
        /// These are merged with the built-in set during validation.
        /// </summary>
        public static HashSet<string> AdditionalValidZones { get; set; }

        /// <summary>
        /// Validate a CityDef JSON string. Returns error list (empty = valid).
        /// Called BEFORE NormalizeCityDef — checks the raw LLM output.
        /// </summary>
        public static List<string> AuditCityDef(string json)
        {
            var errors = new List<string>();

            json = CityDefParser.SanitizeJson(json);
            CityDef city;
            try { city = JsonUtility.FromJson<CityDef>(json); }
            catch (Exception e)
            {
                errors.Add($"JSON parse error: {e.Message}");
                return errors;
            }
            if (city == null) { errors.Add("JSON parsed to null"); return errors; }

            // Name
            if (string.IsNullOrEmpty(city.name))
                errors.Add("Missing required field: \"name\"");

            // Build combined valid zones
            var allValidZones = new HashSet<string>(ValidZones, StringComparer.OrdinalIgnoreCase);
            if (AdditionalValidZones != null)
                foreach (var z in AdditionalValidZones)
                    allValidZones.Add(z);

            // Streets
            if (city.streets == null || city.streets.Length == 0)
                errors.Add("Missing \"streets\" array (need at least 1 street)");
            else
            {
                for (int i = 0; i < city.streets.Length; i++)
                {
                    var s = city.streets[i];
                    if (string.IsNullOrEmpty(s.name))
                        errors.Add($"streets[{i}]: missing \"name\"");
                    bool hasFlat = s.length > 0 || s.centerX != 0 || s.centerZ != 0;
                    bool hasPoints = s.points != null && s.points.Length >= 2;
                    if (!hasFlat && !hasPoints)
                        errors.Add($"streets[{i}] \"{s.name}\": needs either (centerX,centerZ,length) or (points array with 2+ points)");
                }
            }

            // Buildings
            if (city.buildings == null || city.buildings.Length == 0)
                errors.Add("Missing \"buildings\" array (need at least 1 building)");
            else
            {
                bool anyUsedNestedPosition = false;
                for (int i = 0; i < city.buildings.Length; i++)
                {
                    var b = city.buildings[i];
                    if (string.IsNullOrEmpty(b.name))
                        errors.Add($"buildings[{i}]: missing \"name\"");

                    if (b.position != null && (b.position.x != 0 || b.position.z != 0))
                        anyUsedNestedPosition = true;

                    if (string.IsNullOrEmpty(b.zone))
                    {
                        string inferred = CityDefParser.InferZoneFromInteriorOrName(b.interior, b.name);
                        if (inferred != null)
                            errors.Add($"buildings[{i}] \"{b.name}\": missing \"zone\" field. Should be \"{inferred}\" based on interior/name. Add: \"zone\": \"{inferred}\"");
                        else
                            errors.Add($"buildings[{i}] \"{b.name}\": missing \"zone\" field");
                    }
                    else if (!allValidZones.Contains(b.zone))
                        errors.Add($"buildings[{i}] \"{b.name}\": unknown zone \"{b.zone}\".");

                    if (string.IsNullOrEmpty(b.side))
                    {
                        if (b.position == null || (b.position.x == 0 && b.position.z == 0))
                            errors.Add($"buildings[{i}] \"{b.name}\": missing \"side\" field. Must be \"Left\", \"Right\", or \"End\"");
                    }
                    else if (!ValidSides.Contains(b.side))
                        errors.Add($"buildings[{i}] \"{b.name}\": invalid side \"{b.side}\". Must be \"Left\", \"Right\", or \"End\"");

                    if (!string.IsNullOrEmpty(b.interior) && !ValidInteriors.Contains(b.interior))
                        errors.Add($"buildings[{i}] \"{b.name}\": unknown interior \"{b.interior}\".");
                }

                if (anyUsedNestedPosition)
                    errors.Add("WRONG FORMAT: Buildings use nested \"position\" objects. Use flat fields instead: \"side\": \"Left\", \"zPos\": 0, \"streetIndex\": 0");
            }

            // NPCs
            if (city.npcs != null)
            {
                for (int i = 0; i < city.npcs.Length; i++)
                {
                    var n = city.npcs[i];
                    if (string.IsNullOrEmpty(n.prefab))
                        errors.Add($"npcs[{i}]: missing \"prefab\"");
                    else if (ValidPrefabs != null && !ValidPrefabs.Contains(n.prefab))
                        errors.Add($"npcs[{i}] \"{n.name}\": unknown prefab \"{n.prefab}\".");
                    if (string.IsNullOrEmpty(n.name))
                        errors.Add($"npcs[{i}]: missing \"name\"");
                }
            }

            return errors;
        }
    }
}

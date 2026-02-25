using UnityEngine;
using System;
using System.IO;

namespace OpenClawWorlds.Protocols
{
    /// <summary>
    /// Save/load CityDef JSON and origins to persistent storage.
    /// Saved cities can be rebuilt at their original positions on scene load.
    /// </summary>
    public static class CityDefPersistence
    {
        /// <summary>Override the save directory. Defaults to Application.persistentDataPath/cities.</summary>
        public static string SaveDirOverride { get; set; }

        static string SaveDir =>
            !string.IsNullOrEmpty(SaveDirOverride)
                ? SaveDirOverride
                : Path.Combine(Application.persistentDataPath, "cities");

        /// <summary>Save a city JSON and its world origin to disk.</summary>
        public static void SaveCity(string json, Vector3 origin)
        {
            try
            {
                Directory.CreateDirectory(SaveDir);
                var saved = new SavedCity
                {
                    cityJson = json,
                    originX = origin.x,
                    originY = origin.y,
                    originZ = origin.z
                };
                string filename = $"city_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.json";
                File.WriteAllText(Path.Combine(SaveDir, filename), JsonUtility.ToJson(saved));
                Debug.Log($"[CityDef] Saved city to {filename}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CityDef] Failed to save city: {e.Message}");
            }
        }

        /// <summary>Delete any saved city files whose JSON contains the given town name.</summary>
        public static void DeleteSavedCity(string townName)
        {
            if (!Directory.Exists(SaveDir)) return;
            foreach (var file in Directory.GetFiles(SaveDir, "city_*.json"))
            {
                try
                {
                    string data = File.ReadAllText(file);
                    var saved = JsonUtility.FromJson<SavedCity>(data);
                    if (saved != null && !string.IsNullOrEmpty(saved.cityJson))
                    {
                        var def = JsonUtility.FromJson<CityDef>(saved.cityJson);
                        if (def != null && def.name == townName)
                        {
                            File.Delete(file);
                            Debug.Log($"[CityDef] Deleted old save: {Path.GetFileName(file)}");
                        }
                    }
                }
                catch { }
            }
        }

        /// <summary>Load all saved cities and rebuild them using the provided build function.</summary>
        public static void LoadSavedCities(System.Action<string, Vector3> buildAction)
        {
            if (!Directory.Exists(SaveDir)) return;

            var files = Directory.GetFiles(SaveDir, "city_*.json");
            if (files.Length == 0) return;

            Debug.Log($"[CityDef] Loading {files.Length} saved cities...");
            foreach (var file in files)
            {
                try
                {
                    string data = File.ReadAllText(file);
                    var saved = JsonUtility.FromJson<SavedCity>(data);
                    if (saved != null && !string.IsNullOrEmpty(saved.cityJson))
                    {
                        var origin = new Vector3(saved.originX, saved.originY, saved.originZ);
                        buildAction?.Invoke(saved.cityJson, origin);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[CityDef] Failed to load {Path.GetFileName(file)}: {e.Message}");
                }
            }
        }

        /// <summary>Update the saved origin for a city by name.</summary>
        public static void UpdateSavedCityOrigin(string townName, Vector3 newOrigin)
        {
            if (!Directory.Exists(SaveDir)) return;
            foreach (var file in Directory.GetFiles(SaveDir, "city_*.json"))
            {
                try
                {
                    string data = File.ReadAllText(file);
                    var saved = JsonUtility.FromJson<SavedCity>(data);
                    if (saved == null || string.IsNullOrEmpty(saved.cityJson)) continue;
                    var def = JsonUtility.FromJson<CityDef>(saved.cityJson);
                    if (def == null || def.name != townName) continue;

                    saved.originX = newOrigin.x;
                    saved.originY = newOrigin.y;
                    saved.originZ = newOrigin.z;
                    File.WriteAllText(file, JsonUtility.ToJson(saved));
                    Debug.Log($"[CityDef] Updated origin for {townName} to {newOrigin}");
                    return;
                }
                catch { }
            }
        }

        /// <summary>Find saved city JSON by name and return it.</summary>
        public static string FindSavedCityJson(string townName)
        {
            if (!Directory.Exists(SaveDir)) return null;
            foreach (var file in Directory.GetFiles(SaveDir, "city_*.json"))
            {
                try
                {
                    string data = File.ReadAllText(file);
                    var saved = JsonUtility.FromJson<SavedCity>(data);
                    if (saved == null || string.IsNullOrEmpty(saved.cityJson)) continue;
                    var def = JsonUtility.FromJson<CityDef>(CityDefParser.SanitizeJson(saved.cityJson));
                    if (def != null && def.name == townName)
                        return saved.cityJson;
                }
                catch { }
            }
            return null;
        }
    }
}

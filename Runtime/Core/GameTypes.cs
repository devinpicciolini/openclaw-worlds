using UnityEngine;
using System.Collections.Generic;

namespace OpenClawWorlds
{
    /// <summary>
    /// Building zone classification. Determines which prefab to spawn
    /// and what interior to generate. Extend this enum for your project.
    /// </summary>
    public enum Zone
    {
        Wilderness,
        MainStreet,
        SecondStreet,
        Saloon,
        Bank,
        Sheriff,
        TradingPost,
        Hotel,
        PostOffice,
        Church,
        Blacksmith,
        Doctor,
        GeneralStore,
        Stables,
        Schoolhouse,
        University,
        Library,
        Theater,
        Marketplace,
        Residential,
        Recreation,
        Park,
        TownSquare,
        CivicRow,
        Courthouse,
        TownLibrary,
        Newspaper,
        FireDept,
        MillLane,
        RanchRoad,
        LumberYard,
        GrainMill,
        Bakery,
        RanchHouse,
        Barn,
        FeedStore,
        TrainStation,
        Cemetery,
        Office,
        Warehouse
    }

    public enum InteractableType
    {
        Door,
        NPC,
        Pickup,
        Shop,
        Poker,
        BountyBoard
    }

    /// <summary>Which side of the street a building faces.</summary>
    public enum StreetSide { Left, Right, End }

    /// <summary>
    /// Interior furniture style. Determines what props the
    /// InteriorBuilder auto-generates inside a building.
    /// </summary>
    public enum InteriorStyle
    {
        Empty,
        Saloon,
        Office,
        Shop,
        Jail,
        Hotel,
        Church,
        Warehouse,
        School,
        Library,
        Theater,
        Clinic,
        Smithy
    }

    [System.Serializable]
    public struct BuildingDef
    {
        public string name;
        public Zone zone;
        public Vector3 position;
        public float rotation;
        public Vector3 size;
        public Color wallColor;
        public bool hasDoor;
        public InteriorStyle interior;
        public float scale;
    }

    public static class ZoneExtensions
    {
        static readonly Dictionary<Zone, string> NameOverrides = new Dictionary<Zone, string>
        {
            { Zone.SecondStreet,  "Commerce Row" },
            { Zone.Saloon,        "The Saloon" },
            { Zone.Bank,          "Frontier Savings" },
            { Zone.Hotel,         "Grand Hotel" },
            { Zone.PostOffice,    "Telegraph & Gazette" },
            { Zone.Doctor,        "Doc's Office" },
            { Zone.Stables,       "Livery Stables" },
            { Zone.FireDept,      "Fire Department" },
        };

        /// <summary>
        /// Human-readable zone name. Auto-splits PascalCase ("GrainMill" → "Grain Mill").
        /// </summary>
        public static string DisplayName(this Zone zone)
        {
            if (NameOverrides.TryGetValue(zone, out var name)) return name;
            var s = zone.ToString();
            var sb = new System.Text.StringBuilder(s.Length + 4);
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && char.IsUpper(s[i])) sb.Append(' ');
                sb.Append(s[i]);
            }
            return sb.ToString();
        }

        public static bool IsOutdoor(this Zone zone)
        {
            return zone == Zone.MainStreet || zone == Zone.SecondStreet || zone == Zone.Wilderness
                || zone == Zone.TownSquare || zone == Zone.CivicRow
                || zone == Zone.MillLane || zone == Zone.RanchRoad
                || zone == Zone.TrainStation || zone == Zone.Cemetery;
        }

        public static bool IsIndoor(this Zone zone) => !zone.IsOutdoor();
    }
}

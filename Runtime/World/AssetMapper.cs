using UnityEngine;
using System.Collections.Generic;
using OpenClawWorlds;

namespace OpenClawWorlds.World
{
    /// <summary>
    /// Interface for mapping building zone types to prefab names.
    /// Implement this to plug in any asset pack (POLYGON Western, Synty Fantasy, custom, etc.)
    /// The SDK ships with a fallback that uses primitive cubes.
    /// </summary>
    public interface IAssetMapper
    {
        /// <summary>Return the prefab name for a building zone type.</summary>
        string GetBuildingPrefab(BuildingDef def);

        /// <summary>Return the animator controller path for this character type.</summary>
        string GetAnimatorController(bool feminine);

        /// <summary>Return NPC template data for an interior style. Null = no NPC for this style.</summary>
        NPCTemplate GetNPCTemplate(InteriorStyle interior);

        /// <summary>Return a zone override NPC template. Null = use default interior template.</summary>
        NPCTemplate GetZoneOverrideNPC(Zone zone);

        /// <summary>Whether a given prefab name represents a feminine character (for animator selection).</summary>
        bool IsFeminine(string prefabName);
    }

    /// <summary>NPC template data for the asset mapper.</summary>
    public struct NPCTemplate
    {
        public string prefab;
        public string name;
        public string greeting;
        public string[] offerings;
        public float zFraction;
        public bool persistent;
    }

    /// <summary>
    /// Default asset mapper that works with NO asset pack — uses primitive geometry.
    /// Override individual methods or replace entirely with your asset pack's mapper.
    /// </summary>
    public class DefaultAssetMapper : IAssetMapper
    {
        public virtual string GetBuildingPrefab(BuildingDef def)
        {
            // No prefab — BuildingBuilder will use fallback cube
            return null;
        }

        public virtual string GetAnimatorController(bool feminine)
        {
            return feminine
                ? "Animations/AC_Polygon_Feminine"
                : "Animations/AC_Polygon_Masculine";
        }

        public virtual NPCTemplate GetNPCTemplate(InteriorStyle interior)
        {
            // Default generic NPCs — override with your own character pack
            switch (interior)
            {
                case InteriorStyle.Saloon:
                    return new NPCTemplate { name = "Bartender", greeting = "What'll it be?", offerings = new[] { "Chat", "Order a drink" }, zFraction = -0.25f, persistent = true };
                case InteriorStyle.Shop:
                    return new NPCTemplate { name = "Shopkeeper", greeting = "Welcome! Take a look around.", offerings = new[] { "Browse wares", "Ask about specials" }, zFraction = 0.25f, persistent = true };
                case InteriorStyle.Office:
                    return new NPCTemplate { name = "Clerk", greeting = "How can I help you today?", offerings = new[] { "File paperwork", "Ask a question" }, zFraction = -0.17f };
                case InteriorStyle.Jail:
                    return new NPCTemplate { name = "Sheriff", greeting = "Stay out of trouble.", offerings = new[] { "Report a crime", "Ask about bounties" }, zFraction = -0.17f, persistent = true };
                case InteriorStyle.Hotel:
                    return new NPCTemplate { name = "Innkeeper", greeting = "Need a room?", offerings = new[] { "Rent a room", "Ask for directions" }, zFraction = 0.25f };
                case InteriorStyle.Church:
                    return new NPCTemplate { name = "Preacher", greeting = "Welcome, friend.", offerings = new[] { "Seek counsel", "Make a donation" }, zFraction = -0.5f };
                case InteriorStyle.Smithy:
                    return new NPCTemplate { name = "Smith", greeting = "What needs fixing?", offerings = new[] { "Repair equipment", "Order custom work" }, zFraction = 0.25f, persistent = true };
                case InteriorStyle.Clinic:
                    return new NPCTemplate { name = "Doctor", greeting = "What seems to be the trouble?", offerings = new[] { "Get treatment", "Buy supplies" }, zFraction = 0.17f, persistent = true };
                case InteriorStyle.School:
                    return new NPCTemplate { name = "Teacher", greeting = "Ready to learn?", offerings = new[] { "Take a lesson", "Ask a question" }, zFraction = -0.33f };
                case InteriorStyle.Library:
                    return new NPCTemplate { name = "Librarian", greeting = "Looking for something?", offerings = new[] { "Search archives", "Request a book" }, zFraction = 0.25f };
                case InteriorStyle.Warehouse:
                    return new NPCTemplate { name = "Foreman", greeting = "Everything's in order.", offerings = new[] { "Check inventory", "Place an order" }, zFraction = 0.25f };
                default:
                    return default;
            }
        }

        public virtual NPCTemplate GetZoneOverrideNPC(Zone zone)
        {
            switch (zone)
            {
                case Zone.Bank:
                    return new NPCTemplate { name = "Banker", greeting = "How may I assist you?", offerings = new[] { "Make a deposit", "Apply for a loan" }, zFraction = -0.17f, persistent = true };
                case Zone.Courthouse:
                    return new NPCTemplate { name = "Judge", greeting = "Order in the court.", offerings = new[] { "File a claim", "Review documents" }, zFraction = -0.17f };
                default:
                    return default;
            }
        }

        public virtual bool IsFeminine(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) return false;
            return prefabName.Contains("Woman") || prefabName.Contains("Girl") || prefabName.Contains("Cowgirl") || prefabName.Contains("Female");
        }
    }
}

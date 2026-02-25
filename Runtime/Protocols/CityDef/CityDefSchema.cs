using System;

namespace OpenClawWorlds.Protocols
{
    // ═══════════════════════════════════════════════════════════════════
    //  CityDef Schema — JSON types for AI world-building
    //
    //  LLMs may output either the canonical flat format OR nested variants.
    //  Both are supported:
    //    Canonical:  { "side":"Left", "zPos":-20, "streetIndex":0 }
    //    LLM style:  { "position":{"x":-5,"z":-20} }
    // ═══════════════════════════════════════════════════════════════════

    [Serializable]
    public class CityPosDef { public float x; public float z; }

    [Serializable]
    public class CityWanderingNpcDef { public string prefab; }

    [Serializable]
    public class CityPaletteDef
    {
        public string roof;
        public string walls;
        public string trim;
    }

    [Serializable]
    public class CityDef
    {
        public string name;
        public bool edit;              // true = replace existing town with same name
        public float worldX;           // absolute positioning (0 = use player-relative)
        public float worldZ;
        public CityStreetDef[] streets;
        public CityBuildingDef[] buildings;
        public CityNPCDef[] npcs;
        public CityPropDef[] props;
        // LLM alternatives that get normalized:
        public CityWanderingNpcDef[] wanderingNpcs;
        public CityPaletteDef palette;
    }

    [Serializable]
    public class CityStreetDef
    {
        public string name;
        public string zone;
        public float centerX;
        public float centerZ;
        public float length;
        // LLM alternative: "points":[{"x":0,"z":-50},{"x":0,"z":50}]
        public CityPosDef[] points;
    }

    [Serializable]
    public class CityBuildingDef
    {
        public string name;
        public string zone;
        public string side;
        public float zPos;
        public float width;
        public float height;
        public float depth;
        public float[] color;
        public string interior;
        public int streetIndex;
        public string agentId;
        // LLM alternative: "position":{"x":-5,"z":-40}
        public CityPosDef position;
    }

    [Serializable]
    public class CityNPCDef
    {
        public string prefab;
        public string name;
        public string role;
        public string greeting;
        public string personality;
        public float x;
        public float z;
        public float speed;
        public float radius;
        public CityPosDef position;
    }

    [Serializable]
    public class CityPropDef
    {
        public string type;
        public float x;
        public float z;
        public float yaw;
        public float height;
        public float scale;
        public CityPosDef position;
    }

    [Serializable]
    public class SavedCity
    {
        public string cityJson;
        public float originX, originY, originZ;
    }
}

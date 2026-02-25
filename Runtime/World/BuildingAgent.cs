using UnityEngine;

namespace OpenClawWorlds.World
{
    /// <summary>
    /// Stores an OpenClaw agent ID on a building GameObject.
    /// Routes NPC interactions to this agent's session.
    /// </summary>
    public class BuildingAgent : MonoBehaviour
    {
        public string agentId;
    }
}

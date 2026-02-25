using UnityEngine;

namespace OpenClawWorlds.Agents
{
    /// <summary>
    /// Attached to NPC GameObjects. Stores identity, offerings, and agent binding.
    /// </summary>
    public class NPCData : MonoBehaviour
    {
        public string npcName;
        public string greeting;
        public string[] offerings;
        public string agentId;

        /// <summary>
        /// Persistent NPCs get their own dedicated agent with full memory.
        /// Disposable NPCs share rotating pool slots that get re-skinned.
        /// </summary>
        public bool persistent;

        public void Init(string name, string greet, string[] items, string agent = null, bool isPersistent = false)
        {
            npcName = name;
            greeting = greet;
            offerings = items;
            agentId = agent;
            persistent = isPersistent;
        }

        public bool HasAgent => !string.IsNullOrEmpty(agentId);
    }
}

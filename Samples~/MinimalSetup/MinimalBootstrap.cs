using UnityEngine;
using OpenClawWorlds;
using OpenClawWorlds.Gateway;
using OpenClawWorlds.Agents;
using OpenClawWorlds.World;

namespace OpenClawWorlds.Samples
{
    /// <summary>
    /// Minimal getting-started example.
    /// Creates a floor, player, one NPC agent, and connects to the OpenClaw gateway.
    /// Attach this to an empty GameObject in your scene and hit Play.
    ///
    /// SETUP:
    /// 1. Install the OpenClaw gateway: npm install -g @anthropic-ai/claw
    /// 2. Start it: claw gateway --api-key YOUR_API_KEY
    /// 3. Set the gateway URL below (default: ws://localhost:3001)
    /// 4. Hit Play in Unity
    /// </summary>
    public class MinimalBootstrap : MonoBehaviour
    {
        [Header("Gateway")]
        [Tooltip("WebSocket URL of the OpenClaw gateway")]
        public string gatewayUrl = "ws://localhost:3001";

        [Header("Agent")]
        [Tooltip("Primary agent ID (must match a running OpenClaw agent)")]
        public string primaryAgentId = "default";

        [Header("NPC")]
        [Tooltip("Name of the demo NPC")]
        public string npcName = "Shopkeeper";
        [Tooltip("Greeting shown when the player approaches")]
        public string npcGreeting = "Welcome! What can I help you with?";

        void Start()
        {
            Debug.Log("[MinimalBootstrap] Starting OpenClaw Worlds minimal setup...");

            // 1. Configure the agent pool
            AgentPool.PrimaryAgentId = primaryAgentId;

            // 2. Build a simple ground plane
            BuildGround();

            // 3. Spawn one NPC with an agent
            SpawnNPC();

            // 4. Connect to the gateway
            ConnectGateway();

            Debug.Log("[MinimalBootstrap] Setup complete. Walk up to the NPC to interact.");
        }

        void BuildGround()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Ground";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(5f, 1f, 5f);
            var renderer = floor.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material = TownMaterials.QuickMat(new Color(0.4f, 0.35f, 0.25f));
        }

        void SpawnNPC()
        {
            var npc = new GameObject(npcName);
            npc.transform.position = new Vector3(0, 0, 3f);

            // Visible capsule
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Body";
            capsule.transform.SetParent(npc.transform);
            capsule.transform.localPosition = new Vector3(0, 1f, 0);
            capsule.transform.localScale = new Vector3(0.4f, 0.8f, 0.4f);
            var r = capsule.GetComponent<Renderer>();
            if (r != null) r.material = TownMaterials.QuickMat(new Color(0.8f, 0.6f, 0.4f));

            // Interaction trigger
            var col = npc.AddComponent<BoxCollider>();
            col.size = new Vector3(2f, 2.5f, 2f);
            col.center = new Vector3(0, 1f, 0);
            col.isTrigger = true;

            // Interactable — subscribe to the static event in your game code
            var interactable = npc.AddComponent<Interactable>();
            interactable.Init(InteractableType.NPC, $"[E] Talk to {npcName}", Zone.MainStreet);

            // NPC data
            var data = npc.AddComponent<NPCData>();
            data.Init(npcName, npcGreeting,
                new[] { "Chat", "Ask a question" },
                agentId: null,
                persistent: true);

            // Agent pool (singleton)
            if (AgentPool.Instance == null)
                npc.AddComponent<AgentPool>();

            Debug.Log($"[MinimalBootstrap] NPC '{npcName}' spawned at {npc.transform.position}");
        }

        void ConnectGateway()
        {
            // Set gateway URL
            AIConfig.GatewayUrl = gatewayUrl;

            // Create the client
            var clientGO = new GameObject("OpenClawClient");
            clientGO.AddComponent<OpenClawClient>();

            Debug.Log($"[MinimalBootstrap] Connecting to gateway at {gatewayUrl}...");
        }
    }
}

using UnityEngine;
using OpenClawWorlds;
using OpenClawWorlds.Gateway;
using OpenClawWorlds.Agents;
using OpenClawWorlds.World;
using OpenClawWorlds.Player;
using OpenClawWorlds.UI;

namespace OpenClawWorlds.Samples
{
    /// <summary>
    /// Minimal getting-started example.
    /// Creates a floor, one NPC agent, a chat UI, and connects to the OpenClaw gateway.
    /// Attach this to an empty GameObject in your scene, hit Play, press Tab to chat.
    ///
    /// SETUP:
    /// 1. Install OpenClaw: curl -fsSL https://openclaw.ai/install.sh | bash
    /// 2. Run onboarding: openclaw onboard
    /// 3. Gateway runs on ws://127.0.0.1:18789 by default
    /// 4. Hit Play in Unity, press Tab to chat
    /// </summary>
    public class MinimalBootstrap : MonoBehaviour
    {
        [Header("Gateway")]
        [Tooltip("WebSocket URL of the OpenClaw gateway")]
        public string gatewayUrl = "ws://127.0.0.1:18789";

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

            // 3. Spawn the player (WASD movement)
            SpawnPlayer();

            // 4. Spawn one NPC with an agent
            SpawnNPC();

            // 5. Connect to the gateway
            ConnectGateway();

            // 6. Add the built-in chat UI (press Tab to open)
            CreateChatUI();

            Debug.Log("[MinimalBootstrap] Setup complete. Press Tab to chat!");
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

        void SpawnPlayer()
        {
            var player = new GameObject("Player");
            player.transform.position = new Vector3(0, 0.1f, -2f);

            // Visible capsule body
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "PlayerBody";
            body.transform.SetParent(player.transform);
            body.transform.localPosition = new Vector3(0, 1f, 0);
            body.transform.localScale = new Vector3(0.4f, 0.8f, 0.4f);
            // Remove the capsule's default collider (CharacterController handles collision)
            var bodyCol = body.GetComponent<Collider>();
            if (bodyCol != null) Destroy(bodyCol);
            var r = body.GetComponent<Renderer>();
            if (r != null) r.material = TownMaterials.QuickMat(new Color(0.3f, 0.5f, 0.8f));

            // WASD controller
            player.AddComponent<SimplePlayerController>();

            // Camera setup
            var cam = Camera.main;
            if (cam != null)
                cam.backgroundColor = new Color(0.15f, 0.18f, 0.25f);

            Debug.Log("[MinimalBootstrap] Player spawned with WASD controls. Right-click to look around.");
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
                agent: null,
                isPersistent: true);

            // Agent pool (singleton)
            if (AgentPool.Instance == null)
                npc.AddComponent<AgentPool>();

            Debug.Log($"[MinimalBootstrap] NPC '{npcName}' spawned at {npc.transform.position}");
        }

        void ConnectGateway()
        {
            // AIConfig must exist before OpenClawClient — the gateway
            // connection reads its URL and token from AIConfig.Instance.
            var configGO = new GameObject("AIConfig");
            var config = configGO.AddComponent<AIConfig>();
            config.gatewayWsUrl = gatewayUrl;
            config.agentId = primaryAgentId;

            // Create the client (auto-connects via GatewayConnection)
            var clientGO = new GameObject("OpenClawClient");
            clientGO.AddComponent<OpenClawClient>();

            Debug.Log($"[MinimalBootstrap] Connecting to gateway at {gatewayUrl}...");
        }

        void CreateChatUI()
        {
            var chatGO = new GameObject("OpenClawChat");
            chatGO.AddComponent<OpenClawChatUI>();
        }
    }
}

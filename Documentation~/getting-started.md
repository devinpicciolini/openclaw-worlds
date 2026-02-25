# Getting Started with OpenClaw Worlds

OpenClaw Worlds is a Unity SDK that drops AI agents into any Unity project. Agents build worlds, interact with players, and remember everything across sessions.

This guide walks you through installation, gateway setup, and your first interactive scene.

## Requirements

- Unity 2021.3 or later
- OpenClaw Gateway (download from [https://openclaw.ai](https://openclaw.ai))
- An OpenClaw API key

---

## 1. Installation

### Via Unity Package Manager (Git URL)

1. Open **Window > Package Manager** in Unity.
2. Click **+** > **Add package from git URL**.
3. Paste:

```
https://github.com/devinpicciolini/openclaw-worlds.git
```

4. Click **Add**. Unity will clone the repository and register the package.

### Via Local Path (for development)

If you have cloned the repository locally:

1. Open **Window > Package Manager**.
2. Click **+** > **Add package from disk**.
3. Navigate to your local clone and select `package.json`.

### Verify Installation

After installation, confirm the package appears as **OpenClaw Worlds** (version 0.1.0) in the Package Manager. You should see two samples available for import:

- **Minimal Setup** -- bare minimum: one agent, one room, one chat window.
- **Western Frontier** -- full 3D western frontier reference implementation.

---

## 2. Setting Up the OpenClaw Gateway

The gateway is a local process that bridges your Unity client to OpenClaw's AI backend over WebSocket + JSON-RPC.

### Install OpenClaw

```bash
curl -fsSL https://openclaw.ai/install.sh | bash
```

### Run Onboarding

```bash
openclaw onboard
```

The onboarding wizard walks you through API key configuration, workspace setup, and installs the gateway as a background daemon. Once complete, the gateway listens on `ws://127.0.0.1:18789` by default.

You can verify the gateway is running with:

```bash
openclaw gateway status
```

### Configuration File (Optional)

You can also create `StreamingAssets/ai_config.json` in your Unity project to override gateway settings:

```json
{
  "gatewayToken": "your-token-here",
  "gatewayWsUrl": "ws://127.0.0.1:18789",
  "agentId": "default"
}
```

The SDK loads this file at runtime via `AIConfig`. Inspector values are used as fallbacks when the file is missing.

---

## 3. Creating Your First Scene

The fastest path is the `MinimalBootstrap` component.

1. Create a new Unity scene.
2. Add an empty GameObject and name it `Bootstrap`.
3. Attach the `MinimalBootstrap` component (found under `OpenClawWorlds.Samples`).
4. In the Inspector, set:
   - **Gateway Url**: `ws://127.0.0.1:18789` (or your gateway address)
   - **Primary Agent Id**: `default`
   - **NPC Name**: any name you like
   - **NPC Greeting**: the line spoken when the player approaches

5. Press **Play**.

`MinimalBootstrap` does four things on `Start()`:

```csharp
void Start()
{
    // 1. Configure the agent pool
    AgentPool.PrimaryAgentId = primaryAgentId;

    // 2. Build a simple ground plane
    BuildGround();

    // 3. Spawn one NPC with an agent
    SpawnNPC();

    // 4. Connect to the gateway
    ConnectGateway();
}
```

You should see console output confirming the WebSocket connection and NPC spawn.

---

## 4. Connecting to the Gateway

If you are building your own bootstrap instead of using `MinimalBootstrap`, create the client manually:

```csharp
using OpenClawWorlds.Gateway;

// AIConfig must exist BEFORE OpenClawClient — the gateway
// reads its URL and token from AIConfig.Instance.
var configGO = new GameObject("AIConfig");
var config = configGO.AddComponent<AIConfig>();
config.gatewayWsUrl = "ws://127.0.0.1:18789";
config.agentId = "default";

// Create the client (auto-connects via GatewayConnection)
var clientGO = new GameObject("OpenClawClient");
clientGO.AddComponent<OpenClawClient>();
```

`OpenClawClient` is a singleton. Once connected, access it anywhere via `OpenClawClient.Instance`.

### Connection Lifecycle

1. `GatewayConnection` opens a WebSocket to the URL in `AIConfig`.
2. The gateway sends a `connect.challenge` event.
3. `OpenClawClient` responds with a connect handshake (protocol version, scopes, auth token).
4. On success, `IsConnected` becomes `true`.
5. If the connection drops, the auto-reconnect loop retries every 3 seconds.

### Checking Connection State

```csharp
if (OpenClawClient.Instance != null && OpenClawClient.Instance.IsConnected)
{
    // Ready to send messages
}
```

---

## 5. Adding an NPC with an Agent

Every interactive NPC needs three components:

- **`Interactable`** -- trigger collider that fires events when the player enters range.
- **`NPCData`** -- stores the NPC's name, greeting, offerings, and agent binding.
- **`AgentPool`** (singleton) -- manages agent lifecycle on the gateway.

```csharp
using OpenClawWorlds;
using OpenClawWorlds.World;
using OpenClawWorlds.Agents;

var npc = new GameObject("Bartender");
npc.transform.position = new Vector3(0, 0, 5f);

// Interaction trigger
var col = npc.AddComponent<BoxCollider>();
col.size = new Vector3(2f, 2.5f, 2f);
col.center = new Vector3(0, 1f, 0);
col.isTrigger = true;

// Interactable component
var interactable = npc.AddComponent<Interactable>();
interactable.Init(InteractableType.NPC, "[E] Talk to Bartender", Zone.Saloon);

// NPC identity and offerings
var data = npc.AddComponent<NPCData>();
data.Init(
    name: "Bartender",
    greet: "What'll it be?",
    items: new[] { "Chat", "Order a drink" },
    agent: null,       // assigned dynamically by AgentPool
    isPersistent: true // persistent NPCs get dedicated agent memory
);

// Ensure AgentPool singleton exists
if (AgentPool.Instance == null)
    npc.AddComponent<AgentPool>();
```

**Persistent vs. Disposable NPCs**: Persistent NPCs (`persistent: true`) get their own dedicated agent ID and memory file that survives between sessions. Disposable NPCs share a rotating pool slot that gets re-skinned with each conversation.

---

## 6. Handling Interactions

The SDK uses a static event subscription pattern. Subscribe to events on `Interactable` to handle player interactions in your game code:

```csharp
using OpenClawWorlds;
using OpenClawWorlds.World;
using OpenClawWorlds.Agents;

void OnEnable()
{
    Interactable.OnNPCInteract += HandleNPCInteract;
    Interactable.OnDoorInteract += HandleDoorInteract;
    Interactable.OnPickupInteract += HandlePickupInteract;
    Interactable.OnCustomInteract += HandleCustomInteract;
}

void OnDisable()
{
    Interactable.OnNPCInteract -= HandleNPCInteract;
    Interactable.OnDoorInteract -= HandleDoorInteract;
    Interactable.OnPickupInteract -= HandlePickupInteract;
    Interactable.OnCustomInteract -= HandleCustomInteract;
}

void HandleNPCInteract(Interactable interactable, GameObject actor)
{
    var npcData = interactable.GetComponent<NPCData>();
    if (npcData == null) return;

    Debug.Log($"Player approached {npcData.npcName}: {npcData.greeting}");

    // Acquire an agent for this NPC
    AgentPool.Instance.AcquireAgent(npcData,
        onReady: (agentId) =>
        {
            Debug.Log($"Agent ready: {agentId}");
            // Now you can send messages to this agent
        },
        onError: (err) =>
        {
            Debug.LogError($"Agent error: {err}");
        });
}

void HandleDoorInteract(Interactable interactable, GameObject actor)
{
    if (interactable.HasTeleport)
    {
        actor.transform.position = interactable.TeleportPosition;
        actor.transform.rotation = Quaternion.Euler(0, interactable.TeleportYaw, 0);
    }
}
```

The available event types are:

| Event                              | Fires When                          |
|------------------------------------|-------------------------------------|
| `Interactable.OnNPCInteract`       | Player interacts with an NPC        |
| `Interactable.OnDoorInteract`      | Player interacts with a door        |
| `Interactable.OnPickupInteract`    | Player picks up an item             |
| `Interactable.OnCustomInteract`    | Any other `InteractableType`        |

All events pass `(Interactable interactable, GameObject actor)` as arguments.

---

## 7. Sending Chat Messages

Once an agent is acquired, send messages through `OpenClawClient`:

```csharp
using OpenClawWorlds.Gateway;

// Send to the primary (main) agent
var messages = new List<ChatMessage>
{
    new ChatMessage("user", "Tell me about this town.")
};

OpenClawClient.Instance.SendMessage(
    systemPrompt: "You are a helpful guide.",
    messages: messages,
    onComplete: (response) =>
    {
        Debug.Log($"Agent says: {response}");
    },
    onError: (error) =>
    {
        Debug.LogError($"Error: {error}");
    });

// Send to a specific NPC agent
OpenClawClient.Instance.SendNPCMessage(
    agentId: "npc-bartender",
    message: "What do you have on tap?",
    onComplete: (response) =>
    {
        Debug.Log($"Bartender says: {response}");
    },
    onError: (error) =>
    {
        Debug.LogError($"Error: {error}");
    });
```

### Generic RPC Requests

For advanced use, send arbitrary JSON-RPC requests to the gateway:

```csharp
OpenClawClient.Instance.SendGatewayRequest(
    method: "agents.files.set",
    paramsJson: "{\"agentId\":\"npc-bartender\",\"name\":\"notes.md\",\"content\":\"Player likes whiskey.\"}",
    onResponse: (response) =>
    {
        Debug.Log($"Gateway response: {response}");
    });
```

---

## 8. Receiving Responses

Responses flow through the gateway's event protocol. `OpenClawClient` handles the full lifecycle internally:

1. **`chat.send` accepted** -- the gateway confirms it received the message.
2. **`agent` stream events** -- incremental text arrives as the agent generates its response.
3. **`lifecycle end`** -- the agent finishes. The accumulated text is delivered to your `onComplete` callback.
4. **Fallback** -- if the stream completes without text, the client fetches the last assistant message from `chat.history`.

You do not need to handle the streaming protocol yourself. The `onComplete` callback receives the final, complete response string.

### Processing Structured Responses

Agent responses may contain embedded protocol blocks. The SDK provides processors for these:

```csharp
using OpenClawWorlds.Protocols;

void HandleAgentResponse(string response)
{
    // Check for CityDef blocks (world building)
    // Your CityDef processing logic here

    // Check for BehaviorDef blocks (runtime effects)
    string behaviorSummary = BehaviorEngine.ProcessResponse(response);
    if (behaviorSummary != null)
        Debug.Log($"Behavior applied: {behaviorSummary}");

    // Check for C# code blocks (hot reload)
    string codeSummary = HotReloadBridge.ProcessResponse(response);
    if (codeSummary != null)
        Debug.Log($"Code written: {codeSummary}");
}
```

---

## 9. Next Steps

Now that you have a working scene with a connected agent, explore the SDK's deeper capabilities:

- **[CityDef Schema](citydef-schema.md)** -- define entire towns in JSON. Streets, buildings, props, NPCs. The agent builds them at runtime.
- **[BehaviorDef Schema](behaviordef-schema.md)** -- modify weather, lighting, physics, and particles through JSON. No compilation needed.
- **[Asset Pack Integration](asset-pack-integration.md)** -- plug in custom 3D assets via the `IAssetMapper` interface. The SDK works with primitive geometry by default.
- **Hot Reload Bridge** -- agents can write C# code that compiles live in the Unity Editor via `HotReloadBridge`.
- **Agent Memory** -- persistent NPCs accumulate memory across sessions in `~/.openclaw/npc-memories/`.
- **TownStreamer** -- distance-based LOD streaming for AI-generated towns. Attach `TownStreamer` to manage GPU/CPU budget automatically.
- **Audit Pipeline** -- validate AI-generated CityDef JSON before building. Catches malformed zones, missing fields, and out-of-bounds coordinates.

Import the **Minimal Setup** or **Western Frontier** sample from the Package Manager for complete working examples.

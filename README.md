# OpenClaw Worlds

**AI agents that live inside Unity. They build towns, run businesses, remember everything, and do real work.**

OpenClaw Worlds is a Unity SDK that connects [OpenClaw](https://openclaw.ai) AI agents to your game through structured JSON protocols. Agents don't just chat — they generate entire towns from JSON, modify weather and physics at runtime, hot-reload C# code, and accumulate persistent memory across sessions. All at runtime. All from conversation.

```
You: "Build me a frontier town with a saloon, bank, and sheriff's office"
Agent: *spawns a full town with buildings, interiors, NPCs, street lamps, and hitching posts*
Agent: *each building has a furnished interior with a shopkeeper who remembers your conversations*
```

---

## Quick Start

### 1. Install OpenClaw

```bash
curl -fsSL https://openclaw.ai/install.sh | bash
openclaw onboard
```

This installs OpenClaw and runs the onboarding wizard (API key, workspace, gateway setup). The gateway listens on `ws://127.0.0.1:18789` by default.

### 2. Add the SDK to Unity

Add to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.openclaw.worlds": "https://github.com/devinpicciolini/openclaw-worlds.git"
  }
}
```

Or clone locally and reference by path: `"com.openclaw.worlds": "file:../openclaw-worlds"`

### 3. Configure your gateway token

Create `Assets/StreamingAssets/ai_config.json`:

```json
{
  "gatewayToken": "your-token-here",
  "gatewayWsUrl": "ws://127.0.0.1:18789",
  "agentId": "your-agent-name",
  "assistantName": "Silas"
}
```

You can find your gateway token by running `openclaw doctor` or checking `~/.openclaw/openclaw.json`.

### 4. Add MinimalBootstrap and hit Play

1. Create an empty GameObject in your scene
2. Attach `MinimalBootstrap` (from `Samples~/MinimalSetup/`)
3. Hit Play
4. **Press Tab to chat**

That's it. MinimalBootstrap creates the ground, NPC, gateway connection, and a built-in chat UI automatically. You'll see "Connected to OpenClaw" in the top-left, and Tab opens the chat panel on the right side of the screen.

---

## What Does It Actually Do?

The SDK implements three protocols that let AI agents control your Unity world:

### CityDef — JSON that builds worlds

An agent returns a JSON block, and an entire town appears in your scene. Streets, buildings with furnished interiors, props, wandering NPCs — all from one structured response. Buildings use real 3D prefabs when you have an asset pack, or colored primitives when you don't.

### BehaviorDef — JSON that changes the rules

Same idea, different protocol. Agent returns JSON, and the weather changes. Rain particles follow the player, fog rolls in, torches flicker, gravity shifts. No compilation, no scene reload — same-frame execution.

### C# Hot Reload — code that compiles live

Agent writes C# code blocks, and they compile and run in the editor. The most dangerous protocol. The most fun.

---

## How It Works

```
┌──────────────────────────────────────────────────┐
│                  Your Game                        │
│  ┌───────────┐  ┌───────────┐  ┌──────────────┐  │
│  │  Player   │  │  Chat UI  │  │  Game Logic  │  │
│  │ Controller│  │(built-in) │  │   (yours)    │  │
│  └─────┬─────┘  └─────┬─────┘  └──────┬───────┘  │
│        │              │               │           │
├────────┼──────────────┼───────────────┼───────────┤
│        │     OpenClaw Worlds SDK      │           │
│  ┌─────▼──────────────────────────────▼────────┐  │
│  │              Gateway Layer                   │  │
│  │  OpenClawClient <-> GatewayConnection       │  │
│  └───────────────────┬─────────────────────────┘  │
│                      │                            │
│  ┌───────────────────▼─────────────────────────┐  │
│  │              Protocols                       │  │
│  │  CityDef     BehaviorDef     HotReload      │  │
│  │  (JSON->Town) (JSON->FX)   (C#->Compile)    │  │
│  └───────────────────┬─────────────────────────┘  │
│                      │                            │
│  ┌───────────────────▼─────────────────────────┐  │
│  │              World Builders                  │  │
│  │  BuildingBuilder  PropBuilder  NPCBuilder    │  │
│  │  InteriorBuilder  TownStreamer               │  │
│  └───────────────────┬─────────────────────────┘  │
│                      │                            │
│  ┌───────────────────▼─────────────────────────┐  │
│  │              Agents                          │  │
│  │  AgentPool (lifecycle, identity, memory)     │  │
│  └─────────────────────────────────────────────┘  │
│                                                   │
└───────────────────────────────────────────────────┘
          │
          v WebSocket (JSON-RPC)
┌──────────────────────┐
│   OpenClaw Gateway    │
│  (openclaw gateway)   │
└──────────────────────┘
```

---

## Built-in Chat UI

The SDK ships with `OpenClawChatUI` — a zero-setup IMGUI chat window. Press **Tab** to open, type a message, press **Enter** to send. The panel docks to the right so you can see your scene while chatting.

MinimalBootstrap creates it automatically. For your own game, add it to any GameObject:

```csharp
using OpenClawWorlds.UI;

gameObject.AddComponent<OpenClawChatUI>();
```

It talks to whatever agent is configured in `ai_config.json`. Replace it with your own UI when you're ready for production.

---

## Core Concepts

### No Art Required (But Art Makes It Better)

Every building, NPC, and prop has a **primitive geometry fallback**. Buildings are colored cubes, NPCs are capsules, street lamps are cylinders with point lights. You can prototype an entire game with zero art assets.

When you're ready for real visuals, implement `IAssetMapper` to plug in any asset pack:

```csharp
using OpenClawWorlds;
using OpenClawWorlds.World;

public class MyAssetMapper : DefaultAssetMapper
{
    public override string GetBuildingPrefab(BuildingDef def)
    {
        return def.zone switch
        {
            Zone.Saloon => "MyPack_Saloon_01",
            Zone.Bank   => "MyPack_Bank_01",
            _           => null  // falls back to primitives
        };
    }
}

// Register before spawning any towns
BuildingBuilder.AssetMapper = new MyAssetMapper();
```

### Event-Based Interactions

No singletons to fight. Subscribe to static events:

```csharp
// Player talks to an NPC
Interactable.OnNPCInteract += (interactable, actor) =>
{
    var npc = interactable.GetComponent<NPCData>();
    OpenMyChatWindow(npc);
};

// Player enters a door
Interactable.OnDoorInteract += (interactable, actor) =>
{
    TeleportPlayer(interactable.TeleportPosition, interactable.TeleportYaw);
};

// Player enters a new zone
ZoneTrigger.OnZoneEntered += (zone) =>
{
    UpdateMinimap(zone);
};
```

### TARDIS Interiors

Building interiors are 3.5x larger than the exterior — they feel spacious from inside while looking proportional from outside. Each `InteriorStyle` gets auto-generated furniture: bars and stools in saloons, pews and altars in churches, anvils and forges in smithies, jail cells with bars in the sheriff's office. There are 12 fully furnished interior styles.

### Persistent NPC Memory

Persistent NPCs get dedicated agent IDs and memory files that survive between sessions:

```
~/.openclaw/
├── npc-memories/
│   ├── bartender.md       # Every conversation the bartender has had
│   ├── sheriff.md         # Every conversation the sheriff has had
│   └── shopkeeper.md      # Every conversation the shopkeeper has had
└── workspace-npc-bartender/
    ├── memory/            # Agent-local working memory
    └── skills/            # Symlinked global skills
```

Disposable NPCs share a rotating pool slot that gets re-skinned per conversation.

### Agent Lifecycle

```csharp
using OpenClawWorlds.Agents;

// Configure the agent pool
AgentPool.PrimaryAgentId = "my-agent";
AgentPool.DisposableSlotId = "npc-townfolk";

// Acquire an agent when player approaches an NPC
AgentPool.Instance.AcquireAgent(npcData,
    onReady: (agentId) => { /* agent is live, send messages */ },
    onError: (err)     => { /* connection or creation failed */ });

// Release when player walks away
AgentPool.Instance.ReleaseAgent();
```

### Processing Structured Responses

Agent responses can contain embedded protocol blocks. The SDK detects and executes them:

```csharp
using OpenClawWorlds.Protocols;

void HandleAgentResponse(string response)
{
    // BehaviorDef blocks -> runtime effects (weather, lighting, physics)
    string behaviorSummary = BehaviorEngine.ProcessResponse(response);

    // C# code blocks -> compile and execute in the editor
    string codeSummary = HotReloadBridge.ProcessResponse(response);
}
```

---

## Configuration Reference

### Gateway Connection

```csharp
// Option A: StreamingAssets/ai_config.json (recommended)
{
  "gatewayToken": "your-token",
  "gatewayWsUrl": "ws://127.0.0.1:18789",
  "agentId": "your-agent",
  "assistantName": "Silas"
}

// Option B: AIConfig MonoBehaviour
var config = gameObject.AddComponent<AIConfig>();
config.gatewayWsUrl = "ws://127.0.0.1:18789";
config.gatewayToken = "your-token";
config.agentId = "default";

// Option C: Set values in the Unity Inspector
```

### World Builders

```csharp
BuildingBuilder.AssetMapper = new MyAssetMapper();   // Custom prefab mapping
InteriorBuilder.InteriorScale = 3.5f;                // TARDIS interior multiplier
InteriorActivator.ActivateDistance = 8f;              // Interior toggle distance
PrefabLibrary.SearchPaths = new[] { "MyPack/", "" }; // Prefab search paths
```

### CityDef Spawner

```csharp
CityDefSpawner.IsForbiddenZone = (pos) => /* your map boundaries */;
CityDefSpawner.NudgeOrigin = (pos) => /* adjust spawn position */;
CityDefSpawner.MaxWorldRadius = 800f;  // Safety net for absurd LLM coordinates
```

### Agent Pool

```csharp
AgentPool.PrimaryAgentId = "my-agent";
AgentPool.DisposableSlotId = "npc-townfolk";
AgentPool.CustomIdentityBuilder = (name, greeting) => "...";
AgentPool.CustomBootstrap = (agentId) => { /* custom setup */ };
```

---

## Package Structure

```
Runtime/
├── Core/           # Enums, materials, prefab loading, animator hashes
├── Gateway/        # WebSocket transport + JSON-RPC client
├── Protocols/      # CityDef, BehaviorDef, HotReload bridges
├── Agents/         # Agent lifecycle, memory, NPC data
├── World/          # Building, prop, NPC, interior builders + IAssetMapper
├── UI/             # Built-in chat UI (OpenClawChatUI)
├── Validation/     # CityDef audit pipeline
└── Utilities/      # JSON parsing helpers

Samples~/
├── MinimalSetup/   # One scene, one NPC, one agent -- start here
└── WesternFrontier/# Full reference implementation notes

Documentation~/
├── getting-started.md        # Installation -> first agent
├── citydef-schema.md         # Full CityDef JSON reference
├── behaviordef-schema.md     # Full BehaviorDef JSON reference
└── asset-pack-integration.md # Plugging in custom art packs
```

---

## Documentation

| Doc | What it covers |
|-----|---------------|
| **[Getting Started](Documentation~/getting-started.md)** | Installation, gateway setup, first interactive scene |
| **[CityDef Schema](Documentation~/citydef-schema.md)** | Complete JSON schema for town generation |
| **[BehaviorDef Schema](Documentation~/behaviordef-schema.md)** | JSON schema for runtime effects |
| **[Asset Pack Integration](Documentation~/asset-pack-integration.md)** | How to plug in any 3D asset pack via `IAssetMapper` |

---

## Requirements

- Unity 2021.3 LTS or newer
- .NET Standard 2.1
- OpenClaw Gateway running (for agent features)
- Zero third-party dependencies

## License

MIT -- see [LICENSE](LICENSE).

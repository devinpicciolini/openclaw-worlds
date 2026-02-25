# OpenClaw Worlds

> Drop OpenClaw agents into any Unity project. They build worlds, do real work, and remember everything.

A Unity Package Manager SDK that connects [OpenClaw](https://openclaw.ai) AI agents to your game. Agents communicate through structured protocols to spawn buildings, modify behaviors, hot-reload code, and maintain persistent memory — all at runtime.

## Features

- **CityDef Protocol** — Agents generate JSON that spawns entire towns: buildings, interiors, NPCs, props
- **BehaviorDef Protocol** — Agents modify runtime behaviors: weather, lighting, physics, particles
- **C# Hot Reload** — Agents write code that compiles and runs in the live editor
- **Persistent Memory** — NPCs remember every conversation across sessions
- **Pluggable Assets** — Works with primitive geometry out of the box; plug in any art pack via `IAssetMapper`
- **Event-Based Architecture** — Subscribe to interaction events instead of fighting singletons
- **Zero Dependencies** — Pure C#, no third-party packages required

## Quick Start

### 1. Install the Package

Add to your `manifest.json`:

```json
{
  "dependencies": {
    "com.openclaw.worlds": "https://github.com/devinpicciolini/openclaw-worlds.git"
  }
}
```

Or clone locally and use `"com.openclaw.worlds": "file:../openclaw-worlds"`.

### 2. Start the Gateway

```bash
npm install -g @anthropic-ai/claw
claw gateway --api-key YOUR_ANTHROPIC_API_KEY
```

### 3. Add to Your Scene

```csharp
using OpenClawWorlds.Gateway;

// Connect to the gateway
AIConfig.GatewayUrl = "ws://localhost:3001";
var client = gameObject.AddComponent<OpenClawClient>();
```

### 4. Let Agents Build

```csharp
using OpenClawWorlds.Protocols;

// Agent sends CityDef JSON → town appears in your world
CityDefSpawner.Build(cityDefJson, spawnOrigin, materials);
```

See `Samples~/MinimalSetup/` for a complete working example.

## Architecture

```
┌─────────────────────────────────────────────────┐
│                  Your Game                       │
│  ┌───────────┐  ┌───────────┐  ┌─────────────┐ │
│  │ Player    │  │ Chat UI   │  │ Game Logic  │ │
│  │ Controller│  │ (yours)   │  │ (yours)     │ │
│  └─────┬─────┘  └─────┬─────┘  └──────┬──────┘ │
│        │              │               │         │
├────────┼──────────────┼───────────────┼─────────┤
│        │     OpenClaw Worlds SDK      │         │
│  ┌─────▼─────────────────────────────▼──────┐   │
│  │              Gateway Layer                │   │
│  │  OpenClawClient ←→ GatewayConnection     │   │
│  └──────────────────┬───────────────────────┘   │
│                     │                           │
│  ┌──────────────────▼───────────────────────┐   │
│  │              Protocols                    │   │
│  │  CityDef    BehaviorDef    HotReload     │   │
│  │  (JSON→Town) (JSON→FX)   (C#→Compile)   │   │
│  └──────────────────┬───────────────────────┘   │
│                     │                           │
│  ┌──────────────────▼───────────────────────┐   │
│  │              World Builders               │   │
│  │  BuildingBuilder  PropBuilder  NPCBuilder │   │
│  │  InteriorBuilder  TownStreamer            │   │
│  └──────────────────┬───────────────────────┘   │
│                     │                           │
│  ┌──────────────────▼───────────────────────┐   │
│  │              Agents                       │   │
│  │  AgentPool (lifecycle, identity, memory)  │   │
│  └──────────────────────────────────────────┘   │
│                                                 │
└─────────────────────────────────────────────────┘
         │
         ▼ WebSocket (JSON-RPC)
┌─────────────────────┐
│   OpenClaw Gateway   │
│   (claw gateway)     │
└─────────────────────┘
```

## Package Structure

```
Runtime/
├── Core/           # Types, materials, prefab loading, animator hashes
├── Gateway/        # WebSocket transport + JSON-RPC client
├── Protocols/      # CityDef, BehaviorDef, HotReload bridges
├── Agents/         # Agent lifecycle, memory, NPC data
├── World/          # Building, prop, NPC, interior builders
├── Validation/     # CityDef audit pipeline
└── Utilities/      # JSON parsing helpers

Samples~/
├── MinimalSetup/   # One scene, one NPC, one agent
└── WesternFrontier/# Reference implementation notes

Documentation~/
├── getting-started.md
├── citydef-schema.md
├── behaviordef-schema.md
└── asset-pack-integration.md
```

## Key Concepts

### Pluggable Asset Packs

The SDK works with **no art assets** — buildings are colored cubes, NPCs are capsules. To upgrade visuals, implement `IAssetMapper`:

```csharp
public class MyAssetMapper : DefaultAssetMapper
{
    public override string GetBuildingPrefab(BuildingDef def)
    {
        return def.zone switch
        {
            Zone.Saloon => "MyPack_Saloon_01",
            Zone.Bank => "MyPack_Bank_01",
            _ => null // falls back to primitive geometry
        };
    }
}

// Register it
BuildingBuilder.AssetMapper = new MyAssetMapper();
```

### Event-Based Interactions

No singletons required. Subscribe to events:

```csharp
Interactable.OnNPCInteract += (interactable, actor) => {
    var npc = interactable.GetComponentInParent<NPCData>();
    OpenMyChatWindow(npc);
};

Interactable.OnDoorInteract += (interactable, actor) => {
    TeleportPlayer(interactable.TeleportPosition, interactable.TeleportYaw);
};

ZoneTrigger.OnZoneEntered += (zone) => {
    UpdateMinimap(zone);
};
```

### Agent Memory

NPCs remember conversations across sessions. Memory persists to `~/.openclaw/npc-memories/`:

```
~/.openclaw/
├── npc-memories/
│   ├── bartender.md      # Bartender's accumulated memories
│   ├── sheriff.md        # Sheriff's accumulated memories
│   └── shopkeeper.md     # Shopkeeper's accumulated memories
└── workspace-npc-bartender/
    ├── memory/           # Agent-local memory
    └── skills/           # Symlinked global skills
```

## Configuration

### Gateway

```csharp
AIConfig.GatewayUrl = "ws://localhost:3001";    // Gateway WebSocket URL
AIConfig.DefaultModel = "claude-sonnet-4-20250514";    // Model for agents
```

### Agent Pool

```csharp
AgentPool.PrimaryAgentId = "my-agent";          // Primary agent to copy auth from
AgentPool.DisposableSlotId = "npc-townfolk";     // Shared slot for non-persistent NPCs
AgentPool.CustomIdentityBuilder = (name, greeting) => "..."; // Custom identity template
AgentPool.CustomBootstrap = (agentId) => { /* custom setup */ };
```

### World Builders

```csharp
BuildingBuilder.AssetMapper = new MyAssetMapper();  // Custom prefab mapping
InteriorBuilder.InteriorScale = 3.5f;               // TARDIS interior multiplier
InteriorActivator.ActivateDistance = 8f;             // Interior toggle distance
PrefabLibrary.SearchPaths = new[] { "MyPack/", "" }; // Prefab search paths
```

### CityDef Spawner

```csharp
CityDefSpawner.IsForbiddenZone = (pos) => /* your map boundaries */;
CityDefSpawner.NudgeOrigin = (pos) => /* adjust spawn position */;
```

## Protocols Reference

See the documentation folder for detailed schema references:

- **CityDef** — `Documentation~/citydef-schema.md` — JSON schema for town generation
- **BehaviorDef** — `Documentation~/behaviordef-schema.md` — JSON schema for runtime behaviors
- **Asset Integration** — `Documentation~/asset-pack-integration.md` — Plugging in art packs

## Requirements

- Unity 2021.3 LTS or newer
- .NET Standard 2.1
- OpenClaw Gateway running (for agent features)

## License

MIT License. See [LICENSE](LICENSE) for details.

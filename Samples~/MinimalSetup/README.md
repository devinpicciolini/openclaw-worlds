# Minimal Setup Sample

The simplest possible OpenClaw Worlds integration. One scene, one NPC, one agent.

## Prerequisites

1. **Unity 2021.3+** (LTS recommended)
2. **OpenClaw** installed and onboarded:
   ```bash
   curl -fsSL https://openclaw.ai/install.sh | bash
   openclaw onboard
   ```

## Quick Start

1. Create a new Unity scene
2. Add an empty GameObject and attach `MinimalBootstrap.cs`
3. Set the **Gateway URL** in the Inspector (default: `ws://127.0.0.1:18789`)
4. Hit **Play**
5. Walk up to the NPC capsule and press **E** to interact

## What This Creates

- A ground plane
- One NPC (capsule with `Interactable` + `NPCData` components)
- An `AgentPool` singleton
- An `OpenClawClient` that connects to your gateway

## Handling Interactions

Subscribe to the interaction events in your game code:

```csharp
using OpenClawWorlds.World;

void OnEnable()
{
    Interactable.OnNPCInteract += HandleNPCInteract;
}

void OnDisable()
{
    Interactable.OnNPCInteract -= HandleNPCInteract;
}

void HandleNPCInteract(Interactable interactable, GameObject actor)
{
    var npcData = interactable.GetComponentInParent<NPCData>();
    if (npcData == null) return;

    // Acquire an agent and start a conversation
    AgentPool.Instance.AcquireAgent(npcData,
        onReady: (agentId) => {
            Debug.Log($"Agent ready: {agentId}");
            // Send messages via OpenClawClient.Instance.SendMessage(...)
        },
        onError: (err) => Debug.LogError(err)
    );
}
```

## Next Steps

- Add a **player controller** (the SDK doesn't include one)
- Build a **chat UI** to display NPC conversations
- Try the **CityDef protocol** to let agents build entire towns
- See `Documentation~/getting-started.md` for the full guide

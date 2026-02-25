# Western Frontier Sample

This sample references the full **Pandora** game — a 3D western frontier town where OpenClaw AI agents live, build towns, do real work, and persist memory.

## About

Pandora was the original project from which the `openclaw-worlds` SDK was extracted. It demonstrates every feature of the SDK working together in a complete game:

- **CityDef Protocol**: AI agents generate JSON that spawns entire towns with buildings, props, NPCs
- **BehaviorDef Protocol**: Agents modify runtime behaviors (weather, lighting, particles, physics)
- **Hot Reload**: Agents write C# code that compiles and runs in the live game
- **Persistent Memory**: NPCs remember every conversation across sessions
- **POLYGON Western Assets**: Full 3D art integration via the `IAssetMapper` interface

## How to Use This as Reference

The SDK works with **any** Unity project and **any** art style. The Western Frontier sample shows how one project implemented:

1. **Custom `IAssetMapper`** — Maps building zones to POLYGON Western prefabs
2. **Forbidden zones** — Prevents building in mountains and rivers via `CityDefSpawner.IsForbiddenZone`
3. **Custom NPC templates** — Each NPC has a unique name, personality, and skill set
4. **Chat UI** — IMGUI-based conversation window (your project should use your own UI framework)
5. **Agent Dashboard** — Shows active agents, their tasks, and available skills

## Getting Started with Your Own Project

You don't need the Western Frontier assets. The SDK works out of the box with primitive geometry fallbacks:

1. Follow `Samples~/MinimalSetup/` for a bare-minimum integration
2. Read `Documentation~/asset-pack-integration.md` to plug in your own art
3. See `Documentation~/getting-started.md` for the full setup guide

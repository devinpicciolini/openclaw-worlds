# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-02-24

### Added

- **Gateway layer** -- WebSocket + JSON-RPC transport to the OpenClaw gateway via `GatewayConnection` and `OpenClawClient`. Auto-reconnect loop, protocol handshake, auth token support, and main-thread message dispatch.
- **CityDef protocol** -- full parse, validate, normalize, and spawn pipeline for AI-generated town definitions. Supports streets, buildings, props, and NPCs from a single JSON block. Handles LLM output variants (nested position objects, points arrays, missing zones) via `CityDefParser`.
- **BehaviorDef protocol** -- schema and runtime engine (`BehaviorEngine`) for modifying weather, lighting, particles, physics, fog, and timed effects through JSON. Same-frame execution with no compilation.
- **C# Hot Reload bridge** -- `HotReloadBridge` detects ```csharp code blocks in agent responses, writes them to `Assets/Scripts/Generated/`, and triggers `AssetDatabase.Refresh()` for live domain reload in the Unity Editor.
- **Agent pool with persistent memory** -- `AgentPool` manages NPC agent lifecycle. Persistent NPCs get dedicated agent IDs with long-term memory files in `~/.openclaw/npc-memories/`. Disposable NPCs share a rotating pool slot. Includes identity generation, workspace bootstrapping, auth copying, and skill symlinking.
- **World builders**:
  - `BuildingBuilder` -- spawns buildings from prefabs or fallback cube geometry with doors, triggers, signs, ceiling lights, and interior furniture.
  - `PropBuilder` -- master prop spawner supporting 18+ prop types (StreetLamp, Barrel, Bench, PineTree, Rock, Horse, Cart, CampFire, WaterTower, and more) with prefab-first, primitive-fallback strategy.
  - `NPCBuilder` -- spawns interior shopkeeper NPCs and outdoor wandering townsfolk with animator assignment, interaction triggers, and agent binding.
  - `InteriorBuilder` -- auto-generates interior furniture layouts based on `InteriorStyle` enum.
  - `TownStreamer` -- distance-based LOD streaming for AI-generated towns. Three tiers (Close, Medium, Far) manage lights, interiors, and full activation to preserve GPU/CPU budget.
- **IAssetMapper interface** -- pluggable asset pack system. Implement `IAssetMapper` to map zone types to prefabs, assign animator controllers, and configure NPC templates. Ships with `DefaultAssetMapper` that uses primitive geometry.
- **Event-based interaction system** -- `Interactable` component with static events (`OnNPCInteract`, `OnDoorInteract`, `OnPickupInteract`, `OnCustomInteract`). Subscribe from game code to handle player interactions without tight coupling.
- **CityDef audit/validation pipeline** -- `AuditPipeline` validates AI-generated JSON before building. Checks required fields, zone/interior enum values, side values, prefab names, and detects incorrect nested position format.
- **PrefabLibrary** -- centralized prefab loading and caching with configurable search paths, texture atlas material fix, and spawn helpers.
- **AIConfig** -- singleton configuration component with runtime config loading from `StreamingAssets/ai_config.json`. Supports gateway URL, auth token, agent ID, assistant name, and system prompt.
- **GameTypes** -- core enums (`Zone`, `InteriorStyle`, `StreetSide`, `InteractableType`) and `BuildingDef` struct with `ZoneExtensions` for display names and indoor/outdoor classification.
- **CityDefPersistence** -- save and restore AI-generated towns across sessions.
- **MinimalSetup sample** -- bare minimum example: one ground plane, one NPC agent, one gateway connection. Attach `MinimalBootstrap` to an empty GameObject and press Play.

[0.1.0]: https://github.com/devinpicciolini/openclaw-worlds/releases/tag/v0.1.0

# BehaviorDef Schema Reference

BehaviorDef is the JSON protocol for modifying runtime behavior -- weather, lighting, physics, particles, and timed effects. While CityDef builds the world, BehaviorDef changes the rules. Both are JSON. Both execute instantly. Zero compilation.

Agent responses can contain BehaviorDef blocks wrapped in triple-backtick fences:

````
```behaviordef
{
  "type": "particle",
  "name": "rain",
  "followPlayer": true,
  "particles": { ... }
}
```
````

The `BehaviorEngine` detects these blocks, parses the JSON, and creates the effect in the same frame.

---

## Top-Level Fields (`BehaviorDef`)

| Field          | Type          | Required | Description                                                       |
|----------------|---------------|----------|-------------------------------------------------------------------|
| `type`         | `string`      | Yes      | Effect type: `"particle"`, `"light"`, `"physics"`, `"fog"`, `"timer"`, `"remove"`. |
| `name`         | `string`      | No       | Unique name for this behavior. Used for removal and overwriting. Auto-generated if omitted. |
| `followPlayer` | `bool`        | No       | If `true`, the effect GameObject tracks the player's position every frame. |
| `particles`    | `ParticleDef` | No       | Particle system configuration. Used when `type` is `"particle"`.  |
| `lighting`     | `LightDef`    | No       | Light configuration. Used when `type` is `"light"`.               |
| `physics`      | `PhysicsDef`  | No       | Physics modification. Used when `type` is `"physics"`.            |
| `fog`          | `FogDef`      | No       | Fog settings. Used when `type` is `"fog"`.                        |
| `timer`        | `TimerDef`    | No       | Delayed removal of another behavior. Used when `type` is `"timer"`. |

### Behavior Lifecycle

- Each behavior is stored by `name` in an internal dictionary.
- Creating a behavior with an existing name destroys the old one first (overwrite semantics).
- Use `type: "remove"` with a `name` to explicitly destroy a behavior.
- Call `BehaviorEngine.ClearAll()` to destroy all active behaviors and reset physics/timescale.

---

## Particles (`ParticleDef`)

Controls a Unity `ParticleSystem` created at runtime.

| Field        | Type       | Default   | Description                                                    |
|--------------|------------|-----------|----------------------------------------------------------------|
| `count`      | `int`      | 500       | Maximum particle count (`main.maxParticles`).                  |
| `lifetime`   | `float`    | 2.0       | Particle lifetime in seconds.                                  |
| `speed`      | `float`    | 5.0       | Start speed of emitted particles.                              |
| `size`       | `float`    | 0.1       | Start size of each particle.                                   |
| `color`      | `float[]`  | white     | RGBA color as `[r, g, b]` or `[r, g, b, a]` (0--1 range).    |
| `gravity`    | `float`    | 0         | Gravity modifier applied to particles.                         |
| `shape`      | `string`   | `"box"`   | Emitter shape: `"box"`, `"sphere"`, `"cone"`, `"hemisphere"`. |
| `shapeScale` | `float[]`  | default   | Emitter shape scale as `[x, y, z]`.                            |
| `offset`     | `float[]`  | origin    | World position offset as `[x, y, z]`.                          |
| `rate`       | `float`    | 100       | Emission rate (particles per second).                           |
| `duration`   | `float`    | 0         | Effect duration in seconds. `0` = loop forever.                |
| `additive`   | `bool`     | false     | Use additive blending (bright effects like fire, sparks).      |

### Particle Example: Rain

```json
{
  "type": "particle",
  "name": "rain",
  "followPlayer": true,
  "particles": {
    "count": 2000,
    "lifetime": 1.5,
    "speed": 12,
    "size": 0.03,
    "color": [0.7, 0.8, 0.9, 0.6],
    "gravity": 1.0,
    "shape": "box",
    "shapeScale": [40, 1, 40],
    "offset": [0, 20, 0],
    "rate": 500
  }
}
```

### Particle Example: Campfire Sparks

```json
{
  "type": "particle",
  "name": "campfire_sparks",
  "particles": {
    "count": 200,
    "lifetime": 1.0,
    "speed": 3,
    "size": 0.05,
    "color": [1.0, 0.6, 0.1, 0.9],
    "gravity": -0.3,
    "shape": "cone",
    "offset": [0, 0.5, 0],
    "rate": 50,
    "additive": true
  }
}
```

---

## Lighting (`LightDef`)

Creates or modifies lights in the scene.

| Field        | Type       | Default      | Description                                                 |
|--------------|------------|--------------|-------------------------------------------------------------|
| `mode`       | `string`   | `"ambient"`  | Light mode: `"ambient"`, `"point"`, `"directional"`, `"pulse"`. |
| `color`      | `float[]`  | white        | RGB color as `[r, g, b]` (0--1 range).                     |
| `intensity`  | `float`    | 1.0          | Light intensity.                                            |
| `range`      | `float`    | 50.0         | Range for point lights (world units).                       |
| `pulseSpeed` | `float`    | 0            | If > 0, intensity oscillates sinusoidally at this speed.    |
| `position`   | `float[]`  | origin       | World position as `[x, y, z]`.                              |

### Light Modes

- **`ambient`** -- Sets `RenderSettings.ambientLight` and `ambientIntensity`. No Light component created.
- **`point`** -- Creates a point light at the specified position.
- **`directional`** -- Creates a directional light (position is used for the GameObject but direction comes from rotation).
- **`pulse`** -- Creates a point light with a `PulseLight` component that oscillates intensity using `sin(time * pulseSpeed)`.

### Lighting Example: Sunset Ambient

```json
{
  "type": "light",
  "name": "sunset",
  "lighting": {
    "mode": "ambient",
    "color": [1.0, 0.6, 0.3],
    "intensity": 0.7
  }
}
```

### Lighting Example: Flickering Torch

```json
{
  "type": "light",
  "name": "torch",
  "lighting": {
    "mode": "point",
    "color": [1.0, 0.7, 0.3],
    "intensity": 2.5,
    "range": 12,
    "pulseSpeed": 4.0,
    "position": [5, 3, -2]
  }
}
```

---

## Fog (`FogDef`)

Controls Unity's built-in fog via `RenderSettings`.

| Field     | Type       | Default          | Description                                      |
|-----------|------------|------------------|--------------------------------------------------|
| `enabled` | `bool`     | `true`           | Enable or disable fog.                           |
| `color`   | `float[]`  | current setting  | Fog color as `[r, g, b]` (0--1 range).          |
| `density` | `float`    | 0.02             | Fog density (for exponential modes).             |
| `mode`    | `string`   | `"exponential"`  | Fog mode: `"linear"`, `"exponential"`, `"exponentialsquared"`. |

When the behavior's GameObject is destroyed, the `FogRestorer` component automatically disables fog.

### Fog Example: Morning Mist

```json
{
  "type": "fog",
  "name": "morning_mist",
  "fog": {
    "enabled": true,
    "color": [0.85, 0.85, 0.9],
    "density": 0.015,
    "mode": "exponential"
  }
}
```

---

## Physics (`PhysicsDef`)

Modifies global physics settings. Restored to defaults when the behavior is destroyed.

| Field       | Type    | Default | Description                                              |
|-------------|---------|---------|----------------------------------------------------------|
| `gravity`   | `float` | -9.81   | Y-axis gravity (`Physics.gravity.y`).                    |
| `timescale` | `float` | 1.0     | `Time.timeScale`. Clamped to 0.1--3.0 for safety.       |

### Physics Example: Low Gravity

```json
{
  "type": "physics",
  "name": "low_gravity",
  "physics": {
    "gravity": -3.0,
    "timescale": 1.0
  }
}
```

### Physics Example: Slow Motion

```json
{
  "type": "physics",
  "name": "slow_mo",
  "physics": {
    "gravity": -9.81,
    "timescale": 0.3
  }
}
```

---

## Timer (`TimerDef`)

Creates a delayed removal trigger. After `delay` seconds, the timer destroys the behavior named by `removeBehavior`.

| Field            | Type     | Default | Description                                        |
|------------------|----------|---------|----------------------------------------------------|
| `delay`          | `float`  | 0       | Seconds to wait before executing.                  |
| `removeBehavior` | `string` | null    | Name of the behavior to remove when the timer fires. |

### Timer Example: Stop Rain After 30 Seconds

```json
{
  "type": "timer",
  "name": "rain_timer",
  "timer": {
    "delay": 30,
    "removeBehavior": "rain"
  }
}
```

---

## Follow Player

When `followPlayer` is `true`, the behavior's GameObject gets a `FollowPlayer` component. This component:

1. Checks `BehaviorEngine.PlayerTransform` first (assign this from your game code).
2. Falls back to `GameObject.FindWithTag("Player")`.
3. Every `LateUpdate`, sets `transform.position = player.position + offset`.

The offset is the initial world position of the behavior (from `particles.offset` or `lighting.position`).

```csharp
// Set the player reference explicitly (recommended)
BehaviorEngine.PlayerTransform = myPlayerTransform;
```

---

## Removing Behaviors

Explicit removal by name:

```json
{
  "type": "remove",
  "name": "rain"
}
```

Programmatic removal from C#:

```csharp
// Remove a specific behavior
BehaviorEngine.Execute("{\"type\":\"remove\",\"name\":\"rain\"}");

// Remove all behaviors and reset physics
BehaviorEngine.ClearAll();
```

---

## Processing Agent Responses

To automatically detect and execute BehaviorDef blocks in agent responses:

```csharp
using OpenClawWorlds.Protocols;

void OnAgentResponse(string response)
{
    string summary = BehaviorEngine.ProcessResponse(response);
    if (summary != null)
        Debug.Log($"Behaviors applied: {summary}");
}
```

`ProcessResponse` scans for all ```` ```behaviordef ```` fenced blocks, parses each one, and executes them in order. Returns a newline-separated summary of what was created, or `null` if no blocks were found.

### Events

Subscribe to `BehaviorEngine.OnBehaviorCreated` to react when behaviors are applied:

```csharp
BehaviorEngine.OnBehaviorCreated += (name, type, summary) =>
{
    Debug.Log($"New behavior: {name} ({type}): {summary}");
};
```

---

## Complete Example: Thunderstorm

An agent could return a response containing multiple BehaviorDef blocks to create a full thunderstorm:

````
```behaviordef
{
  "type": "fog",
  "name": "storm_fog",
  "fog": {
    "enabled": true,
    "color": [0.3, 0.3, 0.35],
    "density": 0.04,
    "mode": "exponential"
  }
}
```

```behaviordef
{
  "type": "light",
  "name": "storm_ambient",
  "lighting": {
    "mode": "ambient",
    "color": [0.4, 0.4, 0.5],
    "intensity": 0.5
  }
}
```

```behaviordef
{
  "type": "particle",
  "name": "storm_rain",
  "followPlayer": true,
  "particles": {
    "count": 3000,
    "lifetime": 1.2,
    "speed": 15,
    "size": 0.02,
    "color": [0.6, 0.7, 0.8, 0.5],
    "gravity": 1.5,
    "shape": "box",
    "shapeScale": [50, 1, 50],
    "offset": [0, 25, 0],
    "rate": 800
  }
}
```

```behaviordef
{
  "type": "timer",
  "name": "storm_end",
  "timer": {
    "delay": 60,
    "removeBehavior": "storm_rain"
  }
}
```
````

All four blocks are parsed and executed in sequence within a single frame.

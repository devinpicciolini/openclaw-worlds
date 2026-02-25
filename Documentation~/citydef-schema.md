# CityDef Schema Reference

CityDef is the JSON protocol that AI agents use to build towns at runtime. A single CityDef JSON block describes an entire settlement: streets, buildings, props, and NPCs. The SDK parses, validates, normalizes, and instantiates it in one frame.

Pipeline: **JSON** -> `CityDefParser.Parse()` -> `CityDefSpawner.Build()` -> `BuildingBuilder` / `PropBuilder` / `NPCBuilder`

---

## Top-Level Fields

| Field          | Type              | Required | Description                                                      |
|----------------|-------------------|----------|------------------------------------------------------------------|
| `name`         | `string`          | Yes      | Unique town name. Used as the root GameObject name (`City_{name}`). |
| `edit`         | `bool`            | No       | If `true`, replaces an existing town with the same name.         |
| `worldX`       | `float`           | No       | Absolute X position in world space. `0` = use caller-provided origin. |
| `worldZ`       | `float`           | No       | Absolute Z position in world space. `0` = use caller-provided origin. |
| `streets`      | `CityStreetDef[]` | Yes      | Array of street definitions. At least one street is required.    |
| `buildings`    | `CityBuildingDef[]` | Yes    | Array of building definitions. At least one building is required. |
| `npcs`         | `CityNPCDef[]`    | No       | Array of wandering NPC definitions.                              |
| `props`        | `CityPropDef[]`   | No       | Array of prop definitions (lamps, barrels, trees, etc.).         |
| `palette`      | `CityPaletteDef`  | No       | Color palette hint: `roof`, `walls`, `trim` (string hex values). |
| `wanderingNpcs`| `CityWanderingNpcDef[]` | No | LLM alternative to `npcs`. Auto-converted during normalization.  |

---

## Street Fields (`CityStreetDef`)

| Field        | Type           | Required | Description                                                   |
|--------------|----------------|----------|---------------------------------------------------------------|
| `name`       | `string`       | Yes      | Display name of the street.                                   |
| `zone`       | `string`       | No       | Zone enum value for the street's trigger zone.                |
| `centerX`    | `float`        | *        | X center of the street relative to the town origin.           |
| `centerZ`    | `float`        | *        | Z center of the street relative to the town origin.           |
| `length`     | `float`        | *        | Length of the street along the Z axis. Clamped to 30--100.    |
| `points`     | `CityPosDef[]` | *        | LLM alternative: array of `{x, z}` waypoints (2+ required).  |

*Either `(centerX, centerZ, length)` or `points` must be provided. If `points` is used, the parser auto-computes center and length.

---

## Building Fields (`CityBuildingDef`)

| Field         | Type           | Required | Description                                                          |
|---------------|----------------|----------|----------------------------------------------------------------------|
| `name`        | `string`       | Yes      | Building name (displayed on signs for fallback buildings).           |
| `zone`        | `string`       | Yes*     | Zone enum value. If omitted, inferred from `interior` or `name`.    |
| `side`        | `string`       | Yes*     | Street side: `"Left"`, `"Right"`, or `"End"`.                       |
| `zPos`        | `float`        | No       | Position along the street. Used as ordering hint; auto-packed.      |
| `width`       | `float`        | No       | Building width (X). Default: 10. Clamped to 5--30.                  |
| `height`      | `float`        | No       | Building height (Y). Default: 6. Clamped to 3--15.                  |
| `depth`       | `float`        | No       | Building depth (Z). Default: 8. Clamped to 4--25.                   |
| `color`       | `float[]`      | No       | Wall color as `[r, g, b]` (0--1 range). Default: warm brown.       |
| `interior`    | `string`       | No       | InteriorStyle enum value. Determines auto-generated furniture.      |
| `streetIndex` | `int`          | No       | Which street this building belongs to (0-indexed). Default: 0.      |
| `agentId`     | `string`       | No       | Bind a specific OpenClaw agent to this building.                    |
| `position`    | `{x, z}`       | No       | LLM alternative to `side`/`zPos`. Auto-normalized during parsing.   |

*If `zone` is omitted, the parser infers it from `interior` and `name` via `CityDefParser.InferZoneFromInteriorOrName()`. If `side` is omitted but `position` is provided, the parser computes `side`, `zPos`, and `streetIndex` from the coordinates.

---

## Prop Fields (`CityPropDef`)

| Field     | Type       | Required | Description                                               |
|-----------|------------|----------|-----------------------------------------------------------|
| `type`    | `string`   | Yes      | Prop type name. See supported types below.                |
| `x`       | `float`    | No       | X offset from town origin.                                |
| `z`       | `float`    | No       | Z offset from town origin.                                |
| `yaw`     | `float`    | No       | Rotation around Y axis in degrees.                        |
| `height`  | `float`    | No       | Height parameter (used by trees). Default: 6.             |
| `scale`   | `float`    | No       | Scale multiplier (used by rocks and crates). Default: 1.  |
| `position`| `{x, z}`  | No       | LLM alternative to flat `x`/`z`. Auto-normalized.         |

### Supported Prop Types

These are the values accepted by `PropBuilder.SpawnProp()`:

**Simple props (position only):**
- `StreetLamp` -- lamp post with point light
- `Barrel` -- wooden barrel
- `HitchingPost` -- hitching rail
- `WaterTrough` -- water trough
- `NoticeBoard` -- notice/bulletin board
- `Fountain` -- stone fountain
- `Flagpole` -- flagpole
- `HayBale` -- hay bale
- `WoodPile` -- stacked wood
- `CampFire` -- campfire with point light
- `WaterTower` -- elevated water tank

**Props with yaw (position + rotation):**
- `Bench` -- park/street bench
- `Horse` -- standing horse
- `Cart` -- wooden cart

**Trees (position + height):**
- `PineTree` -- pine/conifer tree
- `OakTree` -- deciduous oak tree

**Scaled props (position + scale):**
- `Rock` -- boulder
- `Crate` -- wooden crate

Any unrecognized `type` value is passed directly to `PrefabLibrary.Spawn()` as a prefab name, allowing custom asset packs to extend the prop vocabulary.

---

## NPC Fields (`CityNPCDef`)

| Field     | Type       | Required | Description                                            |
|-----------|------------|----------|--------------------------------------------------------|
| `name`    | `string`   | Yes      | Display name of the NPC.                               |
| `prefab`  | `string`   | Yes      | Prefab name loaded via `PrefabLibrary`.                |
| `x`       | `float`    | No       | X position offset from town origin.                    |
| `z`       | `float`    | No       | Z position offset from town origin.                    |
| `speed`   | `float`    | No       | Wander speed. Default: 0.8.                            |
| `radius`  | `float`    | No       | Wander radius from spawn point. Default: 10.           |
| `position`| `{x, z}`  | No       | LLM alternative to flat `x`/`z`. Auto-normalized.      |

NPCs defined in the CityDef are outdoor wandering townsfolk. Interior shopkeeper NPCs are generated automatically based on each building's `zone` and `interior` style via the `IAssetMapper`.

---

## Auto-Packing

When multiple buildings share the same street and side, the parser auto-packs them into tight, non-overlapping plots. The `zPos` values are treated as ordering hints, not absolute positions.

The packing algorithm:

1. Groups buildings by `(streetIndex, side)`.
2. Sorts each group by `zPos`.
3. Computes per-building plot widths based on zone type (e.g., `Church` = 20 units, `GeneralStore` = 13 units).
4. Inserts a 3-unit gutter between plots.
5. If the total width exceeds the street length, the street is auto-extended.
6. Distributes buildings symmetrically around the street center.

This means you can define buildings with rough `zPos` ordering and the SDK will arrange them cleanly.

---

## Zone Enum Values

The `Zone` enum classifies building types and street areas. Used for interior generation, NPC assignment, and zone triggers.

```
Wilderness        MainStreet        SecondStreet      Saloon
Bank              Sheriff           TradingPost       Hotel
PostOffice        Church            Blacksmith        Doctor
GeneralStore      Stables           Schoolhouse       University
Library           Theater           Marketplace       Residential
Recreation        Park              TownSquare        CivicRow
Courthouse        TownLibrary       Newspaper         FireDept
MillLane          RanchRoad         LumberYard        GrainMill
Bakery            RanchHouse        Barn              FeedStore
TrainStation      Cemetery          Office            Warehouse
```

Outdoor zones (streets, plazas): `MainStreet`, `SecondStreet`, `Wilderness`, `TownSquare`, `CivicRow`, `MillLane`, `RanchRoad`, `TrainStation`, `Cemetery`.

All other zones are indoor (building interiors).

---

## InteriorStyle Enum Values

The `InteriorStyle` enum determines what furniture and props the `InteriorBuilder` generates inside a building, and which NPC template the `IAssetMapper` assigns.

```
Empty       Saloon      Office      Shop        Jail
Hotel       Church      Warehouse   School      Library
Theater     Clinic      Smithy
```

---

## Example CityDef JSON

```json
{
  "name": "Dusty Gulch",
  "streets": [
    {
      "name": "Main Street",
      "zone": "MainStreet",
      "centerX": 0,
      "centerZ": 0,
      "length": 80
    }
  ],
  "buildings": [
    {
      "name": "The Rusty Spur Saloon",
      "zone": "Saloon",
      "side": "Left",
      "zPos": -20,
      "streetIndex": 0,
      "interior": "Saloon",
      "color": [0.55, 0.35, 0.2],
      "height": 7,
      "width": 14,
      "depth": 10
    },
    {
      "name": "Sheriff's Office",
      "zone": "Sheriff",
      "side": "Right",
      "zPos": -20,
      "streetIndex": 0,
      "interior": "Jail",
      "color": [0.6, 0.55, 0.45]
    },
    {
      "name": "Frontier Savings Bank",
      "zone": "Bank",
      "side": "Left",
      "zPos": 0,
      "streetIndex": 0,
      "interior": "Office",
      "color": [0.7, 0.65, 0.55]
    },
    {
      "name": "Grand Hotel",
      "zone": "Hotel",
      "side": "Right",
      "zPos": 0,
      "streetIndex": 0,
      "interior": "Hotel",
      "color": [0.65, 0.5, 0.35]
    },
    {
      "name": "First Church",
      "zone": "Church",
      "side": "End",
      "zPos": 40,
      "streetIndex": 0,
      "interior": "Church",
      "color": [0.85, 0.82, 0.75]
    }
  ],
  "props": [
    { "type": "StreetLamp", "x": -4, "z": -30 },
    { "type": "StreetLamp", "x": 4, "z": -30 },
    { "type": "StreetLamp", "x": -4, "z": 0 },
    { "type": "StreetLamp", "x": 4, "z": 0 },
    { "type": "Barrel", "x": -8, "z": -18 },
    { "type": "Bench", "x": 8, "z": 5, "yaw": 90 },
    { "type": "HitchingPost", "x": -6, "z": -25 },
    { "type": "WaterTrough", "x": 6, "z": -25 },
    { "type": "PineTree", "x": 25, "z": -35, "height": 8 },
    { "type": "Rock", "x": -30, "z": 20, "scale": 2.5 }
  ],
  "npcs": [
    {
      "name": "Dusty Pete",
      "prefab": "SM_Chr_Cowboy_01",
      "x": 3,
      "z": -10,
      "speed": 0.6,
      "radius": 15
    },
    {
      "name": "Clara",
      "prefab": "SM_Chr_Cowgirl_01",
      "x": -5,
      "z": 10,
      "speed": 0.8,
      "radius": 12
    }
  ]
}
```

## Validation

Before building, run the CityDef through the audit pipeline to catch errors:

```csharp
using OpenClawWorlds.Validation;

string json = "{ ... }";
List<string> errors = AuditPipeline.AuditCityDef(json);

if (errors.Count > 0)
{
    foreach (var err in errors)
        Debug.LogError($"CityDef validation: {err}");
}
else
{
    CityDefSpawner.Build(json, playerPosition, out Vector3 townPos);
}
```

The audit pipeline checks:
- Required fields (`name`, at least one street, at least one building).
- Valid `zone` values against the Zone enum.
- Valid `interior` values against the InteriorStyle enum.
- Valid `side` values (`Left`, `Right`, `End`).
- NPC prefab names (if `AuditPipeline.ValidPrefabs` is configured).
- Detects incorrect nested `position` objects and suggests flat field format.

## Limits

The parser enforces safety limits on AI-generated content:

| Limit            | Value |
|------------------|-------|
| Max buildings    | 40    |
| Max props        | 50    |
| Max NPCs         | 10    |
| Max world radius | 800   |
| Street length    | 30--100 |

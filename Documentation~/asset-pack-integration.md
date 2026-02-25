# Asset Pack Integration

## Overview

The OpenClaw Worlds SDK works with primitive geometry by default. Every building, NPC, and prop has a fallback implementation using Unity's built-in cube, sphere, cylinder, and capsule primitives. This means you can prototype and test without any external assets.

To use real 3D models -- from POLYGON Western, Synty Fantasy, your own models, or any other asset pack -- implement the `IAssetMapper` interface. The SDK calls your mapper at every decision point where a visual asset is needed.

---

## IAssetMapper Interface

Defined in `OpenClawWorlds.World.AssetMapper`:

```csharp
public interface IAssetMapper
{
    /// <summary>Return the prefab name for a building zone type.</summary>
    string GetBuildingPrefab(BuildingDef def);

    /// <summary>Return the animator controller path for this character type.</summary>
    string GetAnimatorController(bool feminine);

    /// <summary>Return NPC template data for an interior style. Null = no NPC.</summary>
    NPCTemplate GetNPCTemplate(InteriorStyle interior);

    /// <summary>Return a zone override NPC template. Null = use default.</summary>
    NPCTemplate GetZoneOverrideNPC(Zone zone);

    /// <summary>Whether a prefab name represents a feminine character.</summary>
    bool IsFeminine(string prefabName);
}
```

### Method Details

#### `GetBuildingPrefab(BuildingDef def) -> string`

Called by `BuildingBuilder.Build()` for every building. Return the name of a prefab that `PrefabLibrary` can resolve (via `Resources.Load`), or return `null` to fall back to primitive geometry.

The `BuildingDef` parameter gives you access to:
- `def.zone` -- the Zone enum value (e.g., `Zone.Saloon`, `Zone.Bank`)
- `def.name` -- the building's display name
- `def.interior` -- the InteriorStyle
- `def.size` -- requested dimensions (width, height, depth)
- `def.scale` -- optional scale override

#### `GetAnimatorController(bool feminine) -> string`

Called by `NPCBuilder` when assigning an animator to a spawned NPC. Return a `Resources`-relative path to a `RuntimeAnimatorController`. The `feminine` parameter comes from `IsFeminine()`.

#### `GetNPCTemplate(InteriorStyle interior) -> NPCTemplate`

Called for every building that has an interior. Returns the NPC that should be spawned inside (e.g., a Bartender in a Saloon, a Shopkeeper in a Shop). Return a `default(NPCTemplate)` (with `name` as `null`) to skip NPC placement for that interior style.

#### `GetZoneOverrideNPC(Zone zone) -> NPCTemplate`

Called before `GetNPCTemplate`. If this returns a valid template (non-null `name`), it takes priority over the interior-based template. Use this for zone-specific NPCs that override the default interior NPC (e.g., a Banker in the Bank zone regardless of interior style).

#### `IsFeminine(string prefabName) -> bool`

Called to determine which animator controller variant to use. The default implementation checks for substrings like `"Woman"`, `"Girl"`, `"Cowgirl"`, and `"Female"` in the prefab name.

---

## NPCTemplate Struct

```csharp
public struct NPCTemplate
{
    public string prefab;       // Prefab name for PrefabLibrary
    public string name;         // Display name (e.g., "Bartender")
    public string greeting;     // Line spoken when player approaches
    public string[] offerings;  // Interaction menu options
    public float zFraction;     // Z offset as fraction of building depth
    public bool persistent;     // Persistent = dedicated agent with memory
}
```

| Field        | Description                                                                         |
|--------------|-------------------------------------------------------------------------------------|
| `prefab`     | Prefab name resolved by `PrefabLibrary.Find()`. `null` = use fallback capsule.     |
| `name`       | Display name shown in the interaction prompt (e.g., `"[E] Talk to Bartender"`).    |
| `greeting`   | First line the NPC says when the player initiates a conversation.                  |
| `offerings`  | Array of interaction options presented to the player (e.g., `["Chat", "Buy"]`).    |
| `zFraction`  | Position offset inside the building as a fraction of depth. Negative = toward door. |
| `persistent` | If `true`, the NPC gets a dedicated agent ID and persistent memory file.            |

---

## DefaultAssetMapper

The SDK ships with `DefaultAssetMapper`, which provides sensible defaults using no external assets:

- `GetBuildingPrefab()` returns `null` for all zones (primitive cube buildings).
- `GetAnimatorController()` returns `"Animations/AC_Polygon_Feminine"` or `"Animations/AC_Polygon_Masculine"`.
- `GetNPCTemplate()` returns named NPCs for each interior style:

| InteriorStyle | NPC Name    | Greeting                           | Persistent |
|---------------|-------------|-------------------------------------|------------|
| `Saloon`      | Bartender   | "What'll it be?"                   | Yes        |
| `Shop`        | Shopkeeper  | "Welcome! Take a look around."     | Yes        |
| `Office`      | Clerk       | "How can I help you today?"        | No         |
| `Jail`        | Sheriff     | "Stay out of trouble."             | Yes        |
| `Hotel`       | Innkeeper   | "Need a room?"                     | No         |
| `Church`      | Preacher    | "Welcome, friend."                 | No         |
| `Smithy`      | Smith       | "What needs fixing?"               | Yes        |
| `Clinic`      | Doctor      | "What seems to be the trouble?"    | Yes        |
| `School`      | Teacher     | "Ready to learn?"                  | No         |
| `Library`     | Librarian   | "Looking for something?"           | No         |
| `Warehouse`   | Foreman     | "Everything's in order."           | No         |

- `GetZoneOverrideNPC()` provides overrides for:
  - `Zone.Bank` -> Banker ("How may I assist you?")
  - `Zone.Courthouse` -> Judge ("Order in the court.")

---

## Creating a Custom Mapper

The recommended approach is to subclass `DefaultAssetMapper` and override only the methods you need:

```csharp
using OpenClawWorlds;
using OpenClawWorlds.World;

public class MyAssetMapper : DefaultAssetMapper
{
    public override string GetBuildingPrefab(BuildingDef def)
    {
        switch (def.zone)
        {
            case Zone.Saloon:      return "SM_Bld_Saloon_01";
            case Zone.Bank:        return "SM_Bld_Bank_01";
            case Zone.Sheriff:     return "SM_Bld_Sheriff_01";
            case Zone.Hotel:       return "SM_Bld_Hotel_01";
            case Zone.Church:      return "SM_Bld_Church_01";
            case Zone.GeneralStore:return "SM_Bld_GeneralStore_01";
            case Zone.Blacksmith:  return "SM_Bld_Blacksmith_01";
            case Zone.Stables:     return "SM_Bld_Stable_01";
            case Zone.Doctor:      return "SM_Bld_Doctor_01";
            case Zone.Barn:        return "SM_Bld_Barn_01";
            default:               return null; // fallback to primitives
        }
    }

    public override NPCTemplate GetNPCTemplate(InteriorStyle interior)
    {
        switch (interior)
        {
            case InteriorStyle.Saloon:
                return new NPCTemplate
                {
                    prefab = "SM_Chr_Bartender_Male_01",
                    name = "Whiskey Jack",
                    greeting = "Belly up to the bar, stranger.",
                    offerings = new[] { "Order whiskey", "Ask about rumors", "Play poker" },
                    zFraction = -0.25f,
                    persistent = true
                };

            case InteriorStyle.Shop:
                return new NPCTemplate
                {
                    prefab = "SM_Chr_Shopkeeper_Female_01",
                    name = "Martha",
                    greeting = "Fresh stock just came in on the train.",
                    offerings = new[] { "Browse wares", "Sell items", "Ask about specials" },
                    zFraction = 0.25f,
                    persistent = true
                };

            default:
                return base.GetNPCTemplate(interior);
        }
    }

    public override bool IsFeminine(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName)) return false;
        return prefabName.Contains("Female") || prefabName.Contains("Cowgirl")
            || prefabName.Contains("Woman") || prefabName.Contains("Girl")
            || prefabName.Contains("Shopkeeper_Female");
    }
}
```

### Registering Your Mapper

Set the mapper on `BuildingBuilder` before any CityDef is spawned:

```csharp
using OpenClawWorlds.World;

void Awake()
{
    BuildingBuilder.AssetMapper = new MyAssetMapper();
}
```

All builders (`BuildingBuilder`, `NPCBuilder`, `InteriorBuilder`) read from `BuildingBuilder.AssetMapper`.

---

## PrefabLibrary Configuration

`PrefabLibrary` is the centralized prefab loader. It searches through configurable paths and caches results.

### Search Paths

Prefabs are resolved by trying each search path in order:

```csharp
PrefabLibrary.SearchPaths = new string[] {
    "Western/Props/",
    "Western/Buildings/",
    "Western/Environments/",
    "Western/Characters/",
    "Western/Vehicles/",
    "Western/Weapons/",
    "Western/FX/",
    "Starter/",
    ""  // root of Resources
};
```

When you call `PrefabLibrary.Find("SM_Bld_Saloon_01")`, it tries:
1. `Resources.Load("Western/Props/SM_Bld_Saloon_01")`
2. `Resources.Load("Western/Buildings/SM_Bld_Saloon_01")`
3. ... and so on through each path.

Override `SearchPaths` for your asset pack's folder structure:

```csharp
PrefabLibrary.SearchPaths = new string[] {
    "MyAssetPack/Buildings/",
    "MyAssetPack/Characters/",
    "MyAssetPack/Props/",
    "MyAssetPack/Vehicles/",
    ""
};
```

### Texture Names

The SDK applies texture atlas materials to spawned prefabs via `PrefabLibrary.FixMaterials()`. Configure the texture names for your asset pack:

```csharp
// Primary texture atlas (applied to most prefabs)
PrefabLibrary.PrimaryTextureName = "PolygonWestern_Texture_01_A";

// Secondary/fallback texture atlas
PrefabLibrary.SecondaryTextureName = "PolygonStarter_Texture_01";
```

These textures are loaded from `Resources` and applied as `_BaseMap` / `_MainTex` on a URP Lit material.

### Skipping Material Fix

Some prefabs (particles, sky domes, clouds) should not have their materials replaced. The SDK has built-in skip rules for common prefixes (`FX_`, `SkyDome`, `SM_Env_Cloud`, etc.). Add your own:

```csharp
PrefabLibrary.CustomSkipCheck = (prefabName) =>
{
    // Skip material fix for water and terrain prefabs
    return prefabName.StartsWith("SM_Water_") || prefabName.StartsWith("SM_Terrain_");
};
```

### Cache Management

```csharp
// Clear the prefab cache (useful when swapping asset packs at runtime)
PrefabLibrary.ClearCache();
```

---

## Example: Mapping a Hypothetical Asset Pack

Suppose you have the "Fantasy Village" asset pack with prefabs in `Resources/Fantasy/`:

```
Resources/
  Fantasy/
    Buildings/
      FV_Building_Inn.prefab
      FV_Building_Blacksmith.prefab
      FV_Building_Church.prefab
      FV_Building_Shop.prefab
    Characters/
      FV_Character_Innkeeper.prefab
      FV_Character_Blacksmith.prefab
      FV_Character_Priest.prefab
      FV_Character_Merchant.prefab
    Props/
      FV_Prop_Barrel.prefab
      FV_Prop_Bench.prefab
    Textures/
      FV_Atlas_01.png
```

Setup:

```csharp
using OpenClawWorlds;
using OpenClawWorlds.World;

public class FantasySetup : MonoBehaviour
{
    void Awake()
    {
        // 1. Configure PrefabLibrary search paths
        PrefabLibrary.SearchPaths = new string[] {
            "Fantasy/Buildings/",
            "Fantasy/Characters/",
            "Fantasy/Props/",
            ""
        };

        // 2. Configure texture atlas
        PrefabLibrary.PrimaryTextureName = "FV_Atlas_01";

        // 3. Register custom asset mapper
        BuildingBuilder.AssetMapper = new FantasyMapper();
    }
}

public class FantasyMapper : DefaultAssetMapper
{
    public override string GetBuildingPrefab(BuildingDef def)
    {
        switch (def.zone)
        {
            case Zone.Hotel:       return "FV_Building_Inn";
            case Zone.Blacksmith:  return "FV_Building_Blacksmith";
            case Zone.Church:      return "FV_Building_Church";
            case Zone.GeneralStore:return "FV_Building_Shop";
            default:               return null;
        }
    }

    public override NPCTemplate GetNPCTemplate(InteriorStyle interior)
    {
        switch (interior)
        {
            case InteriorStyle.Hotel:
                return new NPCTemplate
                {
                    prefab = "FV_Character_Innkeeper",
                    name = "Innkeeper",
                    greeting = "Welcome, weary traveler.",
                    offerings = new[] { "Rent a room", "Buy a meal" },
                    zFraction = 0.25f,
                    persistent = true
                };
            case InteriorStyle.Smithy:
                return new NPCTemplate
                {
                    prefab = "FV_Character_Blacksmith",
                    name = "Blacksmith",
                    greeting = "Need something forged?",
                    offerings = new[] { "Repair armor", "Commission weapon" },
                    zFraction = 0.25f,
                    persistent = true
                };
            case InteriorStyle.Church:
                return new NPCTemplate
                {
                    prefab = "FV_Character_Priest",
                    name = "Priest",
                    greeting = "Blessings upon you.",
                    offerings = new[] { "Heal wounds", "Seek counsel" },
                    zFraction = -0.5f,
                    persistent = false
                };
            case InteriorStyle.Shop:
                return new NPCTemplate
                {
                    prefab = "FV_Character_Merchant",
                    name = "Merchant",
                    greeting = "See anything you like?",
                    offerings = new[] { "Buy supplies", "Sell loot" },
                    zFraction = 0.25f,
                    persistent = true
                };
            default:
                return base.GetNPCTemplate(interior);
        }
    }

    public override string GetAnimatorController(bool feminine)
    {
        return feminine
            ? "Fantasy/Animations/AC_Fantasy_Female"
            : "Fantasy/Animations/AC_Fantasy_Male";
    }

    public override bool IsFeminine(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName)) return false;
        return prefabName.Contains("Female") || prefabName.Contains("Woman");
    }
}
```

With this configuration, CityDef JSON that references `Zone.Hotel` will spawn `FV_Building_Inn` instead of a primitive cube, and the interior will be staffed by the `FV_Character_Innkeeper` prefab. Any zones you do not override will continue using the fallback primitive geometry.

---

## Runtime Asset Pack Switching

You can swap asset packs at runtime by changing the mapper and clearing the cache:

```csharp
void SwitchToFantasyPack()
{
    PrefabLibrary.ClearCache();
    PrefabLibrary.SearchPaths = new string[] { "Fantasy/Buildings/", "Fantasy/Characters/", "Fantasy/Props/", "" };
    PrefabLibrary.PrimaryTextureName = "FV_Atlas_01";
    BuildingBuilder.AssetMapper = new FantasyMapper();
}

void SwitchToWesternPack()
{
    PrefabLibrary.ClearCache();
    PrefabLibrary.SearchPaths = new string[] { "Western/Props/", "Western/Buildings/", "Western/Characters/", "" };
    PrefabLibrary.PrimaryTextureName = "PolygonWestern_Texture_01_A";
    BuildingBuilder.AssetMapper = new WesternMapper();
}
```

Existing towns are not affected by mapper changes. Only newly spawned buildings and NPCs use the updated mapper. To rebuild existing towns with the new assets, destroy the `City_*` root GameObjects and re-invoke `CityDefSpawner.Build()`.

# Walk the World Open-World Whitepaper

Date: 2026-05-25

Project: Walk the World, a RimWorld 1.6 mod that lets drafted pawns move from map edge to neighboring world tile maps for open-world style roaming.

Primary goal: evolve Walk the World from a map-edge traversal mod into a persistent world-tile exploration and progression framework that other progression, survival, faction, caravan, quest, RPG, and biome mods want to target.

This document is intended to survive chat loss. Another coding agent should be able to read it and continue the project without needing prior conversation context.

## 1. Executive Summary

Walk the World currently enables a strong fantasy: pawns can physically walk from one RimWorld map to the next, making the planet feel traversable instead of abstract. The next stage should focus on making that travel matter.

The best path is a hybrid persistence model. RimWorld can preserve some full maps when the generated map is a full tile size recognized by the game and the player has materially changed it, such as by building structures. Smaller untouched exploration maps should still use stateful regeneration: record durable facts about each explored world tile, then apply those facts whenever the tile is generated again.

The mod should become a world-tile memory layer. It should remember visited tiles, looted ruins, consumed landmarks, cleared threats, camps, cached supplies, local reputation effects, depletion, danger, and repeated routes. It should also expose hooks and defs so other mods can add progression rewards and consequences.

The high-level design direction is:

1. Keep map traversal responsive and compatible.
2. Record per-tile exploration state in save data.
3. Summarize maps when they are left or removed.
4. Reapply durable effects during map generation.
5. Add world-object UI and player actions for explored tiles.
6. Add a public API and XML def surface for progression mods.
7. Keep persistence configurable and performance-safe.

The result should feel like this: the player does not just enter another temporary map. They move through a planet that remembers where they have been and what they have done.

## 2. Current Repository State

Workspace root:

```text
C:\Users\ortiz\Documents\github\WalkTheWorld
```

Target RimWorld install:

```text
C:\Program Files (x86)\Steam\steamapps\common\RimWorld
```

Local test mod deploy path:

```text
C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\WalkTheWorld
```

Installed RimWorld version observed:

```text
1.6.4633 rev1260
```

Relevant Workshop dependencies and references:

```text
Harmony Workshop ID: 2009463077
Installed Workshop Walk the World ID: 3546716725
Canonical packageId from Workshop copy: addvans.WalkTheWorld
```

The working tree has uncommitted first-pass changes. Important modified/added files include:

```text
CellByCell.csproj
MapGenerator.cs
README.md
Settings/ModSettings.cs
Settings/SettingsMenu.cs
Utility/WalkTheWorld_WorldTileUtility.cs
WalkTheWorldComponent.cs
HarmonyPatches/JobDriver_Goto_MakeNewToils_Patch.cs
Properties/AssemblyInfo.cs
About/About.xml
Defs/WorldObjectDef.xml
Languages/English/Keyed/English.xml
Textures/GenericWorldSite.png
About/ModIcon.png
About/Preview.png
Scripts/Deploy-RimWorldMod.ps1
.gitignore
```

Editor diagnostics were clean after the packaging work.

## 3. Build and Deploy Workflow

Build and deploy for local in-game testing:

```powershell
& ".\Scripts\Deploy-RimWorldMod.ps1"
```

The deploy script:

1. Builds `CellByCell.sln`.
2. Emits the assembly to `1.6/Assemblies/WalkTheWorld.dll`.
3. Copies `About`, `Defs`, `Languages`, `Textures`, and `1.6` to the Steam local mod folder.
4. Cleans stale folders in the target local mod before copying fresh files.

Manual build only:

```powershell
dotnet build CellByCell.sln -c Debug
```

Expected package shape after deploy:

```text
WalkTheWorld/
  About/
    About.xml
    ModIcon.png
    Preview.png
  Defs/
    WorldObjectDef.xml
  Languages/
    English/
      Keyed/
        English.xml
  Textures/
    GenericWorldSite.png
  1.6/
    Assemblies/
      WalkTheWorld.dll
      WalkTheWorld.pdb
```

Important project setting:

RimWorld, Unity, and Harmony references in `CellByCell.csproj` should remain `Private=False`. Otherwise MSBuild copies many game assemblies into `1.6/Assemblies`, producing an incorrect mod package.

Potential future cleanup:

The Harmony reference currently points at a local Workshop version folder used by the project. The build succeeds, but for RimWorld 1.6 the reference should eventually be reviewed and possibly pointed at the `Current` Harmony assembly folder if available.

## 4. RimWorld Modding Documentation Findings

Sources consulted:

1. Installed RimWorld `ModUpdating.txt`.
2. Installed official game data under `Data/Core`, `Data/Odyssey`, etc.
3. Installed Workshop copy of Walk the World at Workshop ID `3546716725`.
4. Web search snippets for RimWorld Wiki pages about `About.xml`, mod folder structure, `supportedVersions`, and `LoadFolders.xml`.

The RimWorld Wiki fetch was blocked by HTTP 403, so installed documentation and local examples were used as primary references.

Confirmed packaging rules:

1. A mod must have `About/About.xml`.
2. `About.xml` should use `ModMetaData` as root.
3. `packageId` is required for modern RimWorld mods and must be unique.
4. `supportedVersions` should list the game versions explicitly tested, here `1.6`.
5. C# mod assemblies belong under `Assemblies` or a versioned folder such as `1.6/Assemblies`.
6. XML defs belong under `Defs`.
7. Keyed translations belong under `Languages/English/Keyed`.
8. Textures can be referenced by def path/name from `Textures`.
9. `LoadFolders.xml` is optional. The current project can use the simple versioned `1.6` folder without adding `LoadFolders.xml` yet.
10. Harmony should be declared as a dependency and loaded before this mod.

Current `About.xml` should retain:

```xml
<packageId>addvans.WalkTheWorld</packageId>
```

This preserves identity with the original Workshop mod and avoids turning this fork into an unrelated mod for saves/mod lists.

## 5. Current Runtime Architecture

The core mod code is centered around these files:

### `WalkTheWorldComponent.cs`

Current role:

1. GameComponent singleton via `WalkTheWorld.Instance`.
2. Detects edge travel.
3. Validates whether a pawn can start travel.
4. Computes target world tile.
5. Shows confirmation preview.
6. Chooses traveler set.
7. Generates/reuses target map.
8. Converts pawns to caravan and enters target map.
9. Handles camera behavior and pawn reselection.
10. Keeps a tick-based fallback path for compatibility.

Important first-pass changes already made:

1. Added centralized `TryStartTravel(Pawn pawn)` and `CanStartTravel(Pawn pawn)`.
2. Added support for selected/everyone/always-ask traveler selection.
3. Fixed always-ask flow so the dialog callback performs travel after a choice.
4. Reselects all traveling pawns after map entry.
5. Keeps `GameComponentTick` fallback instead of fully relying on a Harmony toil patch.

### `HarmonyPatches/JobDriver_Goto_MakeNewToils_Patch.cs`

Current role:

Adds a final toil to `JobDriver_Goto.MakeNewToils`. When a drafted pawn finishes moving to the edge, it calls:

```csharp
WalkTheWorld.Instance?.TryStartTravel(__instance.pawn);
```

This is based on the relevant part of PR #1. It improves responsiveness compared with polling every 64 ticks. The tick fallback remains because the PR author warned that removing it could break WASD-style pawn control compatibility.

### `MapGenerator.cs`

Current role:

1. Chooses what world object def/map generator should be used for the target tile.
2. Generates exploration tiles, settlements, landmarks, sites, and other map parents.
3. Transfers weather events from source map to target map.
4. Spawns settlement trader behavior for friendly settlements.
5. Optionally fires random incidents on newly generated exploration maps.

Important first-pass changes already made:

1. Added configurable exploration map size.
2. Added `ExplorationTileDef` property.
3. Added null faction checks.
4. Avoided event generation on reused maps.
5. Replaced recursive incident retry with bounded attempts.
6. Added guards around null maps and traders.

### `VisitCell.cs`

Current role:

Custom world object class for exploration tiles. It inherits from `Camp`.

Important existing behavior:

1. `IsAffectedByPlayer(Map map)` checks for colonist buildings, player haulables, stockpiles, and growing zones.
2. `ShouldRemoveMapNow` removes the map/world object if no pawns block removal and the site has not been affected by the player.
3. `Notify_MyMapRemoved` handles map removal and currently removes configured Odyssey tile mutators from landmark tiles.

This is the best existing hook for durable world-tile memory. When a map is removed, summarize what happened and write it to save data.

### `Utility/WalkTheWorld_WorldTileUtility.cs`

Current role:

1. Detects walkable world tiles.
2. Detects map edge cells.
3. Converts pawn position relative to map center into `Direction8Way`.
4. Finds neighboring world tiles.
5. Builds entry cell predicates for destination maps.
6. Builds preview text for target world tile.

Important first-pass changes already made:

1. Neighbor tile search now tries the exact direction and then clockwise/counter-clockwise fallbacks.
2. Entry placement now normalizes against old map size before mirroring into the new map. This matters because exploration map size is now configurable.

### `Settings/ModSettings.cs` and `Settings/SettingsMenu.cs`

Current role:

1. Stores mod settings.
2. Draws settings UI.
3. Supports configurable map size and event behavior.
4. Supports mutator removal settings.

Important first-pass changes already made:

1. Added min/default/max map size constants: `30`, `60`, `300`.
2. Clamps map size, event chance, and map count in `ExposeData`.
3. Uses the constants in UI and defaults.

### `Defs/WorldObjectDef.xml`

Current role:

Defines `ExplorationTile`, used by `MapGenerator`.

Important detail:

The original Workshop copy used this class path:

```xml
<worldObjectClass>walktheworld.VisitCell</worldObjectClass>
```

The project namespace is actually `WalkTheWorld`, so this repo now uses:

```xml
<worldObjectClass>WalkTheWorld.VisitCell</worldObjectClass>
```

This should be verified in game. If RimWorld type lookup is unexpectedly case-insensitive, both work; if it is case-sensitive, the corrected version is required.

## 6. Current Functional Limitations

README-listed limitations:

1. Map generation can be slow. Exploration maps default to `60x60`, configurable up to `300x300`.
2. Exploration sites are not always fully saved after leaving. Re-entering a small untouched temporary tile can regenerate it from scratch. Verified testing showed that a full-size exploration map can persist across exit and re-entry when the player has changed the map, such as by building structures.

Additional technical limits and risks:

1. Full map persistence for every walked tile is not realistic as the default. It risks huge saves and degraded performance. Full-map retention should be treated as an intentional higher persistence tier for full-size, player-touched, camped, or claimed maps.
2. Travel is edge-based and depends on map/world direction mapping. Edge cases around poles, oceans, lakes, and weird world topology need testing.
3. Settlement/quest site entry relies on vanilla/map-parent behavior and compatibility patches.
4. Weather event transfer currently registers active conditions from one map to another. Future review should confirm this is safe, especially if condition instances should not be shared between maps.
5. `TryStartTravel` currently updates `lastEnterTick` and `lastEnterPos` before checking target tile validity. This may cause a cooldown even when walking into an invalid ocean/lake target. Consider moving those assignments after target validation.
6. The mod has many Harmony patches touching settlement, trade, caravan, and exit-map behavior. Every new feature should preserve compatibility and avoid broad rewrites.

## 7. Product Vision

The mod should become a durable open-world framework with three promises:

1. The world is physically traversable.
2. Explored places remember the player.
3. Other mods can build progression on top of travel.

Target player fantasy:

1. A nomadic colony can walk tile by tile across the planet.
2. Routes matter because the player remembers safe roads, dangerous areas, and useful camps.
3. Ruins cannot be farmed forever unless the player configures that behavior.
4. Friendly and hostile settlements remember the player's actions.
5. Progression mods can reward exploration, survival, tracking, scouting, archaeology, diplomacy, and long-distance journeys.

Design mantra:

```text
Do not save every map forever. Save enough world-tile state to make regenerated maps feel remembered.
```

## 8. Recommended Core Architecture

Add a durable per-tile registry owned by `WalkTheWorldComponent`.

### 8.1 Main Save Data

Add a new saveable model, likely in a new file such as:

```text
World/ExploredTileRecord.cs
```

or, if keeping the project shallow:

```text
ExploredTileRecord.cs
```

Suggested model:

```csharp
public class ExploredTileRecord : IExposable
{
    public int tileId;
    public int visitCount;
    public int firstVisitedTick;
    public int lastVisitedTick;
    public int lastLeftTick;

    public bool hasBeenEntered;
    public bool ruinsLooted;
    public bool majorThreatCleared;
    public bool playerBuiltHere;
    public bool playerCacheLeft;
    public bool resourcesDepleted;
    public bool landmarkConsumed;
    public bool hasKnownHostiles;
    public bool hasKnownFriendlyPresence;
    public bool hasRoadAccess;

    public float dangerLevel;
    public float scavengingDepletion;
    public float forageDepletion;

    public int hostilePawnsSeen;
    public int hostilePawnsRemaining;
    public int playerBuildingsSeen;
    public int playerItemsLeft;
    public int firesSeen;
    public int animalsSeen;

    public List<string> removedMutators = new List<string>();
    public List<string> discoveredFeatures = new List<string>();
    public List<string> eventTags = new List<string>();

    public void ExposeData()
    {
        Scribe_Values.Look(ref tileId, "tileId");
        Scribe_Values.Look(ref visitCount, "visitCount");
        Scribe_Values.Look(ref firstVisitedTick, "firstVisitedTick");
        Scribe_Values.Look(ref lastVisitedTick, "lastVisitedTick");
        Scribe_Values.Look(ref lastLeftTick, "lastLeftTick");
        Scribe_Values.Look(ref hasBeenEntered, "hasBeenEntered");
        Scribe_Values.Look(ref ruinsLooted, "ruinsLooted");
        Scribe_Values.Look(ref majorThreatCleared, "majorThreatCleared");
        Scribe_Values.Look(ref playerBuiltHere, "playerBuiltHere");
        Scribe_Values.Look(ref playerCacheLeft, "playerCacheLeft");
        Scribe_Values.Look(ref resourcesDepleted, "resourcesDepleted");
        Scribe_Values.Look(ref landmarkConsumed, "landmarkConsumed");
        Scribe_Values.Look(ref hasKnownHostiles, "hasKnownHostiles");
        Scribe_Values.Look(ref hasKnownFriendlyPresence, "hasKnownFriendlyPresence");
        Scribe_Values.Look(ref hasRoadAccess, "hasRoadAccess");
        Scribe_Values.Look(ref dangerLevel, "dangerLevel");
        Scribe_Values.Look(ref scavengingDepletion, "scavengingDepletion");
        Scribe_Values.Look(ref forageDepletion, "forageDepletion");
        Scribe_Values.Look(ref hostilePawnsSeen, "hostilePawnsSeen");
        Scribe_Values.Look(ref hostilePawnsRemaining, "hostilePawnsRemaining");
        Scribe_Values.Look(ref playerBuildingsSeen, "playerBuildingsSeen");
        Scribe_Values.Look(ref playerItemsLeft, "playerItemsLeft");
        Scribe_Values.Look(ref firesSeen, "firesSeen");
        Scribe_Values.Look(ref animalsSeen, "animalsSeen");
        Scribe_Collections.Look(ref removedMutators, "removedMutators", LookMode.Value);
        Scribe_Collections.Look(ref discoveredFeatures, "discoveredFeatures", LookMode.Value);
        Scribe_Collections.Look(ref eventTags, "eventTags", LookMode.Value);
    }
}
```

Keep it conservative first. Do not store individual item stacks, pawn references, or building references in the first implementation. Store facts and counters.

### 8.2 GameComponent Registry

Add to `WalkTheWorldComponent.cs`:

```csharp
private Dictionary<int, ExploredTileRecord> exploredTiles = new Dictionary<int, ExploredTileRecord>();
private List<int> exploredTileKeys;
private List<ExploredTileRecord> exploredTileValues;
```

Expose with Scribe:

```csharp
public override void ExposeData()
{
    base.ExposeData();
    Scribe_Collections.Look(
        ref exploredTiles,
        "exploredTiles",
        LookMode.Value,
        LookMode.Deep,
        ref exploredTileKeys,
        ref exploredTileValues);

    if (Scribe.mode == LoadSaveMode.PostLoadInit && exploredTiles == null)
        exploredTiles = new Dictionary<int, ExploredTileRecord>();
}
```

Add helper methods:

```csharp
public ExploredTileRecord GetOrCreateTileRecord(int tileId)
public bool TryGetTileRecord(int tileId, out ExploredTileRecord record)
public void RecordTileEntered(Map map, PlanetTile tile)
public void RecordTileLeft(VisitCell visitCell, Map map)
public IEnumerable<ExploredTileRecord> AllExploredTileRecords()
```

Use `int tileId` for persistence rather than trying to serialize `PlanetTile` directly.

### 8.3 Entry and Exit Recording

On entry, after `generatedMap` is available in `MapGenerator.StartGeneration` or after `FinalizeTravel`, record:

1. Tile entered.
2. Visit count.
3. First/last visited ticks.
4. Biome and road tags.
5. Landmark presence.
6. Initial threat/animals/features counts if cheap.

On exit/removal, from `VisitCell.Notify_MyMapRemoved(Map map)` record:

1. Last left tick.
2. Player-built state.
3. Player cached items.
4. Mutators removed.
5. Whether hostiles remain.
6. Whether the tile was meaningfully changed.
7. Depletion values.

Do not perform expensive whole-map scans every tick. Summarize at generation and removal time.

## 9. Lasting Effects Model

The mod should define a small vocabulary of lasting tile states. These can be flags now and later become extensible defs.

Suggested first states:

```text
Unvisited
Explored
Looted
Cleared
Dangerous
Depleted
Scarred
Camped
Claimed
LandmarkConsumed
SettlementAngered
SettlementHelped
```

How each state works:

### Explored

Set after first entry. World UI should show the tile has been visited.

### Looted

Set when a ruin/landmark/site mutator was consumed or when map contents indicate meaningful loot was removed. Future generation should reduce or remove ruin rewards.

### Cleared

Set when hostile presence was seen and later no hostile pawns remain. Future generation can lower threat or replace combat encounters with aftermath.

### Dangerous

Set when hostiles remain, dangerous incidents fire, infestations exist, mech threats exist, or the player leaves during danger. Future generation can warn the player and spawn danger again.

### Depleted

Set when foraging/scavenging/resource extraction has been performed repeatedly. Future world actions should give weaker rewards.

### Scarred

Set when the map was heavily burned, bombarded, polluted, or otherwise visibly damaged. Future generation can add filth, rubble, dead plants, or reduced wildlife.

### Camped

Set when the player leaves meaningful camp infrastructure or uses a camp/claim action.

### Claimed

Optional stronger state. The tile becomes a persistent or semi-persistent camp/base with more expensive consequences.

### LandmarkConsumed

Set when Odyssey or modded landmark/ruin mutators were removed. Prevents repeated landmark exploitation.

## 10. Regenerative Persistence

The practical open-world solution is to regenerate maps with memory.

Implementation idea:

1. Generate map normally.
2. Fetch `ExploredTileRecord` for the tile.
3. Apply lightweight mutations after generation.
4. Show messages/inspect data explaining state.

Possible helpers:

```csharp
public static class TileMemoryApplicator
{
    public static void Apply(Map map, ExploredTileRecord record)
    {
        ApplyLootDepletion(map, record);
        ApplyThreatMemory(map, record);
        ApplyScars(map, record);
        ApplyCampMarkers(map, record);
    }
}
```

First-pass effects should be conservative:

1. If `ruinsLooted`, remove or reduce high-value loose items from regenerated exploration maps.
2. If `landmarkConsumed`, ensure configured mutators remain removed from the world tile.
3. If `majorThreatCleared`, avoid firing a new random combat incident immediately on re-entry.
4. If `dangerous`, show a warning and maybe preserve threat chance.
5. If `depleted`, reduce survey/scavenge rewards.

Avoid destructive mutation of settlements, quest sites, or other mods' special map parents in the first implementation. Apply lasting effects mainly to `VisitCell` exploration tiles until the behavior is proven safe.

## 11. World Object UX

Exploration should be visible on the world map.

Add or override in `VisitCell`:

```csharp
public override string GetInspectString()
public override IEnumerable<Gizmo> GetGizmos()
```

Potential inspect text:

```text
Explored 3 times
Last visited 7.4 days ago
Status: Looted, Cleared, Camped
Danger: Low
Forage: Depleted
```

Potential gizmos:

1. Abandon exploration site.
2. Mark camp.
3. Survey tile.
4. Scavenge tile.
5. View travel history.
6. Clear marker.
7. Claim site.

Current code has `HarmonyPatches/WorldObject_GetGizmos_Patch.cs`; review it before adding overrides, so gizmo logic is not duplicated or contradictory.

World icon improvements:

1. Keep `GenericWorldSite.png` for generic explored tile.
2. Add alternate icons later for camped, dangerous, looted, and claimed.
3. Do not add many assets until the state system is stable.

## 12. Survey, Scavenge, and Camp Actions

This is the most direct route to making the mod feel like progression glue.

### Survey

World action usable by caravans or map pawns at/near a tile.

Results:

1. Reveals discovered features.
2. Identifies danger level.
3. Marks roads, caves, ruins, landmarks, water, forage quality.
4. Grants hooks for progression mods.

Potential dependencies:

1. Pawn skills: intellectual, plants, animals, shooting, melee, construction, mining.
2. Biome and hilliness.
3. Weather/season.
4. Tech/progression mods.

### Scavenge

World action that produces small rewards from explored or ruined tiles without loading a full map.

Rules:

1. Reward depends on biome, ruins, roads, prior depletion, and danger.
2. Repeated use increases `scavengingDepletion`.
3. May trigger ambush, disease, injury, or discovery.
4. Other mods can add reward tables.

### Forage

World action for food, herbs, wood, leather, etc.

Rules:

1. Reward depends on biome, season, rainfall, temperature, and skills.
2. Repeated use increases `forageDepletion`.
3. Recovers slowly over time if desired.

### Camp

World action that intentionally creates or upgrades an exploration tile.

Potential benefits:

1. Safer rest.
2. Known re-entry location.
3. Small storage abstraction.
4. Travel network marker.

Potential costs:

1. Visibility to raiders.
2. Food upkeep.
3. Weather exposure.
4. Faction territory tension.

## 13. Anchored Camps and Semi-Persistent Bases

Do not make every tile permanent. Instead, add intentional anchoring.

Possible mechanics:

1. Player builds a special camp marker building.
2. Player uses a world gizmo to claim or anchor a tile.
3. Player leaves enough structures/items that the mod marks it as camped.
4. Anchored tiles can persist longer or be regenerated with stronger memory.

Persistence tiers:

### Light

Only records summary facts. Small or untouched maps unload normally.

### Standard

Records facts plus abstract caches/camps. Maps unload normally unless vanilla keeps them because the tile is full-size and player-touched.

### Immersive

Allows intentionally claimed camps or full-size player-touched maps to remain loaded or semi-persistent, with warnings about performance/save size.

Settings should make these tiers explicit.

## 14. Settlement Memory

The mod already patches settlement entry/trade/quests. This can become a major RPG layer.

Potential `SettlementVisitRecord` fields:

```csharp
public int tileId;
public string factionDefName;
public int visitCount;
public int lastVisitedTick;
public float stolenMarketValue;
public int pawnsKilled;
public int buildingsDestroyed;
public bool traderUsedRecently;
public bool questRequestedRecently;
public bool angeredSettlement;
public bool helpedSettlement;
```

Possible features:

1. Settlement remembers theft and violence locally.
2. Trader inventory cooldowns.
3. Quest-giver cooldowns.
4. Settlement-specific reputation.
5. Warnings before entering hostile remembered settlements.
6. Hospitality-style or diplomacy-style extension hooks.
7. Local bounties or patrols if the player repeatedly raids settlements.

Start with recording visits and theft/destruction summaries. Do not overbuild faction AI first.

## 15. Public API for Other Mods

This is how Walk the World becomes a must-have framework.

### 15.1 C# Events

Add a public static API class:

```csharp
public static class WalkTheWorldAPI
{
    public static event Action<Pawn, int> TileTravelStarted;
    public static event Action<Map, ExploredTileRecord> ExplorationMapGenerated;
    public static event Action<Map, ExploredTileRecord> ExplorationMapLeaving;
    public static event Action<ExploredTileRecord> TileFirstExplored;
    public static event Action<ExploredTileRecord> TileRecordUpdated;

    public static bool TryGetTileRecord(int tileId, out ExploredTileRecord record)
    public static IReadOnlyList<ExploredTileRecord> AllRecords { get; }
}
```

Keep event firing defensive. Wrap external mod callbacks in try/catch or document expectations carefully.

### 15.2 Def-Based Extension Points

Add defs that other mods can write in XML.

Candidate defs:

```text
WalkTheWorldTileEffectDef
WalkTheWorldSurveyOutcomeDef
WalkTheWorldScavengeOutcomeDef
WalkTheWorldBiomeProfileDef
WalkTheWorldProgressionHookDef
```

Example shape:

```xml
<WalkTheWorldSurveyOutcomeDef>
  <defName>WTW_Survey_AncientRoadCache</defName>
  <label>ancient road cache</label>
  <biomes>
    <li>TemperateForest</li>
  </biomes>
  <requiresRoad>true</requiresRoad>
  <baseChance>0.05</baseChance>
  <discoveredFeature>AncientRoadCache</discoveredFeature>
</WalkTheWorldSurveyOutcomeDef>
```

Implementation can start simple: load defs, filter by tile/biome/record, choose outcomes.

### 15.3 Compatibility Goals

Make it easy for mods to add:

1. Exploration XP.
2. Skill progression from surveying/tracking/foraging.
3. Biome-specific discoveries.
4. Rare loot tables.
5. New world events.
6. Settlement contracts.
7. Archaeology or relic systems.
8. Survival challenges.
9. Vehicle travel bonuses.
10. Faction patrols and territorial control.

Do not hard-depend on these mods. Provide hooks and optional integration.

## 16. Settings Roadmap

Current settings:

1. Map size.
2. Event chance.
3. Guaranteed event after map count.
4. Confirmation preview.
5. Exit grid behavior.
6. Leaving pawn selection.
7. Camera focus.
8. Event filter.
9. Mutators to delete.

Recommended new settings:

```text
Persistence mode: Light / Standard / Immersive
Remember looted ruins: true/false
Remember cleared threats: true/false
Remember forage/scavenge depletion: true/false
Enable survey actions: true/false
Enable scavenge actions: true/false
Enable anchored camps: true/false
Max remembered tiles: 0 means unlimited, otherwise prune old low-value records
Show explored tile world markers: true/false
Show travel history inspect text: true/false
```

Settings UI should remain simple. Group advanced persistence settings on a separate page.

## 17. Performance Strategy

Rules:

1. No expensive scans every tick.
2. Do map summarization only on map generation/removal or explicit player action.
3. Store compact tile records.
4. Avoid storing Thing, Pawn, Map, or WorldObject references in long-term records.
5. Store def names/integers/floats/bools instead.
6. Add pruning only after basic record persistence works.

Potential pruning rules:

1. Never prune camped or claimed tiles.
2. Never prune settlement memory if violence/theft occurred.
3. Prune old simple explored-only records if over max remembered tiles.
4. Allow user setting to disable pruning.

## 18. Testing Plan

### 18.1 Build and Package Tests

Run:

```powershell
& ".\Scripts\Deploy-RimWorldMod.ps1"
```

Verify deployed local package contains only expected files:

```text
1.6/Assemblies/WalkTheWorld.dll
1.6/Assemblies/WalkTheWorld.pdb
About/About.xml
About/ModIcon.png
About/Preview.png
Defs/WorldObjectDef.xml
Languages/English/Keyed/English.xml
Textures/GenericWorldSite.png
```

It should not include copied RimWorld, Unity, or Harmony DLLs.

### 18.2 RimWorld Load Tests

1. Launch RimWorld.
2. Enable Harmony.
3. Enable local Walk the World.
4. Confirm no red XML/class load errors.
5. Check RimWorld log if errors occur:

```text
%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log
```

### 18.3 Travel Tests

1. Start a new test colony.
2. Draft one pawn.
3. Walk to map edge using normal right-click movement.
4. Confirm preview appears immediately after `Goto` completes.
5. Confirm target tile is directional.
6. Confirm selected pawn enters new map.
7. Select multiple drafted pawns and repeat.
8. Confirm all traveling pawns are reselected after map entry.
9. Test with confirmation disabled.
10. Test leaving type: selected, everyone, always ask.
11. Test moving toward ocean/lake and confirm no bad map generation.

### 18.4 Map Size Tests

1. Default `60x60` exploration map.
2. Minimum `30x30`.
3. Larger `200x200` or `300x300`.
4. Confirm entry placement still lands near expected opposite edge.

### 18.5 Persistence Tests After Future Implementation

1. Visit tile, leave, save, reload, confirm record remains.
2. Loot landmark, leave, re-enter, confirm loot state is remembered.
3. Clear hostiles, leave, re-enter, confirm cleared state is remembered.
4. Leave hostile map uncleared, re-enter, confirm danger state is remembered.
5. Build camp structures, leave, confirm camp state.
6. Use survey/scavenge repeatedly, confirm depletion.
7. Confirm old saves load safely with null/default records.

### 18.6 Compatibility Tests

1. Harmony only.
2. With Odyssey active.
3. With common biome mods.
4. With settlement/faction mods.
5. With caravan/progression mods.
6. With WASD/direct pawn control mods if available.

## 19. Recommended Implementation Phases

### Phase 0: Packaging and Build Foundation

Status: mostly done.

Completed:

1. Added `About/About.xml`.
2. Added `Defs/WorldObjectDef.xml`.
3. Added `Languages/English/Keyed/English.xml`.
4. Added texture/icon assets.
5. Build emits to `1.6/Assemblies`.
6. Added deploy script.
7. Clean local deploy verified.

Remaining:

1. Verify in-game that local mod loads without XML/type errors.
2. Review Harmony reference path for RimWorld 1.6.

### Phase 1: Tile Memory Save Data

Goal: add `ExploredTileRecord` and save/load registry.

Tasks:

1. Add `ExploredTileRecord.cs`.
2. Add compile include to `CellByCell.csproj`.
3. Add dictionary and Scribe logic to `WalkTheWorldComponent.cs`.
4. Add helper API for getting/creating records.
5. Record first entry and visit count.
6. Build and load-test save/reload.

Success criteria:

1. Entering an exploration tile creates a saved record.
2. Save/reload preserves records.
3. Old saves without records load safely.

### Phase 2: Map Removal Summary

Goal: record durable effects when temporary maps disappear.

Tasks:

1. Add `MapMemoryScanner` helper.
2. Call it from `VisitCell.Notify_MyMapRemoved`.
3. Record player-built, player-cache, hostiles remaining, mutator removal, and depletion flags.
4. Keep scans simple and map-removal-only.

Success criteria:

1. Leaving a changed exploration tile updates its record.
2. Mutator removal is persisted in the record.
3. No tick performance impact.

### Phase 3: Reapply Memory On Generation

Goal: make re-entered maps reflect prior state.

Tasks:

1. Add `TileMemoryApplicator`.
2. Call after exploration map generation.
3. Apply conservative effects for looted/cleared/dangerous/depleted states.
4. Avoid modifying settlements and special quest sites initially.

Success criteria:

1. Re-entered looted tiles do not feel freshly unlooted.
2. Cleared tiles avoid immediate repeated threat reward loops.
3. Dangerous uncleared tiles warn the player.

### Phase 4: World UI and Gizmos

Goal: make exploration history visible.

Tasks:

1. Add inspect string showing visit count and states.
2. Add basic gizmos: abandon, survey, scavenge.
3. Review existing `WorldObject_GetGizmos_Patch.cs` first.
4. Add language keys.

Success criteria:

1. Player can inspect explored tile history from world map.
2. Player can perform at least one useful world action.

### Phase 5: Survey and Scavenge System

Goal: add progression-friendly world actions.

Tasks:

1. Implement survey action.
2. Implement scavenge action.
3. Add depletion/recovery logic.
4. Add event hooks for mods.
5. Add settings toggles.

Success criteria:

1. Survey reveals useful info.
2. Scavenge gives modest biome/context rewards.
3. Repeated use depletes rewards.
4. Other mods can hook into outcomes.

### Phase 6: Public API and Def Extensions

Goal: become a platform.

Tasks:

1. Add `WalkTheWorldAPI` class.
2. Add C# events.
3. Add first custom Def type for survey/scavenge outcomes.
4. Document API in a repo markdown file or README section.
5. Add sample XML def.

Success criteria:

1. Another mod can detect tile exploration without hard patching internals.
2. Another mod can add a survey/scavenge outcome through XML or C#.

### Phase 7: Anchored Camps

Goal: allow intentional semi-persistent road camps.

Tasks:

1. Add camped/claimed tile state.
2. Add world gizmo or in-map building trigger.
3. Add settings and warnings.
4. Avoid keeping too many maps alive by default.

Success criteria:

1. Player can intentionally create a meaningful travel camp.
2. Camps are visible and useful on world map.
3. Save/performance risk is controlled.

### Phase 8: Settlement Memory

Goal: settlements remember player actions.

Tasks:

1. Add local settlement visit records.
2. Track visits, trade, quest, theft, violence.
3. Add cooldowns and local consequences.
4. Expose hooks for diplomacy/faction mods.

Success criteria:

1. Revisited settlements feel like continuous places.
2. Theft/destruction has local memory beyond instant goodwill changes.

## 20. Known Code Areas To Review Before Major Changes

Before implementing persistence, read these files fully:

```text
WalkTheWorldComponent.cs
MapGenerator.cs
VisitCell.cs
Utility/WalkTheWorld_WorldTileUtility.cs
HarmonyPatches/WorldObject_GetGizmos_Patch.cs
HarmonyPatches/FloatMenuMakerMap_GetProviderOptions_Patch.cs
HarmonyPatches/Pawn_TraderTracker_Goods_Patch.cs
HarmonyPatches/Pawn_TraderTracker_GiveSoldThingToPlayer_Patch.cs
HarmonyPatches/SettlementDefeatUtility_CheckDefeated_Patch.cs
HarmonyPatches/TradeDeal_AddAllTradeables_Patch.cs
```

Reasons:

1. World-object gizmos may already include abandon/walk tile actions.
2. Settlement/trade patches may already track stolen goods or settlement consequences.
3. Map removal and caravan formation patches may affect persistence semantics.
4. Travel entry points must remain consistent across edge walking and caravan world gizmos.

## 21. Agent Handoff Instructions

If another agent continues this work, start with:

```powershell
git status --short
& ".\Scripts\Deploy-RimWorldMod.ps1"
```

Then inspect any RimWorld load errors from:

```text
%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log
```

Project rules:

1. Do not revert unrelated user changes.
2. Use `apply_patch` for manual edits.
3. Add new `.cs` files to `CellByCell.csproj` or they will not compile.
4. Keep package ID `addvans.WalkTheWorld` unless explicitly asked otherwise.
5. Keep RimWorld/Unity/Harmony references `Private=False`.
6. Keep generated `1.6/Assemblies/*.dll` and `*.pdb` out of git unless explicitly asked.
7. Prefer compact save data by default; reserve full map persistence for full-size, player-touched, camped, or claimed tiles.
8. Preserve the tick fallback travel check for compatibility.
9. Make behavior configurable before changing core player expectations.
10. Test in a new save before testing old saves.

## 22. Strategic Positioning

To become a modpack staple, Walk the World should present itself as:

```text
The exploration memory layer for RimWorld.
```

Not just:

```text
A mod that lets you walk to the next map.
```

The strongest modpack pitch:

1. Pairs with progression mods by exposing exploration events and records.
2. Pairs with biome mods by making biome traversal matter.
3. Pairs with faction mods by giving settlements local memory.
4. Pairs with survival mods by adding forage, camp, and travel consequences.
5. Pairs with quest mods by making tile history and route history available.
6. Pairs with RPG mods by turning travel into a source of experience, discovery, and risk.

The mod should become infrastructure for stories like:

1. A caravan crossing the planet and building a chain of safe camps.
2. A faction remembering that the player looted a border village.
3. An explorer returning to a ruin and finding only rubble because they already stripped it.
4. A survivalist using survey skill to find food and avoid danger.
5. A progression mod rewarding long-range expeditions, landmark discovery, and road scouting.

## 23. First Concrete Next Step

The next best coding task is Phase 1: add `ExploredTileRecord` save data.

Why this first:

1. It is foundational.
2. It is low risk.
3. It can be tested without designing every future feature.
4. It unlocks inspect text, regeneration, survey/scavenge, and API work.

Minimal Phase 1 acceptance test:

1. Build and deploy.
2. Start a new game.
3. Walk to a new exploration tile.
4. Save game.
5. Reload game.
6. Confirm via log/debug inspect that the tile has a persisted record with `visitCount = 1`.

Once that works, the mod has a durable memory spine. Every open-world feature can attach to it.
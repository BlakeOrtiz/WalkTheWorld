# Walk The World 
Rimworld mod for seamless exploration of the world map. Explore every inch of the planet! Walk your drafted pawns all the way to the corner of the map in order to move them to neighbor map. Attack hostile settlements, visit and trade with friendly settlements - become a fully independent explorer!

* Walk around the planet by going into map's edges
* Explore map tiles - all their quests, ruins, camps and etc.
* Visit friendly settlements - trade, get quests, or steal stuff and be punished
* Modded biomes, Odyssey Landmarks are supported
* Way more customizability options since the last version were added
* Get quests from the friendly settlements

## How it works?

Every 64 ticks mod checks if any of the drafted pawns are on the edge of map. If so, a confirmation menu will appear to agree to leave this map and move to the next. Next map is chosen directionally – going to the up part of the map will check for a Northern tile on the world, going down will check for a Southern etc. 

New map will be marked as Exploration tile – such tile is derived from vanilla Camp sites, therefore inheriting most of it's features, including mapgen features. That will prevent any rich resources from spawning, still leaving however some usual ones like trees and stones, and some empty ruins.

If new tile already has any tile - settlement, quest site or any other -, mod will attempt to enter it the most compatible way. If entered settlement is friendly or neutral, villagers will remain non-agressive. Furthermore, one pawn will always trade with visitors, aswell as offering them quests. Stealing or destroying settlements is punished – stealing will affect relations with that faction as much as expensive the stolen items were.

## Odyssey support

Odyssey's landmarks **are supported** – new landmarks can be explored seamlessly. Odyssey puts ruins around using map mutators, that affects the way tile is generated. Walk the World remove mutator from visited tile upon leaving. I.e. entering tile with "Ancient ruins" mutator will allow to loot it, but ruins will be removed from the tile upon leaving, therefore making it impossible to reloot it again. Mutator removing system is customizable – new mutators to remove can be added in order to support modded ruins, and Odyssey's mutators can be removed from the kill-list in order to abuse it and loot away.

## Technical limitations

I tried to went **big** with this mod. However, some limitations are there:
* Mapgen is long. Therefore, Light-mode exploration sites are limited to 30x30 or 60x60 cells. Standard mode locks exploration maps to 200x200, the smallest persistent-size map identified in vanilla/DLC data. Immersive mode allows persistent-size maps from 200x200 up to 325x325 in 25-cell increments;
* Light-mode exploration maps unload when no pawns remain, even if the player changed or built on them. Standard and Immersive maps are persistent-size maps; visited exploration maps are kept in the save, but hibernate while empty so their pawns/things do not actively tick. Exploration tiles do not use caravan-ambush incident tags, but retaining many saved maps still has save and memory cost.

## Mod compatibility

Modded settlements schemes, new biomes or any other **mapgen** stuff should be supported, as I've tried to run map generator as mod-friendly as possible. It is worth noting that mods affecting settlements behaviours are, probably, not supported, since faction bases are heavily patched by Walk The World. 

Trader's quests are derived from the in-game lists of quests where QuestGiverTag is Trader (Spacer factions can also give OrbitalScanner quests). Therefore, adding more quests with that tag should make Walk The World be available to give it quest, too.

If you're a modder and want to create something more complicated, feel free to contact me.

## Local testing build

This repository is set up as a RimWorld 1.6 local mod package. Build output goes to `1.6/Assemblies/WalkTheWorld.dll`, matching RimWorld's versioned mod folder convention.

To build and deploy a local test copy into the Steam RimWorld install, run from this folder:

```powershell
& ".\Scripts\Deploy-RimWorldMod.ps1"
```

By default the script deploys to:

```text
C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\WalkTheWorld
```

After deploying, enable the local **Walk the World** mod in RimWorld's mod list. Harmony must also be installed and loaded before this mod.


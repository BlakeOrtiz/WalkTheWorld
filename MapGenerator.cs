using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace WalkTheWorld
{
    public class MapGenerator
    {
        public Map generatedMap;
        private WorldObjectDef decidedDef;
        private Settlement settlement;
        private int targetTile;
        private bool hasLandmark = false;
        private static int mapsSinceLastEvent = 0;
        private static int mapSize => WalkTheWorldMod.Settings.mapSize;
        private static int eventChance => WalkTheWorldMod.Settings.eventChance;
        private static int mapCountForEvent => WalkTheWorldMod.Settings.mapCountForEvent;
        private static WorldObjectDef ExplorationTileDef => DefDatabase<WorldObjectDef>.GetNamed("ExplorationTile");
        private static IntVec3 decidedSize => new IntVec3(mapSize, 1, mapSize);

        public MapGenerator(PlanetTile tile)
        {
            this.targetTile = tile.tileId;
            hasLandmark = tile.Tile.Landmark != null;
            var existingObjects = Find.WorldObjects.AllWorldObjects.Where(w => w.Tile == targetTile).ToList();
            bool isFriendlySettlement = existingObjects.OfType<Settlement>()
                                                     .Any(s => s.Faction != null && s.Faction != Faction.OfPlayer && s.Faction.PlayerGoodwill >= 0);

            if (isFriendlySettlement)
            {
                settlement = existingObjects.OfType<Settlement>().First(s => s.Faction != null && s.Faction != Faction.OfPlayer && s.Faction.PlayerGoodwill >= 0);
                decidedDef = settlement.def;
            }
            else if (existingObjects.Any(o => o is MapParent || o is Site || o is Settlement))
            {
                var primaryObject = existingObjects.FirstOrDefault(o => o is Settlement || o is Site) ?? existingObjects.First();
                decidedDef = primaryObject.def;
            }
            else
            {
                decidedDef = ExplorationTileDef;
            }
        }

        public void StartGeneration()
        {
            Map existingMap = Current.Game.FindMap(targetTile);
            bool generatedNewMap = existingMap == null;
            if (settlement != null || decidedDef != ExplorationTileDef || hasLandmark)
                generatedMap = GetOrGenerateMapUtility.GetOrGenerateMap(targetTile, decidedDef);
            else
                generatedMap = GetOrGenerateMapUtility.GetOrGenerateMap(targetTile, decidedSize, decidedDef);
            if (generatedNewMap && Find.CurrentMap != null && generatedMap != Find.CurrentMap)
                TransferWeatherEvent(Find.CurrentMap, generatedMap);
            if (settlement != null)
            {
                SpawnSettlementTrader(generatedMap, settlement);
            }
            else if (generatedNewMap)
                if (!TryCreateEventForMap(generatedMap))
                    mapsSinceLastEvent += 1;
        }
        
        public static void TransferWeatherEvent(Map fromMap, Map toMap)
        {
            foreach (var b in fromMap.gameConditionManager.ActiveConditions)
            {
                if(b.Permanent) continue;
                toMap.gameConditionManager.RegisterCondition(b);
            }
        }

        public static bool TryCreateEventForMap(Map map)
        {
            try
            {
                if (map == null)
                    return false;
                if (!(UnityEngine.Random.Range(1, 101) <= eventChance || (mapCountForEvent > 0 && mapsSinceLastEvent >= mapCountForEvent)))
                    return false;
                IncidentParms parms = new IncidentParms
                {
                    target = map,
                    points = StorytellerUtility.DefaultThreatPointsNow(Find.World),
                    forced = true 
                };
                var c = DefDatabase<IncidentDef>.AllDefs;
                List<IncidentDef> b = new List<IncidentDef>();
                if (WalkTheWorldMod.Settings.eventsFilter == RandomEventsFilterType.Filtered)
                    b = c.Where(x => x.TargetAllowed(map)).ToList();
                else
                    b = c.ToList();

                if (!b.Any())
                    return false;

                int attempts = Math.Min(b.Count, 12);
                for (int i = 0; i < attempts; i++)
                {
                    IncidentDef incident = b.RandomElement();
                    FiringIncident fi = new FiringIncident(
                        def: incident,
                        Find.Storyteller.storytellerComps.FirstOrDefault(),
                        parms: parms
                    );
                    if (Find.Storyteller.TryFire(fi))
                    {
                        mapsSinceLastEvent = 0;
                        return true;
                    }
                    b.Remove(incident);
                    if (!b.Any())
                        break;
                }
                return false;
            }
            catch (Exception ex)
            {
                Log.Error($"Error generating event! {ex.ToString()}");
                return false;
            }

        }
        public static void SpawnSettlementTrader(Map map, Settlement settlement)
        {
            if (map == null || settlement?.trader?.TraderKind == null)
                return;

            Pawn traderPawn = map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.Faction == settlement.Faction &&
                           p.RaceProps.Humanlike &&
                           !p.IsPrisoner &&
                           !p.Downed);
            if (traderPawn == null)
                return;

            if (traderPawn.trader == null)
            {
                traderPawn.trader = new Pawn_TraderTracker(traderPawn);
            }
            traderPawn.trader.traderKind = settlement.trader.TraderKind;
            traderPawn.mindState.wantsToTradeWithColony = true;
        }
       
       
    }
}

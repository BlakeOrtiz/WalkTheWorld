using RimWorld.Planet;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace WalkTheWorld
{
    public class VisitCell : Camp
    {
        public bool affected = false;
        public static bool IsAffectedByPlayer(Map map)
        {
            if (map.listerBuildings.allBuildingsColonist.Any())
                return true;
            if (map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableAlways).Any(t => t.Faction == Faction.OfPlayer))
                return true;
            if (map.zoneManager.AllZones.Any(z => z is Zone_Stockpile || z is Zone_Growing))
                return true;

            return false;
        }

        public bool DoPawnBlockRemove()
        {
            return base.Map.mapPawns.AnyPawnBlockingMapRemoval;
        }

        public override string GetInspectString()
        {
            string inspectString = base.GetInspectString();
            if (WalkTheWorld.Instance == null || !WalkTheWorld.Instance.TryGetTileRecord(this.Tile.tileId, out ExploredTileRecord record))
                return inspectString;

            if (!string.IsNullOrEmpty(inspectString))
                inspectString += "\n";
            inspectString += "WTW_ExploredTile_Visits".Translate(record.visitCount.ToString());

            if (record.lastVisitedTick >= 0)
            {
                int ticksAgo = Math.Max(0, Find.TickManager.TicksGame - record.lastVisitedTick);
                float daysAgo = ticksAgo / 60000f;
                inspectString += "\n" + "WTW_ExploredTile_LastVisited".Translate(daysAgo.ToString("0.0"));
            }

            return inspectString;
        }

        public override void Notify_MyMapRemoved(Map map)
        {
            List<WorldObjectComp> allComps = base.AllComps;
            for (int i = 0; i < allComps.Count; i++)
            {
                allComps[i].PostMyMapRemoved();
            }
            QuestUtility.SendQuestTargetSignals(questTags, "MapRemoved", this.Named("SUBJECT"));

            if (ModsConfig.OdysseyActive && this.Tile.Tile.Landmark != null)
            {
                List<TileMutatorDef> listToRemove = WalkTheWorldMod.Settings.mutatorsToDelete
                     .Select(defName => DefDatabase<TileMutatorDef>.GetNamedSilentFail(defName))
                     .Where(def => def != null)
                     .ToList();
                foreach (var mut in listToRemove)
                    if (this.Tile.Tile.Mutators.Contains(mut))
                        this.Tile.Tile.Mutators.Remove(mut);
            }
        }
        public bool TaskedToRemove = false;
        public override bool ShouldRemoveMapNow(out bool alsoRemoveWorldObject)
        {
            if (TaskedToRemove)
            {
                alsoRemoveWorldObject = true;
                return true;
            }
            if (!base.Map.mapPawns.AnyPawnBlockingMapRemoval)
            {
                if (!affected)
                    affected = IsAffectedByPlayer(this.Map);
                if (!affected)
                {
                    alsoRemoveWorldObject = true;
                    return true;
                }
            }
            alsoRemoveWorldObject = false;
            return false;
        }
        public override void Notify_CaravanFormed(Caravan caravan)
        {
            base.Notify_CaravanFormed(caravan);
        }
        public override void PostMapGenerate()
        {
            base.PostMapGenerate();

        }
    }
}

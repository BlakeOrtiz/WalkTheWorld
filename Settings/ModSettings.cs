using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace WalkTheWorld
{
    public class WalkTheWorldModSettings : ModSettings
    {

        public int mapSize = 60;
        public int eventChance = 15;
        public int mapCountForEvent = 5;
        public bool showConfirmationPreviewMenu = true;
        public bool disableExitMapGridEverywhere = true;
        public LeavingType leavingType = LeavingType.Selected;
        public CameraFocusMode camFocus = CameraFocusMode.OnEnteredPawns;
        public RandomEventsFilterType eventsFilter = RandomEventsFilterType.Filtered;
        public PersistenceMode persistenceMode = PersistenceMode.Light;
        public List<string> mutatorsToDelete = new List<string>();
        public bool initialized = false;

        public const int MinExplorationMapSize = 30;
        public const int DefaultExplorationMapSize = 60;
        public const int LightCompactMapSize = MinExplorationMapSize;
        public const int LightDefaultMapSize = DefaultExplorationMapSize;
        public const int MinPersistentMapSize = 200;
        public const int StandardExplorationMapSize = MinPersistentMapSize;
        public const int ImmersiveMapSizeStep = 25;
        public const int MaxExplorationMapSize = 325;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref initialized, "initialized", false);
            Scribe_Values.Look(ref mapSize, "mapSize", DefaultExplorationMapSize);
            Scribe_Values.Look(ref eventChance, "eventChance", 15);
            Scribe_Values.Look(ref mapCountForEvent, "mapCountForEvent", 5);
            Scribe_Values.Look(ref leavingType, "leavingType", LeavingType.Selected);
            Scribe_Values.Look(ref eventsFilter, "eventsFilter", RandomEventsFilterType.Filtered);
            Scribe_Values.Look(ref persistenceMode, "persistenceMode", PersistenceMode.Light);
            Scribe_Values.Look(ref camFocus, "camFocus", CameraFocusMode.OnEnteredPawns);
            Scribe_Values.Look(ref showConfirmationPreviewMenu, "showConfirmationPreviewMenu", true);
            Scribe_Values.Look(ref disableExitMapGridEverywhere, "disableExitMapGridEverywhere", true);
            Scribe_Collections.Look(ref mutatorsToDelete, "mutatorsToDeleteNames", LookMode.Value);
            ApplyMapSizePolicy();
            eventChance = Math.Max(0, Math.Min(100, eventChance));
            mapCountForEvent = Math.Max(0, Math.Min(40, mapCountForEvent));
            if (mutatorsToDelete == null)
                mutatorsToDelete = new List<string>();
        }

        public int MinMapSizeForCurrentMode()
        {
            return persistenceMode == PersistenceMode.Light ? MinExplorationMapSize : MinPersistentMapSize;
        }

        public int MaxMapSizeForCurrentMode()
        {
            return persistenceMode == PersistenceMode.Light ? LightDefaultMapSize : MaxExplorationMapSize;
        }

        public bool MapSizeLockedByMode()
        {
            return persistenceMode == PersistenceMode.Standard;
        }

        public void ApplyMapSizePolicy()
        {
            if (persistenceMode == PersistenceMode.Light)
            {
                mapSize = SnapToLightSize(mapSize);
                return;
            }

            if (MapSizeLockedByMode())
            {
                mapSize = StandardExplorationMapSize;
                return;
            }

            int minSize = MinMapSizeForCurrentMode();
            int maxSize = MaxMapSizeForCurrentMode();
            mapSize = Math.Max(minSize, Math.Min(maxSize, mapSize));
            if (persistenceMode == PersistenceMode.Immersive)
                mapSize = SnapToImmersiveStep(mapSize);
        }

        public int SnapToLightSize(int value)
        {
            int midpoint = (LightCompactMapSize + LightDefaultMapSize) / 2;
            return value <= midpoint ? LightCompactMapSize : LightDefaultMapSize;
        }

        public int SnapToImmersiveStep(int value)
        {
            int clamped = Math.Max(MinPersistentMapSize, Math.Min(MaxExplorationMapSize, value));
            int offset = clamped - MinPersistentMapSize;
            int snappedOffset = (int)Math.Round(offset / (float)ImmersiveMapSizeStep, MidpointRounding.AwayFromZero) * ImmersiveMapSizeStep;
            return Math.Max(MinPersistentMapSize, Math.Min(MaxExplorationMapSize, MinPersistentMapSize + snappedOffset));
        }

        public void InitializeMutators()
        {
            initialized = true;
            mutatorsToDelete = DefDatabase<TileMutatorDef>.AllDefs
                     .Where(m => m.defName != null && (
                         m.defName.Contains("Ancient") ||
                         m.defName.Contains("Abandoned") ||
                         m.defName.Contains("Stockpile") ||
                         m.defName.Contains("Ruins")))
                     .Select(m => m.defName) 
                     .ToList();
        }
    }

}

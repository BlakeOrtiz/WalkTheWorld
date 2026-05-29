using Verse;

namespace WalkTheWorld
{
    public class ExploredTileRecord : IExposable
    {
        public int tileId = -1;
        public int visitCount = 0;
        public int firstVisitedTick = -1;
        public int lastVisitedTick = -1;
        public int lastLeftTick = -1;
        public int lastKnownMapSizeX = -1;
        public int lastKnownMapSizeZ = -1;
        public bool hasBeenEntered = false;
        public bool playerChangedMap = false;
        public bool unloadedAfterPlayerChanges = false;

        public ExploredTileRecord()
        {
        }

        public ExploredTileRecord(int tileId)
        {
            this.tileId = tileId;
        }

        public void NotifyEntered(int tick)
        {
            if (firstVisitedTick < 0)
                firstVisitedTick = tick;
            lastVisitedTick = tick;
            visitCount += 1;
            hasBeenEntered = true;
        }

        public void NotifyMapRemoved(int tick, Map map, bool changedByPlayer, bool unloadedChangedMap)
        {
            lastLeftTick = tick;
            playerChangedMap = playerChangedMap || changedByPlayer;
            unloadedAfterPlayerChanges = unloadedAfterPlayerChanges || unloadedChangedMap;
            if (map != null)
            {
                lastKnownMapSizeX = map.Size.x;
                lastKnownMapSizeZ = map.Size.z;
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref tileId, "tileId", -1);
            Scribe_Values.Look(ref visitCount, "visitCount", 0);
            Scribe_Values.Look(ref firstVisitedTick, "firstVisitedTick", -1);
            Scribe_Values.Look(ref lastVisitedTick, "lastVisitedTick", -1);
            Scribe_Values.Look(ref lastLeftTick, "lastLeftTick", -1);
            Scribe_Values.Look(ref lastKnownMapSizeX, "lastKnownMapSizeX", -1);
            Scribe_Values.Look(ref lastKnownMapSizeZ, "lastKnownMapSizeZ", -1);
            Scribe_Values.Look(ref hasBeenEntered, "hasBeenEntered", false);
            Scribe_Values.Look(ref playerChangedMap, "playerChangedMap", false);
            Scribe_Values.Look(ref unloadedAfterPlayerChanges, "unloadedAfterPlayerChanges", false);
        }
    }
}
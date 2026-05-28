using Verse;

namespace WalkTheWorld
{
    public class ExploredTileRecord : IExposable
    {
        public int tileId = -1;
        public int visitCount = 0;
        public int firstVisitedTick = -1;
        public int lastVisitedTick = -1;
        public bool hasBeenEntered = false;

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

        public void ExposeData()
        {
            Scribe_Values.Look(ref tileId, "tileId", -1);
            Scribe_Values.Look(ref visitCount, "visitCount", 0);
            Scribe_Values.Look(ref firstVisitedTick, "firstVisitedTick", -1);
            Scribe_Values.Look(ref lastVisitedTick, "lastVisitedTick", -1);
            Scribe_Values.Look(ref hasBeenEntered, "hasBeenEntered", false);
        }
    }
}
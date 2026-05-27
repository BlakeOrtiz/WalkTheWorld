using RimWorld.Planet;
using RimWorld;
using System;
using Verse;
using UnityEngine;

namespace WalkTheWorld
{
    public static class WalkTheWorld_WorldTileUtility
    {
        public static bool isTileWalkable(PlanetTile tile)
        {
            if (tile.Tile.PrimaryBiome == BiomeDefOf.Ocean ||
          tile.Tile.PrimaryBiome == BiomeDefOf.Lake)
            {
                return false;
            }
            return true;
        }

        public static bool IsOnEdge(IntVec3 c, Map map, int edgeSize = 1)
        {
            return c.x < edgeSize || c.z < edgeSize || c.x >= map.Size.x - edgeSize || c.z >= map.Size.z - edgeSize;
        }

        public static IntVec3 GetDirectionFromCenter(Map map, IntVec3 pos)
        {
            IntVec3 center = map.Center;
            IntVec3 offset = pos - center;
            return offset;
        }

        public static Direction8Way ToDirection8Way(IntVec3 offset)
        {
            if (offset.x == 0 && offset.z == 0)
                return Direction8Way.North;
            float angle = Mathf.Atan2(offset.z, offset.x) * Mathf.Rad2Deg;
            angle = (90f - angle + 360f) % 360f;
            angle = (angle + 22.5f) % 360f;
            int sector = (int)(angle / 45f);
            return (Direction8Way)sector;
        }

        public static int GetTileInDirection(int fromTile, Direction8Way dir)
        {
            int res = GetTileInSpecificDirection(fromTile, dir);
            if (res != -1)
                return res;

            for (int offset = 1; offset <= 4; offset++)
            {
                Direction8Way clockwise = (Direction8Way)(((int)dir + offset) % 8);
                res = GetTileInSpecificDirection(fromTile, clockwise);
                if (res != -1)
                    return res;

                Direction8Way counterClockwise = (Direction8Way)(((int)dir - offset + 8) % 8);
                res = GetTileInSpecificDirection(fromTile, counterClockwise);
                if (res != -1)
                    return res;
            }
            return -1;
        }

        public static int GetTileInSpecificDirection(int fromTile, Direction8Way dir)
        {
            for (int i = 0; i < Find.WorldGrid.GetTileNeighborCount(fromTile); i++)
            {
                int neighborTile = Find.WorldGrid.GetTileNeighbor(fromTile, i);
                Direction8Way newDir = Find.WorldGrid.GetDirection8WayFromTo(fromTile, neighborTile);

                if (newDir == dir)
                {
                    return neighborTile;
                }
            }
            return -1;

        }
        public static Predicate<IntVec3> GetEntryPredicate(Map map, IntVec3 oldPos,IntVec3 oldMapSize, out IntVec3 camPos)
        {
            IntVec3 newCenter = map.Center;
            float normalizedX = (float)oldPos.x / oldMapSize.x;
            float normalizedZ = (float)oldPos.z / oldMapSize.z;
            IntVec3 mirroredPos = new IntVec3(
                (int)(map.Size.x * (1 - normalizedX)),
                0,
                (int)(map.Size.z * (1 - normalizedZ))
            );
            mirroredPos = mirroredPos.ClampInsideMap(map);

            Predicate<IntVec3> extraValidator = (IntVec3 c) =>
                c.InBounds(map) &&
                c.Standable(map) &&
                (c - mirroredPos).LengthHorizontalSquared <= 100 &&
                (c.x <= 2 || c.x >= map.Size.x - 3 || c.z <= 2 || c.z >= map.Size.z - 3);

            camPos = mirroredPos;
            return extraValidator;
        }
        public static Predicate<IntVec3> OldGetEntryPredicate(Map map, IntVec3 oldPos, out IntVec3 camPos)
        {
            IntVec3 newMapSize = map.Size;
            IntVec3 newCenter = map.Center;
            IntVec3 deltaFromCenter = oldPos - newCenter;
            IntVec3 mirroredDelta = new IntVec3(-deltaFromCenter.x, 0, -deltaFromCenter.z);
            IntVec3 mirroredPos = newCenter + mirroredDelta;
            Predicate<IntVec3> extraValidator = (IntVec3 c) =>
                                c.InBounds(map) &&
                                c.Standable(map) &&
                                (c - mirroredPos).LengthHorizontalSquared <= 100 && 
                                (c.x <= 2 || c.x >= map.Size.x - 3 || c.z <= 2 || c.z >= map.Size.z - 3);
            camPos = mirroredPos;
            return extraValidator;
        }
        public static string GetTileName(PlanetTile tile)
        {
            string nm;
            nm = $"{"Biome_Label".Translate()}: {tile.Tile.PrimaryBiome.LabelCap}\n";
            nm += $"{tile.Tile.hilliness.GetLabelCap()}\n";
            if (Find.WorldGrid[tile.tileId]?.Roads?.Count > 0)
                nm += $"{Find.WorldGrid[tile.tileId]?.Roads[0].road.LabelCap}\n";
            var b = Find.WorldObjects.WorldObjectAt<WorldObject>(tile);
            if (b != null)
            {
                nm += $"{b.LabelCap}\n";
                if (b.Faction != null)
                    if (b.Faction != Faction.OfPlayer)
                        nm += $"{"Faction_Label".Translate()}: ({b.Faction.Name}) - {b.Faction.RelationKindWith(Faction.OfPlayer)}";
            }
            var c = tile.Tile.Landmark;
            if (c != null)
            {
                nm += $"\n{"Landmark".Translate()}: {c.def.LabelCap} ";
            }
            if (tile.Tile.Mutators?.Count > 0)
            {
                nm += $"\n{"Features_Label".Translate()}:";
                foreach (var m in tile.Tile.Mutators)
                {
                    nm += $"\n{m.LabelCap}";
                }
            }
            return nm;
        }
    }
}

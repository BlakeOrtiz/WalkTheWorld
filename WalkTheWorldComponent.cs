using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using RimWorld.Planet;
using System;

namespace WalkTheWorld
{
    public class WalkTheWorld : GameComponent
    {
        public static WalkTheWorld Instance;
        public int TicksCooldown = 64;
        public int PromptRetryCooldownTicks = 600;
        public int lastEnterTick = 0;
        public IntVec3 lastEnterPos = IntVec3.Zero;
        private const int HibernationCheckIntervalTicks = 250;
        private int suppressPromptUntilTick = 0;
        private int nextHibernationCheckTick = 0;
        private bool confirmationWindowOpen = false;
        private bool welcomeDialogShown = false;
        private HashSet<int> hibernatedExplorationTiles = new HashSet<int>();
        private Dictionary<int, ExploredTileRecord> exploredTiles = new Dictionary<int, ExploredTileRecord>();
        private List<int> exploredTileKeys;
        private List<ExploredTileRecord> exploredTileValues;

        public WalkTheWorld(Game game)
        {
            Instance = this;
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            Instance = this;
            if (exploredTiles == null)
                exploredTiles = new Dictionary<int, ExploredTileRecord>();
            if (hibernatedExplorationTiles == null)
                hibernatedExplorationTiles = new HashSet<int>();

        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref welcomeDialogShown, "welcomeDialogShown", false);
            Scribe_Collections.Look(ref exploredTiles, "exploredTiles", LookMode.Value, LookMode.Deep, ref exploredTileKeys, ref exploredTileValues);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && exploredTiles == null)
                exploredTiles = new Dictionary<int, ExploredTileRecord>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit && hibernatedExplorationTiles == null)
                hibernatedExplorationTiles = new HashSet<int>();
        }

        private void TryShowWelcomeDialog()
        {
            if (welcomeDialogShown)
                return;
            if (Current.Game == null || Find.World == null || Find.WindowStack == null || Find.TickManager == null)
                return;
            if (Find.TickManager.TicksGame < 1)
                return;

            welcomeDialogShown = true;
            Find.WindowStack.Add(new Dialog_MessageBox(
                "WTW_WelcomeDialog_Text".Translate(),
                "WTW_WelcomeDialog_OpenSettings".Translate(),
                WalkTheWorldMod.OpenSettingsWindow,
                "WTW_WelcomeDialog_Continue".Translate(),
                null));
        }

        public ExploredTileRecord GetOrCreateTileRecord(int tileId)
        {
            if (exploredTiles == null)
                exploredTiles = new Dictionary<int, ExploredTileRecord>();
            if (!exploredTiles.TryGetValue(tileId, out ExploredTileRecord record) || record == null)
            {
                record = new ExploredTileRecord(tileId);
                exploredTiles[tileId] = record;
            }
            return record;
        }

        public bool TryGetTileRecord(int tileId, out ExploredTileRecord record)
        {
            record = null;
            return exploredTiles != null && exploredTiles.TryGetValue(tileId, out record) && record != null;
        }

        public ExploredTileRecord RecordTileEntered(int tileId)
        {
            ExploredTileRecord record = GetOrCreateTileRecord(tileId);
            record.NotifyEntered(Find.TickManager.TicksGame);
            return record;
        }

        public ExploredTileRecord RecordTileMapRemoved(int tileId, Map map, bool changedByPlayer, bool unloadedChangedMap)
        {
            ExploredTileRecord record = GetOrCreateTileRecord(tileId);
            record.NotifyMapRemoved(Find.TickManager.TicksGame, map, changedByPlayer, unloadedChangedMap);
            hibernatedExplorationTiles?.Remove(tileId);
            return record;
        }

        public bool IsMapHibernated(Map map)
        {
            return map != null && hibernatedExplorationTiles != null && hibernatedExplorationTiles.Contains(map.Tile);
        }

        public void WakeMap(Map map)
        {
            if (map == null || hibernatedExplorationTiles == null || !hibernatedExplorationTiles.Contains(map.Tile))
                return;

            foreach (Thing thing in map.listerThings.AllThings.ToList())
            {
                Find.TickManager.RegisterAllTickabilityFor(thing);
            }
            hibernatedExplorationTiles.Remove(map.Tile);
        }

        private void HibernateMap(Map map)
        {
            if (map == null || hibernatedExplorationTiles == null || hibernatedExplorationTiles.Contains(map.Tile))
                return;

            Find.TickManager.RemoveAllFromMap(map);
            hibernatedExplorationTiles.Add(map.Tile);
        }

        private bool ShouldHibernateMap(Map map)
        {
            if (map == null || !(map.Parent is VisitCell))
                return false;
            if (!VisitCell.ShouldKeepExplorationMapPersistent(map))
                return false;

            return !map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer).Any();
        }

        private void UpdateExplorationMapHibernation()
        {
            if (Current.Game == null || Find.TickManager == null || Find.TickManager.TicksGame < nextHibernationCheckTick)
                return;

            nextHibernationCheckTick = Find.TickManager.TicksGame + HibernationCheckIntervalTicks;
            foreach (Map map in Current.Game.Maps.ToList())
            {
                if (ShouldHibernateMap(map))
                    HibernateMap(map);
                else if (IsMapHibernated(map))
                    WakeMap(map);
            }
        }

        public bool TryStartTravel(Pawn pawn)
        {
            if (!CanStartTravel(pawn))
                return false;

            Map sourceMap = pawn.Map;
            lastEnterTick = Find.TickManager.TicksGame;
            lastEnterPos = pawn.Position;

            PlanetTile targetTile = WalkTheWorld_WorldTileUtility.GetTileInDirection(sourceMap.Tile, WalkTheWorld_WorldTileUtility.ToDirection8Way(WalkTheWorld_WorldTileUtility.GetDirectionFromCenter(sourceMap, pawn.Position)));
            if (targetTile == -1 || !WalkTheWorld_WorldTileUtility.isTileWalkable(targetTile))
                return false;

            if (WalkTheWorldMod.Settings.showConfirmationPreviewMenu)
            {
                ShowConfirmationWindow(targetTile, pawn);
            }
            else
            {
                ChooseTravelersAndFinalize(pawn, targetTile);
            }
            return true;
        }

        private bool CanStartTravel(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null || !pawn.Spawned)
                return false;
            if (!pawn.IsColonistPlayerControlled || !pawn.Drafted)
                return false;
            if (!WorldRendererUtility.DrawingMap || Find.Selector.SelectedPawns.Count <= 0)
                return false;
            if (confirmationWindowOpen || Find.TickManager.TicksGame < suppressPromptUntilTick)
                return false;
            if (Find.TickManager.TicksGame - lastEnterTick < TicksCooldown)
                return false;
            if (HasNonEdgeJobDestination(pawn))
                return false;
            if (!WalkTheWorld_WorldTileUtility.IsOnEdge(pawn.Position, pawn.Map) || pawn.Position == lastEnterPos)
                return false;
            return true;
        }

        private bool HasNonEdgeJobDestination(Pawn pawn)
        {
            if (pawn?.Map == null || pawn.CurJob == null || !pawn.CurJob.targetA.IsValid)
                return false;

            IntVec3 targetCell = pawn.CurJob.targetA.Cell;
            if (!targetCell.IsValid || !targetCell.InBounds(pawn.Map) || targetCell == pawn.Position)
                return false;

            return !WalkTheWorld_WorldTileUtility.IsOnEdge(targetCell, pawn.Map);
        }

        private void SuppressTravelPrompts(int ticks)
        {
            int untilTick = Find.TickManager.TicksGame + ticks;
            if (untilTick > suppressPromptUntilTick)
                suppressPromptUntilTick = untilTick;
            lastEnterTick = Find.TickManager.TicksGame;
        }

        public void FinalizeTravel(Pawn pawn, Map targetMap)
        {
            FinalizeTravel(pawn, targetMap, GetLeavingPawns(pawn));
        }

        public void FinalizeTravel(Pawn pawn, Map targetMap, List<Pawn> leavingPawns)
        {
            if (pawn == null || pawn.Map == null || targetMap == null)
                return;

            var oldPos = pawn.Position;
            var oldSize = pawn.Map.Size;
            Caravan caravan = LeaveMap(pawn.Map, targetMap, leavingPawns);
            if (caravan == null || !caravan.PawnsListForReading.Any())
                return;

            var camPos = IntVec3.Zero;
            EnterMap(targetMap, caravan, WalkTheWorld_WorldTileUtility.GetEntryPredicate(targetMap, oldPos, oldSize, out camPos));
        }

       public void EnterMap(Map targetMap, Caravan caravan, Predicate<IntVec3> predicate = null)
        {
            List<Pawn> caravanPawns = caravan.PawnsListForReading.ToList();
            if (!caravanPawns.Any())
                return;

            WakeMap(targetMap);
            Pawn firstPawn = caravanPawns[0];
            CaravanEnterMapUtility.Enter(caravan, targetMap, CaravanEnterMode.Edge,
                extraCellValidator: predicate,
                draftColonists: true);
            Current.Game.CurrentMap = targetMap;
            Find.Selector.ClearSelection();
            foreach (Pawn pawn in caravanPawns.Where(p => p.Spawned && p.Map == targetMap))
            {
                Find.Selector.Select(pawn);
            }
            WakeMap(targetMap);
            ResetCamera(GetNewCameraPosition(firstPawn, targetMap));
            RecordTileEntered(targetMap.Tile);
            lastEnterPos = firstPawn.Position;
            lastEnterTick = Find.TickManager.TicksGame;
        }

        IntVec3 GetNewCameraPosition(Pawn pawn, Map newMap)
        {
            Find.World.renderer.wantedMode = WorldRenderMode.None;
            if (WalkTheWorldMod.Settings.camFocus == CameraFocusMode.OnEnteredPawns)
                return pawn.Position;
            if (WalkTheWorldMod.Settings.camFocus == CameraFocusMode.Centered)
                return newMap.Center;
            if (WalkTheWorldMod.Settings.camFocus == CameraFocusMode.Ignore)
                return Find.CameraDriver.MapPosition;
            return Find.CameraDriver.MapPosition;
        }
        void ResetCamera(IntVec3 camPos)
        {
            float zoom = Find.CameraDriver.ZoomRootSize;
            Find.CameraDriver.JumpToCurrentMapLoc(camPos);
            Find.CameraDriver.SetRootSize(zoom);

        }

        Caravan LeaveMap(Map sourceMap, Map targetMap, List<Pawn> pawns)
        {
            pawns = pawns?
                .Where(p => p != null && p.Spawned && p.Map == sourceMap && p.Faction == Faction.OfPlayer)
                .Distinct()
                .ToList() ?? new List<Pawn>();
            if (!pawns.Any())
            {
                Messages.Message("No valid pawns selected to travel.", MessageTypeDefOf.RejectInput, false);
                return null;
            }

            var caravan = CaravanExitMapUtility.ExitMapAndCreateCaravan(pawns, Faction.OfPlayer, sourceMap.Tile, Direction8Way.North, targetMap.Tile, sendMessage: false);
            return caravan;
        }

        List<Pawn> GetLeavingPawns(Pawn triggeringPawn)
        {
            Map sourceMap = triggeringPawn?.Map ?? Find.CurrentMap;
            if (sourceMap == null)
                return new List<Pawn>();

            if (WalkTheWorldMod.Settings?.leavingType == LeavingType.Selected)
                return GetSelectedLeavingPawns(sourceMap, triggeringPawn);
            if (WalkTheWorldMod.Settings?.leavingType == LeavingType.Everyone)
                return GetEveryoneLeavingPawns(sourceMap);

            return GetSelectedLeavingPawns(sourceMap, triggeringPawn);
        }

        List<Pawn> GetSelectedLeavingPawns(Map sourceMap, Pawn triggeringPawn)
        {
            List<Pawn> pawns = Find.Selector.SelectedPawns
                .Where(p => p.IsColonistPlayerControlled && p.Map == sourceMap)
                .Distinct()
                .ToList();
            if (!pawns.Any() && triggeringPawn != null && triggeringPawn.IsColonistPlayerControlled && triggeringPawn.Map == sourceMap)
                pawns.Add(triggeringPawn);
            return pawns;
        }

        List<Pawn> GetEveryoneLeavingPawns(Map sourceMap)
        {
            return sourceMap.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer)
                .Where(p => p.IsColonistPlayerControlled)
                .Distinct()
                .ToList();
        }

        void ShowChoosingWindow(Pawn triggeringPawn, Action<List<Pawn>> onChosen)
        {
            Map sourceMap = triggeringPawn?.Map ?? Find.CurrentMap;
            Find.WindowStack.Add(new Dialog_MessageBox($"WTW_Settings_WhoLeavingTheMapLabel".Translate(),//НАДОПЕРЕВЕСТИ!!
                              "WTW_Settings_EveryoneLeavingTheMap".Translate(), () =>
                              {
                                  onChosen(GetEveryoneLeavingPawns(sourceMap));

                              }, "WTW_Settings_SelecetedLeavingTheMap".Translate(), () =>
                              {
                                  onChosen(GetSelectedLeavingPawns(sourceMap, triggeringPawn));
                              }));
        }

        void ChooseTravelersAndFinalize(Pawn pawn, PlanetTile targetTile)
        {
            if (WalkTheWorldMod.Settings?.leavingType == LeavingType.AlwaysAsk)
            {
                ShowChoosingWindow(pawn, leavingPawns => GenerateAndFinalizeTravel(pawn, targetTile, leavingPawns));
                return;
            }

            GenerateAndFinalizeTravel(pawn, targetTile, GetLeavingPawns(pawn));
        }

        void GenerateAndFinalizeTravel(Pawn pawn, PlanetTile targetTile, List<Pawn> leavingPawns)
        {
            if (leavingPawns == null || !leavingPawns.Any())
            {
                Messages.Message("No valid pawns selected to travel.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            var mapGenerator = new MapGenerator(targetTile);
            mapGenerator.StartGeneration();
            FinalizeTravel(pawn, mapGenerator.generatedMap, leavingPawns);
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            TryShowWelcomeDialog();
            UpdateExplorationMapHibernation();
            if (Find.TickManager.TicksGame - lastEnterTick < TicksCooldown || !WorldRendererUtility.DrawingMap || Find.Selector.SelectedPawns.Count <= 0)
                return;
            Pawn pawn = GetLeavingPawn();
            if (pawn == null)
                return;
            TryStartTravel(pawn);
        }

        Pawn GetLeavingPawn()
        {
            foreach (Pawn pawn in Find.Selector.SelectedPawns)
            {
                if (pawn.Drafted && WalkTheWorld_WorldTileUtility.IsOnEdge(pawn.Position, Find.CurrentMap) && pawn.Position != lastEnterPos)
                    return pawn;
            }
            return null;
        }

        public void ShowConfirmationWindow(PlanetTile targetTile, Pawn pawn)
        {
            confirmationWindowOpen = true;
            FocusCameraOnTile(targetTile);
            var dialog = new Dialog_MessageBoxAdjusted($"{"LetterLabelAreaRevealed".Translate()}:\n\n{WalkTheWorld_WorldTileUtility.GetTileName(targetTile)}\n\n{"WantToContinue".Translate()}",
             "Confirm".Translate(), () => {
                 confirmationWindowOpen = false;
                 SuppressTravelPrompts(TicksCooldown);
                 Find.World.renderer.wantedMode = WorldRenderMode.None;
                 ChooseTravelersAndFinalize(pawn, targetTile);

             }, "GoBack".Translate(), () =>
             {
                 confirmationWindowOpen = false;
                 FocusCameraOnPawn(pawn);
                 SuppressTravelPrompts(PromptRetryCooldownTicks);
             });
            Find.WindowStack.Add(dialog);
        }
        
        void FocusCameraOnTile(PlanetTile targetTile)
        {
            Find.World.renderer.wantedMode = WorldRenderMode.Planet;
            Find.WorldCameraDriver.JumpTo(targetTile);
            Find.WorldCameraDriver.ResetAltitude();
            Find.WorldSelector.SelectedTile = targetTile;
        }

        void FocusCameraOnPawn(Pawn pawn)
        {
            Find.World.renderer.wantedMode = WorldRenderMode.None;
            Find.CameraDriver.JumpToCurrentMapLoc(pawn.Position);
        }
        
        public override void GameComponentOnGUI()
        {
            base.GameComponentOnGUI();
        }
   
    }
}

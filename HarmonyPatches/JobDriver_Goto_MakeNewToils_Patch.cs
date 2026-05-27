using System.Collections.Generic;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace WalkTheWorld.HarmonyPatches
{
    [HarmonyPatch(typeof(JobDriver_Goto), "MakeNewToils")]
    public static class JobDriver_Goto_MakeNewToils_Patch
    {
        public static IEnumerable<Toil> Postfix(IEnumerable<Toil> __result, JobDriver_Goto __instance)
        {
            foreach (Toil toil in __result)
            {
                yield return toil;
            }

            Toil travelCheck = ToilMaker.MakeToil("WalkTheWorld_CheckEdgeTravel");
            travelCheck.initAction = delegate
            {
                WalkTheWorld.Instance?.TryStartTravel(__instance.pawn);
            };
            travelCheck.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return travelCheck;
        }
    }
}

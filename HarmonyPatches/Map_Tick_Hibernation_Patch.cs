using HarmonyLib;
using Verse;

namespace WalkTheWorld.HarmonyPatches
{
    [HarmonyPatch(typeof(Map), nameof(Map.MapPreTick))]
    public static class Map_MapPreTick_Hibernation_Patch
    {
        static bool Prefix(Map __instance)
        {
            return WalkTheWorld.Instance == null || !WalkTheWorld.Instance.IsMapHibernated(__instance);
        }
    }

    [HarmonyPatch(typeof(Map), nameof(Map.MapPostTick))]
    public static class Map_MapPostTick_Hibernation_Patch
    {
        static bool Prefix(Map __instance)
        {
            return WalkTheWorld.Instance == null || !WalkTheWorld.Instance.IsMapHibernated(__instance);
        }
    }
}
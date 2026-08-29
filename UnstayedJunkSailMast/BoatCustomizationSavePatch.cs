using HarmonyLib;

namespace UnstayedJunkSailMast;

[HarmonyPatch(typeof(SaveableBoatCustomization), "GetData")]
internal static class BoatCustomizationSavePatch
{
	private static void Postfix(SaveableBoatCustomization __instance, SaveBoatCustomizationData __result)
	{
		UnstayedSaveCompatibility.RecordActiveOptions(__instance, __result);
	}
}

using System;
using HarmonyLib;

namespace UnstayedJunkSailMast;

[HarmonyPatch(typeof(SaveableBoatCustomization), "Awake")]
[HarmonyAfter(new string[] { "com.nandbrew.shipyardexpansion" })]
[HarmonyPriority(0)]
internal static class BoatCustomizationAwakePatch
{
	private static void Postfix(SaveableBoatCustomization __instance, BoatCustomParts ___parts, BoatRefs ___refs)
	{
		try
		{
			UnstayedMastBuilder.TryBuild(__instance, ___parts, ___refs);
		}
		catch (Exception ex)
		{
			Plugin.LogSource?.LogError("Could not build unstayed mast options on " + __instance.name + ": " + ex);
		}
	}
}

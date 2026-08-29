using System.Collections.Generic;
using HarmonyLib;

namespace UnstayedJunkSailMast;

[HarmonyPatch(typeof(SaveLoadManager), "LoadNeeds")]
[HarmonyBefore(new string[] { "com.nandbrew.shipyardexpansion" })]
[HarmonyPriority(800)]
internal static class EarlyModDataLoadPatch
{
	private static void Postfix(SaveContainer __0)
	{
		GameState.modData = ((__0 != null && __0.modData != null) ? __0.modData : new Dictionary<string, string>());
		UnstayedMastIndexCoordinator.RebindFromLoadedModData();
	}
}

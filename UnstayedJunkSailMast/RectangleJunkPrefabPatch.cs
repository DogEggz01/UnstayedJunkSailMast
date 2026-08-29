using HarmonyLib;

namespace UnstayedJunkSailMast;

[HarmonyPatch(typeof(PrefabsDirectory), "Start")]
[HarmonyAfter(new string[] { "com.nandbrew.shipyardexpansion" })]
internal static class RectangleJunkPrefabPatch
{
	private static void Prefix(PrefabsDirectory __instance)
	{
		RectangleJunkSails.EnsureRegistered(__instance);
	}
}

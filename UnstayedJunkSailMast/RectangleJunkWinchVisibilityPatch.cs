using HarmonyLib;

namespace UnstayedJunkSailMast;

[HarmonyPatch(typeof(Mast), "UpdateWinchesEnabled")]
internal static class RectangleJunkWinchVisibilityPatch
{
	private static void Postfix(Mast __instance)
	{
		RectangleJunkSails.SynchronizeWinchVisibility(__instance);
	}
}

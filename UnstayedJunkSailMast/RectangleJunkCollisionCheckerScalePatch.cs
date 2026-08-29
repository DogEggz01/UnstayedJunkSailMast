using HarmonyLib;

namespace UnstayedJunkSailMast;

[HarmonyPatch(typeof(ShipyardSailColChecker), "RunColCheck")]
internal static class RectangleJunkCollisionCheckerScalePatch
{
	private static void Postfix(Sail ___sail)
	{
		RectangleJunkSails.SynchronizeCollisionCheckerScale(___sail);
	}
}

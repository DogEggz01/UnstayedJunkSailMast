using HarmonyLib;

namespace UnstayedJunkSailMast;

[HarmonyPatch(typeof(BoatRefs), "RegisterMast")]
internal static class IdempotentUnstayedMastRegistrationPatch
{
	private static bool Prefix(BoatRefs __instance, Mast mast)
	{
		if (__instance == null || mast == null || mast.GetComponent<UnstayedMastMarker>() == null || __instance.masts == null)
		{
			return true;
		}
		int orderIndex = mast.orderIndex;
		if (orderIndex >= 0 && orderIndex < __instance.masts.Length)
		{
			return __instance.masts[orderIndex] != mast;
		}
		return true;
	}
}

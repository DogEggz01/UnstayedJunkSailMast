using HarmonyLib;

namespace UnstayedJunkSailMast;

[HarmonyPatch(typeof(SaveableBoatCustomization), "LoadData")]
[HarmonyAfter(new string[] { "com.nandbrew.shipyardexpansion" })]
internal static class BoatCustomizationLoadPatch
{
	private static void Prefix(SaveableBoatCustomization __instance, SaveBoatCustomizationData data)
	{
		UnstayedSaveCompatibility.PrepareLoad(__instance, data);
	}

	private static void Postfix(SaveableBoatCustomization __instance)
	{
		BoatCustomParts component = __instance.GetComponent<BoatCustomParts>();
		if (UnstayedSelectionRules.NormalizeActive(component))
		{
			component.RefreshParts();
		}
	}
}

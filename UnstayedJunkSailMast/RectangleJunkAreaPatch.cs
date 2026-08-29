using HarmonyLib;

namespace UnstayedJunkSailMast;

[HarmonyPatch(typeof(Sail), "GetSailArea")]
internal static class RectangleJunkAreaPatch
{
	private static void Postfix(Sail __instance, ref float __result)
	{
		RectangleJunkSailRig component = __instance.GetComponent<RectangleJunkSailRig>();
		if (component != null)
		{
			__result = component.GetTransformedSailArea(__result);
		}
	}
}

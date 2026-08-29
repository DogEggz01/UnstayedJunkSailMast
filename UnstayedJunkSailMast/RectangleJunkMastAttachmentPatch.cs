using HarmonyLib;

namespace UnstayedJunkSailMast;

[HarmonyPatch(typeof(Mast), "UpdateControllerAttachments")]
internal static class RectangleJunkMastAttachmentPatch
{
	private static void Prefix(Mast __instance)
	{
		RectangleJunkSails.PrepareMast(__instance);
	}

	private static void Postfix(Mast __instance)
	{
		RectangleJunkSails.ApplyToMast(__instance);
	}
}

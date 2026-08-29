using HarmonyLib;
using UnityEngine;

namespace UnstayedJunkSailMast;

[HarmonyPatch(typeof(Mast), "AttachSailToMast")]
internal static class RectangleJunkShipyardInstallPatch
{
	private static void Prefix(GameObject sailObject, out bool __state)
	{
		Sail sail = ((sailObject != null) ? sailObject.GetComponent<Sail>() : null);
		__state = GameState.currentShipyard != null && sail != null && !sail.IsInstalled() && RectangleJunkSails.IsRectangle(sailObject);
	}

	private static void Postfix(GameObject sailObject, bool __state)
	{
		if (__state)
		{
			RectangleJunkSails.FurlNewlyInstalledSail(sailObject);
		}
	}
}

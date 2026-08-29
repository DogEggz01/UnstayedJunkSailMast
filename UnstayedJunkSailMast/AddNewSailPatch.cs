using HarmonyLib;
using UnityEngine;

namespace UnstayedJunkSailMast;

[HarmonyPatch(typeof(ShipyardSailInstaller), "AddNewSail")]
internal static class AddNewSailPatch
{
	private static bool Prefix(ShipyardSailInstaller __instance, GameObject sailObject)
	{
		Mast currentMast = __instance.GetCurrentMast();
		if (currentMast == null || currentMast.GetComponent<UnstayedMastMarker>() == null || UnstayedSailRules.IsAllowed(sailObject))
		{
			return true;
		}
		Sail sail = ((sailObject != null) ? sailObject.GetComponent<Sail>() : null);
		Plugin.LogSource?.LogWarning("Rejected " + ((sail != null) ? sail.sailName : "unknown sail") + " on an unstayed mast.");
		if (sailObject != null && sailObject.scene.IsValid())
		{
			Object.Destroy(sailObject);
		}
		return false;
	}
}

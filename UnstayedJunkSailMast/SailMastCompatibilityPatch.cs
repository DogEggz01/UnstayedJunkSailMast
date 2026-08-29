using HarmonyLib;
using UnityEngine;

namespace UnstayedJunkSailMast;

[HarmonyPatch(typeof(ShipyardUI), "SailMastCompatible")]
internal static class SailMastCompatibilityPatch
{
	private static bool Prefix(GameObject sailPrefab, ref bool __result)
	{
		Shipyard currentShipyard = GameState.currentShipyard;
		Mast mast = ((currentShipyard != null) ? currentShipyard.sailInstaller.GetCurrentMast() : null);
		if (mast == null || mast.GetComponent<UnstayedMastMarker>() == null)
		{
			return true;
		}
		__result = UnstayedSailRules.IsAllowed(sailPrefab);
		return false;
	}
}

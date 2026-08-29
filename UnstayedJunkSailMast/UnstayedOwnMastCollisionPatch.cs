using HarmonyLib;
using UnityEngine;

namespace UnstayedJunkSailMast;

[HarmonyPatch(typeof(ShipyardSailColChecker), "OnTriggerEnter")]
internal static class UnstayedOwnMastCollisionPatch
{
	private static bool Prefix(Collider other, Sail ___sail)
	{
		if (other == null || ___sail == null)
		{
			return true;
		}
		UnstayedScaledBodyMarker component = other.GetComponent<UnstayedScaledBodyMarker>();
		if (component == null || !component.IsWalkBody)
		{
			return true;
		}
		Transform parent = ___sail.transform.parent;
		Mast mast = ((parent != null) ? parent.GetComponent<Mast>() : null);
		if (mast == null || mast.GetComponent<UnstayedMastMarker>() == null)
		{
			return true;
		}
		return component.LogicalRoot != mast.walkColMast;
	}
}

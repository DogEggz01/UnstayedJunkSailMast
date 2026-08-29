using HarmonyLib;
using UnityEngine;

namespace UnstayedJunkSailMast;

[HarmonyPatch(typeof(Sail), "UpdateInstallPosition")]
[HarmonyAfter(new string[] { "com.nandbrew.shipyardexpansion" })]
internal static class UnstayedSailInstallPositionPatch
{
	private static void Postfix(Sail __instance)
	{
		Transform parent = __instance.transform.parent;
		Mast mast = ((parent != null) ? parent.GetComponent<Mast>() : null);
		UnstayedMastMarker unstayedMastMarker = ((mast != null) ? mast.GetComponent<UnstayedMastMarker>() : null);
		if (!(unstayedMastMarker == null) && UnstayedSailRules.UsesDiameterCompensation(__instance) && UnstayedMastBuilder.TryGetSailMountOffset(mast, __instance, unstayedMastMarker.DiameterScale, out var localOffset))
		{
			Vector3 localPosition = __instance.transform.localPosition;
			localPosition.x = localOffset.x;
			localPosition.y = localOffset.y;
			__instance.transform.localPosition = localPosition;
			HingeJoint component = __instance.GetComponent<HingeJoint>();
			if (component != null && component.connectedBody != null)
			{
				component.connectedAnchor = component.connectedBody.transform.InverseTransformPoint(__instance.transform.position);
			}
		}
	}
}

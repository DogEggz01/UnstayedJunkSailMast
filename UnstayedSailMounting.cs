using HarmonyLib;
using UnityEngine;

namespace UnstayedJunkSailMast
{
    [HarmonyPatch(typeof(Sail), "UpdateInstallPosition")]
    [HarmonyAfter(Plugin.ShipyardExpansionGuid)]
    internal static class UnstayedSailInstallPositionPatch
    {
        private static void Postfix(Sail __instance)
        {
            Transform parent = __instance.transform.parent;
            Mast mast = parent != null ? parent.GetComponent<Mast>() : null;
            UnstayedMastMarker marker = mast != null
                ? mast.GetComponent<UnstayedMastMarker>()
                : null;
            if (marker == null ||
                !UnstayedSailRules.UsesDiameterCompensation(__instance))
            {
                return;
            }

            Vector3 offset;
            if (!UnstayedMastBuilder.TryGetSailMountOffset(
                    mast,
                    __instance,
                    marker.DiameterScale,
                    out offset))
            {
                return;
            }

            // Vanilla owns the install height and deliberately resets both
            // radial axes. Restore only the extra clearance required by the
            // enlarged mast body, using an absolute value so repeated updates
            // cannot accumulate drift.
            Vector3 localPosition = __instance.transform.localPosition;
            localPosition.x = offset.x;
            localPosition.y = offset.y;
            __instance.transform.localPosition = localPosition;

            // Mast.AttachSailToMast assigns connectedBody before its final
            // UpdateInstallPosition call. Keep the physical hinge at the
            // corrected sail root instead of the vanilla mast-axis position.
            HingeJoint hinge = __instance.GetComponent<HingeJoint>();
            if (hinge != null && hinge.connectedBody != null)
            {
                hinge.connectedAnchor = hinge.connectedBody.transform
                    .InverseTransformPoint(__instance.transform.position);
            }
        }
    }
}

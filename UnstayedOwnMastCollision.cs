using HarmonyLib;
using UnityEngine;

namespace UnstayedJunkSailMast
{
    internal sealed class UnstayedScaledBodyMarker : MonoBehaviour
    {
        internal Transform LogicalRoot;
        internal bool IsWalkBody;
    }

    [HarmonyPatch(typeof(ShipyardSailColChecker), "OnTriggerEnter")]
    internal static class UnstayedOwnMastCollisionPatch
    {
        private static bool Prefix(Collider other, Sail ___sail)
        {
            if (other == null || ___sail == null)
            {
                return true;
            }

            UnstayedScaledBodyMarker body =
                other.GetComponent<UnstayedScaledBodyMarker>();
            if (body == null || !body.IsWalkBody)
            {
                return true;
            }

            Transform sailParent = ___sail.transform.parent;
            Mast mast = sailParent != null
                ? sailParent.GetComponent<Mast>()
                : null;
            if (mast == null ||
                mast.GetComponent<UnstayedMastMarker>() == null)
            {
                return true;
            }

            // Suppress only this sail's own generated walk body. All other
            // mast, hull, shroud, and fitting colliders retain vanilla checks.
            return body.LogicalRoot != mast.walkColMast;
        }
    }
}

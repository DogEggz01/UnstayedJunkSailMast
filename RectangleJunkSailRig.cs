using System;
using UnityEngine;

namespace UnstayedJunkSailMast
{
    internal sealed class RectangleJunkSailRig : MonoBehaviour
    {
        internal bool Initialize(Mast owningMast)
        {
            mast = owningMast;
            if (initialized)
            {
                return true;
            }

            sail = GetComponent<Sail>();
            SailConnections connections = GetComponent<SailConnections>();
            reefController = connections != null
                ? connections.reefController as RopeControllerSailReef
                : null;
            reefEffect = GetComponentInChildren<
                ReefEffectAnimUniversal>(true);
            if (sail == null || connections == null ||
                reefController == null || reefEffect == null)
            {
                return Fail(
                    "its reefing components are incomplete");
            }

            ApplyControllerDirection();
            initialized = true;
            refreshClothPending = true;
            return true;
        }

        internal float GetTransformedSailArea(float fallback)
        {
            if (sail == null)
            {
                sail = GetComponent<Sail>();
            }

            SkinnedMeshRenderer renderer = sail != null && sail.cloth != null
                ? sail.cloth.GetComponent<SkinnedMeshRenderer>()
                : null;
            Mesh mesh = renderer != null ? renderer.sharedMesh : null;
            if (mesh == null)
            {
                return fallback;
            }

            int[] triangles = mesh.triangles;
            Vector3[] vertices = mesh.vertices;
            float area = 0f;
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                Vector3 first = renderer.transform.TransformVector(
                    vertices[triangles[i]]);
                Vector3 second = renderer.transform.TransformVector(
                    vertices[triangles[i + 1]]);
                Vector3 third = renderer.transform.TransformVector(
                    vertices[triangles[i + 2]]);
                area += Vector3.Cross(
                    second - first,
                    third - first).magnitude * 0.5f;
            }

            return area > 0f ? area : fallback;
        }

        internal void PrepareForMastUpdate()
        {
            RestoreWinch();
        }

        internal void BindAssignedWinch()
        {
            if (!initialized || mast == null)
            {
                return;
            }

            Transform boatRoot = mast.shipRigidbody != null
                ? mast.shipRigidbody.transform
                : mast.transform;
            GPButtonRopeWinch[] winches =
                boatRoot.GetComponentsInChildren<GPButtonRopeWinch>(true);
            for (int i = 0; i < winches.Length; i++)
            {
                if (winches[i] != null &&
                    winches[i].rope == reefController)
                {
                    BindWinch(winches[i]);
                    return;
                }
            }

            RestoreWinch();
        }

        private void LateUpdate()
        {
            if (!initialized || !refreshClothPending)
            {
                return;
            }

            refreshClothPending = false;
            RefreshCloth();

            // All remaining behavior is invoked explicitly by the mast and
            // sail patches. Stop paying for a lifetime LateUpdate after the
            // one deferred Cloth refresh has completed.
            enabled = false;
        }

        private void OnDestroy()
        {
            RestoreWinch();
        }

        private void ApplyControllerDirection()
        {
            float currentUnroll = Mathf.Clamp01(sail.currentUnroll);
            reefController.reverseReefing = true;
            reefController.currentLength = 1f - currentUnroll;
            reefController.changed = true;
        }

        private void BindWinch(GPButtonRopeWinch winch)
        {
            if (boundWinch == winch)
            {
                winch.reverseWindResistance = true;
                return;
            }

            RestoreWinch();
            boundWinch = winch;
            originalWinchReverseResistance =
                winch.reverseWindResistance;
            winch.reverseWindResistance = true;
        }

        private void RestoreWinch()
        {
            if (boundWinch != null)
            {
                boundWinch.reverseWindResistance =
                    originalWinchReverseResistance;
            }

            boundWinch = null;
        }

        private void RefreshCloth()
        {
            if (reefEffect == null || !reefEffect.isActiveAndEnabled)
            {
                return;
            }

            try
            {
                reefEffect.RefreshCloth();
            }
            catch (Exception exception)
            {
                Plugin.LogSource?.LogWarning(
                    "Could not refresh Rectangle Junk cloth for " +
                    name + ": " + exception.Message);
            }
        }

        private bool Fail(string reason)
        {
            if (!warningLogged)
            {
                warningLogged = true;
                Plugin.LogSource?.LogWarning(
                    "Could not initialize Rectangle Junk rig for " +
                    name + ": " + reason + ".");
            }

            return false;
        }

        private Mast mast;
        private Sail sail;
        private RopeControllerSailReef reefController;
        private ReefEffectAnimUniversal reefEffect;
        private GPButtonRopeWinch boundWinch;
        private bool originalWinchReverseResistance;
        private bool initialized;
        private bool refreshClothPending;
        private bool warningLogged;
    }
}

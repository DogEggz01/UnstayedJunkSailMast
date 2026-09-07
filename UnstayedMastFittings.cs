using System.Collections.Generic;
using UnityEngine;

namespace UnstayedJunkSailMast
{
    internal static partial class UnstayedMastBuilder
    {
        private const float MaximumSurfaceCorrection = 1f;
        private const float MinimumMastSectionBand = 0.01f;
        private const float MastSectionBandFraction = 0.0025f;

        private static List<SurfaceFittingPlacement>
            CaptureSurfaceFittings(Transform mastRoot)
        {
            List<SurfaceFittingPlacement> result =
                new List<SurfaceFittingPlacement>();
            HashSet<Transform> added = new HashSet<Transform>();
            List<Transform> candidates = new List<Transform>();
            Transform[] descendants =
                mastRoot.GetComponentsInChildren<Transform>(true);
            CapsuleCollider mastCapsule =
                mastRoot.GetComponent<CapsuleCollider>();
            MeshFilter mastFilter = mastRoot.GetComponent<MeshFilter>();
            Mesh mastMesh = mastFilter != null
                ? mastFilter.sharedMesh
                : null;
            Vector3[] mastVertices =
                mastMesh != null && mastMesh.isReadable
                    ? mastMesh.vertices
                    : null;
            Bounds mastBounds = mastMesh != null
                ? mastMesh.bounds
                : new Bounds();

            for (int i = 0; i < descendants.Length; i++)
            {
                Transform candidate = descendants[i];
                if (candidate == mastRoot)
                {
                    continue;
                }

                // A real winch component is authoritative even when the
                // visible object's name is generic or its mesh is nested.
                if (candidate.GetComponent<GPButtonRopeWinch>() != null ||
                    (IsNamedSurfaceFitting(candidate) &&
                     HasOwnVisibleGeometry(candidate)))
                {
                    candidates.Add(candidate);
                }
            }

            candidates.Sort((first, second) =>
                GetHierarchyDepth(first).CompareTo(
                    GetHierarchyDepth(second)));
            for (int i = 0; i < candidates.Count; i++)
            {
                Transform candidate = candidates[i];
                if (HasSelectedAncestor(candidate, added))
                {
                    continue;
                }

                AddSurfaceFitting(
                    result,
                    added,
                    candidate,
                    mastRoot,
                    mastCapsule,
                    mastVertices,
                    mastBounds);
            }

            return result;
        }

        private static void AddSurfaceFitting(
            List<SurfaceFittingPlacement> result,
            HashSet<Transform> added,
            Transform candidate,
            Transform mastRoot,
            CapsuleCollider mastCapsule,
            Vector3[] mastVertices,
            Bounds mastBounds)
        {
            Vector3 rootPosition;
            if (!TryGetVisibleCenterInRoot(
                    candidate,
                    mastRoot,
                    out rootPosition))
            {
                rootPosition = mastRoot.InverseTransformPoint(
                    candidate.position);
            }

            Vector2 mastCenter;
            Vector2 radial;
            float mastRadius;
            if (!TryResolveMastSection(
                    rootPosition,
                    mastCapsule,
                    mastVertices,
                    mastBounds,
                    out mastCenter,
                    out radial,
                    out mastRadius))
            {
                return;
            }

            added.Add(candidate);
            result.Add(new SurfaceFittingPlacement
            {
                Target = candidate,
                RootRadial = radial,
                SourceSurfaceRadius = mastRadius,
                SourceSurfaceRootPoint = new Vector2(
                    mastCenter.x + radial.x * mastRadius,
                    mastCenter.y + radial.y * mastRadius),
                OriginalWorldPosition = candidate.position
            });
        }

        private static bool TryResolveMastSection(
            Vector3 fittingCenter,
            CapsuleCollider mastCapsule,
            Vector3[] mastVertices,
            Bounds mastBounds,
            out Vector2 center,
            out Vector2 radial,
            out float radius)
        {
            if (mastCapsule != null && mastCapsule.direction == 2 &&
                mastCapsule.radius > 0f)
            {
                center = new Vector2(
                    mastCapsule.center.x,
                    mastCapsule.center.y);
                radial = new Vector2(
                    fittingCenter.x - center.x,
                    fittingCenter.y - center.y);
                if (!NormalizeAndValidateFittingRadial(ref radial))
                {
                    radius = 0f;
                    return false;
                }

                radius = mastCapsule.radius;
                return true;
            }

            if (mastVertices == null || mastVertices.Length == 0)
            {
                center = Vector2.zero;
                radial = Vector2.zero;
                radius = 0f;
                return false;
            }

            float nearestHeight = float.PositiveInfinity;
            for (int i = 0; i < mastVertices.Length; i++)
            {
                nearestHeight = Mathf.Min(
                    nearestHeight,
                    Mathf.Abs(mastVertices[i].z - fittingCenter.z));
            }

            float band = Mathf.Max(
                MinimumMastSectionBand,
                mastBounds.size.z * MastSectionBandFraction);
            float heightLimit = nearestHeight + band;
            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;
            int sectionVertices = 0;
            for (int i = 0; i < mastVertices.Length; i++)
            {
                Vector3 vertex = mastVertices[i];
                if (Mathf.Abs(vertex.z - fittingCenter.z) > heightLimit)
                {
                    continue;
                }

                minX = Mathf.Min(minX, vertex.x);
                maxX = Mathf.Max(maxX, vertex.x);
                minY = Mathf.Min(minY, vertex.y);
                maxY = Mathf.Max(maxY, vertex.y);
                sectionVertices++;
            }

            if (sectionVertices < 3)
            {
                center = Vector2.zero;
                radial = Vector2.zero;
                radius = 0f;
                return false;
            }

            center = new Vector2(
                (minX + maxX) * 0.5f,
                (minY + maxY) * 0.5f);
            radial = new Vector2(
                fittingCenter.x - center.x,
                fittingCenter.y - center.y);
            if (!NormalizeAndValidateFittingRadial(ref radial))
            {
                radius = 0f;
                return false;
            }

            radius = 0f;
            for (int i = 0; i < mastVertices.Length; i++)
            {
                Vector3 vertex = mastVertices[i];
                if (Mathf.Abs(vertex.z - fittingCenter.z) > heightLimit)
                {
                    continue;
                }

                float projected = Vector2.Dot(
                    new Vector2(vertex.x, vertex.y) - center,
                    radial);
                radius = Mathf.Max(radius, projected);
            }

            return radius > 0.001f;
        }

        internal static bool TryGetSailMountOffset(
            Mast mast,
            Sail sail,
            float diameterScale,
            out Vector3 localOffset)
        {
            localOffset = Vector3.zero;
            if (mast == null || sail == null || sail.windcenter == null ||
                diameterScale <= 1f)
            {
                return false;
            }

            Transform mastRoot = mast.transform;
            Vector3 sailCenter = mastRoot.InverseTransformPoint(
                sail.windcenter.position);
            Vector3 sailOrigin = mastRoot.InverseTransformPoint(
                sail.transform.position);
            Vector2 radial = new Vector2(
                sailCenter.x - sailOrigin.x,
                sailCenter.y - sailOrigin.y);
            if (radial.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            radial.Normalize();
            CapsuleCollider mastCapsule =
                mastRoot.GetComponent<CapsuleCollider>();
            MeshFilter mastFilter = mastRoot.GetComponent<MeshFilter>();
            Mesh mastMesh = mastFilter != null
                ? mastFilter.sharedMesh
                : null;
            Vector3[] mastVertices =
                mastMesh != null && mastMesh.isReadable
                    ? mastMesh.vertices
                    : null;
            Bounds mastBounds = mastMesh != null
                ? mastMesh.bounds
                : new Bounds();
            Vector2 mastCenter;
            float sourceRadius;
            if (!TryResolveMastSurfaceAtHeight(
                    sail.transform.localPosition.z,
                    radial,
                    mastCapsule,
                    mastVertices,
                    mastBounds,
                    out mastCenter,
                    out sourceRadius))
            {
                return false;
            }

            Vector2 sourceSurfaceRootPoint = mastCenter +
                radial * sourceRadius;
            Vector3 correctionWorld;
            if (!TryGetSurfaceCorrectionWorld(
                    mastRoot,
                    sourceSurfaceRootPoint,
                    diameterScale,
                    out correctionWorld))
            {
                return false;
            }

            Vector3 correctionLocal = mastRoot.InverseTransformVector(
                correctionWorld);
            localOffset = new Vector3(
                correctionLocal.x,
                correctionLocal.y,
                0f);
            return localOffset.sqrMagnitude > 0.000001f;
        }

        private static bool TryResolveMastSurfaceAtHeight(
            float height,
            Vector2 radial,
            CapsuleCollider mastCapsule,
            Vector3[] mastVertices,
            Bounds mastBounds,
            out Vector2 center,
            out float radius)
        {
            if (mastCapsule != null && mastCapsule.direction == 2 &&
                mastCapsule.radius > 0f)
            {
                center = new Vector2(
                    mastCapsule.center.x,
                    mastCapsule.center.y);
                radius = mastCapsule.radius;
                return true;
            }

            if (mastVertices == null || mastVertices.Length == 0)
            {
                center = Vector2.zero;
                radius = 0f;
                return false;
            }

            float nearestHeight = float.PositiveInfinity;
            for (int i = 0; i < mastVertices.Length; i++)
            {
                nearestHeight = Mathf.Min(
                    nearestHeight,
                    Mathf.Abs(mastVertices[i].z - height));
            }

            float band = Mathf.Max(
                MinimumMastSectionBand,
                mastBounds.size.z * MastSectionBandFraction);
            float heightLimit = nearestHeight + band;
            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;
            int sectionVertices = 0;
            for (int i = 0; i < mastVertices.Length; i++)
            {
                Vector3 vertex = mastVertices[i];
                if (Mathf.Abs(vertex.z - height) > heightLimit)
                {
                    continue;
                }

                minX = Mathf.Min(minX, vertex.x);
                maxX = Mathf.Max(maxX, vertex.x);
                minY = Mathf.Min(minY, vertex.y);
                maxY = Mathf.Max(maxY, vertex.y);
                sectionVertices++;
            }

            if (sectionVertices < 3)
            {
                center = Vector2.zero;
                radius = 0f;
                return false;
            }

            center = new Vector2(
                (minX + maxX) * 0.5f,
                (minY + maxY) * 0.5f);
            radius = 0f;
            for (int i = 0; i < mastVertices.Length; i++)
            {
                Vector3 vertex = mastVertices[i];
                if (Mathf.Abs(vertex.z - height) > heightLimit)
                {
                    continue;
                }

                float projected = Vector2.Dot(
                    new Vector2(vertex.x, vertex.y) - center,
                    radial);
                radius = Mathf.Max(radius, projected);
            }

            return radius > 0.001f;
        }

        private static bool NormalizeAndValidateFittingRadial(
            ref Vector2 radial)
        {
            float distance = radial.magnitude;
            if (distance <= 0.001f || distance > MaxSurfaceFittingOffset)
            {
                return false;
            }

            radial /= distance;
            return true;
        }

        private static bool IsNamedSurfaceFitting(Transform candidate)
        {
            string text = UnstayedNameRules.Normalize(
                candidate != null ? candidate.name : null);
            return text.Contains("rope holder") ||
                   text.Contains("rope att") ||
                   text.Contains("reef att") ||
                   text.Contains("winch") ||
                   text.Contains("windcloth") ||
                   text.Contains("flag");
        }

        private static bool HasOwnVisibleGeometry(Transform candidate)
        {
            return candidate.GetComponent<MeshFilter>() != null ||
                   candidate.GetComponent<SkinnedMeshRenderer>() != null ||
                   candidate.GetComponent<Collider>() != null;
        }

        private static bool HasSelectedAncestor(
            Transform candidate,
            HashSet<Transform> selected)
        {
            Transform current = candidate.parent;
            while (current != null)
            {
                if (selected.Contains(current))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static int GetHierarchyDepth(Transform candidate)
        {
            int depth = 0;
            Transform current = candidate;
            while (current != null)
            {
                depth++;
                current = current.parent;
            }

            return depth;
        }

        private static void PlaceSurfaceFittings(
            Transform mastRoot,
            List<SurfaceFittingPlacement> fittings)
        {
            for (int i = 0; i < fittings.Count; i++)
            {
                SurfaceFittingPlacement fitting = fittings[i];
                if (fitting.Target == null)
                {
                    continue;
                }

                Vector3 correctionWorld;
                if (!TryGetSurfaceCorrectionWorld(
                        mastRoot,
                        fitting.SourceSurfaceRootPoint,
                        DiameterScale,
                        out correctionWorld))
                {
                    Plugin.LogSource?.LogWarning(
                        "Skipped implausible mast-fitting correction for " +
                        fitting.Target.name + " on " + mastRoot.name +
                        ": sourceRadius=" +
                        fitting.SourceSurfaceRadius.ToString("0.###") +
                        ", sourcePoint=" +
                        fitting.SourceSurfaceRootPoint +
                        ", radial=" + fitting.RootRadial + ".");
                    continue;
                }

                // Preserve the source fitting's exact gap from the mast.
                // Only compensate for the increase in mast radius.
                fitting.Target.position = fitting.OriginalWorldPosition +
                                          correctionWorld;
                Plugin.LogSource?.LogDebug(
                    "Placed mast fitting " + fitting.Target.name +
                    " on the enlarged surface of " + mastRoot.name +
                    " (correction " +
                    correctionWorld.magnitude.ToString("0.###") + ").");
            }
        }

        private static bool TryGetSurfaceCorrectionWorld(
            Transform mastRoot,
            Vector2 sourceSurfaceRootPoint,
            float diameterScale,
            out Vector3 correctionWorld)
        {
            correctionWorld = Vector3.zero;
            if (mastRoot == null || diameterScale <= 1f ||
                sourceSurfaceRootPoint.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            Vector3 correctionLocal = new Vector3(
                sourceSurfaceRootPoint.x * (diameterScale - 1f),
                sourceSurfaceRootPoint.y * (diameterScale - 1f),
                0f);
            correctionWorld = mastRoot.TransformVector(correctionLocal);
            if (correctionWorld.magnitude > MaximumSurfaceCorrection)
            {
                correctionWorld = Vector3.zero;
                return false;
            }

            return true;
        }

        private static bool TryGetVisibleCenterInRoot(
            Transform fitting,
            Transform mastRoot,
            out Vector3 center)
        {
            Vector3 total = Vector3.zero;
            int count = 0;
            MeshFilter[] filters =
                fitting.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                if (filter.sharedMesh == null)
                {
                    continue;
                }

                total += mastRoot.InverseTransformPoint(
                    filter.transform.TransformPoint(
                        filter.sharedMesh.bounds.center));
                count++;
            }

            SkinnedMeshRenderer[] skinned =
                fitting.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinned.Length; i++)
            {
                total += mastRoot.InverseTransformPoint(
                    skinned[i].transform.TransformPoint(
                        skinned[i].localBounds.center));
                count++;
            }

            if (count == 0)
            {
                center = Vector3.zero;
                return false;
            }

            center = total / count;
            return true;
        }

        private sealed class SurfaceFittingPlacement
        {
            internal Transform Target;
            internal Vector2 RootRadial;
            internal float SourceSurfaceRadius;
            internal Vector2 SourceSurfaceRootPoint;
            internal Vector3 OriginalWorldPosition;
        }
    }
}

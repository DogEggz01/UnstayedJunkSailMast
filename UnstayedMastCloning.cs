using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnstayedJunkSailMast
{
    internal static partial class UnstayedMastBuilder
    {
        private const float DiameterScale = 1.41f;
        private const float MaxSurfaceFittingOffset = 1f;

        private static readonly HashSet<string> SmallJunkRemovedNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "mast_holder",
                "mast_holder_001",
                "mast_holder_002"
            };

        private static BoatPartOption CloneUnstayedMast(
            BoatPartOption sourceOption,
            BoatRefs refs,
            int sceneIndex,
            int mastIndex,
            List<RestrictedPartSelection> restrictedSelections)
        {
            Mast sourceMast = sourceOption.GetComponent<Mast>();
            if (sourceMast == null || sourceMast.walkColMast == null)
            {
                Plugin.LogSource?.LogError(
                    "Skipped " + sourceOption.optionName +
                    ": source mast or walk collider is missing.");
                return null;
            }

            string sourceDisplayName = string.IsNullOrWhiteSpace(
                sourceOption.optionName)
                ? sourceOption.name
                : sourceOption.optionName;
            string sourceId = CreateSourceId(sourceOption);
            Transform cloneTransform = null;
            Transform cloneWalk = null;
            try
            {
                bool sourceWasActive = sourceOption.gameObject.activeSelf;
                try
                {
                    sourceOption.gameObject.SetActive(false);
                    cloneTransform = UnityEngine.Object.Instantiate(
                        sourceOption.transform,
                        sourceOption.transform.parent);
                    cloneTransform.gameObject.SetActive(false);

                    cloneWalk = UnityEngine.Object.Instantiate(
                        sourceMast.walkColMast,
                        sourceMast.walkColMast.parent);
                    cloneWalk.gameObject.SetActive(false);
                }
                finally
                {
                    sourceOption.gameObject.SetActive(sourceWasActive);
                }

                InitializeCloneTransforms(
                    sourceOption,
                    sourceMast,
                    cloneTransform,
                    cloneWalk);

                Mast cloneMast = cloneTransform.GetComponent<Mast>();
                BoatPartOption cloneOption =
                    cloneTransform.GetComponent<BoatPartOption>();
                if (cloneMast == null || cloneOption == null)
                {
                    throw new InvalidOperationException(
                        "cloned components are missing");
                }

                InitializeClonedMast(
                    cloneMast,
                    cloneWalk,
                    sourceOption,
                    mastIndex);
                InitializeClonedOption(
                    cloneOption,
                    cloneWalk,
                    sourceOption,
                    sourceDisplayName,
                    sceneIndex);

                DisableClonedRiggingVisuals(
                    sourceOption,
                    sourceMast.walkColMast,
                    cloneOption,
                    cloneWalk,
                    restrictedSelections);

                PruneHierarchy(cloneTransform, sceneIndex);
                PruneHierarchy(cloneWalk, sceneIndex);
                cloneMast.mastCols = CloneMastColliders(
                    sourceMast,
                    cloneMast,
                    sceneIndex);
                ScaleMastBodyAndPlaceFittings(cloneTransform);
                ScaleMastBodyAndPlaceFittings(cloneWalk);

                UnstayedMastMarker marker = cloneTransform.gameObject
                    .AddComponent<UnstayedMastMarker>();
                marker.SourceId = sourceId;
                return cloneOption;
            }
            catch (Exception exception)
            {
                if (cloneTransform != null)
                {
                    UnityEngine.Object.DestroyImmediate(
                        cloneTransform.gameObject);
                }

                if (cloneWalk != null)
                {
                    UnityEngine.Object.DestroyImmediate(cloneWalk.gameObject);
                }

                Plugin.LogSource?.LogError(
                    "Skipped " + sourceDisplayName + ": " + exception);
                return null;
            }
        }

        private static void InitializeCloneTransforms(
            BoatPartOption sourceOption,
            Mast sourceMast,
            Transform cloneTransform,
            Transform cloneWalk)
        {
            cloneTransform.name = "Unstayed_" + sourceOption.name;
            cloneWalk.name = "Unstayed_" + sourceMast.walkColMast.name;
            cloneTransform.localPosition = sourceOption.transform.localPosition;
            cloneTransform.localEulerAngles =
                sourceOption.transform.localEulerAngles;
            cloneTransform.localScale = sourceOption.transform.localScale;
            cloneWalk.localPosition = sourceMast.walkColMast.localPosition;
            cloneWalk.localEulerAngles = sourceMast.walkColMast.localEulerAngles;
            cloneWalk.localScale = sourceMast.walkColMast.localScale;
        }

        private static void InitializeClonedMast(
            Mast cloneMast,
            Transform cloneWalk,
            BoatPartOption sourceOption,
            int mastIndex)
        {
            cloneMast.orderIndex = mastIndex;
            cloneMast.walkColMast = cloneWalk;
            cloneMast.shipRigidbody =
                sourceOption.GetComponentInParent<Rigidbody>();
            cloneMast.startSailPrefab = null;
            cloneMast.startSailPrefabs = new GameObject[0];
            cloneMast.startSailsHeightOffsets = new float[0];
        }

        private static void InitializeClonedOption(
            BoatPartOption cloneOption,
            Transform cloneWalk,
            BoatPartOption sourceOption,
            string sourceDisplayName,
            int sceneIndex)
        {
            cloneOption.optionName = "Unstayed " + sourceDisplayName;
            cloneOption.mass = sourceOption.mass * 2;
            cloneOption.walkColObject = cloneWalk.gameObject;
            cloneOption.requires = cloneOption.requires ??
                new List<BoatPartOption>();
            cloneOption.requiresDisabled = cloneOption.requiresDisabled ??
                new List<BoatPartOption>();
            cloneOption.childOptions = FilterChildOptions(
                cloneOption.childOptions,
                sceneIndex);
        }

        private static void DisableClonedRiggingVisuals(
            BoatPartOption sourceOption,
            Transform sourceWalk,
            BoatPartOption cloneOption,
            Transform cloneWalk,
            List<RestrictedPartSelection> selections)
        {
            HashSet<GameObject> disabled = new HashSet<GameObject>();
            for (int i = 0; i < selections.Count; i++)
            {
                RestrictedPartSelection selection = selections[i];
                if (selection.Kind != RestrictedPartKind.Rigging &&
                    selection.Kind != RestrictedPartKind.RiggingAccessory)
                {
                    continue;
                }

                for (int j = 0; j < selection.NonEmptyOptions.Count; j++)
                {
                    BoatPartOption riggingOption =
                        selection.NonEmptyOptions[j];
                    if (riggingOption == null ||
                        riggingOption.childOptions == null)
                    {
                        continue;
                    }

                    for (int k = 0;
                         k < riggingOption.childOptions.Length;
                         k++)
                    {
                        GameObject sourceVisual =
                            riggingOption.childOptions[k];
                        if (sourceVisual == null)
                        {
                            continue;
                        }

                        AddMappedVisual(
                            disabled,
                            sourceVisual.transform,
                            sourceOption.transform,
                            cloneOption.transform);
                        AddMappedVisual(
                            disabled,
                            sourceVisual.transform,
                            sourceWalk,
                            cloneWalk);
                    }
                }
            }

            AddOrphanedTelltales(disabled, cloneOption.transform);
            AddOrphanedTelltales(disabled, cloneWalk);

            if (disabled.Count == 0)
            {
                return;
            }

            foreach (GameObject visual in disabled)
            {
                if (visual != null)
                {
                    visual.SetActive(false);
                }
            }

            List<GameObject> keptChildren = new List<GameObject>();
            if (cloneOption.childOptions != null)
            {
                for (int i = 0; i < cloneOption.childOptions.Length; i++)
                {
                    GameObject child = cloneOption.childOptions[i];
                    if (child != null && !disabled.Contains(child))
                    {
                        keptChildren.Add(child);
                    }
                }
            }

            cloneOption.childOptions = keptChildren.ToArray();
        }

        private static void AddMappedVisual(
            HashSet<GameObject> result,
            Transform sourceVisual,
            Transform sourceRoot,
            Transform cloneRoot)
        {
            Transform mapped = MapToClone(
                sourceVisual,
                sourceRoot,
                cloneRoot);
            if (mapped != null && mapped != cloneRoot)
            {
                result.Add(mapped.gameObject);
            }
        }

        private static void AddOrphanedTelltales(
            HashSet<GameObject> result,
            Transform mastRoot)
        {
            if (mastRoot == null)
            {
                return;
            }

            Transform[] descendants =
                mastRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                Transform descendant = descendants[i];
                if (descendant == mastRoot)
                {
                    continue;
                }

                string name = UnstayedNameRules.Normalize(descendant.name);
                bool namedTelltale = name.Contains("telltale");
                bool orphanedWindFlag = name == "wind flag" &&
                                        descendant.parent == mastRoot;
                if (namedTelltale || orphanedWindFlag)
                {
                    result.Add(descendant.gameObject);
                }
            }
        }

        private static void ScaleMastBodyAndPlaceFittings(Transform root)
        {
            if (root == null)
            {
                return;
            }

            Transform[] children = new Transform[root.childCount];
            for (int i = 0; i < children.Length; i++)
            {
                children[i] = root.GetChild(i);
            }

            Vector2 mastCenter;
            float mastRadius;
            bool hasCrossSection = TryGetMastCrossSection(
                root,
                out mastCenter,
                out mastRadius);
            Vector3 inverseDiameterScale = new Vector3(
                1f / DiameterScale,
                1f / DiameterScale,
                1f);

            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                Vector3 originalPosition = child.localPosition;
                Quaternion originalRotation = child.localRotation;
                Vector3 originalScale = child.localScale;
                Vector2 finalPosition = new Vector2(
                    originalPosition.x,
                    originalPosition.y);

                if (hasCrossSection &&
                    IsNearbySurfaceFitting(child, mastCenter))
                {
                    Vector2 radial = finalPosition - mastCenter;
                    Vector2 enlargedCenter = mastCenter * DiameterScale;
                    finalPosition = enlargedCenter + radial.normalized *
                        (radial.magnitude +
                         mastRadius * (DiameterScale - 1f));
                }

                GameObject compensationObject = new GameObject(
                    "UJSM_unscaled_" + i + "_" + child.name);
                compensationObject.layer = root.gameObject.layer;
                Transform compensation = compensationObject.transform;
                compensation.SetParent(root, false);
                compensation.localPosition = new Vector3(
                    finalPosition.x / DiameterScale,
                    finalPosition.y / DiameterScale,
                    originalPosition.z);
                compensation.localRotation = Quaternion.identity;
                compensation.localScale = inverseDiameterScale;

                child.SetParent(compensation, false);
                child.localPosition = Vector3.zero;
                child.localRotation = originalRotation;
                child.localScale = originalScale;
            }

            Vector3 sourceScale = root.localScale;
            root.localScale = new Vector3(
                sourceScale.x * DiameterScale,
                sourceScale.y * DiameterScale,
                sourceScale.z);
        }

        private static bool TryGetMastCrossSection(
            Transform root,
            out Vector2 center,
            out float radius)
        {
            CapsuleCollider capsule = root.GetComponent<CapsuleCollider>();
            if (capsule != null && capsule.direction == 2 &&
                capsule.radius > 0f)
            {
                center = new Vector2(
                    capsule.center.x,
                    capsule.center.y);
                radius = capsule.radius;
                return true;
            }

            MeshFilter filter = root.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                Bounds bounds = filter.sharedMesh.bounds;
                center = new Vector2(bounds.center.x, bounds.center.y);
                radius = Mathf.Max(bounds.extents.x, bounds.extents.y);
                return radius > 0f;
            }

            center = Vector2.zero;
            radius = 0f;
            return false;
        }

        private static bool IsNearbySurfaceFitting(
            Transform child,
            Vector2 mastCenter)
        {
            string text = UnstayedNameRules.Normalize(
                child != null ? child.name : null);
            bool isFitting = text.Contains("rope holder") ||
                             text.Contains("rope att") ||
                             text.Contains("reef att") ||
                             text.Contains("winch") ||
                             text.Contains("windcloth") ||
                             text.Contains("flag");
            if (!isFitting)
            {
                return false;
            }

            Vector2 radial = new Vector2(
                child.localPosition.x - mastCenter.x,
                child.localPosition.y - mastCenter.y);
            return radial.sqrMagnitude > 0.000001f &&
                   radial.sqrMagnitude <=
                       MaxSurfaceFittingOffset * MaxSurfaceFittingOffset;
        }

        internal static string CreateSourceId(BoatPartOption source)
        {
            Mast mast = source.GetComponent<Mast>();
            return UnstayedNameRules.Normalize(source.optionName) + "|" +
                   UnstayedNameRules.Normalize(source.name) + "|" +
                   (mast != null ? mast.orderIndex.ToString() : "-");
        }

        private static GameObject[] FilterChildOptions(
            GameObject[] childOptions,
            int sceneIndex)
        {
            if (childOptions == null || childOptions.Length == 0)
            {
                return new GameObject[0];
            }

            List<GameObject> result = new List<GameObject>();
            for (int i = 0; i < childOptions.Length; i++)
            {
                GameObject child = childOptions[i];
                if (child != null &&
                    !ShouldRemove(child.transform, sceneIndex))
                {
                    result.Add(child);
                }
            }

            return result.ToArray();
        }

        private static void PruneHierarchy(Transform root, int sceneIndex)
        {
            Transform[] descendants =
                root.GetComponentsInChildren<Transform>(true);
            for (int i = descendants.Length - 1; i >= 0; i--)
            {
                Transform descendant = descendants[i];
                if (descendant != root &&
                    ShouldRemove(descendant, sceneIndex))
                {
                    UnityEngine.Object.DestroyImmediate(descendant.gameObject);
                }
            }
        }

        private static bool ShouldRemove(Transform transform, int sceneIndex)
        {
            if (transform == null)
            {
                return false;
            }

            if (sceneIndex == 90 &&
                SmallJunkRemovedNames.Contains(transform.name))
            {
                return true;
            }

            string text = UnstayedNameRules.Normalize(
                transform.name + " " + GetHierarchyPath(transform));
            return text.Contains("static rig") ||
                   text.Contains("static rope atts") ||
                   text.Contains("static rope attachment") ||
                   text.Contains("shroud") ||
                   text.Contains("crowsnest") ||
                   text.Contains("crows nest") ||
                   text.Contains("crownest") ||
                   text.Contains("crow nest");
        }

        private static CapsuleCollider[] CloneMastColliders(
            Mast source,
            Mast clone,
            int sceneIndex)
        {
            if (source.mastCols == null)
            {
                return new CapsuleCollider[0];
            }

            List<CapsuleCollider> result = new List<CapsuleCollider>();
            for (int i = 0; i < source.mastCols.Length; i++)
            {
                CapsuleCollider sourceCollider = source.mastCols[i];
                if (sourceCollider == null ||
                    ShouldRemove(sourceCollider.transform, sceneIndex))
                {
                    continue;
                }

                CapsuleCollider mapped = MapCollider(
                    sourceCollider,
                    source.transform,
                    clone.transform);
                if (mapped == null && source.walkColMast != null &&
                    clone.walkColMast != null)
                {
                    mapped = MapCollider(
                        sourceCollider,
                        source.walkColMast,
                        clone.walkColMast);
                }

                if (mapped != null)
                {
                    AddUnique(result, mapped);
                }
                else if (!IsWithin(sourceCollider.transform, source.transform) &&
                         !IsWithin(
                             sourceCollider.transform,
                             source.walkColMast))
                {
                    AddUnique(result, sourceCollider);
                }
            }

            return result.ToArray();
        }

        private static CapsuleCollider MapCollider(
            CapsuleCollider sourceCollider,
            Transform sourceRoot,
            Transform cloneRoot)
        {
            Transform mappedTransform = MapToClone(
                sourceCollider.transform,
                sourceRoot,
                cloneRoot);
            return mappedTransform != null
                ? mappedTransform.GetComponent<CapsuleCollider>()
                : null;
        }

        private static Transform MapToClone(
            Transform sourceTransform,
            Transform sourceRoot,
            Transform cloneRoot)
        {
            if (!IsWithin(sourceTransform, sourceRoot))
            {
                return null;
            }

            string relativePath = GetRelativePath(
                sourceTransform,
                sourceRoot);
            return string.IsNullOrEmpty(relativePath)
                ? cloneRoot
                : cloneRoot.Find(relativePath);
        }

    }
}

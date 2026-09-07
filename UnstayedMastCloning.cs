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
            int sceneIndex,
            int mastIndex,
            UnstayedMastSourceIdentity identity,
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
                Dictionary<Collider, Collider> visualColliderMap =
                    ExtractScaledRootBodyAndPlaceFittings(
                        cloneTransform,
                        "UJSM_scaled_mast_body",
                        true,
                        false);
                Dictionary<Collider, Collider> walkColliderMap =
                    ExtractScaledRootBodyAndPlaceFittings(
                        cloneWalk,
                        "UJSM_scaled_walk_body",
                        false,
                        true);
                cloneMast.mastCols = CloneMastColliders(
                    sourceMast,
                    cloneMast,
                    sceneIndex,
                    visualColliderMap,
                    walkColliderMap);
                cloneMast.walkColMast = cloneWalk;
                cloneOption.walkColObject = cloneWalk.gameObject;

                UnstayedMastMarker marker = cloneTransform.gameObject
                    .AddComponent<UnstayedMastMarker>();
                marker.Identity = identity;
                marker.DiameterScale = DiameterScale;
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

        private static Dictionary<Collider, Collider>
            ExtractScaledRootBodyAndPlaceFittings(
            Transform root,
            string bodyName,
            bool forceTriggerColliders,
            bool isWalkBody)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            Vector3 sourceRootScale = root.localScale;
            List<HierarchyTransformSnapshot> hierarchy =
                CaptureHierarchy(root);
            List<SurfaceFittingPlacement> surfaceFittings =
                CaptureSurfaceFittings(root);
            GameObject bodyObject = new GameObject(bodyName);
            bodyObject.layer = root.gameObject.layer;
            bodyObject.tag = root.gameObject.tag;
            bodyObject.isStatic = root.gameObject.isStatic;
            UnstayedScaledBodyMarker marker =
                bodyObject.AddComponent<UnstayedScaledBodyMarker>();
            marker.LogicalRoot = root;
            marker.IsWalkBody = isWalkBody;
            Transform body = bodyObject.transform;
            body.SetParent(root, false);
            body.localPosition = Vector3.zero;
            body.localRotation = Quaternion.identity;
            body.localScale = new Vector3(
                DiameterScale,
                DiameterScale,
                1f);

            bool copiedGeometry = CopyRootRenderer(root, bodyObject);
            Dictionary<Collider, Collider> colliderMap =
                CopyRootColliders(
                    root,
                    bodyObject,
                    forceTriggerColliders);
            if (!copiedGeometry && colliderMap.Count == 0)
            {
                UnityEngine.Object.DestroyImmediate(bodyObject);
                throw new InvalidOperationException(
                    "root mast body has no renderer or collider components");
            }

            if (surfaceFittings.Count > 0)
            {
                PlaceSurfaceFittings(root, surfaceFittings);
            }

            ValidateExtractedRootBody(
                root,
                body,
                sourceRootScale,
                hierarchy,
                surfaceFittings);
            return colliderMap;
        }

        private static List<HierarchyTransformSnapshot> CaptureHierarchy(
            Transform root)
        {
            List<HierarchyTransformSnapshot> result =
                new List<HierarchyTransformSnapshot>();
            Transform[] descendants =
                root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                Transform descendant = descendants[i];
                if (descendant == root)
                {
                    continue;
                }

                result.Add(new HierarchyTransformSnapshot
                {
                    Target = descendant,
                    Parent = descendant.parent,
                    LocalPosition = descendant.localPosition,
                    LocalRotation = descendant.localRotation,
                    LocalScale = descendant.localScale
                });
            }

            return result;
        }

        private static void ValidateExtractedRootBody(
            Transform root,
            Transform body,
            Vector3 sourceRootScale,
            List<HierarchyTransformSnapshot> hierarchy,
            List<SurfaceFittingPlacement> surfaceFittings)
        {
            if ((root.localScale - sourceRootScale).sqrMagnitude >
                0.00000001f ||
                body.parent != root ||
                body.localPosition.sqrMagnitude > 0.00000001f ||
                Quaternion.Angle(body.localRotation, Quaternion.identity) >
                0.001f ||
                (body.localScale - new Vector3(
                     DiameterScale,
                     DiameterScale,
                     1f)).sqrMagnitude > 0.00000001f)
            {
                throw new InvalidOperationException(
                    "scaled mast body hierarchy is invalid");
            }

            HashSet<Transform> translatedFittings = new HashSet<Transform>();
            for (int i = 0; i < surfaceFittings.Count; i++)
            {
                translatedFittings.Add(surfaceFittings[i].Target);
            }

            for (int i = 0; i < hierarchy.Count; i++)
            {
                HierarchyTransformSnapshot snapshot = hierarchy[i];
                Transform target = snapshot.Target;
                if (target == null || target.parent != snapshot.Parent ||
                    Quaternion.Angle(
                        target.localRotation,
                        snapshot.LocalRotation) > 0.001f ||
                    (target.localScale - snapshot.LocalScale).sqrMagnitude >
                    0.00000001f ||
                    (!translatedFittings.Contains(target) &&
                     (target.localPosition - snapshot.LocalPosition)
                     .sqrMagnitude > 0.00000001f))
                {
                    throw new InvalidOperationException(
                        "mast child hierarchy changed unexpectedly at " +
                        (target != null ? target.name : "destroyed child"));
                }
            }
        }

        private static bool CopyRootRenderer(
            Transform sourceRoot,
            GameObject target)
        {
            MeshRenderer sourceRenderer =
                sourceRoot.GetComponent<MeshRenderer>();
            MeshFilter sourceFilter = sourceRoot.GetComponent<MeshFilter>();
            Renderer unsupportedRenderer = sourceRoot.GetComponent<Renderer>();
            if (sourceRenderer == null)
            {
                if (unsupportedRenderer != null)
                {
                    throw new InvalidOperationException(
                        "unsupported root renderer type " +
                        unsupportedRenderer.GetType().Name);
                }

                return false;
            }

            if (sourceFilter == null || sourceFilter.sharedMesh == null)
            {
                throw new InvalidOperationException(
                    "root MeshRenderer has no source mesh");
            }

            MeshFilter targetFilter = target.AddComponent<MeshFilter>();
            targetFilter.sharedMesh = sourceFilter.sharedMesh;
            MeshRenderer targetRenderer = target.AddComponent<MeshRenderer>();
            targetRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
            targetRenderer.shadowCastingMode =
                sourceRenderer.shadowCastingMode;
            targetRenderer.receiveShadows = sourceRenderer.receiveShadows;
            targetRenderer.lightProbeUsage = sourceRenderer.lightProbeUsage;
            targetRenderer.reflectionProbeUsage =
                sourceRenderer.reflectionProbeUsage;
            targetRenderer.probeAnchor = sourceRenderer.probeAnchor;
            targetRenderer.motionVectorGenerationMode =
                sourceRenderer.motionVectorGenerationMode;
            targetRenderer.allowOcclusionWhenDynamic =
                sourceRenderer.allowOcclusionWhenDynamic;
            targetRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            targetRenderer.sortingOrder = sourceRenderer.sortingOrder;
            targetRenderer.enabled = sourceRenderer.enabled;
            sourceRenderer.enabled = false;
            return true;
        }

        private static Dictionary<Collider, Collider> CopyRootColliders(
            Transform sourceRoot,
            GameObject target,
            bool forceTrigger)
        {
            Dictionary<Collider, Collider> result =
                new Dictionary<Collider, Collider>();
            Collider[] sourceColliders =
                sourceRoot.GetComponents<Collider>();
            for (int i = 0; i < sourceColliders.Length; i++)
            {
                Collider source = sourceColliders[i];
                Collider copy = CopyCollider(source, target);
                copy.sharedMaterial = source.sharedMaterial;
                copy.isTrigger = forceTrigger || source.isTrigger;
                copy.contactOffset = source.contactOffset;
                copy.enabled = source.enabled;
                source.enabled = false;
                result[source] = copy;
            }

            return result;
        }

        private static Collider CopyCollider(
            Collider source,
            GameObject target)
        {
            CapsuleCollider sourceCapsule = source as CapsuleCollider;
            if (sourceCapsule != null)
            {
                CapsuleCollider copy = target.AddComponent<CapsuleCollider>();
                copy.center = sourceCapsule.center;
                copy.radius = sourceCapsule.radius;
                copy.height = sourceCapsule.height;
                copy.direction = sourceCapsule.direction;
                return copy;
            }

            MeshCollider sourceMesh = source as MeshCollider;
            if (sourceMesh != null)
            {
                MeshCollider copy = target.AddComponent<MeshCollider>();
                copy.sharedMesh = sourceMesh.sharedMesh;
                copy.convex = sourceMesh.convex;
                copy.cookingOptions = sourceMesh.cookingOptions;
                return copy;
            }

            BoxCollider sourceBox = source as BoxCollider;
            if (sourceBox != null)
            {
                BoxCollider copy = target.AddComponent<BoxCollider>();
                copy.center = sourceBox.center;
                copy.size = sourceBox.size;
                return copy;
            }

            SphereCollider sourceSphere = source as SphereCollider;
            if (sourceSphere != null)
            {
                SphereCollider copy = target.AddComponent<SphereCollider>();
                copy.center = sourceSphere.center;
                copy.radius = sourceSphere.radius;
                return copy;
            }

            throw new InvalidOperationException(
                "unsupported root collider type " + source.GetType().Name);
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
            int sceneIndex,
            Dictionary<Collider, Collider> visualColliderMap,
            Dictionary<Collider, Collider> walkColliderMap)
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

                mapped = ResolveScaledCapsule(
                    mapped,
                    visualColliderMap,
                    walkColliderMap);

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

        private static CapsuleCollider ResolveScaledCapsule(
            CapsuleCollider mapped,
            Dictionary<Collider, Collider> visualColliderMap,
            Dictionary<Collider, Collider> walkColliderMap)
        {
            if (mapped == null)
            {
                return null;
            }

            Collider scaled;
            if (visualColliderMap != null &&
                visualColliderMap.TryGetValue(mapped, out scaled))
            {
                return scaled as CapsuleCollider;
            }

            if (walkColliderMap != null &&
                walkColliderMap.TryGetValue(mapped, out scaled))
            {
                return scaled as CapsuleCollider;
            }

            return mapped;
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

        private sealed class HierarchyTransformSnapshot
        {
            internal Transform Target;
            internal Transform Parent;
            internal Vector3 LocalPosition;
            internal Quaternion LocalRotation;
            internal Vector3 LocalScale;
        }

    }
}

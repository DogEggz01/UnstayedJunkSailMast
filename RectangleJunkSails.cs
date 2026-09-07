using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnstayedJunkSailMast
{
    internal static class RectangleJunkSails
    {
        internal const int LegacyNarrowIndex = 131;
        internal const int LegacyWideIndex = 132;
        internal const int NarrowIndex = 200;
        internal const int WideIndex = 201;
        private const int ShipyardExpansionSailCapacity = 512;
        private const float WindAxisToleranceDegrees = 0.01f;

        private static readonly int[] RuntimeMaterialSourceIndices =
        {
            101,
            27,
            28,
            29,
            100,
            102,
            103
        };

        internal static GameObject NarrowPrefab { get; private set; }
        internal static GameObject WidePrefab { get; private set; }

        internal static bool EnsureRegistered(PrefabsDirectory directory)
        {
            if (directory == null || directory.sails == null)
            {
                return false;
            }

            NarrowPrefab = GetRegisteredRectangle(directory, NarrowIndex);
            WidePrefab = GetRegisteredRectangle(directory, WideIndex);

            if (NarrowPrefab == null || WidePrefab == null)
            {
                Build(directory);
                NarrowPrefab = GetRegisteredRectangle(
                    directory,
                    NarrowIndex);
                WidePrefab = GetRegisteredRectangle(
                    directory,
                    WideIndex);
            }

            if (NarrowPrefab != null)
            {
                EnsureShipyardExpansionComponents(NarrowPrefab);
            }

            if (WidePrefab != null)
            {
                EnsureShipyardExpansionComponents(WidePrefab);
            }

            return NarrowPrefab != null && WidePrefab != null;
        }

        internal static bool CanMigrateLegacyIndex(int prefabIndex)
        {
            if (prefabIndex == LegacyNarrowIndex)
            {
                return legacyNarrowIndexWasAvailable;
            }

            return prefabIndex == LegacyWideIndex &&
                   legacyWideIndexWasAvailable;
        }

        internal static void Build(PrefabsDirectory directory)
        {
            if (directory == null || directory.sails == null)
            {
                Plugin.LogSource?.LogError(
                    "Could not add Rectangle Junk sails: " +
                    "PrefabsDirectory.sails is unavailable.");
                return;
            }

            if (directory.sails.Length < ShipyardExpansionSailCapacity)
            {
                legacyNarrowIndexWasAvailable = false;
                legacyWideIndexWasAvailable = false;
                Plugin.LogSource?.LogError(
                    "Could not add Rectangle Junk sails: Shipyard " +
                    "Expansion did not provide its 512-slot sail registry.");
                return;
            }

            legacyNarrowIndexWasAvailable =
                directory.sails[LegacyNarrowIndex] == null;
            legacyWideIndexWasAvailable =
                directory.sails[LegacyWideIndex] == null;

            GameObject runtimeMaterialSource =
                FindRuntimeMaterialSource(directory);
            if (runtimeMaterialSource == null)
            {
                Plugin.LogSource?.LogError(
                    "Could not add Rectangle Junk sails: no live Junk " +
                    "Square rendering materials were found.");
                return;
            }

            EnsurePrefabContainer();
            NarrowPrefab = EnsureFinalRectangle(
                directory,
                runtimeMaterialSource,
                RectangleJunkAssets.LoadNarrowPrefab(),
                NarrowIndex,
                "narrow rectangle junk");
            WidePrefab = EnsureFinalRectangle(
                directory,
                runtimeMaterialSource,
                RectangleJunkAssets.LoadWidePrefab(),
                WideIndex,
                "wide rectangle junk");
            Plugin.LogSource?.LogInfo(
                NarrowPrefab != null
                    ? "Registered narrow rectangle junk at sail index " +
                      NarrowIndex + "."
                    : "Narrow rectangle junk was not registered.");
            Plugin.LogSource?.LogInfo(
                WidePrefab != null
                    ? "Registered wide rectangle junk at sail index " +
                      WideIndex + "."
                    : "Wide rectangle junk was not registered.");
        }

        internal static void AddToShipyard(
            PrefabsDirectory directory,
            ref GameObject[] sailPrefabs)
        {
            NarrowPrefab = GetRegisteredRectangle(directory, NarrowIndex);
            WidePrefab = GetRegisteredRectangle(directory, WideIndex);
            if (NarrowPrefab == null || WidePrefab == null)
            {
                return;
            }

            List<GameObject> result = new List<GameObject>();
            HashSet<GameObject> seen = new HashSet<GameObject>();
            if (sailPrefabs != null)
            {
                for (int i = 0; i < sailPrefabs.Length; i++)
                {
                    GameObject prefab = sailPrefabs[i];
                    Sail sail = prefab != null
                        ? prefab.GetComponent<Sail>()
                        : null;
                    if (prefab != null &&
                        (sail == null || !IsRectangleIndex(
                            sail.prefabIndex)) &&
                        seen.Add(prefab))
                    {
                        result.Add(prefab);
                    }
                }
            }

            AddUnique(result, seen, NarrowPrefab);
            AddUnique(result, seen, WidePrefab);
            sailPrefabs = result.ToArray();
        }

        internal static void RemoveFromShipyard(
            ref GameObject[] sailPrefabs)
        {
            if (sailPrefabs == null)
            {
                return;
            }

            List<GameObject> result =
                new List<GameObject>(sailPrefabs.Length);
            for (int i = 0; i < sailPrefabs.Length; i++)
            {
                GameObject prefab = sailPrefabs[i];
                Sail sail = prefab != null
                    ? prefab.GetComponent<Sail>()
                    : null;
                if (sail == null || !IsRectangleIndex(sail.prefabIndex))
                {
                    result.Add(prefab);
                }
            }

            sailPrefabs = result.ToArray();
        }

        internal static void PrepareMast(Mast mast)
        {
            if (mast == null || mast.sails == null)
            {
                return;
            }

            for (int i = 0; i < mast.sails.Count; i++)
            {
                GameObject sailObject = mast.sails[i];
                RectangleJunkSailRig rig = sailObject != null
                    ? sailObject.GetComponent<RectangleJunkSailRig>()
                    : null;
                rig?.PrepareForMastUpdate();
            }
        }

        internal static void ApplyToMast(Mast mast)
        {
            if (mast == null || mast.sails == null)
            {
                return;
            }

            for (int i = 0; i < mast.sails.Count; i++)
            {
                GameObject sailObject = mast.sails[i];
                RectangleJunkSailRig rig = sailObject != null
                    ? sailObject.GetComponent<RectangleJunkSailRig>()
                    : null;
                if (rig != null && rig.Initialize(mast))
                {
                    rig.BindAssignedWinch();
                }
            }
        }

        internal static void Reset()
        {
            NarrowPrefab = null;
            WidePrefab = null;
            legacyNarrowIndexWasAvailable = false;
            legacyWideIndexWasAvailable = false;
            RectangleJunkRenderState.Reset();
            if (prefabContainer != null)
            {
                UnityEngine.Object.Destroy(prefabContainer);
                prefabContainer = null;
            }
        }

        private static void EnsurePrefabContainer()
        {
            if (prefabContainer != null)
            {
                return;
            }

            prefabContainer = new GameObject("UJSM Rectangle Junk Prefabs");
            UnityEngine.Object.DontDestroyOnLoad(prefabContainer);
            prefabContainer.SetActive(false);
        }

        private static GameObject EnsureFinalRectangle(
            PrefabsDirectory directory,
            GameObject runtimeMaterialSource,
            GameObject authoredSource,
            int prefabIndex,
            string sailName)
        {
            GameObject occupied = directory.sails[prefabIndex];
            if (occupied != null)
            {
                RectangleJunkSailRig existingRig =
                    occupied.GetComponent<RectangleJunkSailRig>();
                Sail existingSail = occupied.GetComponent<Sail>();
                if (existingRig != null && existingSail != null &&
                    existingSail.prefabIndex == prefabIndex)
                {
                    EnsureShipyardExpansionComponents(occupied);
                    return occupied;
                }

                Plugin.LogSource?.LogError(
                    "Could not register " + sailName + " at index " +
                    prefabIndex + ": that sail slot is already occupied by " +
                    occupied.name + ".");
                return null;
            }

            if (authoredSource == null)
            {
                return null;
            }

            GameObject clone = null;
            try
            {
                clone = UnityEngine.Object.Instantiate(
                    authoredSource,
                    prefabContainer.transform);
                clone.name = prefabIndex + " SAIL " + sailName;
                ConfigureFinalRectangle(
                    clone,
                    runtimeMaterialSource,
                    prefabIndex,
                    sailName);
                directory.sails[prefabIndex] = clone;
                return clone;
            }
            catch (Exception exception)
            {
                if (clone != null)
                {
                    UnityEngine.Object.DestroyImmediate(clone);
                }

                Plugin.LogSource?.LogError(
                    "Could not build authored " + sailName + ": " +
                    exception);
                return null;
            }
        }

        private static void ConfigureFinalRectangle(
            GameObject clone,
            GameObject runtimeMaterialSource,
            int prefabIndex,
            string sailName)
        {
            Sail sail = RequireComponent<Sail>(clone, "Sail");
            SailConnections connections =
                RequireComponent<SailConnections>(clone, "SailConnections");
            HingeJoint hinge =
                RequireComponent<HingeJoint>(clone, "HingeJoint");
            Animator animator = clone.GetComponentInChildren<Animator>(true);
            Transform visualRoot = FindDescendant(
                animator != null ? animator.transform : null,
                "SAIL_junk_square");
            Transform sheetAnchor = FindDescendant(
                visualRoot,
                "sail_rope_att__angle_mid_rectangle_");
            if (animator == null || visualRoot == null ||
                sheetAnchor == null)
            {
                throw new InvalidOperationException(
                    "the authored Rectangle Junk hierarchy is incomplete");
            }

            ValidateFinalRuntimeRig(
                clone,
                sail,
                connections,
                hinge,
                sheetAnchor,
                prefabIndex,
                sailName);

            AssignRuntimeMaterials(clone, runtimeMaterialSource);

            clone.AddComponent<RectangleJunkSailRig>();
            EnsureShipyardExpansionComponents(clone);
        }

        private static void EnsureShipyardExpansionComponents(
            GameObject prefab)
        {
            if (prefab.GetComponent<ShipyardExpansion.SailScaler>() == null)
            {
                prefab.AddComponent<ShipyardExpansion.SailScaler>();
            }

            ShipyardExpansion.Scripts.SailTextureChanger textureChanger =
                prefab.GetComponent<
                    ShipyardExpansion.Scripts.SailTextureChanger>();
            if (textureChanger == null)
            {
                textureChanger = prefab.AddComponent<
                    ShipyardExpansion.Scripts.SailTextureChanger>();
                textureChanger.Setup();
            }
        }

        private static GameObject GetRegisteredRectangle(
            PrefabsDirectory directory,
            int prefabIndex)
        {
            if (directory == null || directory.sails == null ||
                prefabIndex < 0 || prefabIndex >= directory.sails.Length)
            {
                return null;
            }

            GameObject candidate = directory.sails[prefabIndex];
            Sail sail = candidate != null
                ? candidate.GetComponent<Sail>()
                : null;
            RectangleJunkSailRig rig = candidate != null
                ? candidate.GetComponent<RectangleJunkSailRig>()
                : null;
            return sail != null && rig != null &&
                   sail.prefabIndex == prefabIndex
                ? candidate
                : null;
        }

        private static void ValidateFinalRuntimeRig(
            GameObject clone,
            Sail sail,
            SailConnections connections,
            HingeJoint hinge,
            Transform sheetAnchor,
            int prefabIndex,
            string sailName)
        {
            RopeControllerSailReef reef =
                connections.reefController as RopeControllerSailReef;
            RopeControllerSailAngle controller =
                connections.angleControllerMid as RopeControllerSailAngle;
            Transform ropeAttachment = connections.midRopeAttachment;
            RopeEffect sheet = ropeAttachment != null
                ? ropeAttachment.GetComponent<RopeEffect>()
                : null;
            RopeEffect controllerRope = controller != null
                ? controller.GetComponent<RopeEffect>()
                : null;

            if (sail.prefabIndex != prefabIndex ||
                sail.sailName != sailName ||
                sail.squareSail || sail.obsolete ||
                sail.windcenter == null ||
                Quaternion.Angle(
                    clone.transform.rotation,
                    sail.windcenter.rotation) >
                    WindAxisToleranceDegrees ||
                connections.sail != sail ||
                reef == null || controller == null ||
                ropeAttachment == null || sheet == null ||
                controllerRope == null ||
                connections.angleControllerLeft != null ||
                connections.angleControllerRight != null ||
                controller.sailHinge != hinge ||
                controller.transform.parent != clone.transform ||
                ropeAttachment.parent != clone.transform ||
                controllerRope.attachment != ropeAttachment ||
                sheet.attachment != sheetAnchor || !sheet.sheet ||
                reef.sail != sail || !reef.reverseReefing ||
                clone.GetComponent<SquareAngleMaster>() != null ||
                clone.GetComponent<SquareTopsailAngleMirror>() != null)
            {
                throw new InvalidOperationException(
                    "the final Rectangle Junk prefab has invalid serialized " +
                    "vanilla Junk rig or wind-axis configuration");
            }
        }

        private static GameObject FindRuntimeMaterialSource(
            PrefabsDirectory directory)
        {
            for (int i = 0;
                 i < RuntimeMaterialSourceIndices.Length;
                 i++)
            {
                int index = RuntimeMaterialSourceIndices[i];
                if (index < 0 || index >= directory.sails.Length)
                {
                    continue;
                }

                GameObject candidate = directory.sails[index];
                if (HasRuntimeMaterials(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool HasRuntimeMaterials(GameObject candidate)
        {
            Sail sourceSail = candidate != null
                ? candidate.GetComponent<Sail>()
                : null;
            Renderer clothRenderer = sourceSail != null &&
                                     sourceSail.cloth != null
                ? sourceSail.cloth.GetComponent<Renderer>()
                : null;
            ReefEffectAnimUniversal reefEffect = candidate != null
                ? candidate.GetComponentInChildren<
                    ReefEffectAnimUniversal>(true)
                : null;
            return clothRenderer != null &&
                   clothRenderer.sharedMaterials.Length > 0 &&
                   clothRenderer.sharedMaterials[0] != null &&
                   reefEffect != null &&
                   reefEffect.furledSail != null &&
                   reefEffect.furledSail.sharedMaterials.Length > 0 &&
                   reefEffect.furledSail.sharedMaterials[0] != null;
        }

        private static void AssignRuntimeMaterials(
            GameObject target,
            GameObject source)
        {
            Sail targetSail = RequireComponent<Sail>(target, "Sail");
            Sail sourceSail = RequireComponent<Sail>(
                source,
                "Junk Square Sail");
            Renderer targetCloth = targetSail.cloth != null
                ? targetSail.cloth.GetComponent<Renderer>()
                : null;
            Renderer sourceCloth = sourceSail.cloth != null
                ? sourceSail.cloth.GetComponent<Renderer>()
                : null;
            ReefEffectAnimUniversal targetReef =
                target.GetComponentInChildren<
                    ReefEffectAnimUniversal>(true);
            ReefEffectAnimUniversal sourceReef =
                source.GetComponentInChildren<
                    ReefEffectAnimUniversal>(true);
            if (targetCloth == null || sourceCloth == null ||
                targetReef == null || targetReef.furledSail == null ||
                sourceReef == null || sourceReef.furledSail == null ||
                sourceCloth.sharedMaterials.Length == 0 ||
                sourceReef.furledSail.sharedMaterials.Length == 0)
            {
                throw new InvalidOperationException(
                    "the live Junk Square rendering materials are " +
                    "incomplete");
            }

            // Only renderer material references are borrowed. The authored
            // Narrow mesh, Cloth component, constraints, bones, and collision
            // stay untouched.
            RectangleJunkRenderState.CaptureAndApply(
                target,
                source,
                targetCloth,
                sourceCloth,
                targetReef.furledSail,
                sourceReef.furledSail);
        }

        internal static bool IsRectangle(GameObject sailObject)
        {
            Sail sail = sailObject != null
                ? sailObject.GetComponent<Sail>()
                : null;
            return sail != null && IsRectangleIndex(sail.prefabIndex);
        }

        private static bool IsRectangleIndex(int prefabIndex)
        {
            return prefabIndex == NarrowIndex || prefabIndex == WideIndex;
        }

        internal static void SynchronizeCollisionCheckerScale(Sail sail)
        {
            if (sail == null ||
                !IsRectangle(sail.gameObject) ||
                sail.cloth == null ||
                sail.cloth.transform.parent == null)
            {
                return;
            }

            SailConnections connections =
                sail.GetComponent<SailConnections>();
            Transform checker = connections != null &&
                                connections.colChecker != null
                ? connections.colChecker.transform
                : null;

            // Before RunColCheck, the checker is still below the scaled sail
            // visual. Scaling it there would be applied twice. Vanilla first
            // reparents it below walkColMast (layer 8), so only synchronize
            // after that transition has happened.
            if (checker == null ||
                checker.parent == null ||
                checker.parent.gameObject.layer != 8)
            {
                return;
            }

            Vector3 visualScale =
                sail.cloth.transform.parent.localScale;
            checker.localScale = new Vector3(
                visualScale.x,
                visualScale.z,
                visualScale.y);
        }

        internal static void FurlNewlyInstalledSail(GameObject sailObject)
        {
            if (!IsRectangle(sailObject))
            {
                return;
            }

            Sail sail = sailObject.GetComponent<Sail>();
            SailConnections connections =
                sailObject.GetComponent<SailConnections>();
            RopeControllerSailReef controller = connections != null
                ? connections.reefController as RopeControllerSailReef
                : null;
            if (controller == null)
            {
                Plugin.LogSource?.LogWarning(
                    "Could not furl newly installed Rectangle " +
                    "Junk: its reef controller is missing.");
                return;
            }

            sail.currentUnroll = 0f;
            controller.reverseReefing = true;
            controller.currentLength = 1f;
            controller.changed = true;
        }

        internal static void SynchronizeWinchVisibility(Mast mast)
        {
            if (mast == null)
            {
                return;
            }

            bool hasRectangle = false;
            if (mast.sails != null)
            {
                for (int i = 0; i < mast.sails.Count; i++)
                {
                    if (IsRectangle(mast.sails[i]))
                    {
                        hasRectangle = true;
                        break;
                    }
                }
            }

            HashSet<GPButtonRopeWinch> seen =
                new HashSet<GPButtonRopeWinch>();
            SynchronizeWinchArray(mast.reefWinch, hasRectangle, seen);
            SynchronizeWinchArray(mast.midAngleWinch, hasRectangle, seen);
            SynchronizeWinchArray(mast.leftAngleWinch, hasRectangle, seen);
            SynchronizeWinchArray(mast.rightAngleWinch, hasRectangle, seen);
        }

        private static void SynchronizeWinchArray(
            GPButtonRopeWinch[] winches,
            bool hasRectangle,
            HashSet<GPButtonRopeWinch> seen)
        {
            if (winches == null)
            {
                return;
            }

            for (int i = 0; i < winches.Length; i++)
            {
                GPButtonRopeWinch winch = winches[i];
                if (winch == null || !seen.Add(winch))
                {
                    continue;
                }

                RectangleJunkWinchVisibility state =
                    winch.GetComponent<RectangleJunkWinchVisibility>();
                if (state == null && hasRectangle && winch.rope == null)
                {
                    state = winch.gameObject.AddComponent<
                        RectangleJunkWinchVisibility>();
                }

                state?.SetHidden(hasRectangle && winch.rope == null);
            }
        }

        private static T RequireComponent<T>(
            GameObject target,
            string componentName)
            where T : Component
        {
            T component = target.GetComponent<T>();
            if (component == null)
            {
                throw new InvalidOperationException(
                    componentName + " is missing");
            }

            return component;
        }

        private static Transform FindDescendant(
            Transform root,
            string name)
        {
            if (root == null)
            {
                return null;
            }

            Transform[] descendants =
                root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                if (descendants[i].name == name)
                {
                    return descendants[i];
                }
            }

            return null;
        }

        private static void AddUnique(
            List<GameObject> result,
            HashSet<GameObject> seen,
            GameObject prefab)
        {
            if (prefab != null && seen.Add(prefab))
            {
                result.Add(prefab);
            }
        }

        private static GameObject prefabContainer;
        private static bool legacyNarrowIndexWasAvailable;
        private static bool legacyWideIndexWasAvailable;
    }
}

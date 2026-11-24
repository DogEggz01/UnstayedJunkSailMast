using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using ShipyardExpansion.Scripts;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace UnstayedJunkSailMast
{
    internal static class RectangleJunkAssets
    {
        private const string BundleFileName =
            "unstayedjunksailmast_assets";
        private const string NarrowPrefabPath =
            "assets/ujsmfinal/narrow_rectangle_junk.prefab";
        private const string WidePrefabPath =
            "assets/ujsmfinal/wide_rectangle_junk.prefab";

        internal static GameObject LoadNarrowPrefab()
        {
            if (narrowPrefab != null)
            {
                return narrowPrefab;
            }

            if (!EnsureBundleLoaded())
            {
                return null;
            }

            narrowPrefab = bundle.LoadAsset<GameObject>(NarrowPrefabPath);
            if (narrowPrefab == null)
            {
                Plugin.LogSource?.LogError(
                    "Could not find the final narrow rectangle Junk " +
                    "prefab in the Rectangle Junk AssetBundle.");
            }

            return narrowPrefab;
        }

        internal static GameObject LoadWidePrefab()
        {
            if (widePrefab != null)
            {
                return widePrefab;
            }

            if (!EnsureBundleLoaded())
            {
                return null;
            }

            widePrefab = bundle.LoadAsset<GameObject>(WidePrefabPath);
            if (widePrefab == null)
            {
                Plugin.LogSource?.LogError(
                    "Could not find the final wide rectangle Junk " +
                    "prefab in the Rectangle Junk AssetBundle.");
            }

            return widePrefab;
        }

        private static bool EnsureBundleLoaded()
        {
            if (bundle != null)
            {
                return true;
            }

            string bundlePath = Path.Combine(
                Plugin.PluginDirectory,
                "assets",
                BundleFileName);
            if (!File.Exists(bundlePath))
            {
                Plugin.LogSource?.LogError(
                    "Could not add Rectangle Junk sails: AssetBundle " +
                    "not found at " + bundlePath + ".");
                return false;
            }

            try
            {
                bundle = AssetBundle.LoadFromFile(bundlePath);
                if (bundle != null)
                {
                    return true;
                }

                Plugin.LogSource?.LogError(
                    "Could not load Rectangle Junk AssetBundle at " +
                    bundlePath + ".");
            }
            catch (Exception exception)
            {
                Plugin.LogSource?.LogError(
                    "Could not load final Rectangle Junk assets: " +
                    exception);
            }

            bundle = null;
            return false;
        }

        internal static void Reset()
        {
            narrowPrefab = null;
            widePrefab = null;
            bundle?.Unload(false);
            bundle = null;
        }

        private static AssetBundle bundle;
        private static GameObject narrowPrefab;
        private static GameObject widePrefab;
    }

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

            // Only the renderers' materials are borrowed; the authored mesh,
            // Cloth, constraints, bones and collision stay as they are.
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
            return IsRectangle(sail);
        }

        internal static bool IsRectangle(Sail sail)
        {
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

    internal sealed class RectangleJunkSailRig : MonoBehaviour
    {
        internal const float TangentialWindFraction = 0.5f;
        internal const float StaticWindMultiplier = 1f;
        internal const float BendingStiffness = 0.7f;

        private const float OrdinaryJunkSolverFrequency = 120f;

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
            windCloth = sail != null && sail.cloth != null
                ? sail.cloth.GetComponent<WindCloth>()
                : null;
            if (sail == null || connections == null ||
                reefController == null || reefEffect == null ||
                sail.cloth == null || windCloth == null)
            {
                return Fail(
                    "its reefing components are incomplete");
            }

            ApplyRectangleJunkClothDynamics();
            ApplyControllerDirection();
            initialized = true;
            refreshClothPending = true;
            return true;
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

            // The one deferred Cloth refresh is done; everything else runs
            // from the mast and sail patches.
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

        private void ApplyRectangleJunkClothDynamics()
        {
            // The cloth's visual tuning; its mesh and constraints stay.
            sail.cloth.bendingStiffness =
                BendingStiffness;
            sail.cloth.clothSolverFrequency =
                OrdinaryJunkSolverFrequency;
            windCloth.staticMultiplier =
                StaticWindMultiplier;
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
        private WindCloth windCloth;
        private GPButtonRopeWinch boundWinch;
        private bool originalWinchReverseResistance;
        private bool initialized;
        private bool refreshClothPending;
        private bool warningLogged;
    }

    [HarmonyPatch(typeof(PrefabsDirectory), "Start")]
    [HarmonyAfter(Plugin.ShipyardExpansionGuid)]
    internal static class RectangleJunkPrefabPatch
    {
        private static void Prefix(PrefabsDirectory __instance)
        {
            RectangleJunkSails.EnsureRegistered(__instance);
        }
    }

    // All Sails in All Shipyards' prefix runs after this one and replaces the
    // shipyard's list with every registered sail.
    [HarmonyPatch(typeof(Shipyard), "ActivateDocuments")]
    [HarmonyAfter(Plugin.ShipyardExpansionGuid)]
    internal static class RectangleJunkShipyardPatch
    {
        private static readonly HashSet<string> SaleSceneNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "island 9 E Dragon Cliffs",
                "island 27 Lagoon SwampShipyard"
            };

        private static void Prefix(
            Shipyard __instance,
            ref GameObject[] ___sailPrefabs)
        {
            PrefabsDirectory directory = PrefabsDirectory.instance;
            bool registered =
                RectangleJunkSails.EnsureRegistered(directory);
            bool saleLocation = IsSaleLocation(__instance);
            if (registered && saleLocation)
            {
                RectangleJunkSails.AddToShipyard(
                    directory,
                    ref ___sailPrefabs);
            }
            else
            {
                RectangleJunkSails.RemoveFromShipyard(
                    ref ___sailPrefabs);
            }

            if (!registered && saleLocation)
            {
                Plugin.LogSource?.LogError(
                    "Rectangle Junk sails are unavailable: " +
                    "indices 200/201 could not be registered.");
            }
        }

        internal static bool IsSaleLocation(Shipyard shipyard)
        {
            if (shipyard == null)
            {
                return false;
            }

            Scene scene = shipyard.gameObject.scene;
            return scene.IsValid() && IsSaleSceneName(scene.name);
        }

        internal static bool IsSaleSceneName(string sceneName)
        {
            return !string.IsNullOrEmpty(sceneName) &&
                   SaleSceneNames.Contains(sceneName);
        }
    }

    [HarmonyPatch(typeof(WindCloth), "Update")]
    internal static class RectangleJunkWindClothPatch
    {
        // The Rectangle cloth's wind multiplier and bending are set once by RectangleJunkSailRig.Initialize; the game never
        // rewrites them.
        private static void Prefix(
            Sail ___sail,
            Cloth ___cloth,
            out bool __state)
        {
            __state = RectangleJunkSails.IsRectangle(___sail) &&
                      ___cloth != null;
        }

        private static void Postfix(
            Sail ___sail,
            Cloth ___cloth,
            bool __state)
        {
            if (!__state)
            {
                return;
            }

            Vector3 acceleration = ___cloth.externalAcceleration;
            Vector3 normalAcceleration = Vector3.Project(
                acceleration,
                ___sail.transform.up);
            Vector3 tangentialAcceleration =
                acceleration - normalAcceleration;
            ___cloth.externalAcceleration = normalAcceleration +
                tangentialAcceleration *
                RectangleJunkSailRig.TangentialWindFraction;
        }
    }

    [HarmonyPatch(typeof(Mast), "UpdateControllerAttachments")]
    internal static class RectangleJunkMastAttachmentPatch
    {
        private static void Prefix(Mast __instance)
        {
            RectangleJunkSails.PrepareMast(__instance);
        }

        private static void Postfix(Mast __instance)
        {
            RectangleJunkSails.ApplyToMast(__instance);
        }
    }

    [HarmonyPatch(typeof(Mast), "UpdateWinchesEnabled")]
    internal static class RectangleJunkWinchVisibilityPatch
    {
        private static void Postfix(Mast __instance)
        {
            RectangleJunkSails.SynchronizeWinchVisibility(__instance);
        }
    }

    [HarmonyPatch(typeof(ShipyardSailColChecker), "RunColCheck")]
    internal static class RectangleJunkCollisionCheckerScalePatch
    {
        private static void Postfix(Sail ___sail)
        {
            RectangleJunkSails.SynchronizeCollisionCheckerScale(___sail);
        }
    }

    [HarmonyPatch(typeof(Mast), "AttachSailToMast")]
    internal static class RectangleJunkShipyardInstallPatch
    {
        private static void Prefix(
            GameObject sailObject,
            out bool __state)
        {
            Sail sail = sailObject != null
                ? sailObject.GetComponent<Sail>()
                : null;
            __state = GameState.currentShipyard != null &&
                      sail != null &&
                      !sail.IsInstalled() &&
                      RectangleJunkSails.IsRectangle(sailObject);
        }

        private static void Postfix(
            GameObject sailObject,
            bool __state)
        {
            if (__state)
            {
                RectangleJunkSails.FurlNewlyInstalledSail(sailObject);
            }
        }
    }

    internal static class RectangleJunkRenderState
    {
        private const string MainTextureProperty = "_MainTex";
        private const string ColorProperty = "_Color";
        private const string RenderTypeTag = "RenderType";

        private static readonly string[] BoomRendererNames =
        {
            "boom_010",
            "boom_011",
            "boom_012",
            "boom_013",
            "boom_014"
        };

        private static RendererState clothState;
        private static RendererState furledState;
        private static bool correctionLogged;

        internal static void CaptureAndApply(
            GameObject targetSail,
            GameObject sourceSail,
            Renderer targetCloth,
            Renderer sourceCloth,
            Renderer targetFurled,
            Renderer sourceFurled)
        {
            clothState = RendererState.Capture(sourceCloth);
            furledState = RendererState.Capture(sourceFurled);
            clothState.ApplyShared(targetCloth);
            furledState.ApplyShared(targetFurled);
            ApplyBoomStates(targetSail, sourceSail);
        }

        private static void ApplyBoomStates(
            GameObject targetSail,
            GameObject sourceSail)
        {
            for (int i = 0; i < BoomRendererNames.Length; i++)
            {
                string rendererName = BoomRendererNames[i];
                Renderer target = FindUniqueRenderer(
                    targetSail,
                    rendererName);
                Renderer source = FindUniqueRenderer(
                    sourceSail,
                    rendererName);
                RendererState.Capture(source).ApplyShared(target);
            }
        }

        private static Renderer FindUniqueRenderer(
            GameObject root,
            string rendererName)
        {
            Renderer match = null;
            Renderer[] renderers = root != null
                ? root.GetComponentsInChildren<Renderer>(true)
                : Array.Empty<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer candidate = renderers[i];
                if (candidate.gameObject.name != rendererName)
                {
                    continue;
                }

                if (match != null)
                {
                    throw new InvalidOperationException(
                        "multiple renderers named " + rendererName +
                        " were found in the Junk Square hierarchy");
                }

                match = candidate;
            }

            if (match == null)
            {
                throw new InvalidOperationException(
                    "renderer " + rendererName +
                    " is missing from the Junk Square hierarchy");
            }

            return match;
        }

        internal static void Normalize(GameObject sailObject)
        {
            if (clothState == null || furledState == null ||
                !RectangleJunkSails.IsRectangle(sailObject))
            {
                return;
            }

            Sail sail = sailObject.GetComponent<Sail>();
            Renderer cloth = sail != null && sail.cloth != null
                ? sail.cloth.GetComponent<Renderer>()
                : null;
            ReefEffectAnimUniversal reef =
                sailObject.GetComponentInChildren<
                    ReefEffectAnimUniversal>(true);
            Renderer furled = reef != null ? reef.furledSail : null;
            if (cloth == null || furled == null)
            {
                return;
            }

            bool corrected = clothState.ApplyInstances(cloth);
            corrected |= furledState.ApplyInstances(furled);
            if (corrected && !correctionLogged)
            {
                correctionLogged = true;
                Plugin.LogSource?.LogWarning(
                    "Corrected Rectangle Junk renderer state after a " +
                    "runtime material change so Sailwind fog can use the " +
                    "stock Junk Square shader and depth settings.");
            }
        }

        internal static void Reset()
        {
            clothState = null;
            furledState = null;
            correctionLogged = false;
        }

        private sealed class RendererState
        {
            private readonly Material[] materials;
            private readonly ShadowCastingMode shadowCastingMode;
            private readonly bool receiveShadows;
            private readonly LightProbeUsage lightProbeUsage;
            private readonly ReflectionProbeUsage reflectionProbeUsage;
            private readonly MotionVectorGenerationMode motionVectors;
            private readonly bool allowOcclusionWhenDynamic;
            private readonly int sortingLayerId;
            private readonly int sortingOrder;
            private readonly int layer;

            private RendererState(Renderer source)
            {
                materials = source.sharedMaterials;
                shadowCastingMode = source.shadowCastingMode;
                receiveShadows = source.receiveShadows;
                lightProbeUsage = source.lightProbeUsage;
                reflectionProbeUsage = source.reflectionProbeUsage;
                motionVectors = source.motionVectorGenerationMode;
                allowOcclusionWhenDynamic =
                    source.allowOcclusionWhenDynamic;
                sortingLayerId = source.sortingLayerID;
                sortingOrder = source.sortingOrder;
                layer = source.gameObject.layer;
            }

            internal static RendererState Capture(Renderer source)
            {
                if (source == null ||
                    source.sharedMaterials.Length == 0 ||
                    source.sharedMaterials[0] == null)
                {
                    throw new InvalidOperationException(
                        "the live Junk Square renderer state is incomplete");
                }

                return new RendererState(source);
            }

            internal void ApplyShared(Renderer target)
            {
                target.sharedMaterials = materials;
                ApplyRendererSettings(target);
            }

            internal bool ApplyInstances(Renderer target)
            {
                bool corrected = RendererSettingsDiffer(target);
                Material[] instances = target.materials;
                for (int i = 0; i < instances.Length; i++)
                {
                    Material targetMaterial = instances[i];
                    Material sourceMaterial = materials[
                        Math.Min(i, materials.Length - 1)];
                    if (targetMaterial == null || sourceMaterial == null)
                    {
                        continue;
                    }

                    corrected |= MaterialStateDiffers(
                        targetMaterial,
                        sourceMaterial);
                    ApplyMaterialState(targetMaterial, sourceMaterial);
                }

                target.materials = instances;
                ApplyRendererSettings(target);
                return corrected;
            }

            private void ApplyRendererSettings(Renderer target)
            {
                target.shadowCastingMode = shadowCastingMode;
                target.receiveShadows = receiveShadows;
                target.lightProbeUsage = lightProbeUsage;
                target.reflectionProbeUsage = reflectionProbeUsage;
                target.motionVectorGenerationMode = motionVectors;
                target.allowOcclusionWhenDynamic =
                    allowOcclusionWhenDynamic;
                target.sortingLayerID = sortingLayerId;
                target.sortingOrder = sortingOrder;
                target.gameObject.layer = layer;
            }

            private bool RendererSettingsDiffer(Renderer target)
            {
                return target.shadowCastingMode != shadowCastingMode ||
                       target.receiveShadows != receiveShadows ||
                       target.lightProbeUsage != lightProbeUsage ||
                       target.reflectionProbeUsage !=
                       reflectionProbeUsage ||
                       target.motionVectorGenerationMode != motionVectors ||
                       target.allowOcclusionWhenDynamic !=
                       allowOcclusionWhenDynamic ||
                       target.sortingLayerID != sortingLayerId ||
                       target.sortingOrder != sortingOrder ||
                       target.gameObject.layer != layer;
            }

            private static bool MaterialStateDiffers(
                Material target,
                Material source)
            {
                if (target.shader != source.shader ||
                    target.renderQueue != source.renderQueue ||
                    target.enableInstancing != source.enableInstancing ||
                    target.doubleSidedGI != source.doubleSidedGI ||
                    target.GetTag(RenderTypeTag, false, string.Empty) !=
                    source.GetTag(RenderTypeTag, false, string.Empty) ||
                    !KeywordsMatch(
                        target.shaderKeywords,
                        source.shaderKeywords))
                {
                    return true;
                }

                return FloatPropertyDiffers(target, source, "_ZWrite") ||
                       FloatPropertyDiffers(target, source, "_SrcBlend") ||
                       FloatPropertyDiffers(target, source, "_DstBlend") ||
                       FloatPropertyDiffers(target, source, "_Mode");
            }

            private static void ApplyMaterialState(
                Material target,
                Material source)
            {
                bool hasTexture = target.HasProperty(MainTextureProperty);
                Texture texture = hasTexture ? target.mainTexture : null;
                Vector2 textureScale = hasTexture
                    ? target.mainTextureScale
                    : Vector2.one;
                Vector2 textureOffset = hasTexture
                    ? target.mainTextureOffset
                    : Vector2.zero;
                bool hasColor = target.HasProperty(ColorProperty);
                Color color = hasColor
                    ? target.GetColor(ColorProperty)
                    : Color.white;

                target.shader = source.shader;
                target.CopyPropertiesFromMaterial(source);
                target.shaderKeywords = source.shaderKeywords;
                target.renderQueue = source.renderQueue;
                target.enableInstancing = source.enableInstancing;
                target.doubleSidedGI = source.doubleSidedGI;
                target.globalIlluminationFlags =
                    source.globalIlluminationFlags;
                target.SetOverrideTag(
                    RenderTypeTag,
                    source.GetTag(RenderTypeTag, false, string.Empty));

                if (hasTexture && target.HasProperty(MainTextureProperty))
                {
                    target.mainTexture = texture;
                    target.mainTextureScale = textureScale;
                    target.mainTextureOffset = textureOffset;
                }

                if (hasColor && target.HasProperty(ColorProperty))
                {
                    target.SetColor(ColorProperty, color);
                }
            }

            private static bool FloatPropertyDiffers(
                Material target,
                Material source,
                string propertyName)
            {
                if (!target.HasProperty(propertyName) ||
                    !source.HasProperty(propertyName))
                {
                    return false;
                }

                return !Mathf.Approximately(
                    target.GetFloat(propertyName),
                    source.GetFloat(propertyName));
            }

            private static bool KeywordsMatch(
                string[] first,
                string[] second)
            {
                first = first ?? Array.Empty<string>();
                second = second ?? Array.Empty<string>();
                if (first.Length != second.Length)
                {
                    return false;
                }

                HashSet<string> expected = new HashSet<string>(second);
                for (int i = 0; i < first.Length; i++)
                {
                    if (!expected.Contains(first[i]))
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }

    [HarmonyPatch(typeof(Sail), "Start")]
    internal static class RectangleJunkSailRenderStartPatch
    {
        private static void Postfix(Sail __instance)
        {
            RectangleJunkRenderState.Normalize(__instance.gameObject);
        }
    }

    [HarmonyPatch(typeof(Sail), "ChangeSailColor")]
    internal static class RectangleJunkSailColorRenderPatch
    {
        private static void Postfix(Sail __instance)
        {
            RectangleJunkRenderState.Normalize(__instance.gameObject);
        }
    }

    [HarmonyPatch(typeof(SailTextureChanger), "UpdateMaterial")]
    [HarmonyAfter(Plugin.ShipyardExpansionGuid)]
    internal static class RectangleJunkSailTextureRenderPatch
    {
        private static void Postfix(SailTextureChanger __instance)
        {
            RectangleJunkRenderState.Normalize(__instance.gameObject);
        }
    }

    internal sealed class RectangleJunkWinchVisibility : MonoBehaviour
    {
        internal void SetHidden(bool shouldHide)
        {
            Restore();
            if (!shouldHide)
            {
                return;
            }

            Renderer[] renderers =
                GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer != null && renderer.enabled)
                {
                    hiddenRenderers.Add(renderer);
                    renderer.enabled = false;
                }
            }

            Collider[] colliders =
                GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider != null && collider.enabled)
                {
                    hiddenColliders.Add(collider);
                    collider.enabled = false;
                }
            }
        }

        private void OnDestroy()
        {
            Restore();
        }

        private void Restore()
        {
            for (int i = 0; i < hiddenRenderers.Count; i++)
            {
                if (hiddenRenderers[i] != null)
                {
                    hiddenRenderers[i].enabled = true;
                }
            }

            for (int i = 0; i < hiddenColliders.Count; i++)
            {
                if (hiddenColliders[i] != null)
                {
                    hiddenColliders[i].enabled = true;
                }
            }

            hiddenRenderers.Clear();
            hiddenColliders.Clear();
        }

        private readonly List<Renderer> hiddenRenderers =
            new List<Renderer>();
        private readonly List<Collider> hiddenColliders =
            new List<Collider>();
    }

    internal static class RectangleJunkSailIndexMigration
    {
        private const int CurrentLayoutVersion = 1;

        internal static void PrepareLoad(
            SaveableBoatCustomization customization,
            SaveBoatCustomizationData data)
        {
            if (data == null || data.sails == null)
            {
                return;
            }

            int sceneIndex;
            bool hasSceneIndex = TryGetSceneIndex(
                customization,
                out sceneIndex);
            if (hasSceneIndex && IsCurrent(sceneIndex))
            {
                return;
            }

            bool hasLegacyModFootprint = hasSceneIndex &&
                                         HasLegacyModFootprint(sceneIndex);
            bool migrateNarrow = hasLegacyModFootprint ||
                RectangleJunkSails.CanMigrateLegacyIndex(
                    RectangleJunkSails.LegacyNarrowIndex);
            bool migrateWide = hasLegacyModFootprint ||
                RectangleJunkSails.CanMigrateLegacyIndex(
                    RectangleJunkSails.LegacyWideIndex);
            int remapped = RemapLegacyIndices(
                data.sails,
                migrateNarrow,
                migrateWide);

            if (hasSceneIndex)
            {
                MarkCurrent(sceneIndex);
            }

            if (remapped > 0)
            {
                Plugin.LogSource?.LogInfo(
                    "Migrated " + remapped +
                    " Rectangle Junk sail index record(s) from 131/132 " +
                    "to 200/201" +
                    (hasSceneIndex
                        ? " for boat scene " + sceneIndex
                        : string.Empty) +
                    ".");
            }
        }

        internal static void MarkCurrent(
            SaveableBoatCustomization customization)
        {
            int sceneIndex;
            if (TryGetSceneIndex(customization, out sceneIndex))
            {
                MarkCurrent(sceneIndex);
            }
        }

        internal static int RemapLegacyIndices(
            IList<SaveSailData> sails,
            bool migrateNarrow,
            bool migrateWide)
        {
            if (sails == null)
            {
                return 0;
            }

            int remapped = 0;
            for (int i = 0; i < sails.Count; i++)
            {
                SaveSailData sail = sails[i];
                if (sail == null)
                {
                    continue;
                }

                if (migrateNarrow &&
                    sail.prefabIndex ==
                    RectangleJunkSails.LegacyNarrowIndex)
                {
                    sail.prefabIndex = RectangleJunkSails.NarrowIndex;
                    remapped++;
                }
                else if (migrateWide &&
                         sail.prefabIndex ==
                         RectangleJunkSails.LegacyWideIndex)
                {
                    sail.prefabIndex = RectangleJunkSails.WideIndex;
                    remapped++;
                }
            }

            return remapped;
        }

        private static bool TryGetSceneIndex(
            SaveableBoatCustomization customization,
            out int sceneIndex)
        {
            SaveableObject saveable = customization != null
                ? customization.GetComponent<SaveableObject>()
                : null;
            if (saveable != null)
            {
                sceneIndex = saveable.sceneIndex;
                return true;
            }

            sceneIndex = -1;
            return false;
        }

        private static bool IsCurrent(int sceneIndex)
        {
            if (GameState.modData == null)
            {
                return false;
            }

            string encoded;
            int version;
            return GameState.modData.TryGetValue(
                       GetLayoutKey(sceneIndex),
                       out encoded) &&
                   int.TryParse(encoded, out version) &&
                   version >= CurrentLayoutVersion;
        }

        private static bool HasLegacyModFootprint(int sceneIndex)
        {
            if (GameState.modData == null)
            {
                return false;
            }

            string prefix = Plugin.PluginGuid + "." + sceneIndex;
            return GameState.modData.ContainsKey(
                       prefix + ".activeMasts") ||
                   GameState.modData.ContainsKey(
                       prefix + ".indexLayoutVersion");
        }

        private static void MarkCurrent(int sceneIndex)
        {
            if (GameState.modData != null)
            {
                GameState.modData[GetLayoutKey(sceneIndex)] =
                    CurrentLayoutVersion.ToString();
            }
        }

        private static string GetLayoutKey(int sceneIndex)
        {
            return Plugin.PluginGuid + "." + sceneIndex +
                   ".rectangleSailIndexLayoutVersion";
        }
    }
}

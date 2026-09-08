using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnstayedJunkSailMast
{
    [HarmonyPatch(typeof(PrefabsDirectory), "Start")]
    [HarmonyAfter(Plugin.ShipyardExpansionGuid)]
    internal static class RectangleJunkPrefabPatch
    {
        private static void Prefix(PrefabsDirectory __instance)
        {
            RectangleJunkSails.EnsureRegistered(__instance);
        }
    }

    [HarmonyPatch(typeof(Shipyard), "ActivateDocuments")]
    [HarmonyAfter(Plugin.ShipyardExpansionGuid)]
    [HarmonyPriority(Priority.Last)]
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

    [HarmonyPatch(typeof(Sail), "GetSailArea")]
    internal static class RectangleJunkAreaPatch
    {
        private static void Postfix(Sail __instance, ref float __result)
        {
            RectangleJunkSailRig rig =
                __instance.GetComponent<RectangleJunkSailRig>();
            if (rig != null)
            {
                __result = rig.GetTransformedSailArea(__result);
            }
        }
    }

    [HarmonyPatch(typeof(WindCloth), "Update")]
    internal static class RectangleJunkWindClothPatch
    {
        private static void Prefix(
            WindCloth __instance,
            Sail ___sail,
            Cloth ___cloth,
            out bool __state)
        {
            __state = RectangleJunkSails.IsRectangle(___sail) &&
                      ___cloth != null;
            if (!__state)
            {
                return;
            }

            __instance.staticMultiplier =
                RectangleJunkSailRig.StaticWindMultiplier;
            ___cloth.bendingStiffness =
                RectangleJunkSailRig.BendingStiffness;
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
}

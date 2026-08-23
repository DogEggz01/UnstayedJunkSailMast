using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace UnstayedJunkSailMast
{
    [HarmonyPatch(typeof(SaveableBoatCustomization), "Awake")]
    [HarmonyAfter(Plugin.ShipyardExpansionGuid)]
    [HarmonyPriority(Priority.Last)]
    internal static class BoatCustomizationAwakePatch
    {
        private static void Postfix(
            SaveableBoatCustomization __instance,
            BoatCustomParts ___parts,
            BoatRefs ___refs)
        {
            try
            {
                UnstayedMastBuilder.TryBuild(__instance, ___parts, ___refs);
            }
            catch (Exception exception)
            {
                Plugin.LogSource?.LogError(
                    "Could not build unstayed mast options on " +
                    __instance.name + ": " + exception);
            }
        }
    }

    [HarmonyPatch(typeof(ShipyardUI), "SailMastCompatible")]
    internal static class SailMastCompatibilityPatch
    {
        private static bool Prefix(GameObject sailPrefab, ref bool __result)
        {
            Shipyard shipyard = GameState.currentShipyard;
            Mast mast = shipyard != null
                ? shipyard.sailInstaller.GetCurrentMast()
                : null;
            if (mast == null ||
                mast.GetComponent<UnstayedMastMarker>() == null)
            {
                return true;
            }

            __result = UnstayedSailRules.IsAllowed(sailPrefab);
            return false;
        }
    }

    [HarmonyPatch(typeof(ShipyardSailInstaller), "AddNewSail")]
    internal static class AddNewSailPatch
    {
        private static bool Prefix(
            ShipyardSailInstaller __instance,
            GameObject sailObject)
        {
            Mast mast = __instance.GetCurrentMast();
            if (mast == null ||
                mast.GetComponent<UnstayedMastMarker>() == null ||
                UnstayedSailRules.IsAllowed(sailObject))
            {
                return true;
            }

            Sail sail = sailObject != null
                ? sailObject.GetComponent<Sail>()
                : null;
            Plugin.LogSource?.LogWarning(
                "Rejected " +
                (sail != null ? sail.sailName : "unknown sail") +
                " on an unstayed mast.");
            if (sailObject != null && sailObject.scene.IsValid())
            {
                UnityEngine.Object.Destroy(sailObject);
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(ShipyardPartsInstaller), "UpdateOrder")]
    internal static class ShipyardOrderNormalizationPatch
    {
        private static void Prefix(
            BoatCustomParts ___currentParts,
            BoatPartsOrder ___currentOrder)
        {
            List<int> changedPartIndices =
                UnstayedSelectionRules.NormalizeOrder(
                ___currentParts,
                ___currentOrder);
            if (ShipyardUI.instance == null || ___currentParts == null ||
                ___currentOrder == null)
            {
                return;
            }

            for (int i = 0; i < changedPartIndices.Count; i++)
            {
                int partIndex = changedPartIndices[i];
                if (partIndex < 0 ||
                    partIndex >= ___currentParts.availableParts.Count ||
                    partIndex >= ___currentOrder.orderedOptions.Length)
                {
                    continue;
                }

                BoatPart part = ___currentParts.availableParts[partIndex];
                int optionIndex = ___currentOrder.orderedOptions[partIndex];
                if (optionIndex >= 0 &&
                    optionIndex < part.partOptions.Count)
                {
                    ShipyardUI.instance.ChangePartsOptionText(
                        partIndex,
                        part.partOptions[optionIndex].optionName);
                }
            }
        }
    }

    [HarmonyPatch(typeof(SaveableBoatCustomization), "LoadData")]
    [HarmonyAfter(Plugin.ShipyardExpansionGuid)]
    internal static class BoatCustomizationLoadPatch
    {
        private static void Prefix(
            SaveableBoatCustomization __instance,
            SaveBoatCustomizationData data)
        {
            UnstayedSaveCompatibility.RemapSavedOptions(__instance, data);
        }

        private static void Postfix(SaveableBoatCustomization __instance)
        {
            BoatCustomParts parts = __instance.GetComponent<BoatCustomParts>();
            if (UnstayedSelectionRules.NormalizeActive(parts))
            {
                parts.RefreshParts();
            }
        }
    }

    [HarmonyPatch(typeof(SaveableBoatCustomization), "GetData")]
    internal static class BoatCustomizationSavePatch
    {
        private static void Postfix(
            SaveableBoatCustomization __instance,
            SaveBoatCustomizationData __result)
        {
            UnstayedSaveCompatibility.RecordActiveOptions(
                __instance,
                __result);
        }
    }

    internal static class UnstayedSailRules
    {
        internal static bool IsAllowed(GameObject sailObject)
        {
            Sail sail = sailObject != null
                ? sailObject.GetComponent<Sail>()
                : null;
            if (sail == null)
            {
                return false;
            }

            string name = UnstayedNameRules.Normalize(sail.sailName);
            return sail.category == SailCategory.junk ||
                   (sail.category == SailCategory.other &&
                    name == "fin sail") ||
                   (sail.category == SailCategory.square &&
                    UnstayedNameRules.IsNamedVariant(
                        name,
                        "junk square"));
        }
    }

    internal static class UnstayedSelectionRules
    {
        internal static List<int> NormalizeOrder(
            BoatCustomParts parts,
            BoatPartsOrder order)
        {
            List<int> changedPartIndices = new List<int>();
            UnstayedBoatProfile profile;
            if (parts == null || order == null ||
                !UnstayedBoatRegistry.TryGet(parts, out profile))
            {
                return changedPartIndices;
            }

            for (int i = 0; i < profile.Masts.Count; i++)
            {
                UnstayedMastProfile mast = profile.Masts[i];
                int mastPartIndex = parts.availableParts.IndexOf(mast.MastPart);
                int unstayedIndex = mast.MastPart.partOptions.IndexOf(
                    mast.UnstayedOption);
                if (mastPartIndex < 0 || unstayedIndex < 0 ||
                    mastPartIndex >= order.orderedOptions.Length ||
                    order.orderedOptions[mastPartIndex] != unstayedIndex)
                {
                    continue;
                }

                ForceEmptySelections(
                    parts,
                    order.orderedOptions,
                    mast.RestrictedSelections,
                    changedPartIndices);
            }

            return changedPartIndices;
        }

        internal static bool NormalizeActive(BoatCustomParts parts)
        {
            UnstayedBoatProfile profile;
            if (parts == null ||
                !UnstayedBoatRegistry.TryGet(parts, out profile))
            {
                return false;
            }

            bool changed = false;
            for (int i = 0; i < profile.Masts.Count; i++)
            {
                UnstayedMastProfile mast = profile.Masts[i];
                int unstayedIndex = mast.MastPart.partOptions.IndexOf(
                    mast.UnstayedOption);
                if (unstayedIndex < 0 ||
                    mast.MastPart.activeOption != unstayedIndex)
                {
                    continue;
                }

                for (int j = 0;
                     j < mast.RestrictedSelections.Count;
                     j++)
                {
                    RestrictedPartSelection selection =
                        mast.RestrictedSelections[j];
                    if (selection.Kind != RestrictedPartKind.Rigging ||
                        selection.EmptyOption == null)
                    {
                        continue;
                    }

                    int emptyIndex = selection.Part.partOptions.IndexOf(
                        selection.EmptyOption);
                    if (emptyIndex >= 0 &&
                        selection.Part.activeOption != emptyIndex)
                    {
                        selection.Part.activeOption = emptyIndex;
                        changed = true;
                    }
                }
            }

            return changed;
        }

        private static void ForceEmptySelections(
            BoatCustomParts parts,
            int[] orderedOptions,
            List<RestrictedPartSelection> selections,
            List<int> changedPartIndices)
        {
            for (int i = 0; i < selections.Count; i++)
            {
                RestrictedPartSelection selection = selections[i];
                if (selection.Kind != RestrictedPartKind.Rigging ||
                    selection.EmptyOption == null)
                {
                    continue;
                }

                int partIndex = parts.availableParts.IndexOf(selection.Part);
                int emptyIndex = selection.Part.partOptions.IndexOf(
                    selection.EmptyOption);
                if (partIndex >= 0 && partIndex < orderedOptions.Length &&
                    emptyIndex >= 0 &&
                    orderedOptions[partIndex] != emptyIndex)
                {
                    orderedOptions[partIndex] = emptyIndex;
                    if (!changedPartIndices.Contains(partIndex))
                    {
                        changedPartIndices.Add(partIndex);
                    }
                }
            }
        }
    }

    internal static class UnstayedSaveCompatibility
    {
        private const char RecordSeparator = ';';
        private const char FieldSeparator = '=';

        internal static void RecordActiveOptions(
            SaveableBoatCustomization customization,
            SaveBoatCustomizationData data)
        {
            BoatCustomParts parts =
                customization.GetComponent<BoatCustomParts>();
            UnstayedBoatProfile profile;
            if (data == null ||
                !UnstayedBoatRegistry.TryGet(parts, out profile) ||
                GameState.modData == null)
            {
                return;
            }

            List<string> records = new List<string>();
            for (int i = 0; i < profile.Masts.Count; i++)
            {
                UnstayedMastProfile mast = profile.Masts[i];
                int partIndex = parts.availableParts.IndexOf(mast.MastPart);
                int optionIndex = mast.MastPart.partOptions.IndexOf(
                    mast.UnstayedOption);
                if (partIndex < 0 || optionIndex < 0 ||
                    partIndex >= data.partActiveOptions.Count ||
                    data.partActiveOptions[partIndex] != optionIndex)
                {
                    continue;
                }

                UnstayedMastMarker marker =
                    mast.UnstayedOption.GetComponent<UnstayedMastMarker>();
                if (marker != null && !string.IsNullOrEmpty(marker.SourceId))
                {
                    records.Add(
                        partIndex + FieldSeparator.ToString() +
                        Uri.EscapeDataString(marker.SourceId));
                }
            }

            string key = GetKey(profile.SceneIndex);
            if (records.Count == 0)
            {
                GameState.modData.Remove(key);
            }
            else
            {
                GameState.modData[key] = string.Join(
                    RecordSeparator.ToString(),
                    records.ToArray());
            }
        }

        internal static void RemapSavedOptions(
            SaveableBoatCustomization customization,
            SaveBoatCustomizationData data)
        {
            BoatCustomParts parts =
                customization.GetComponent<BoatCustomParts>();
            UnstayedBoatProfile profile;
            string encoded;
            if (data == null || data.partActiveOptions == null ||
                !UnstayedBoatRegistry.TryGet(parts, out profile) ||
                GameState.modData == null ||
                !GameState.modData.TryGetValue(
                    GetKey(profile.SceneIndex),
                    out encoded) ||
                string.IsNullOrEmpty(encoded))
            {
                return;
            }

            string[] records = encoded.Split(RecordSeparator);
            for (int i = 0; i < records.Length; i++)
            {
                int separator = records[i].IndexOf(FieldSeparator);
                int partIndex;
                if (separator <= 0 ||
                    !int.TryParse(
                        records[i].Substring(0, separator),
                        out partIndex) ||
                    partIndex < 0 ||
                    partIndex >= data.partActiveOptions.Count)
                {
                    continue;
                }

                string sourceId = Uri.UnescapeDataString(
                    records[i].Substring(separator + 1));
                for (int j = 0; j < profile.Masts.Count; j++)
                {
                    UnstayedMastProfile mast = profile.Masts[j];
                    if (parts.availableParts.IndexOf(mast.MastPart) !=
                        partIndex)
                    {
                        continue;
                    }

                    UnstayedMastMarker marker =
                        mast.UnstayedOption.GetComponent<
                            UnstayedMastMarker>();
                    if (marker != null && marker.SourceId == sourceId)
                    {
                        data.partActiveOptions[partIndex] =
                            mast.MastPart.partOptions.IndexOf(
                                mast.UnstayedOption);
                        break;
                    }
                }
            }
        }

        private static string GetKey(int sceneIndex)
        {
            return Plugin.PluginGuid + "." + sceneIndex + ".activeMasts";
        }
    }
}

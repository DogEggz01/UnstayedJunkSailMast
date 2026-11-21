using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace UnstayedJunkSailMast
{
    // Runs before Shipyard Expansion's postfix, which converts the saved sails
    // and needs every unstayed mast in its boat's registry.
    [HarmonyPatch(typeof(SaveLoadManager), "LoadNeeds")]
    [HarmonyBefore(Plugin.ShipyardExpansionGuid)]
    internal static class EarlyModDataLoadPatch
    {
        private static void Postfix(SaveContainer __0)
        {
            GameState.modData = __0 != null && __0.modData != null
                ? __0.modData
                : new Dictionary<string, string>();
            UnstayedMastIndexCoordinator.RebindFromLoadedModData();
        }
    }

    // Runs after Shipyard Expansion's postfix, which adds its mast options.
    [HarmonyPatch(typeof(SaveableBoatCustomization), "Awake")]
    [HarmonyAfter(Plugin.ShipyardExpansionGuid)]
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

    [HarmonyPatch(typeof(BoatRefs), "RegisterMast")]
    internal static class IdempotentUnstayedMastRegistrationPatch
    {
        private static bool Prefix(BoatRefs __instance, Mast mast)
        {
            if (__instance == null || mast == null ||
                mast.GetComponent<UnstayedMastMarker>() == null ||
                __instance.masts == null)
            {
                return true;
            }

            int mastIndex = mast.orderIndex;
            return mastIndex < 0 || mastIndex >= __instance.masts.Length ||
                   __instance.masts[mastIndex] != mast;
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
            UnstayedSaveCompatibility.PrepareLoad(__instance, data);
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

        internal static bool UsesDiameterCompensation(Sail sail)
        {
            return sail != null &&
                   !RectangleJunkSails.IsRectangle(sail.gameObject) &&
                   IsAllowed(sail.gameObject);
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
        private const int IndexLayoutVersion = 2;

        internal static void PrepareLoad(
            SaveableBoatCustomization customization,
            SaveBoatCustomizationData data)
        {
            RectangleJunkSailIndexMigration.PrepareLoad(
                customization,
                data);
            RemapSavedOptions(customization, data);
            MigrateMastIndices(customization, data);
        }

        internal static void RecordActiveOptions(
            SaveableBoatCustomization customization,
            SaveBoatCustomizationData data)
        {
            RectangleJunkSailIndexMigration.MarkCurrent(customization);
            BoatCustomParts parts =
                customization.GetComponent<BoatCustomParts>();
            UnstayedBoatProfile profile;
            if (data == null ||
                !UnstayedBoatRegistry.TryGet(parts, out profile) ||
                GameState.modData == null)
            {
                return;
            }

            bool pendingLegacyMigration =
                GetIndexLayoutVersion(profile.SceneIndex) <
                    IndexLayoutVersion &&
                HasActiveMastRecords(profile.SceneIndex);
            if (pendingLegacyMigration)
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

                UnstayedMastSourceIdentity identity =
                    mast.Marker != null ? mast.Marker.Identity : null;
                if (identity != null &&
                    !string.IsNullOrEmpty(identity.StableId))
                {
                    records.Add(
                        partIndex + FieldSeparator.ToString() +
                        Uri.EscapeDataString(identity.StableId));
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

            GameState.modData[GetIndexLayoutKey(profile.SceneIndex)] =
                IndexLayoutVersion.ToString();
        }

        private static void RemapSavedOptions(
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

                string sourceId;
                try
                {
                    sourceId = Uri.UnescapeDataString(
                        records[i].Substring(separator + 1));
                }
                catch (UriFormatException)
                {
                    continue;
                }

                UnstayedMastProfile match = FindMatchingMastProfile(
                    profile,
                    parts,
                    sourceId,
                    partIndex);

                if (match == null)
                {
                    continue;
                }

                int currentPartIndex =
                    parts.availableParts.IndexOf(match.MastPart);
                int currentOptionIndex =
                    match.MastPart.partOptions.IndexOf(
                        match.UnstayedOption);
                if (currentPartIndex >= 0 &&
                    currentPartIndex < data.partActiveOptions.Count &&
                    currentOptionIndex >= 0)
                {
                    data.partActiveOptions[currentPartIndex] =
                        currentOptionIndex;
                }
            }
        }

        private static UnstayedMastProfile FindMatchingMastProfile(
            UnstayedBoatProfile profile,
            BoatCustomParts parts,
            string persistedSourceId,
            int savedPartIndex)
        {
            UnstayedMastProfile match = null;
            int bestScore = 0;
            bool ambiguous = false;
            for (int i = 0; i < profile.Masts.Count; i++)
            {
                UnstayedMastProfile candidate = profile.Masts[i];
                UnstayedMastSourceIdentity identity =
                    candidate.Marker != null
                        ? candidate.Marker.Identity
                        : null;
                if (identity == null)
                {
                    continue;
                }

                int score = identity.GetPersistedMatchScore(
                    persistedSourceId);
                if (score <= 0)
                {
                    continue;
                }

                if (parts.availableParts.IndexOf(candidate.MastPart) ==
                    savedPartIndex)
                {
                    score += 5;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    match = candidate;
                    ambiguous = false;
                }
                else if (score == bestScore)
                {
                    ambiguous = true;
                }
            }

            if (!ambiguous)
            {
                return match;
            }

            Plugin.LogSource?.LogWarning(
                "Did not remap an ambiguous saved unstayed mast source " +
                "identity: " + persistedSourceId + ".");
            return null;
        }

        private static void MigrateMastIndices(
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

            HashSet<int> discardedIndices = new HashSet<int>();
            if (profile.RetiredMastIndices != null)
            {
                for (int i = 0; i < profile.RetiredMastIndices.Count; i++)
                {
                    discardedIndices.Add(profile.RetiredMastIndices[i]);
                }
            }

            bool legacyLayout = GetIndexLayoutVersion(profile.SceneIndex) <
                                IndexLayoutVersion;
            if (legacyLayout && HasActiveMastRecords(profile.SceneIndex))
            {
                HashSet<int> foreignIndices =
                    FindForeignExtendedMastIndices(customization.transform);
                for (int mastIndex =
                         UnstayedMastIndexRules.ExtendedIndexStart;
                     mastIndex <
                         UnstayedMastIndexRules.MastArrayCapacity;
                     mastIndex++)
                {
                    if (!foreignIndices.Contains(mastIndex))
                    {
                        discardedIndices.Add(mastIndex);
                    }
                }
            }

            int removedSails = RemoveSavedSails(data, discardedIndices);
            int removedExpansionRecords =
                RemoveShipyardExpansionSailRecords(
                    profile.SceneIndex,
                    discardedIndices);
            if (removedSails > 0 || removedExpansionRecords > 0)
            {
                Plugin.LogSource?.LogWarning(
                    "Migrated unstayed mast indices for scene " +
                    profile.SceneIndex + "; discarded " + removedSails +
                    " legacy sail(s) and " + removedExpansionRecords +
                    " Shipyard Expansion sail setting record(s). " +
                    "No refund is issued by this migration.");
            }

            GameState.modData[GetIndexLayoutKey(profile.SceneIndex)] =
                IndexLayoutVersion.ToString();
        }

        private static int RemoveSavedSails(
            SaveBoatCustomizationData data,
            HashSet<int> discardedIndices)
        {
            if (data.sails == null || discardedIndices.Count == 0)
            {
                return 0;
            }

            int removed = 0;
            for (int i = data.sails.Count - 1; i >= 0; i--)
            {
                SaveSailData sail = data.sails[i];
                if (sail != null &&
                    discardedIndices.Contains(sail.mastIndex))
                {
                    data.sails.RemoveAt(i);
                    removed++;
                }
            }

            return removed;
        }

        private static HashSet<int> FindForeignExtendedMastIndices(
            Transform boat)
        {
            HashSet<int> result = new HashSet<int>();
            if (boat == null)
            {
                return result;
            }

            Mast[] masts = boat.GetComponentsInChildren<Mast>(true);
            for (int i = 0; i < masts.Length; i++)
            {
                Mast mast = masts[i];
                if (mast != null &&
                    UnstayedMastIndexRules.IsExtendedIndex(
                        mast.orderIndex) &&
                    mast.GetComponent<UnstayedMastMarker>() == null)
                {
                    result.Add(mast.orderIndex);
                }
            }

            return result;
        }

        private static int RemoveShipyardExpansionSailRecords(
            int sceneIndex,
            HashSet<int> discardedIndices)
        {
            if (discardedIndices.Count == 0 || GameState.modData == null)
            {
                return 0;
            }

            string key = "SEboatSails." + sceneIndex;
            string encoded;
            if (!GameState.modData.TryGetValue(key, out encoded) ||
                string.IsNullOrEmpty(encoded))
            {
                return 0;
            }

            int versionSeparator = encoded.LastIndexOf('|');
            string recordsText = versionSeparator >= 0
                ? encoded.Substring(0, versionSeparator)
                : encoded;
            string versionSuffix = versionSeparator >= 0
                ? encoded.Substring(versionSeparator)
                : string.Empty;
            string[] records = recordsText.Split(
                new[] { ')' },
                StringSplitOptions.RemoveEmptyEntries);
            List<string> kept = new List<string>();
            int removed = 0;
            for (int i = 0; i < records.Length; i++)
            {
                int open = records[i].IndexOf('(');
                int mastIndex;
                if (open <= 0 ||
                    !int.TryParse(
                        records[i].Substring(0, open),
                        out mastIndex) ||
                    !discardedIndices.Contains(mastIndex))
                {
                    kept.Add(records[i] + ")");
                    continue;
                }

                removed++;
            }

            if (removed > 0)
            {
                GameState.modData[key] =
                    string.Concat(kept.ToArray()) + versionSuffix;
            }

            return removed;
        }

        private static bool HasActiveMastRecords(int sceneIndex)
        {
            string encoded;
            return GameState.modData.TryGetValue(
                       GetKey(sceneIndex),
                       out encoded) &&
                   !string.IsNullOrEmpty(encoded);
        }

        private static int GetIndexLayoutVersion(int sceneIndex)
        {
            string encoded;
            int version;
            return GameState.modData.TryGetValue(
                       GetIndexLayoutKey(sceneIndex),
                       out encoded) &&
                   int.TryParse(encoded, out version)
                ? version
                : 0;
        }

        private static string GetKey(int sceneIndex)
        {
            return Plugin.PluginGuid + "." + sceneIndex + ".activeMasts";
        }

        private static string GetIndexLayoutKey(int sceneIndex)
        {
            return Plugin.PluginGuid + "." + sceneIndex +
                   ".indexLayoutVersion";
        }
    }

    internal sealed class UnstayedMastMarker : MonoBehaviour
    {
        // Not serialized: a copy made with Object.Instantiate has none. Read
        // only through UnstayedBoatRegistry, which holds the masts' own boats.
        internal UnstayedMastSourceIdentity Identity;
    }

    internal sealed class EmptyRestrictedPartMarker : MonoBehaviour
    {
    }

    internal sealed class UnstayedBoatMarker : MonoBehaviour
    {
        private void OnDestroy()
        {
            BoatCustomParts parts = GetComponent<BoatCustomParts>();
            if (parts != null)
            {
                UnstayedBoatRegistry.Unregister(parts);
            }
        }
    }

    internal enum RestrictedPartKind
    {
        Rigging,
        RiggingAccessory,
        CrowsNest
    }

    internal sealed class RestrictedPartSelection
    {
        internal BoatPart Part;
        internal List<BoatPart> OwningMastParts;
        internal BoatPartOption EmptyOption;
        internal List<BoatPartOption> NonEmptyOptions;
        internal RestrictedPartKind Kind;
    }

    internal sealed class UnstayedMastProfile
    {
        internal BoatPart MastPart;
        internal BoatPartOption UnstayedOption;
        internal UnstayedMastMarker Marker;
        internal bool UsesFixedVanillaIndex;
        internal List<RestrictedPartSelection> RestrictedSelections;
    }

    internal sealed class UnstayedBoatProfile
    {
        internal BoatCustomParts Parts;
        internal BoatRefs Refs;
        internal int SceneIndex;
        internal List<UnstayedMastProfile> Masts;
        internal List<int> RetiredMastIndices;
    }

    internal static class UnstayedBoatRegistry
    {
        private static readonly Dictionary<BoatCustomParts, UnstayedBoatProfile>
            Profiles = new Dictionary<BoatCustomParts, UnstayedBoatProfile>();

        internal static void Register(UnstayedBoatProfile profile)
        {
            Profiles[profile.Parts] = profile;
        }

        internal static bool TryGet(
            BoatCustomParts parts,
            out UnstayedBoatProfile profile)
        {
            if (parts == null)
            {
                profile = null;
                return false;
            }

            return Profiles.TryGetValue(parts, out profile);
        }

        internal static void Unregister(BoatCustomParts parts)
        {
            Profiles.Remove(parts);
        }

        internal static List<UnstayedBoatProfile> GetProfiles()
        {
            return new List<UnstayedBoatProfile>(Profiles.Values);
        }

        internal static void Clear()
        {
            Profiles.Clear();
        }
    }

    internal static partial class UnstayedMastBuilder
    {
        private static readonly string[] SmallJunkAnchorPaths =
        {
            "junk small/structure/mast",
            "junk small/structure/mast_center",
            "junk small/structure/mast_001"
        };

        private static readonly string[] MediumJunkAnchorPaths =
        {
            "junk medium (actual)/structure/mast_mid_0",
            "junk medium (actual)/structure/mast_mid_1",
            "junk medium (actual)/structure/mast_mizzen_0",
            "junk medium (actual)/structure/mast_mizzen_1",
            "junk medium (actual)/structure/mast_front_"
        };

        private static readonly string[] LargeJunkAnchorPaths =
        {
            "junk large (3)/junk large (3)/structure/masts_structure/mast_main_1",
            "junk large (3)/junk large (3)/structure/masts_structure/mast_main_2",
            "junk large (3)/junk large (3)/structure/masts_structure/mast_back",
            "junk large (3)/junk large (3)/structure/masts_structure/mast_front"
        };

        internal static void TryBuild(
            SaveableBoatCustomization customization,
            BoatCustomParts parts,
            BoatRefs refs)
        {
            if (customization == null || parts == null || refs == null ||
                customization.GetComponent<UnstayedBoatMarker>() != null)
            {
                return;
            }

            SaveableObject saveable = customization.GetComponent<SaveableObject>();
            if (saveable == null || !IsSupportedBoat(saveable.sceneIndex))
            {
                return;
            }

            if (!HasShipyardExpansionMastCapacity(refs))
            {
                return;
            }

            List<BoatPart> targetParts = FindTargetMastParts(
                customization.transform,
                parts,
                saveable.sceneIndex);
            if (targetParts.Count == 0)
            {
                Plugin.LogSource?.LogError(
                    "No eligible mast part groups were found on " +
                    customization.name + ".");
                return;
            }

            Dictionary<BoatPart, List<BoatPartOption>> sourcesByMastPart =
                new Dictionary<BoatPart, List<BoatPartOption>>();
            for (int i = 0; i < targetParts.Count; i++)
            {
                sourcesByMastPart[targetParts[i]] =
                    GetEligibleSources(targetParts[i]);
            }

            List<RestrictedPartSelection> allRestrictedSelections =
                FindRestrictedSelections(
                    customization.transform,
                    parts,
                    targetParts,
                    sourcesByMastPart);
            List<UnstayedMastProfile> mastProfiles =
                new List<UnstayedMastProfile>();
            UnstayedMastIndexAllocator indexAllocator =
                new UnstayedMastIndexAllocator(
                    customization.transform,
                    saveable.sceneIndex);

            for (int i = 0; i < targetParts.Count; i++)
            {
                BoatPart mastPart = targetParts[i];
                List<BoatPartOption> sources = sourcesByMastPart[mastPart];
                if (sources.Count == 0)
                {
                    continue;
                }

                List<RestrictedPartSelection> restrictedSelections =
                    GetRestrictedSelectionsForMast(
                        allRestrictedSelections,
                        mastPart);

                for (int j = 0; j < sources.Count; j++)
                {
                    BoatPartOption source = sources[j];
                    UnstayedMastSourceIdentity identity =
                        UnstayedMastSourceIdentity.Create(
                            source,
                            mastPart,
                            customization.transform,
                            saveable.sceneIndex);
                    int mastIndex;
                    bool usesFixedVanillaIndex;
                    if (!indexAllocator.TryClaim(
                            source,
                            identity,
                            out mastIndex,
                            out usesFixedVanillaIndex))
                    {
                        continue;
                    }

                    BoatPartOption clone = CloneUnstayedMast(
                        source,
                        saveable.sceneIndex,
                        mastIndex,
                        identity,
                        restrictedSelections);
                    if (clone == null)
                    {
                        continue;
                    }

                    mastPart.partOptions.Add(clone);
                    AddMutualRestrictions(clone, restrictedSelections);
                    mastProfiles.Add(new UnstayedMastProfile
                    {
                        MastPart = mastPart,
                        UnstayedOption = clone,
                        Marker = clone.GetComponent<UnstayedMastMarker>(),
                        UsesFixedVanillaIndex = usesFixedVanillaIndex,
                        RestrictedSelections =
                            new List<RestrictedPartSelection>(
                                restrictedSelections)
                    });
                }
            }

            indexAllocator.Commit();

            if (mastProfiles.Count == 0)
            {
                Plugin.LogSource?.LogError(
                    "No unstayed mast options were created on " +
                    customization.name + ".");
                return;
            }

            RestrictNoShroudsToUnstayedMasts(
                allRestrictedSelections);

            customization.gameObject.AddComponent<UnstayedBoatMarker>();
            UnstayedBoatRegistry.Register(new UnstayedBoatProfile
            {
                Parts = parts,
                Refs = refs,
                SceneIndex = saveable.sceneIndex,
                Masts = mastProfiles,
                RetiredMastIndices = indexAllocator.GetRetiredIndices()
            });

            int fixedVanillaCount = 0;
            for (int i = 0; i < mastProfiles.Count; i++)
            {
                if (mastProfiles[i].UsesFixedVanillaIndex)
                {
                    fixedVanillaCount++;
                }
            }

            Plugin.LogSource?.LogInfo(
                "Added " + mastProfiles.Count + " unstayed mast option(s) to " +
                customization.name + " (scene " + saveable.sceneIndex +
                "): " + fixedVanillaCount + " fixed vanilla, " +
                (mastProfiles.Count - fixedVanillaCount) +
                " extended.");
        }

        private static bool IsSupportedBoat(int sceneIndex)
        {
            return sceneIndex == 90 || sceneIndex == 80 || sceneIndex == 70;
        }

        private static bool HasShipyardExpansionMastCapacity(BoatRefs refs)
        {
            if (refs.masts == null ||
                refs.masts.Length <
                UnstayedMastIndexRules.MastArrayCapacity)
            {
                Plugin.LogSource?.LogError(
                    "Could not add unstayed masts: Shipyard Expansion " +
                    "did not provide its 128-slot mast registry.");
                return false;
            }

            return true;
        }

        private static List<BoatPart> FindTargetMastParts(
            Transform boat,
            BoatCustomParts parts,
            int sceneIndex)
        {
            List<BoatPart> result = new List<BoatPart>();
            string[] paths = sceneIndex == 90
                ? SmallJunkAnchorPaths
                : sceneIndex == 80
                    ? MediumJunkAnchorPaths
                    : LargeJunkAnchorPaths;

            for (int i = 0; i < paths.Length; i++)
            {
                Transform anchor = boat.Find(paths[i]);
                if (anchor == null)
                {
                    anchor = FindUniqueMastByName(
                        boat,
                        GetLastPathSegment(paths[i]));
                }

                BoatPartOption option =
                    anchor != null ? anchor.GetComponent<BoatPartOption>() : null;
                BoatPart part = FindContainingPart(parts, option);
                AddUnique(result, part);
            }

            // Shipyard Expansion normally appends mast options to the vanilla
            // groups. This second pass also accepts any expansion-only mast
            // group in the Shipyard's mast category.
            for (int i = 0; i < parts.availableParts.Count; i++)
            {
                BoatPart part = parts.availableParts[i];
                if (part == null || part.category != 0 ||
                    !ContainsEligibleMast(part))
                {
                    continue;
                }

                AddUnique(result, part);
            }

            result.Sort((left, right) =>
                parts.availableParts.IndexOf(left).CompareTo(
                    parts.availableParts.IndexOf(right)));
            return result;
        }

        private static Transform FindUniqueMastByName(
            Transform root,
            string name)
        {
            Mast[] masts = root.GetComponentsInChildren<Mast>(true);
            Transform match = null;
            for (int i = 0; i < masts.Length; i++)
            {
                if (!string.Equals(
                    masts[i].name,
                    name,
                    StringComparison.OrdinalIgnoreCase) ||
                    masts[i].GetComponent<BoatPartOption>() == null)
                {
                    continue;
                }

                if (match != null)
                {
                    return null;
                }

                match = masts[i].transform;
            }

            return match;
        }

        private static string GetLastPathSegment(string path)
        {
            int separator = path.LastIndexOf('/');
            return separator >= 0 ? path.Substring(separator + 1) : path;
        }

        private static BoatPart FindContainingPart(
            BoatCustomParts parts,
            BoatPartOption option)
        {
            if (option == null)
            {
                return null;
            }

            for (int i = 0; i < parts.availableParts.Count; i++)
            {
                BoatPart part = parts.availableParts[i];
                if (part != null && part.partOptions != null &&
                    part.partOptions.Contains(option))
                {
                    return part;
                }
            }

            return null;
        }

        private static bool ContainsEligibleMast(BoatPart part)
        {
            if (part.partOptions == null)
            {
                return false;
            }

            for (int i = 0; i < part.partOptions.Count; i++)
            {
                if (IsEligibleSource(part.partOptions[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<BoatPartOption> GetEligibleSources(BoatPart part)
        {
            List<BoatPartOption> result = new List<BoatPartOption>();
            BoatPartOption[] snapshot = part.partOptions.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                if (IsEligibleSource(snapshot[i]))
                {
                    result.Add(snapshot[i]);
                }
            }

            return result;
        }

        private static bool IsEligibleSource(BoatPartOption option)
        {
            Mast mast = option != null ? option.GetComponent<Mast>() : null;
            if (mast == null ||
                option.GetComponent<UnstayedMastMarker>() != null)
            {
                return false;
            }

            string text = UnstayedNameRules.Normalize(
                option.optionName + " " + option.name + " " +
                GetHierarchyPath(option.transform));
            if (text.Contains("bermuda") || text.Contains("bowsprit") ||
                text.Contains("forestay") || text.Contains("midstay") ||
                text.Contains("front stay") || text.Contains("back stay") ||
                text.Contains("mast stay") || text.Contains("sprit mast") ||
                text.StartsWith("unstayed ", StringComparison.Ordinal) ||
                mast.onlyStaysails)
            {
                return false;
            }

            return true;
        }

        private static string GetHierarchyPath(
            Transform transform,
            Transform stopAt = null)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            List<string> segments = new List<string>();
            Transform current = transform;
            while (current != null && current != stopAt)
            {
                segments.Add(current.name);
                current = current.parent;
            }

            segments.Reverse();
            return string.Join("/", segments.ToArray());
        }

        private static string GetRelativePath(
            Transform transform,
            Transform root)
        {
            if (transform == root)
            {
                return string.Empty;
            }

            List<string> segments = new List<string>();
            Transform current = transform;
            while (current != null && current != root)
            {
                segments.Add(current.name);
                current = current.parent;
            }

            if (current != root)
            {
                return null;
            }

            segments.Reverse();
            return string.Join("/", segments.ToArray());
        }

        private static bool IsWithin(Transform transform, Transform root)
        {
            if (transform == null || root == null)
            {
                return false;
            }

            return transform == root || transform.IsChildOf(root);
        }

        private static bool Contains(
            List<BoatPartOption> options,
            BoatPartOption value)
        {
            return options != null && options.Contains(value);
        }

        private static void AddUnique<T>(List<T> list, T value)
            where T : class
        {
            if (value != null && !list.Contains(value))
            {
                list.Add(value);
            }
        }
    }

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

                UnstayedMastMarker marker = cloneTransform.gameObject
                    .AddComponent<UnstayedMastMarker>();
                marker.Identity = identity;
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

            List<SurfaceFittingPlacement> surfaceFittings =
                CaptureSurfaceFittings(root);
            GameObject bodyObject = new GameObject(bodyName);
            bodyObject.layer = root.gameObject.layer;
            bodyObject.tag = root.gameObject.tag;
            bodyObject.isStatic = root.gameObject.isStatic;
            if (isWalkBody)
            {
                bodyObject.AddComponent<UnstayedScaledBodyMarker>();
            }

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

            return colliderMap;
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

    }

    internal static partial class UnstayedMastBuilder
    {
        private static List<RestrictedPartSelection> FindRestrictedSelections(
            Transform boat,
            BoatCustomParts parts,
            List<BoatPart> mastParts,
            Dictionary<BoatPart, List<BoatPartOption>> sourcesByMastPart)
        {
            List<RestrictedPartSelection> result =
                new List<RestrictedPartSelection>();
            for (int i = 0; i < parts.availableParts.Count; i++)
            {
                BoatPart candidatePart = parts.availableParts[i];
                if (candidatePart == null || mastParts.Contains(candidatePart) ||
                    candidatePart.partOptions == null ||
                    candidatePart.partOptions.Count == 0)
                {
                    continue;
                }

                RestrictedPartKind kind;
                if (!TryGetRestrictedKind(candidatePart, boat, out kind))
                {
                    continue;
                }

                List<BoatPart> owners = FindOwningMastParts(
                    candidatePart,
                    mastParts,
                    sourcesByMastPart);
                if (owners.Count == 0)
                {
                    continue;
                }

                RestrictedPartSelection selection = CreateRestrictedSelection(
                    candidatePart,
                    owners,
                    kind,
                    boat);
                if (selection != null)
                {
                    result.Add(selection);
                }
            }

            return result;
        }

        private static List<RestrictedPartSelection>
            GetRestrictedSelectionsForMast(
                List<RestrictedPartSelection> selections,
                BoatPart mastPart)
        {
            List<RestrictedPartSelection> result =
                new List<RestrictedPartSelection>();
            for (int i = 0; i < selections.Count; i++)
            {
                if (selections[i].OwningMastParts.Contains(mastPart))
                {
                    result.Add(selections[i]);
                }
            }

            return result;
        }

        private static bool TryGetRestrictedKind(
            BoatPart part,
            Transform boat,
            out RestrictedPartKind kind)
        {
            bool directRigging = false;
            bool directAccessory = false;
            bool directCrowsNest = false;
            for (int i = 0; i < part.partOptions.Count; i++)
            {
                BoatPartOption option = part.partOptions[i];
                string directText = GetDirectOptionSignalText(option, boat);
                directRigging |= HasRiggingText(directText);
                directAccessory |= HasRiggingAccessoryText(directText);
                directCrowsNest |= HasCrowsNestText(directText);
            }

            if (directRigging && !directCrowsNest)
            {
                kind = RestrictedPartKind.Rigging;
                return true;
            }

            if (directCrowsNest && !directRigging)
            {
                kind = RestrictedPartKind.CrowsNest;
                return true;
            }

            if (directAccessory && !directRigging && !directCrowsNest)
            {
                kind = RestrictedPartKind.RiggingAccessory;
                return true;
            }

            bool rigging = false;
            bool accessory = false;
            bool crowsNest = false;
            for (int i = 0; i < part.partOptions.Count; i++)
            {
                string text = GetOptionSignalText(part.partOptions[i], boat);
                rigging |= HasRiggingText(text);
                accessory |= HasRiggingAccessoryText(text);
                crowsNest |= HasCrowsNestText(text);
            }

            if (rigging && !crowsNest)
            {
                kind = RestrictedPartKind.Rigging;
                return true;
            }

            if (crowsNest && !rigging)
            {
                kind = RestrictedPartKind.CrowsNest;
                return true;
            }

            if (accessory && !rigging && !crowsNest)
            {
                kind = RestrictedPartKind.RiggingAccessory;
                return true;
            }

            kind = RestrictedPartKind.Rigging;
            return false;
        }

        private static List<BoatPart> FindOwningMastParts(
            BoatPart candidatePart,
            List<BoatPart> mastParts,
            Dictionary<BoatPart, List<BoatPartOption>> sourcesByMastPart)
        {
            List<BoatPart> owners = new List<BoatPart>();
            int bestScore = 0;
            for (int i = 0; i < mastParts.Count; i++)
            {
                BoatPart mastPart = mastParts[i];
                int score = GetPartRelationScore(
                    candidatePart,
                    sourcesByMastPart[mastPart]);
                if (score > bestScore)
                {
                    bestScore = score;
                    owners.Clear();
                    owners.Add(mastPart);
                }
                else if (score > 0 && score == bestScore)
                {
                    owners.Add(mastPart);
                }
            }

            if (bestScore == 0)
            {
                owners.Clear();
            }
            else if (owners.Count > 1 && bestScore < 80)
            {
                Plugin.LogSource?.LogWarning(
                    "Skipped a weak multi-mast Shipyard relationship (" +
                    GetPartLabel(candidatePart) + ").");
                owners.Clear();
            }

            return owners;
        }

        private static int GetPartRelationScore(
            BoatPart candidatePart,
            List<BoatPartOption> sources)
        {
            int bestScore = 0;
            for (int i = 0; i < candidatePart.partOptions.Count; i++)
            {
                BoatPartOption candidate = candidatePart.partOptions[i];
                for (int j = 0; j < sources.Count; j++)
                {
                    bestScore = Math.Max(
                        bestScore,
                        GetOptionRelationScore(candidate, sources[j]));
                }
            }

            return bestScore;
        }

        private static int GetOptionRelationScore(
            BoatPartOption candidate,
            BoatPartOption mastOption)
        {
            if (candidate == null || mastOption == null)
            {
                return 0;
            }

            if (Contains(candidate.requires, mastOption) ||
                Contains(mastOption.requires, candidate) ||
                candidate.childMast == mastOption.GetComponent<Mast>())
            {
                return 100;
            }

            if (Contains(candidate.requiresDisabled, mastOption) ||
                Contains(mastOption.requiresDisabled, candidate))
            {
                return 90;
            }

            if (AnyChildWithin(
                    candidate.childOptions,
                    mastOption.transform,
                    mastOption.walkColObject != null
                        ? mastOption.walkColObject.transform
                        : null))
            {
                return 80;
            }

            if (AnyChildWithin(
                    mastOption.childOptions,
                    candidate.transform,
                    candidate.walkColObject != null
                        ? candidate.walkColObject.transform
                        : null))
            {
                return 70;
            }

            return 0;
        }

        private static bool AnyChildWithin(
            GameObject[] children,
            Transform firstRoot,
            Transform secondRoot)
        {
            if (children == null)
            {
                return false;
            }

            for (int i = 0; i < children.Length; i++)
            {
                Transform child =
                    children[i] != null ? children[i].transform : null;
                if (IsWithin(child, firstRoot) || IsWithin(child, secondRoot))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetPartLabel(BoatPart part)
        {
            if (part == null || part.partOptions == null ||
                part.partOptions.Count == 0 || part.partOptions[0] == null)
            {
                return "unnamed";
            }

            BoatPartOption option = part.partOptions[0];
            return string.IsNullOrWhiteSpace(option.optionName)
                ? option.name
                : option.optionName;
        }

        private static RestrictedPartSelection CreateRestrictedSelection(
            BoatPart part,
            List<BoatPart> owningMastParts,
            RestrictedPartKind kind,
            Transform boat)
        {
            BoatPartOption empty = null;
            List<BoatPartOption> nonEmpty = new List<BoatPartOption>();
            for (int i = 0; i < part.partOptions.Count; i++)
            {
                BoatPartOption option = part.partOptions[i];
                if (option == null)
                {
                    continue;
                }

                if (IsEmptyOption(option, kind))
                {
                    if (empty == null)
                    {
                        empty = option;
                    }
                }
                else
                {
                    nonEmpty.Add(option);
                }
            }

            if (nonEmpty.Count == 0)
            {
                return null;
            }

            if (empty == null && kind == RestrictedPartKind.Rigging)
            {
                empty = CreateNoShroudsOption(part, boat);
            }

            return new RestrictedPartSelection
            {
                Part = part,
                OwningMastParts = new List<BoatPart>(owningMastParts),
                EmptyOption = empty,
                NonEmptyOptions = nonEmpty,
                Kind = kind
            };
        }

        private static bool IsEmptyOption(
            BoatPartOption option,
            RestrictedPartKind kind)
        {
            return option.GetComponent<EmptyRestrictedPartMarker>() != null ||
                   IsExplicitEmptyName(option, kind);
        }

        private static bool IsExplicitEmptyName(
            BoatPartOption option,
            RestrictedPartKind kind)
        {
            string displayName =
                UnstayedNameRules.Normalize(option.optionName);
            string objectName = UnstayedNameRules.Normalize(option.name);
            string text = displayName + " " + objectName;
            if (displayName == "none" || displayName == "empty")
            {
                return true;
            }

            if (kind == RestrictedPartKind.CrowsNest)
            {
                return HasCrowsNestText(text) &&
                       UnstayedNameRules.HasAbsenceToken(text);
            }

            if (kind == RestrictedPartKind.RiggingAccessory)
            {
                return HasRiggingAccessoryText(text) &&
                       UnstayedNameRules.HasAbsenceToken(text);
            }

            return (HasRiggingText(text) ||
                    UnstayedNameRules.ContainsWord(text, "rig")) &&
                   UnstayedNameRules.HasAbsenceToken(text);
        }

        private static BoatPartOption CreateNoShroudsOption(
            BoatPart part,
            Transform boat)
        {
            const string displayName = "(no shrouds)";
            const string objectName = "UJSM_empty_shrouds";

            GameObject visual = new GameObject(objectName);
            visual.SetActive(false);
            visual.transform.SetParent(boat, false);
            BoatPartOption option = visual.AddComponent<BoatPartOption>();

            GameObject walk = new GameObject(objectName + "_walk");
            walk.SetActive(false);
            walk.transform.SetParent(boat, false);

            option.optionName = displayName;
            option.basePrice = 0;
            option.installCost = 0;
            option.mass = 0;
            option.requires = new List<BoatPartOption>();
            option.requiresDisabled = new List<BoatPartOption>();
            option.walkColObject = walk;
            option.canInstall = true;
            option.childOptions = new GameObject[0];
            option.childMast = null;
            visual.AddComponent<EmptyRestrictedPartMarker>();
            part.partOptions.Add(option);

            Plugin.LogSource?.LogInfo(
                "Added missing " + displayName +
                " option to a rigging Shipyard group.");
            return option;
        }

        private static void AddMutualRestrictions(
            BoatPartOption unstayed,
            List<RestrictedPartSelection> selections)
        {
            for (int i = 0; i < selections.Count; i++)
            {
                List<BoatPartOption> nonEmpty =
                    selections[i].NonEmptyOptions;
                for (int j = 0; j < nonEmpty.Count; j++)
                {
                    AddMutualExclusion(unstayed, nonEmpty[j]);
                }
            }
        }

        private static void RestrictNoShroudsToUnstayedMasts(
            List<RestrictedPartSelection> selections)
        {
            for (int i = 0; i < selections.Count; i++)
            {
                RestrictedPartSelection selection = selections[i];
                if (selection.Kind != RestrictedPartKind.Rigging ||
                    selection.EmptyOption == null)
                {
                    continue;
                }

                for (int j = 0;
                     j < selection.OwningMastParts.Count;
                     j++)
                {
                    BoatPart mastPart = selection.OwningMastParts[j];
                    for (int k = 0; k < mastPart.partOptions.Count; k++)
                    {
                        BoatPartOption mastOption = mastPart.partOptions[k];
                        if (mastOption == null ||
                            mastOption.GetComponent<Mast>() == null ||
                            mastOption.GetComponent<UnstayedMastMarker>() !=
                                null)
                        {
                            continue;
                        }

                        AddMutualExclusion(
                            selection.EmptyOption,
                            mastOption);
                    }
                }
            }
        }

        private static void AddMutualExclusion(
            BoatPartOption first,
            BoatPartOption second)
        {
            if (first == null || second == null)
            {
                return;
            }

            first.requiresDisabled = first.requiresDisabled ??
                new List<BoatPartOption>();
            second.requiresDisabled = second.requiresDisabled ??
                new List<BoatPartOption>();
            AddUnique(first.requiresDisabled, second);
            AddUnique(second.requiresDisabled, first);
        }

        private static bool HasRiggingText(string text)
        {
            return text.Contains("shroud") ||
                   text.Contains("static rig") ||
                   text.Contains("static rope atts") ||
                   text.Contains("static rope attachment");
        }

        private static bool HasRiggingAccessoryText(string text)
        {
            return text.Contains("telltale") ||
                   text.Contains("wind flag");
        }

        private static bool HasCrowsNestText(string text)
        {
            return text.Contains("crowsnest") ||
                   text.Contains("crows nest") ||
                   text.Contains("crownest") ||
                   text.Contains("crow nest");
        }

        // The option's names and the paths of its own objects.
        private static StringBuilder BuildDirectSignal(
            BoatPartOption option,
            Transform boat)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(option.optionName).Append(' ')
                .Append(option.name).Append(' ')
                .Append(GetHierarchyPath(option.transform, boat));
            if (option.walkColObject != null)
            {
                builder.Append(' ').Append(
                    GetHierarchyPath(option.walkColObject.transform, boat));
            }

            return builder;
        }

        private static string GetDirectOptionSignalText(
            BoatPartOption option,
            Transform boat)
        {
            return option == null
                ? string.Empty
                : UnstayedNameRules.Normalize(
                    BuildDirectSignal(option, boat).ToString());
        }

        // The direct signal plus the paths of the option's child objects.
        private static string GetOptionSignalText(
            BoatPartOption option,
            Transform boat)
        {
            if (option == null)
            {
                return string.Empty;
            }

            StringBuilder builder = BuildDirectSignal(option, boat);
            if (option.childOptions != null)
            {
                for (int i = 0; i < option.childOptions.Length; i++)
                {
                    if (option.childOptions[i] != null)
                    {
                        builder.Append(' ').Append(
                            GetHierarchyPath(
                                option.childOptions[i].transform,
                                boat));
                    }
                }
            }

            return UnstayedNameRules.Normalize(builder.ToString());
        }

    }

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

            float heightLimit;
            if (!TryGetSectionCenter(
                    fittingCenter.z,
                    mastVertices,
                    mastBounds,
                    out heightLimit,
                    out center))
            {
                radial = Vector2.zero;
                radius = 0f;
                return false;
            }

            radial = new Vector2(
                fittingCenter.x - center.x,
                fittingCenter.y - center.y);
            if (!NormalizeAndValidateFittingRadial(ref radial))
            {
                radius = 0f;
                return false;
            }

            radius = GetSectionRadius(
                fittingCenter.z,
                heightLimit,
                mastVertices,
                center,
                radial);
            return radius > 0.001f;
        }

        // The mast's cross-section at a height: the vertices within a thin
        // band around the vertex height nearest to it. Gives the middle of
        // their X and Y extents.
        private static bool TryGetSectionCenter(
            float height,
            Vector3[] mastVertices,
            Bounds mastBounds,
            out float heightLimit,
            out Vector2 center)
        {
            heightLimit = 0f;
            center = Vector2.zero;
            if (mastVertices == null || mastVertices.Length == 0)
            {
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
            heightLimit = nearestHeight + band;
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
                return false;
            }

            center = new Vector2(
                (minX + maxX) * 0.5f,
                (minY + maxY) * 0.5f);
            return true;
        }

        // How far the section reaches from its centre along a direction.
        private static float GetSectionRadius(
            float height,
            float heightLimit,
            Vector3[] mastVertices,
            Vector2 center,
            Vector2 radial)
        {
            float radius = 0f;
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

            return radius;
        }

        internal static bool TryGetSailMountOffset(
            Mast mast,
            Sail sail,
            out Vector3 localOffset)
        {
            localOffset = Vector3.zero;
            if (mast == null || sail == null || sail.windcenter == null)
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
                    DiameterScale,
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

            float heightLimit;
            if (!TryGetSectionCenter(
                    height,
                    mastVertices,
                    mastBounds,
                    out heightLimit,
                    out center))
            {
                radius = 0f;
                return false;
            }

            radius = GetSectionRadius(
                height,
                heightLimit,
                mastVertices,
                center,
                radial);
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

    [HarmonyPatch(typeof(Sail), "UpdateInstallPosition")]
    [HarmonyAfter(Plugin.ShipyardExpansionGuid)]
    internal static class UnstayedSailInstallPositionPatch
    {
        private static void Postfix(Sail __instance)
        {
            Transform parent = __instance.transform.parent;
            Mast mast = parent != null ? parent.GetComponent<Mast>() : null;
            if (mast == null ||
                mast.GetComponent<UnstayedMastMarker>() == null ||
                !UnstayedSailRules.UsesDiameterCompensation(__instance))
            {
                return;
            }

            Vector3 offset;
            if (!UnstayedMastBuilder.TryGetSailMountOffset(
                    mast,
                    __instance,
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

    // Marks an unstayed mast's enlarged walk body. It holds no data, so a copy
    // of the boat made with Object.Instantiate keeps a working marker.
    internal sealed class UnstayedScaledBodyMarker : MonoBehaviour
    {
    }

    [HarmonyPatch(typeof(ShipyardSailColChecker), "OnTriggerEnter")]
    internal static class UnstayedOwnMastCollisionPatch
    {
        // The game skips the checker's parent, the mast's walk collider. On an
        // unstayed mast that collider's shape sits on the enlarged body under
        // it, so the body is skipped the same way. Other masts' bodies and the
        // walk collider's other children are still checked.
        private static bool Prefix(ShipyardSailColChecker __instance, Collider other)
        {
            if (other == null ||
                !IsOwnMastBody(other.transform.parent, __instance.transform.parent))
            {
                return true;
            }

            return other.GetComponent<UnstayedScaledBodyMarker>() == null;
        }

        internal static bool IsOwnMastBody(Transform colliderParent, Transform checkerParent)
        {
            return colliderParent != null && colliderParent == checkerParent;
        }
    }

    internal sealed class UnstayedMastSourceIdentity
    {
        private const string StablePrefix = "v2|";

        internal string StableId { get; }
        internal string LegacyId { get; }

        private UnstayedMastSourceIdentity(
            string stableId,
            string legacyId)
        {
            StableId = stableId;
            LegacyId = legacyId;
        }

        internal static UnstayedMastSourceIdentity Create(
            BoatPartOption source,
            BoatPart owningPart,
            Transform boat,
            int sceneIndex)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            Mast mast = source.GetComponent<Mast>();
            string legacyId =
                UnstayedNameRules.Normalize(source.optionName) + "|" +
                UnstayedNameRules.Normalize(source.name) + "|" +
                (mast != null ? mast.orderIndex.ToString() : "-");

            int fixedIndex;
            if (UnstayedMastIndexRules.TryGetFixedVanillaIndex(
                    sceneIndex,
                    source,
                    out fixedIndex))
            {
                return new UnstayedMastSourceIdentity(
                    StablePrefix + "boat:" + sceneIndex + "|vanilla:" +
                    NormalizeSegment(source.name),
                    legacyId);
            }

            string groupId = GetGroupId(
                owningPart,
                source,
                boat,
                sceneIndex);
            string objectPath = GetNormalizedRelativePath(
                source.transform,
                boat);
            return new UnstayedMastSourceIdentity(
                StablePrefix + "boat:" + sceneIndex + "|group:" + groupId +
                "|path:" + objectPath,
                legacyId);
        }

        internal int GetPersistedMatchScore(string persistedId)
        {
            if (string.Equals(
                    persistedId,
                    StableId,
                    StringComparison.Ordinal))
            {
                return 100;
            }

            if (string.Equals(
                    persistedId,
                    LegacyId,
                    StringComparison.Ordinal))
            {
                return 90;
            }

            if (string.IsNullOrEmpty(persistedId) ||
                persistedId.StartsWith(
                    StablePrefix,
                    StringComparison.Ordinal))
            {
                return 0;
            }

            string[] persistedFields = persistedId.Split('|');
            string[] currentFields = LegacyId.Split('|');
            if (persistedFields.Length != 3 || currentFields.Length != 3)
            {
                return 0;
            }

            bool sameDisplayName = string.Equals(
                persistedFields[0],
                currentFields[0],
                StringComparison.Ordinal);
            bool sameObjectName = !string.IsNullOrEmpty(currentFields[1]) &&
                                  string.Equals(
                                      persistedFields[1],
                                      currentFields[1],
                                      StringComparison.Ordinal);
            if (sameDisplayName && sameObjectName)
            {
                return 80;
            }

            return sameObjectName ? 70 : 0;
        }

        private static string GetGroupId(
            BoatPart owningPart,
            BoatPartOption source,
            Transform boat,
            int sceneIndex)
        {
            if (owningPart != null && owningPart.partOptions != null)
            {
                List<string> fixedGroupIds = new List<string>();
                for (int i = 0; i < owningPart.partOptions.Count; i++)
                {
                    BoatPartOption option = owningPart.partOptions[i];
                    int fixedIndex;
                    if (option != null &&
                        UnstayedMastIndexRules.TryGetFixedVanillaIndex(
                            sceneIndex,
                            option,
                            out fixedIndex))
                    {
                        string groupId = "vanilla:" +
                                         NormalizeSegment(option.name);
                        if (!fixedGroupIds.Contains(groupId))
                        {
                            fixedGroupIds.Add(groupId);
                        }
                    }
                }

                if (fixedGroupIds.Count > 0)
                {
                    fixedGroupIds.Sort(StringComparer.Ordinal);
                    return fixedGroupIds[0];
                }
            }

            Transform parent = source.transform.parent;
            return "path:" + GetNormalizedRelativePath(parent, boat);
        }

        private static string GetNormalizedRelativePath(
            Transform transform,
            Transform root)
        {
            if (transform == null)
            {
                return "missing";
            }

            List<string> segments = new List<string>();
            Transform current = transform;
            while (current != null && current != root)
            {
                segments.Add(NormalizeSegment(current.name));
                current = current.parent;
            }

            if (current != root)
            {
                return NormalizeSegment(transform.name);
            }

            segments.Reverse();
            return segments.Count > 0
                ? string.Join("/", segments.ToArray())
                : "root";
        }

        private static string NormalizeSegment(string value)
        {
            string normalized = UnstayedNameRules.Normalize(value);
            return string.IsNullOrEmpty(normalized) ? "unnamed" : normalized;
        }
    }

    internal static class UnstayedMastIndexRules
    {
        internal const int ExtendedIndexStart = 96;
        internal const int MastArrayCapacity = 128;

        internal static bool TryGetFixedVanillaIndex(
            int sceneIndex,
            BoatPartOption source,
            out int cloneIndex)
        {
            cloneIndex = -1;
            Mast mast = source != null ? source.GetComponent<Mast>() : null;
            if (mast == null)
            {
                return false;
            }

            string objectName = source.name;
            if (sceneIndex == 90)
            {
                if (Matches(mast, objectName, 5, "mast"))
                {
                    cloneIndex = 27;
                }
                else if (Matches(mast, objectName, 6, "mast_center"))
                {
                    cloneIndex = 28;
                }
                else if (Matches(mast, objectName, 7, "mast_001"))
                {
                    cloneIndex = 29;
                }
            }
            else if (sceneIndex == 80)
            {
                if (Matches(mast, objectName, 9, "mast_front_"))
                {
                    cloneIndex = 25;
                }
                else if (Matches(mast, objectName, 10, "mast_mid_0"))
                {
                    cloneIndex = 26;
                }
                else if (Matches(mast, objectName, 11, "mast_mid_1"))
                {
                    cloneIndex = 27;
                }
                else if (Matches(mast, objectName, 12, "mast_mizzen_0"))
                {
                    cloneIndex = 28;
                }
                else if (Matches(mast, objectName, 13, "mast_mizzen_1"))
                {
                    cloneIndex = 29;
                }
            }
            else if (sceneIndex == 70)
            {
                if (Matches(mast, objectName, 2, "mast_main_1"))
                {
                    cloneIndex = 26;
                }
                else if (Matches(mast, objectName, 3, "mast_main_2"))
                {
                    cloneIndex = 27;
                }
                else if (Matches(mast, objectName, 4, "mast_back"))
                {
                    cloneIndex = 28;
                }
                else if (Matches(mast, objectName, 1, "mast_front"))
                {
                    cloneIndex = 29;
                }
            }

            return cloneIndex >= 0;
        }

        internal static bool IsExtendedIndex(int mastIndex)
        {
            return mastIndex >= ExtendedIndexStart &&
                   mastIndex < MastArrayCapacity;
        }

        internal static string GetExtendedMappingKey(int sceneIndex)
        {
            return Plugin.PluginGuid + "." + sceneIndex +
                   ".extendedMastIndices";
        }

        private static bool Matches(
            Mast mast,
            string objectName,
            int sourceIndex,
            string expectedName)
        {
            return mast.orderIndex == sourceIndex &&
                   string.Equals(
                       objectName,
                       expectedName,
                       StringComparison.OrdinalIgnoreCase);
        }
    }

    internal sealed class UnstayedMastIndexAllocator
    {
        private const char RecordSeparator = ';';
        private const char FieldSeparator = '=';

        private readonly int sceneIndex;
        private readonly HashSet<int> occupiedIndices = new HashSet<int>();
        private readonly Dictionary<string, int> extendedMappings =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<int, string> extendedOwners =
            new Dictionary<int, string>();
        private readonly HashSet<int> retiredIndices = new HashSet<int>();
        private bool mappingsChanged;

        internal UnstayedMastIndexAllocator(
            Transform boat,
            int sceneIndex)
        {
            this.sceneIndex = sceneIndex;
            CollectOccupiedIndices(boat);
            LoadMappings();
            ValidateMappings();
        }

        internal List<int> GetRetiredIndices()
        {
            List<int> result = new List<int>(retiredIndices);
            result.Sort();
            return result;
        }

        internal bool TryClaim(
            BoatPartOption source,
            UnstayedMastSourceIdentity identity,
            out int mastIndex,
            out bool usesFixedVanillaIndex)
        {
            if (UnstayedMastIndexRules.TryGetFixedVanillaIndex(
                    sceneIndex,
                    source,
                    out mastIndex))
            {
                usesFixedVanillaIndex = true;
                if (occupiedIndices.Contains(mastIndex) ||
                    extendedOwners.ContainsKey(mastIndex))
                {
                    Plugin.LogSource?.LogError(
                        "Skipped " + source.optionName +
                        ": fixed vanilla mast index " + mastIndex +
                        " is already occupied.");
                    return false;
                }

                occupiedIndices.Add(mastIndex);
                return true;
            }

            usesFixedVanillaIndex = false;
            return TryClaimExtended(
                identity,
                out mastIndex);
        }

        internal bool TryClaimExtended(
            UnstayedMastSourceIdentity identity,
            out int mastIndex)
        {
            mastIndex = -1;
            if (identity == null || string.IsNullOrEmpty(identity.StableId))
            {
                return false;
            }

            string sourceId = identity.StableId;
            int mappedIndex;
            string mappedOwner;
            if (extendedMappings.TryGetValue(sourceId, out mappedIndex) &&
                UnstayedMastIndexRules.IsExtendedIndex(mappedIndex) &&
                extendedOwners.TryGetValue(mappedIndex, out mappedOwner) &&
                mappedOwner == sourceId)
            {
                if (occupiedIndices.Contains(mappedIndex))
                {
                    Plugin.LogSource?.LogError(
                        "Skipped duplicate Expansion mast source identity " +
                        sourceId + ".");
                    return false;
                }

                occupiedIndices.Add(mappedIndex);
                mastIndex = mappedIndex;
                return true;
            }

            string legacySourceId;
            if (TryFindLegacyMapping(identity, out legacySourceId))
            {
                mappedIndex = extendedMappings[legacySourceId];
                if (occupiedIndices.Contains(mappedIndex))
                {
                    Plugin.LogSource?.LogError(
                        "Skipped duplicate Expansion mast source identity " +
                        sourceId + ".");
                    return false;
                }

                extendedMappings.Remove(legacySourceId);
                extendedMappings[sourceId] = mappedIndex;
                extendedOwners[mappedIndex] = sourceId;
                occupiedIndices.Add(mappedIndex);
                mappingsChanged = true;
                mastIndex = mappedIndex;
                Plugin.LogSource?.LogInfo(
                    "Migrated an Expansion mast source key to v2 at index " +
                    mappedIndex + ".");
                return true;
            }

            for (int candidate = UnstayedMastIndexRules.ExtendedIndexStart;
                 candidate < UnstayedMastIndexRules.MastArrayCapacity;
                 candidate++)
            {
                if (occupiedIndices.Contains(candidate) ||
                    extendedOwners.ContainsKey(candidate))
                {
                    continue;
                }

                extendedMappings[sourceId] = candidate;
                extendedOwners[candidate] = sourceId;
                occupiedIndices.Add(candidate);
                mappingsChanged = true;
                mastIndex = candidate;
                return true;
            }

            Plugin.LogSource?.LogError(
                "No extended mast index remains in the 96-127 range for " +
                sourceId + ".");
            return false;
        }

        private bool TryFindLegacyMapping(
            UnstayedMastSourceIdentity identity,
            out string legacySourceId)
        {
            legacySourceId = null;
            int bestScore = 0;
            bool ambiguous = false;
            foreach (KeyValuePair<string, int> mapping in extendedMappings)
            {
                int score = identity.GetPersistedMatchScore(mapping.Key);
                if (score <= 0 || mapping.Key == identity.StableId)
                {
                    continue;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    legacySourceId = mapping.Key;
                    ambiguous = false;
                }
                else if (score == bestScore)
                {
                    ambiguous = true;
                }
            }

            if (!ambiguous)
            {
                return legacySourceId != null;
            }

            Plugin.LogSource?.LogWarning(
                "Did not migrate an ambiguous legacy Expansion mast source " +
                "key for " + identity.StableId + ".");
            legacySourceId = null;
            return false;
        }

        internal void Commit()
        {
            if (!mappingsChanged)
            {
                return;
            }

            if (GameState.modData == null)
            {
                GameState.modData = new Dictionary<string, string>();
            }

            List<string> sourceIds =
                new List<string>(extendedMappings.Keys);
            sourceIds.Sort(StringComparer.Ordinal);
            List<string> records = new List<string>();
            for (int i = 0; i < sourceIds.Count; i++)
            {
                string sourceId = sourceIds[i];
                records.Add(
                    Uri.EscapeDataString(sourceId) +
                    FieldSeparator.ToString() +
                    extendedMappings[sourceId]);
            }

            GameState.modData[
                UnstayedMastIndexRules.GetExtendedMappingKey(sceneIndex)] =
                string.Join(
                    RecordSeparator.ToString(),
                    records.ToArray());
        }

        private void CollectOccupiedIndices(Transform boat)
        {
            if (boat == null)
            {
                return;
            }

            Mast[] masts = boat.GetComponentsInChildren<Mast>(true);
            for (int i = 0; i < masts.Length; i++)
            {
                Mast mast = masts[i];
                if (mast != null &&
                    mast.GetComponent<UnstayedMastMarker>() == null)
                {
                    occupiedIndices.Add(mast.orderIndex);
                }
            }
        }

        private void LoadMappings()
        {
            string encoded;
            if (GameState.modData == null ||
                !GameState.modData.TryGetValue(
                    UnstayedMastIndexRules.GetExtendedMappingKey(sceneIndex),
                    out encoded) ||
                string.IsNullOrEmpty(encoded))
            {
                return;
            }

            string[] records = encoded.Split(RecordSeparator);
            for (int i = 0; i < records.Length; i++)
            {
                int separator = records[i].LastIndexOf(FieldSeparator);
                int mastIndex;
                if (separator <= 0 ||
                    !int.TryParse(
                        records[i].Substring(separator + 1),
                        out mastIndex))
                {
                    mappingsChanged = true;
                    continue;
                }

                string sourceId;
                try
                {
                    sourceId = Uri.UnescapeDataString(
                        records[i].Substring(0, separator));
                }
                catch (UriFormatException)
                {
                    mappingsChanged = true;
                    continue;
                }

                if (string.IsNullOrEmpty(sourceId))
                {
                    mappingsChanged = true;
                    continue;
                }

                extendedMappings[sourceId] = mastIndex;
            }
        }

        private void ValidateMappings()
        {
            List<string> sourceIds =
                new List<string>(extendedMappings.Keys);
            sourceIds.Sort(StringComparer.Ordinal);
            for (int i = 0; i < sourceIds.Count; i++)
            {
                string sourceId = sourceIds[i];
                int mastIndex = extendedMappings[sourceId];
                if (!UnstayedMastIndexRules.IsExtendedIndex(mastIndex) ||
                    occupiedIndices.Contains(mastIndex) ||
                    extendedOwners.ContainsKey(mastIndex))
                {
                    if (UnstayedMastIndexRules.IsExtendedIndex(mastIndex))
                    {
                        retiredIndices.Add(mastIndex);
                    }

                    extendedMappings.Remove(sourceId);
                    mappingsChanged = true;
                    Plugin.LogSource?.LogWarning(
                        "Released conflicting extended mast index " +
                        mastIndex + " for source " + sourceId + ".");
                    continue;
                }

                extendedOwners[mastIndex] = sourceId;
            }
        }
    }

    internal static class UnstayedMastIndexCoordinator
    {
        internal static void RebindFromLoadedModData()
        {
            List<UnstayedBoatProfile> profiles =
                UnstayedBoatRegistry.GetProfiles();
            for (int i = 0; i < profiles.Count; i++)
            {
                RebindProfile(profiles[i]);
            }
        }

        private static void RebindProfile(UnstayedBoatProfile profile)
        {
            if (profile == null || profile.Parts == null ||
                profile.Refs == null || profile.Masts == null)
            {
                return;
            }

            UnstayedMastIndexAllocator allocator =
                new UnstayedMastIndexAllocator(
                    profile.Parts.transform,
                    profile.SceneIndex);
            Dictionary<Mast, int> assignments =
                new Dictionary<Mast, int>();
            for (int i = 0; i < profile.Masts.Count; i++)
            {
                UnstayedMastProfile mastProfile = profile.Masts[i];
                if (mastProfile.UsesFixedVanillaIndex)
                {
                    continue;
                }

                Mast mast = mastProfile.UnstayedOption != null
                    ? mastProfile.UnstayedOption.GetComponent<Mast>()
                    : null;
                UnstayedMastSourceIdentity identity =
                    mastProfile.Marker != null
                        ? mastProfile.Marker.Identity
                        : null;
                int mastIndex;
                if (mast == null || identity == null ||
                    !allocator.TryClaimExtended(
                        identity,
                        out mastIndex))
                {
                    continue;
                }

                assignments[mast] = mastIndex;
            }

            foreach (KeyValuePair<Mast, int> assignment in assignments)
            {
                Mast mast = assignment.Key;
                if (mast.orderIndex >= 0 &&
                    mast.orderIndex < profile.Refs.masts.Length &&
                    profile.Refs.masts[mast.orderIndex] == mast)
                {
                    profile.Refs.masts[mast.orderIndex] = null;
                }
            }

            foreach (KeyValuePair<Mast, int> assignment in assignments)
            {
                Mast mast = assignment.Key;
                int mastIndex = assignment.Value;
                mast.orderIndex = mastIndex;
            }

            PrepareRegistryForSaveConversion(profile);

            allocator.Commit();
            profile.RetiredMastIndices = allocator.GetRetiredIndices();
            if (assignments.Count > 0)
            {
                Plugin.LogSource?.LogInfo(
                    "Applied " + assignments.Count +
                    " persisted extended mast index mapping(s) for scene " +
                    profile.SceneIndex + ".");
            }
        }

        private static void PrepareRegistryForSaveConversion(
            UnstayedBoatProfile profile)
        {
            if (profile.Refs.masts == null)
            {
                return;
            }

            int preparedCount = 0;
            for (int i = 0; i < profile.Masts.Count; i++)
            {
                UnstayedMastProfile mastProfile = profile.Masts[i];
                Mast mast = mastProfile.UnstayedOption != null
                    ? mastProfile.UnstayedOption.GetComponent<Mast>()
                    : null;
                if (mast == null)
                {
                    continue;
                }

                int mastIndex = mast.orderIndex;
                if (mastIndex < 0 || mastIndex >= profile.Refs.masts.Length)
                {
                    Plugin.LogSource?.LogError(
                        "Could not prepare unstayed mast registry index " +
                        mastIndex + " for scene " + profile.SceneIndex + ".");
                    continue;
                }

                Mast registered = profile.Refs.masts[mastIndex];
                if (registered == mast)
                {
                    continue;
                }

                if (registered != null)
                {
                    Plugin.LogSource?.LogError(
                        "Could not prepare unstayed mast registry index " +
                        mastIndex + " for scene " + profile.SceneIndex +
                        ": the slot is occupied by " +
                        registered.gameObject.name + ".");
                    continue;
                }

                profile.Refs.masts[mastIndex] = mast;
                preparedCount++;
            }

            if (preparedCount > 0)
            {
                Plugin.LogSource?.LogInfo(
                    "Prepared " + preparedCount +
                    " inactive unstayed mast registry entry/entries for " +
                    "save conversion in scene " + profile.SceneIndex + ".");
            }
        }

    }

    internal static class UnstayedNameRules
    {
        internal static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            StringBuilder result = new StringBuilder(value.Length);
            bool previousWasSpace = false;
            for (int i = 0; i < value.Length; i++)
            {
                char character = char.ToLowerInvariant(value[i]);
                if (char.IsLetterOrDigit(character))
                {
                    result.Append(character);
                    previousWasSpace = false;
                }
                else if (!previousWasSpace)
                {
                    result.Append(' ');
                    previousWasSpace = true;
                }
            }

            return result.ToString().Trim();
        }

        internal static bool IsNamedVariant(
            string normalizedName,
            string normalizedBaseName)
        {
            return normalizedName == normalizedBaseName ||
                   normalizedName.StartsWith(
                       normalizedBaseName + " ",
                       StringComparison.Ordinal);
        }

        internal static bool ContainsWord(string normalizedText, string word)
        {
            return (" " + normalizedText + " ").Contains(
                " " + word + " ");
        }

        internal static bool HasAbsenceToken(string normalizedText)
        {
            return ContainsWord(normalizedText, "no") ||
                   ContainsWord(normalizedText, "none") ||
                   ContainsWord(normalizedText, "without") ||
                   ContainsWord(normalizedText, "empty");
        }
    }
}

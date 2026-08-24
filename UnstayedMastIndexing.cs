using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnstayedJunkSailMast
{
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
            mastIndex = -1;
            if (!Plugin.ShipyardExpansionLoaded)
            {
                return false;
            }

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
                if (mast.sails != null)
                {
                    Mast registered = profile.Refs.masts[mastIndex];
                    if (registered == null || registered == mast)
                    {
                        profile.Refs.masts[mastIndex] = mast;
                    }
                    else
                    {
                        Plugin.LogSource?.LogError(
                            "Could not register rebound unstayed mast at " +
                            mastIndex + ": the slot became occupied.");
                    }
                }
            }

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

    }
}

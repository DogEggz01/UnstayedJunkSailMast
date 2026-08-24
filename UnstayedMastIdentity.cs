using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnstayedJunkSailMast
{
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
}

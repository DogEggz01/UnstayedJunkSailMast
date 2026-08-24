using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnstayedJunkSailMast
{
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

            EnsureMastArrayCapacity(refs);

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
                    GetEligibleSources(
                        targetParts[i],
                        saveable.sceneIndex);
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
                        refs,
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

        private static void EnsureMastArrayCapacity(BoatRefs refs)
        {
            if (refs.masts == null)
            {
                refs.masts = new Mast[
                    UnstayedMastIndexRules.MastArrayCapacity];
                return;
            }

            if (refs.masts.Length <
                UnstayedMastIndexRules.MastArrayCapacity)
            {
                Array.Resize(
                    ref refs.masts,
                    UnstayedMastIndexRules.MastArrayCapacity);
            }
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
                    !ContainsEligibleMast(part, sceneIndex))
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

        private static bool ContainsEligibleMast(
            BoatPart part,
            int sceneIndex)
        {
            if (part.partOptions == null)
            {
                return false;
            }

            for (int i = 0; i < part.partOptions.Count; i++)
            {
                if (ShouldCloneSource(
                        part.partOptions[i],
                        sceneIndex))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<BoatPartOption> GetEligibleSources(
            BoatPart part,
            int sceneIndex)
        {
            List<BoatPartOption> result = new List<BoatPartOption>();
            BoatPartOption[] snapshot = part.partOptions.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                if (ShouldCloneSource(snapshot[i], sceneIndex))
                {
                    result.Add(snapshot[i]);
                }
            }

            return result;
        }

        private static bool ShouldCloneSource(
            BoatPartOption option,
            int sceneIndex)
        {
            if (!IsEligibleSource(option))
            {
                return false;
            }

            int fixedIndex;
            return Plugin.ShipyardExpansionLoaded ||
                   UnstayedMastIndexRules.TryGetFixedVanillaIndex(
                       sceneIndex,
                       option,
                       out fixedIndex);
        }

        private static bool IsEligibleSource(BoatPartOption option)
        {
            if (option == null || option.GetComponent<Mast>() == null ||
                option.GetComponent<UnstayedMastMarker>() != null)
            {
                return false;
            }

            Mast mast = option.GetComponent<Mast>();
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
}

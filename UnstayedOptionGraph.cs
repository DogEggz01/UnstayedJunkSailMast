using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace UnstayedJunkSailMast
{
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

        private static string GetDirectOptionSignalText(
            BoatPartOption option,
            Transform boat)
        {
            if (option == null)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            builder.Append(option.optionName).Append(' ')
                .Append(option.name).Append(' ')
                .Append(GetHierarchyPath(option.transform, boat));
            if (option.walkColObject != null)
            {
                builder.Append(' ').Append(
                    GetHierarchyPath(option.walkColObject.transform, boat));
            }

            return UnstayedNameRules.Normalize(builder.ToString());
        }

        private static string GetOptionSignalText(
            BoatPartOption option,
            Transform boat)
        {
            if (option == null)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            builder.Append(option.optionName).Append(' ')
                .Append(option.name).Append(' ')
                .Append(GetHierarchyPath(option.transform, boat));
            if (option.walkColObject != null)
            {
                builder.Append(' ').Append(
                    GetHierarchyPath(option.walkColObject.transform, boat));
            }

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
}

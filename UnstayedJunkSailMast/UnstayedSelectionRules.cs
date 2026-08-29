using System.Collections.Generic;

namespace UnstayedJunkSailMast;

internal static class UnstayedSelectionRules
{
	internal static List<int> NormalizeOrder(BoatCustomParts parts, BoatPartsOrder order)
	{
		List<int> list = new List<int>();
		if (parts == null || order == null || !UnstayedBoatRegistry.TryGet(parts, out var profile))
		{
			return list;
		}
		for (int i = 0; i < profile.Masts.Count; i++)
		{
			UnstayedMastProfile unstayedMastProfile = profile.Masts[i];
			int num = parts.availableParts.IndexOf(unstayedMastProfile.MastPart);
			int num2 = unstayedMastProfile.MastPart.partOptions.IndexOf(unstayedMastProfile.UnstayedOption);
			if (num >= 0 && num2 >= 0 && num < order.orderedOptions.Length && order.orderedOptions[num] == num2)
			{
				ForceEmptySelections(parts, order.orderedOptions, unstayedMastProfile.RestrictedSelections, list);
			}
		}
		return list;
	}

	internal static bool NormalizeActive(BoatCustomParts parts)
	{
		if (parts == null || !UnstayedBoatRegistry.TryGet(parts, out var profile))
		{
			return false;
		}
		bool result = false;
		for (int i = 0; i < profile.Masts.Count; i++)
		{
			UnstayedMastProfile unstayedMastProfile = profile.Masts[i];
			int num = unstayedMastProfile.MastPart.partOptions.IndexOf(unstayedMastProfile.UnstayedOption);
			if (num < 0 || unstayedMastProfile.MastPart.activeOption != num)
			{
				continue;
			}
			for (int j = 0; j < unstayedMastProfile.RestrictedSelections.Count; j++)
			{
				RestrictedPartSelection restrictedPartSelection = unstayedMastProfile.RestrictedSelections[j];
				if (restrictedPartSelection.Kind == RestrictedPartKind.Rigging && !(restrictedPartSelection.EmptyOption == null))
				{
					int num2 = restrictedPartSelection.Part.partOptions.IndexOf(restrictedPartSelection.EmptyOption);
					if (num2 >= 0 && restrictedPartSelection.Part.activeOption != num2)
					{
						restrictedPartSelection.Part.activeOption = num2;
						result = true;
					}
				}
			}
		}
		return result;
	}

	private static void ForceEmptySelections(BoatCustomParts parts, int[] orderedOptions, List<RestrictedPartSelection> selections, List<int> changedPartIndices)
	{
		for (int i = 0; i < selections.Count; i++)
		{
			RestrictedPartSelection restrictedPartSelection = selections[i];
			if (restrictedPartSelection.Kind != RestrictedPartKind.Rigging || restrictedPartSelection.EmptyOption == null)
			{
				continue;
			}
			int num = parts.availableParts.IndexOf(restrictedPartSelection.Part);
			int num2 = restrictedPartSelection.Part.partOptions.IndexOf(restrictedPartSelection.EmptyOption);
			if (num >= 0 && num < orderedOptions.Length && num2 >= 0 && orderedOptions[num] != num2)
			{
				orderedOptions[num] = num2;
				if (!changedPartIndices.Contains(num))
				{
					changedPartIndices.Add(num);
				}
			}
		}
	}
}

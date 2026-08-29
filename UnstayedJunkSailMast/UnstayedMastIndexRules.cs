using System;

namespace UnstayedJunkSailMast;

internal static class UnstayedMastIndexRules
{
	internal const int ExtendedIndexStart = 96;

	internal const int MastArrayCapacity = 128;

	internal static bool TryGetFixedVanillaIndex(int sceneIndex, BoatPartOption source, out int cloneIndex)
	{
		cloneIndex = -1;
		Mast mast = ((source != null) ? source.GetComponent<Mast>() : null);
		if (mast == null)
		{
			return false;
		}
		string name = source.name;
		switch (sceneIndex)
		{
		case 90:
			if (Matches(mast, name, 5, "mast"))
			{
				cloneIndex = 27;
			}
			else if (Matches(mast, name, 6, "mast_center"))
			{
				cloneIndex = 28;
			}
			else if (Matches(mast, name, 7, "mast_001"))
			{
				cloneIndex = 29;
			}
			break;
		case 80:
			if (Matches(mast, name, 9, "mast_front_"))
			{
				cloneIndex = 25;
			}
			else if (Matches(mast, name, 10, "mast_mid_0"))
			{
				cloneIndex = 26;
			}
			else if (Matches(mast, name, 11, "mast_mid_1"))
			{
				cloneIndex = 27;
			}
			else if (Matches(mast, name, 12, "mast_mizzen_0"))
			{
				cloneIndex = 28;
			}
			else if (Matches(mast, name, 13, "mast_mizzen_1"))
			{
				cloneIndex = 29;
			}
			break;
		case 70:
			if (Matches(mast, name, 2, "mast_main_1"))
			{
				cloneIndex = 26;
			}
			else if (Matches(mast, name, 3, "mast_main_2"))
			{
				cloneIndex = 27;
			}
			else if (Matches(mast, name, 4, "mast_back"))
			{
				cloneIndex = 28;
			}
			else if (Matches(mast, name, 1, "mast_front"))
			{
				cloneIndex = 29;
			}
			break;
		}
		return cloneIndex >= 0;
	}

	internal static bool IsExtendedIndex(int mastIndex)
	{
		if (mastIndex >= 96)
		{
			return mastIndex < 128;
		}
		return false;
	}

	internal static string GetExtendedMappingKey(int sceneIndex)
	{
		return "dogeggz.unstayedjunksailmast." + sceneIndex + ".extendedMastIndices";
	}

	private static bool Matches(Mast mast, string objectName, int sourceIndex, string expectedName)
	{
		if (mast.orderIndex == sourceIndex)
		{
			return string.Equals(objectName, expectedName, StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}
}

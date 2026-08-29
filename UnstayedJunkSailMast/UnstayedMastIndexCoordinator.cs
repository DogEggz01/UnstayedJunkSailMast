using System.Collections.Generic;

namespace UnstayedJunkSailMast;

internal static class UnstayedMastIndexCoordinator
{
	internal static void RebindFromLoadedModData()
	{
		List<UnstayedBoatProfile> profiles = UnstayedBoatRegistry.GetProfiles();
		for (int i = 0; i < profiles.Count; i++)
		{
			RebindProfile(profiles[i]);
		}
	}

	private static void RebindProfile(UnstayedBoatProfile profile)
	{
		if (profile == null || profile.Parts == null || profile.Refs == null || profile.Masts == null)
		{
			return;
		}
		UnstayedMastIndexAllocator unstayedMastIndexAllocator = new UnstayedMastIndexAllocator(profile.Parts.transform, profile.SceneIndex);
		Dictionary<Mast, int> dictionary = new Dictionary<Mast, int>();
		for (int i = 0; i < profile.Masts.Count; i++)
		{
			UnstayedMastProfile unstayedMastProfile = profile.Masts[i];
			if (!unstayedMastProfile.UsesFixedVanillaIndex)
			{
				Mast mast = ((unstayedMastProfile.UnstayedOption != null) ? unstayedMastProfile.UnstayedOption.GetComponent<Mast>() : null);
				UnstayedMastSourceIdentity unstayedMastSourceIdentity = ((unstayedMastProfile.Marker != null) ? unstayedMastProfile.Marker.Identity : null);
				if (!(mast == null) && unstayedMastSourceIdentity != null && unstayedMastIndexAllocator.TryClaimExtended(unstayedMastSourceIdentity, out var mastIndex))
				{
					dictionary[mast] = mastIndex;
				}
			}
		}
		foreach (KeyValuePair<Mast, int> item in dictionary)
		{
			Mast key = item.Key;
			if (key.orderIndex >= 0 && key.orderIndex < profile.Refs.masts.Length && profile.Refs.masts[key.orderIndex] == key)
			{
				profile.Refs.masts[key.orderIndex] = null;
			}
		}
		foreach (KeyValuePair<Mast, int> item2 in dictionary)
		{
			Mast key2 = item2.Key;
			int value = item2.Value;
			key2.orderIndex = value;
		}
		PrepareRegistryForSaveConversion(profile);
		unstayedMastIndexAllocator.Commit();
		profile.RetiredMastIndices = unstayedMastIndexAllocator.GetRetiredIndices();
		if (dictionary.Count > 0)
		{
			Plugin.LogSource?.LogInfo("Applied " + dictionary.Count + " persisted extended mast index mapping(s) for scene " + profile.SceneIndex + ".");
		}
	}

	private static void PrepareRegistryForSaveConversion(UnstayedBoatProfile profile)
	{
		if (profile.Refs.masts == null)
		{
			return;
		}
		int num = 0;
		for (int i = 0; i < profile.Masts.Count; i++)
		{
			UnstayedMastProfile unstayedMastProfile = profile.Masts[i];
			Mast mast = ((unstayedMastProfile.UnstayedOption != null) ? unstayedMastProfile.UnstayedOption.GetComponent<Mast>() : null);
			if (mast == null)
			{
				continue;
			}
			int orderIndex = mast.orderIndex;
			if (orderIndex < 0 || orderIndex >= profile.Refs.masts.Length)
			{
				Plugin.LogSource?.LogError("Could not prepare unstayed mast registry index " + orderIndex + " for scene " + profile.SceneIndex + ".");
				continue;
			}
			Mast mast2 = profile.Refs.masts[orderIndex];
			if (!(mast2 == mast))
			{
				if (mast2 != null)
				{
					Plugin.LogSource?.LogError("Could not prepare unstayed mast registry index " + orderIndex + " for scene " + profile.SceneIndex + ": the slot is occupied by " + mast2.gameObject.name + ".");
				}
				else
				{
					profile.Refs.masts[orderIndex] = mast;
					num++;
				}
			}
		}
		if (num > 0)
		{
			Plugin.LogSource?.LogInfo("Prepared " + num + " inactive unstayed mast registry entry/entries for save conversion in scene " + profile.SceneIndex + ".");
		}
	}
}

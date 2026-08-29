using System.Collections.Generic;

namespace UnstayedJunkSailMast;

internal static class UnstayedBoatRegistry
{
	private static readonly Dictionary<BoatCustomParts, UnstayedBoatProfile> Profiles = new Dictionary<BoatCustomParts, UnstayedBoatProfile>();

	internal static void Register(UnstayedBoatProfile profile)
	{
		Profiles[profile.Parts] = profile;
	}

	internal static bool TryGet(BoatCustomParts parts, out UnstayedBoatProfile profile)
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

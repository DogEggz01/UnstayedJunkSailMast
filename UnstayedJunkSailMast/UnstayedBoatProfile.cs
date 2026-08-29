using System.Collections.Generic;

namespace UnstayedJunkSailMast;

internal sealed class UnstayedBoatProfile
{
	internal BoatCustomParts Parts;

	internal BoatRefs Refs;

	internal int SceneIndex;

	internal List<UnstayedMastProfile> Masts;

	internal List<int> RetiredMastIndices;
}

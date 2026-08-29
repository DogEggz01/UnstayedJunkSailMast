using System.Collections.Generic;

namespace UnstayedJunkSailMast;

internal sealed class UnstayedMastProfile
{
	internal BoatPart MastPart;

	internal BoatPartOption UnstayedOption;

	internal UnstayedMastMarker Marker;

	internal bool UsesFixedVanillaIndex;

	internal List<RestrictedPartSelection> RestrictedSelections;
}

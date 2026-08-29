using System.Collections.Generic;

namespace UnstayedJunkSailMast;

internal sealed class RestrictedPartSelection
{
	internal BoatPart Part;

	internal List<BoatPart> OwningMastParts;

	internal BoatPartOption EmptyOption;

	internal List<BoatPartOption> NonEmptyOptions;

	internal RestrictedPartKind Kind;
}

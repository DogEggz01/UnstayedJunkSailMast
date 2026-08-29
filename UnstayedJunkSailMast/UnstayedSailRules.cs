using UnityEngine;

namespace UnstayedJunkSailMast;

internal static class UnstayedSailRules
{
	internal static bool IsAllowed(GameObject sailObject)
	{
		Sail sail = ((sailObject != null) ? sailObject.GetComponent<Sail>() : null);
		if (sail == null)
		{
			return false;
		}
		string text = UnstayedNameRules.Normalize(sail.sailName);
		if (sail.category != SailCategory.junk && (sail.category != SailCategory.other || !(text == "fin sail")))
		{
			if (sail.category == SailCategory.square)
			{
				return UnstayedNameRules.IsNamedVariant(text, "junk square");
			}
			return false;
		}
		return true;
	}

	internal static bool UsesDiameterCompensation(Sail sail)
	{
		if (sail != null && !RectangleJunkSails.IsRectangle(sail.gameObject))
		{
			return IsAllowed(sail.gameObject);
		}
		return false;
	}
}

using System.Collections.Generic;
using HarmonyLib;

namespace UnstayedJunkSailMast;

[HarmonyPatch(typeof(ShipyardPartsInstaller), "UpdateOrder")]
internal static class ShipyardOrderNormalizationPatch
{
	private static void Prefix(BoatCustomParts ___currentParts, BoatPartsOrder ___currentOrder)
	{
		List<int> list = UnstayedSelectionRules.NormalizeOrder(___currentParts, ___currentOrder);
		if (ShipyardUI.instance == null || ___currentParts == null || ___currentOrder == null)
		{
			return;
		}
		for (int i = 0; i < list.Count; i++)
		{
			int num = list[i];
			if (num >= 0 && num < ___currentParts.availableParts.Count && num < ___currentOrder.orderedOptions.Length)
			{
				BoatPart boatPart = ___currentParts.availableParts[num];
				int num2 = ___currentOrder.orderedOptions[num];
				if (num2 >= 0 && num2 < boatPart.partOptions.Count)
				{
					ShipyardUI.instance.ChangePartsOptionText(num, boatPart.partOptions[num2].optionName);
				}
			}
		}
	}
}

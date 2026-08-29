using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace UnstayedJunkSailMast;

[HarmonyPatch(typeof(Shipyard), "ActivateDocuments")]
[HarmonyAfter(new string[] { "com.nandbrew.shipyardexpansion" })]
[HarmonyPriority(0)]
internal static class RectangleJunkShipyardPatch
{
	private static readonly HashSet<string> SaleSceneNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"island 9 E Dragon Cliffs",
		"island 27 Lagoon SwampShipyard"
	};

	private static void Prefix(Shipyard __instance, ref GameObject[] ___sailPrefabs)
	{
		PrefabsDirectory instance = PrefabsDirectory.instance;
		bool registered = RectangleJunkSails.EnsureRegistered(instance);
		bool availableForSale = registered && IsSaleLocation(__instance);
		if (availableForSale)
		{
			RectangleJunkSails.AddToShipyard(instance, ref ___sailPrefabs);
		}
		else
		{
			RectangleJunkSails.RemoveFromShipyard(ref ___sailPrefabs);
		}
		if (!registered && IsSaleLocation(__instance))
		{
			Plugin.LogSource?.LogError("Rectangle Junk sails are unavailable: indices 200/201 could not be registered.");
		}
	}

	internal static bool IsSaleLocation(Shipyard shipyard)
	{
		if (shipyard == null)
		{
			return false;
		}
		UnityEngine.SceneManagement.Scene scene = shipyard.gameObject.scene;
		return scene.IsValid() && IsSaleSceneName(scene.name);
	}

	internal static bool IsSaleSceneName(string sceneName)
	{
		return !string.IsNullOrEmpty(sceneName) && SaleSceneNames.Contains(sceneName);
	}
}

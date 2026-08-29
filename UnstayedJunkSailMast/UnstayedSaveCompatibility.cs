using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnstayedJunkSailMast;

internal static class UnstayedSaveCompatibility
{
	private const char RecordSeparator = ';';

	private const char FieldSeparator = '=';

	private const int IndexLayoutVersion = 2;

	internal static void PrepareLoad(SaveableBoatCustomization customization, SaveBoatCustomizationData data)
	{
		RectangleJunkSailIndexMigration.PrepareLoad(customization, data);
		RemapSavedOptions(customization, data);
		MigrateMastIndices(customization, data);
	}

	internal static void RecordActiveOptions(SaveableBoatCustomization customization, SaveBoatCustomizationData data)
	{
		RectangleJunkSailIndexMigration.MarkCurrent(customization);
		BoatCustomParts component = customization.GetComponent<BoatCustomParts>();
		if (data == null || !UnstayedBoatRegistry.TryGet(component, out var profile) || GameState.modData == null || (GetIndexLayoutVersion(profile.SceneIndex) < 2 && HasActiveMastRecords(profile.SceneIndex)))
		{
			return;
		}
		List<string> list = new List<string>();
		for (int i = 0; i < profile.Masts.Count; i++)
		{
			UnstayedMastProfile unstayedMastProfile = profile.Masts[i];
			int num = component.availableParts.IndexOf(unstayedMastProfile.MastPart);
			int num2 = unstayedMastProfile.MastPart.partOptions.IndexOf(unstayedMastProfile.UnstayedOption);
			if (num >= 0 && num2 >= 0 && num < data.partActiveOptions.Count && data.partActiveOptions[num] == num2)
			{
				UnstayedMastSourceIdentity unstayedMastSourceIdentity = ((unstayedMastProfile.Marker != null) ? unstayedMastProfile.Marker.Identity : null);
				if (unstayedMastSourceIdentity != null && !string.IsNullOrEmpty(unstayedMastSourceIdentity.StableId))
				{
					list.Add(num + "=" + Uri.EscapeDataString(unstayedMastSourceIdentity.StableId));
				}
			}
		}
		string key = GetKey(profile.SceneIndex);
		if (list.Count == 0)
		{
			GameState.modData.Remove(key);
		}
		else
		{
			GameState.modData[key] = string.Join(';'.ToString(), list.ToArray());
		}
		GameState.modData[GetIndexLayoutKey(profile.SceneIndex)] = 2.ToString();
	}

	private static void RemapSavedOptions(SaveableBoatCustomization customization, SaveBoatCustomizationData data)
	{
		BoatCustomParts component = customization.GetComponent<BoatCustomParts>();
		if (data == null || data.partActiveOptions == null || !UnstayedBoatRegistry.TryGet(component, out var profile) || GameState.modData == null || !GameState.modData.TryGetValue(GetKey(profile.SceneIndex), out var value) || string.IsNullOrEmpty(value))
		{
			return;
		}
		string[] array = value.Split(';');
		for (int i = 0; i < array.Length; i++)
		{
			int num = array[i].IndexOf('=');
			if (num <= 0 || !int.TryParse(array[i].Substring(0, num), out var result) || result < 0 || result >= data.partActiveOptions.Count)
			{
				continue;
			}
			string persistedSourceId;
			try
			{
				persistedSourceId = Uri.UnescapeDataString(array[i].Substring(num + 1));
			}
			catch (UriFormatException)
			{
				continue;
			}
			UnstayedMastProfile unstayedMastProfile = FindMatchingMastProfile(profile, component, persistedSourceId, result);
			if (unstayedMastProfile != null)
			{
				int num2 = component.availableParts.IndexOf(unstayedMastProfile.MastPart);
				int num3 = unstayedMastProfile.MastPart.partOptions.IndexOf(unstayedMastProfile.UnstayedOption);
				if (num2 >= 0 && num2 < data.partActiveOptions.Count && num3 >= 0)
				{
					data.partActiveOptions[num2] = num3;
				}
			}
		}
	}

	private static UnstayedMastProfile FindMatchingMastProfile(UnstayedBoatProfile profile, BoatCustomParts parts, string persistedSourceId, int savedPartIndex)
	{
		UnstayedMastProfile result = null;
		int num = 0;
		bool flag = false;
		for (int i = 0; i < profile.Masts.Count; i++)
		{
			UnstayedMastProfile unstayedMastProfile = profile.Masts[i];
			UnstayedMastSourceIdentity unstayedMastSourceIdentity = ((unstayedMastProfile.Marker != null) ? unstayedMastProfile.Marker.Identity : null);
			if (unstayedMastSourceIdentity == null)
			{
				continue;
			}
			int num2 = unstayedMastSourceIdentity.GetPersistedMatchScore(persistedSourceId);
			if (num2 > 0)
			{
				if (parts.availableParts.IndexOf(unstayedMastProfile.MastPart) == savedPartIndex)
				{
					num2 += 5;
				}
				if (num2 > num)
				{
					num = num2;
					result = unstayedMastProfile;
					flag = false;
				}
				else if (num2 == num)
				{
					flag = true;
				}
			}
		}
		if (!flag)
		{
			return result;
		}
		Plugin.LogSource?.LogWarning("Did not remap an ambiguous saved unstayed mast source identity: " + persistedSourceId + ".");
		return null;
	}

	private static void MigrateMastIndices(SaveableBoatCustomization customization, SaveBoatCustomizationData data)
	{
		BoatCustomParts component = customization.GetComponent<BoatCustomParts>();
		if (data == null || !UnstayedBoatRegistry.TryGet(component, out var profile) || GameState.modData == null)
		{
			return;
		}
		HashSet<int> hashSet = new HashSet<int>();
		if (profile.RetiredMastIndices != null)
		{
			for (int i = 0; i < profile.RetiredMastIndices.Count; i++)
			{
				hashSet.Add(profile.RetiredMastIndices[i]);
			}
		}
		if (GetIndexLayoutVersion(profile.SceneIndex) < 2 && HasActiveMastRecords(profile.SceneIndex))
		{
			HashSet<int> hashSet2 = FindForeignExtendedMastIndices(customization.transform);
			for (int j = 96; j < 128; j++)
			{
				if (!hashSet2.Contains(j))
				{
					hashSet.Add(j);
				}
			}
		}
		int num = RemoveSavedSails(data, hashSet);
		int num2 = RemoveShipyardExpansionSailRecords(profile.SceneIndex, hashSet);
		if (num > 0 || num2 > 0)
		{
			Plugin.LogSource?.LogWarning("Migrated unstayed mast indices for scene " + profile.SceneIndex + "; discarded " + num + " legacy sail(s) and " + num2 + " Shipyard Expansion sail setting record(s). No refund is issued by this migration.");
		}
		GameState.modData[GetIndexLayoutKey(profile.SceneIndex)] = 2.ToString();
	}

	private static int RemoveSavedSails(SaveBoatCustomizationData data, HashSet<int> discardedIndices)
	{
		if (data.sails == null || discardedIndices.Count == 0)
		{
			return 0;
		}
		int num = 0;
		for (int num2 = data.sails.Count - 1; num2 >= 0; num2--)
		{
			SaveSailData saveSailData = data.sails[num2];
			if (saveSailData != null && discardedIndices.Contains(saveSailData.mastIndex))
			{
				data.sails.RemoveAt(num2);
				num++;
			}
		}
		return num;
	}

	private static HashSet<int> FindForeignExtendedMastIndices(Transform boat)
	{
		HashSet<int> hashSet = new HashSet<int>();
		if (boat == null)
		{
			return hashSet;
		}
		Mast[] componentsInChildren = boat.GetComponentsInChildren<Mast>(includeInactive: true);
		foreach (Mast mast in componentsInChildren)
		{
			if (mast != null && UnstayedMastIndexRules.IsExtendedIndex(mast.orderIndex) && mast.GetComponent<UnstayedMastMarker>() == null)
			{
				hashSet.Add(mast.orderIndex);
			}
		}
		return hashSet;
	}

	private static int RemoveShipyardExpansionSailRecords(int sceneIndex, HashSet<int> discardedIndices)
	{
		if (discardedIndices.Count == 0 || GameState.modData == null)
		{
			return 0;
		}
		string key = "SEboatSails." + sceneIndex;
		if (!GameState.modData.TryGetValue(key, out var value) || string.IsNullOrEmpty(value))
		{
			return 0;
		}
		int num = value.LastIndexOf('|');
		string obj = ((num >= 0) ? value.Substring(0, num) : value);
		string text = ((num >= 0) ? value.Substring(num) : string.Empty);
		string[] array = obj.Split(new char[1] { ')' }, StringSplitOptions.RemoveEmptyEntries);
		List<string> list = new List<string>();
		int num2 = 0;
		for (int i = 0; i < array.Length; i++)
		{
			int num3 = array[i].IndexOf('(');
			if (num3 <= 0 || !int.TryParse(array[i].Substring(0, num3), out var result) || !discardedIndices.Contains(result))
			{
				list.Add(array[i] + ")");
			}
			else
			{
				num2++;
			}
		}
		if (num2 > 0)
		{
			GameState.modData[key] = string.Concat(string.Concat(list.ToArray()), text);
		}
		return num2;
	}

	private static bool HasActiveMastRecords(int sceneIndex)
	{
		if (GameState.modData.TryGetValue(GetKey(sceneIndex), out var value))
		{
			return !string.IsNullOrEmpty(value);
		}
		return false;
	}

	private static int GetIndexLayoutVersion(int sceneIndex)
	{
		if (!GameState.modData.TryGetValue(GetIndexLayoutKey(sceneIndex), out var value) || !int.TryParse(value, out var result))
		{
			return 0;
		}
		return result;
	}

	private static string GetKey(int sceneIndex)
	{
		return "dogeggz.unstayedjunksailmast." + sceneIndex + ".activeMasts";
	}

	private static string GetIndexLayoutKey(int sceneIndex)
	{
		return "dogeggz.unstayedjunksailmast." + sceneIndex + ".indexLayoutVersion";
	}
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnstayedJunkSailMast;

internal sealed class UnstayedMastIndexAllocator
{
	private const char RecordSeparator = ';';

	private const char FieldSeparator = '=';

	private readonly int sceneIndex;

	private readonly HashSet<int> occupiedIndices = new HashSet<int>();

	private readonly Dictionary<string, int> extendedMappings = new Dictionary<string, int>(StringComparer.Ordinal);

	private readonly Dictionary<int, string> extendedOwners = new Dictionary<int, string>();

	private readonly HashSet<int> retiredIndices = new HashSet<int>();

	private bool mappingsChanged;

	internal UnstayedMastIndexAllocator(Transform boat, int sceneIndex)
	{
		this.sceneIndex = sceneIndex;
		CollectOccupiedIndices(boat);
		LoadMappings();
		ValidateMappings();
	}

	internal List<int> GetRetiredIndices()
	{
		List<int> list = new List<int>(retiredIndices);
		list.Sort();
		return list;
	}

	internal bool TryClaim(BoatPartOption source, UnstayedMastSourceIdentity identity, out int mastIndex, out bool usesFixedVanillaIndex)
	{
		if (UnstayedMastIndexRules.TryGetFixedVanillaIndex(sceneIndex, source, out mastIndex))
		{
			usesFixedVanillaIndex = true;
			if (occupiedIndices.Contains(mastIndex) || extendedOwners.ContainsKey(mastIndex))
			{
				Plugin.LogSource?.LogError("Skipped " + source.optionName + ": fixed vanilla mast index " + mastIndex + " is already occupied.");
				return false;
			}
			occupiedIndices.Add(mastIndex);
			return true;
		}
		usesFixedVanillaIndex = false;
		mastIndex = -1;
		return TryClaimExtended(identity, out mastIndex);
	}

	internal bool TryClaimExtended(UnstayedMastSourceIdentity identity, out int mastIndex)
	{
		mastIndex = -1;
		if (identity == null || string.IsNullOrEmpty(identity.StableId))
		{
			return false;
		}
		string stableId = identity.StableId;
		if (extendedMappings.TryGetValue(stableId, out var value) && UnstayedMastIndexRules.IsExtendedIndex(value) && extendedOwners.TryGetValue(value, out var value2) && value2 == stableId)
		{
			if (occupiedIndices.Contains(value))
			{
				Plugin.LogSource?.LogError("Skipped duplicate Expansion mast source identity " + stableId + ".");
				return false;
			}
			occupiedIndices.Add(value);
			mastIndex = value;
			return true;
		}
		if (TryFindLegacyMapping(identity, out var legacySourceId))
		{
			value = extendedMappings[legacySourceId];
			if (occupiedIndices.Contains(value))
			{
				Plugin.LogSource?.LogError("Skipped duplicate Expansion mast source identity " + stableId + ".");
				return false;
			}
			extendedMappings.Remove(legacySourceId);
			extendedMappings[stableId] = value;
			extendedOwners[value] = stableId;
			occupiedIndices.Add(value);
			mappingsChanged = true;
			mastIndex = value;
			Plugin.LogSource?.LogInfo("Migrated an Expansion mast source key to v2 at index " + value + ".");
			return true;
		}
		for (int i = 96; i < 128; i++)
		{
			if (!occupiedIndices.Contains(i) && !extendedOwners.ContainsKey(i))
			{
				extendedMappings[stableId] = i;
				extendedOwners[i] = stableId;
				occupiedIndices.Add(i);
				mappingsChanged = true;
				mastIndex = i;
				return true;
			}
		}
		Plugin.LogSource?.LogError("No extended mast index remains in the 96-127 range for " + stableId + ".");
		return false;
	}

	private bool TryFindLegacyMapping(UnstayedMastSourceIdentity identity, out string legacySourceId)
	{
		legacySourceId = null;
		int num = 0;
		bool flag = false;
		foreach (KeyValuePair<string, int> extendedMapping in extendedMappings)
		{
			int persistedMatchScore = identity.GetPersistedMatchScore(extendedMapping.Key);
			if (persistedMatchScore > 0 && !(extendedMapping.Key == identity.StableId))
			{
				if (persistedMatchScore > num)
				{
					num = persistedMatchScore;
					legacySourceId = extendedMapping.Key;
					flag = false;
				}
				else if (persistedMatchScore == num)
				{
					flag = true;
				}
			}
		}
		if (!flag)
		{
			return legacySourceId != null;
		}
		Plugin.LogSource?.LogWarning("Did not migrate an ambiguous legacy Expansion mast source key for " + identity.StableId + ".");
		legacySourceId = null;
		return false;
	}

	internal void Commit()
	{
		if (mappingsChanged)
		{
			if (GameState.modData == null)
			{
				GameState.modData = new Dictionary<string, string>();
			}
			List<string> list = new List<string>(extendedMappings.Keys);
			list.Sort(StringComparer.Ordinal);
			List<string> list2 = new List<string>();
			for (int i = 0; i < list.Count; i++)
			{
				string text = list[i];
				list2.Add(Uri.EscapeDataString(text) + "=" + extendedMappings[text]);
			}
			GameState.modData[UnstayedMastIndexRules.GetExtendedMappingKey(sceneIndex)] = string.Join(';'.ToString(), list2.ToArray());
		}
	}

	private void CollectOccupiedIndices(Transform boat)
	{
		if (boat == null)
		{
			return;
		}
		Mast[] componentsInChildren = boat.GetComponentsInChildren<Mast>(includeInactive: true);
		foreach (Mast mast in componentsInChildren)
		{
			if (mast != null && mast.GetComponent<UnstayedMastMarker>() == null)
			{
				occupiedIndices.Add(mast.orderIndex);
			}
		}
	}

	private void LoadMappings()
	{
		if (GameState.modData == null || !GameState.modData.TryGetValue(UnstayedMastIndexRules.GetExtendedMappingKey(sceneIndex), out var value) || string.IsNullOrEmpty(value))
		{
			return;
		}
		string[] array = value.Split(';');
		for (int i = 0; i < array.Length; i++)
		{
			int num = array[i].LastIndexOf('=');
			if (num <= 0 || !int.TryParse(array[i].Substring(num + 1), out var result))
			{
				mappingsChanged = true;
				continue;
			}
			string text;
			try
			{
				text = Uri.UnescapeDataString(array[i].Substring(0, num));
			}
			catch (UriFormatException)
			{
				mappingsChanged = true;
				continue;
			}
			if (string.IsNullOrEmpty(text))
			{
				mappingsChanged = true;
			}
			else
			{
				extendedMappings[text] = result;
			}
		}
	}

	private void ValidateMappings()
	{
		List<string> list = new List<string>(extendedMappings.Keys);
		list.Sort(StringComparer.Ordinal);
		for (int i = 0; i < list.Count; i++)
		{
			string text = list[i];
			int num = extendedMappings[text];
			if (!UnstayedMastIndexRules.IsExtendedIndex(num) || occupiedIndices.Contains(num) || extendedOwners.ContainsKey(num))
			{
				if (UnstayedMastIndexRules.IsExtendedIndex(num))
				{
					retiredIndices.Add(num);
				}
				extendedMappings.Remove(text);
				mappingsChanged = true;
				Plugin.LogSource?.LogWarning("Released conflicting extended mast index " + num + " for source " + text + ".");
			}
			else
			{
				extendedOwners[num] = text;
			}
		}
	}
}

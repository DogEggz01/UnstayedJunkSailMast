using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnstayedJunkSailMast;

internal sealed class UnstayedMastSourceIdentity
{
	private const string StablePrefix = "v2|";

	internal string StableId { get; }

	internal string LegacyId { get; }

	private UnstayedMastSourceIdentity(string stableId, string legacyId)
	{
		StableId = stableId;
		LegacyId = legacyId;
	}

	internal static UnstayedMastSourceIdentity Create(BoatPartOption source, BoatPart owningPart, Transform boat, int sceneIndex)
	{
		if (source == null)
		{
			throw new ArgumentNullException("source");
		}
		Mast component = source.GetComponent<Mast>();
		string legacyId = UnstayedNameRules.Normalize(source.optionName) + "|" + UnstayedNameRules.Normalize(source.name) + "|" + ((component != null) ? component.orderIndex.ToString() : "-");
		if (UnstayedMastIndexRules.TryGetFixedVanillaIndex(sceneIndex, source, out var _))
		{
			return new UnstayedMastSourceIdentity("v2|boat:" + sceneIndex + "|vanilla:" + NormalizeSegment(source.name), legacyId);
		}
		string groupId = GetGroupId(owningPart, source, boat, sceneIndex);
		string normalizedRelativePath = GetNormalizedRelativePath(source.transform, boat);
		return new UnstayedMastSourceIdentity("v2|boat:" + sceneIndex + "|group:" + groupId + "|path:" + normalizedRelativePath, legacyId);
	}

	internal int GetPersistedMatchScore(string persistedId)
	{
		if (string.Equals(persistedId, StableId, StringComparison.Ordinal))
		{
			return 100;
		}
		if (string.Equals(persistedId, LegacyId, StringComparison.Ordinal))
		{
			return 90;
		}
		if (string.IsNullOrEmpty(persistedId) || persistedId.StartsWith("v2|", StringComparison.Ordinal))
		{
			return 0;
		}
		string[] array = persistedId.Split('|');
		string[] array2 = LegacyId.Split('|');
		if (array.Length != 3 || array2.Length != 3)
		{
			return 0;
		}
		bool num = string.Equals(array[0], array2[0], StringComparison.Ordinal);
		bool flag = !string.IsNullOrEmpty(array2[1]) && string.Equals(array[1], array2[1], StringComparison.Ordinal);
		if (num & flag)
		{
			return 80;
		}
		if (!flag)
		{
			return 0;
		}
		return 70;
	}

	private static string GetGroupId(BoatPart owningPart, BoatPartOption source, Transform boat, int sceneIndex)
	{
		if (owningPart != null && owningPart.partOptions != null)
		{
			List<string> list = new List<string>();
			for (int i = 0; i < owningPart.partOptions.Count; i++)
			{
				BoatPartOption boatPartOption = owningPart.partOptions[i];
				if (boatPartOption != null && UnstayedMastIndexRules.TryGetFixedVanillaIndex(sceneIndex, boatPartOption, out var _))
				{
					string item = "vanilla:" + NormalizeSegment(boatPartOption.name);
					if (!list.Contains(item))
					{
						list.Add(item);
					}
				}
			}
			if (list.Count > 0)
			{
				list.Sort(StringComparer.Ordinal);
				return list[0];
			}
		}
		Transform parent = source.transform.parent;
		return "path:" + GetNormalizedRelativePath(parent, boat);
	}

	private static string GetNormalizedRelativePath(Transform transform, Transform root)
	{
		if (transform == null)
		{
			return "missing";
		}
		List<string> list = new List<string>();
		Transform transform2 = transform;
		while (transform2 != null && transform2 != root)
		{
			list.Add(NormalizeSegment(transform2.name));
			transform2 = transform2.parent;
		}
		if (transform2 != root)
		{
			return NormalizeSegment(transform.name);
		}
		list.Reverse();
		if (list.Count <= 0)
		{
			return "root";
		}
		return string.Join("/", list.ToArray());
	}

	private static string NormalizeSegment(string value)
	{
		string text = UnstayedNameRules.Normalize(value);
		if (!string.IsNullOrEmpty(text))
		{
			return text;
		}
		return "unnamed";
	}
}

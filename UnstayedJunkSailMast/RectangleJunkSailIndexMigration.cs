using System.Collections.Generic;

namespace UnstayedJunkSailMast;

internal static class RectangleJunkSailIndexMigration
{
	private const int CurrentLayoutVersion = 1;

	internal static void PrepareLoad(SaveableBoatCustomization customization, SaveBoatCustomizationData data)
	{
		if (data == null || data.sails == null)
		{
			return;
		}
		bool flag = TryGetSceneIndex(customization, out var sceneIndex);
		if (flag && IsCurrent(sceneIndex))
		{
			return;
		}
		int num;
		int num2;
		if (flag)
		{
			num = (HasLegacyModFootprint(sceneIndex) ? 1 : 0);
			if (num != 0)
			{
				num2 = 1;
				goto IL_003d;
			}
		}
		else
		{
			num = 0;
		}
		num2 = (RectangleJunkSails.CanMigrateLegacyIndex(131) ? 1 : 0);
		goto IL_003d;
		IL_003d:
		bool migrateNarrow = (byte)num2 != 0;
		bool migrateWide = num != 0 || RectangleJunkSails.CanMigrateLegacyIndex(132);
		int num3 = RemapLegacyIndices(data.sails, migrateNarrow, migrateWide);
		if (flag)
		{
			MarkCurrent(sceneIndex);
		}
		if (num3 > 0)
		{
			Plugin.LogSource?.LogInfo("Migrated " + num3 + " Rectangle Junk sail index record(s) from 131/132 to 200/201" + (flag ? (" for boat scene " + sceneIndex) : string.Empty) + ".");
		}
	}

	internal static void MarkCurrent(SaveableBoatCustomization customization)
	{
		if (TryGetSceneIndex(customization, out var sceneIndex))
		{
			MarkCurrent(sceneIndex);
		}
	}

	internal static int RemapLegacyIndices(IList<SaveSailData> sails, bool migrateNarrow, bool migrateWide)
	{
		if (sails == null)
		{
			return 0;
		}
		int num = 0;
		for (int i = 0; i < sails.Count; i++)
		{
			SaveSailData saveSailData = sails[i];
			if (saveSailData != null)
			{
				if (migrateNarrow && saveSailData.prefabIndex == 131)
				{
					saveSailData.prefabIndex = 200;
					num++;
				}
				else if (migrateWide && saveSailData.prefabIndex == 132)
				{
					saveSailData.prefabIndex = 201;
					num++;
				}
			}
		}
		return num;
	}

	private static bool TryGetSceneIndex(SaveableBoatCustomization customization, out int sceneIndex)
	{
		SaveableObject saveableObject = ((customization != null) ? customization.GetComponent<SaveableObject>() : null);
		if (saveableObject != null)
		{
			sceneIndex = saveableObject.sceneIndex;
			return true;
		}
		sceneIndex = -1;
		return false;
	}

	private static bool IsCurrent(int sceneIndex)
	{
		if (GameState.modData == null)
		{
			return false;
		}
		if (GameState.modData.TryGetValue(GetLayoutKey(sceneIndex), out var value) && int.TryParse(value, out var result))
		{
			return result >= 1;
		}
		return false;
	}

	private static bool HasLegacyModFootprint(int sceneIndex)
	{
		if (GameState.modData == null)
		{
			return false;
		}
		string text = "dogeggz.unstayedjunksailmast." + sceneIndex;
		if (!GameState.modData.ContainsKey(text + ".activeMasts"))
		{
			return GameState.modData.ContainsKey(text + ".indexLayoutVersion");
		}
		return true;
	}

	private static void MarkCurrent(int sceneIndex)
	{
		if (GameState.modData != null)
		{
			GameState.modData[GetLayoutKey(sceneIndex)] = 1.ToString();
		}
	}

	private static string GetLayoutKey(int sceneIndex)
	{
		return "dogeggz.unstayedjunksailmast." + sceneIndex + ".rectangleSailIndexLayoutVersion";
	}
}

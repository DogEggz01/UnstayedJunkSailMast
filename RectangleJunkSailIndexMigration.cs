using System.Collections.Generic;

namespace UnstayedJunkSailMast
{
    internal static class RectangleJunkSailIndexMigration
    {
        private const int CurrentLayoutVersion = 1;

        internal static void PrepareLoad(
            SaveableBoatCustomization customization,
            SaveBoatCustomizationData data)
        {
            if (data == null || data.sails == null)
            {
                return;
            }

            int sceneIndex;
            bool hasSceneIndex = TryGetSceneIndex(
                customization,
                out sceneIndex);
            if (hasSceneIndex && IsCurrent(sceneIndex))
            {
                return;
            }

            bool hasLegacyModFootprint = hasSceneIndex &&
                                         HasLegacyModFootprint(sceneIndex);
            bool migrateNarrow = hasLegacyModFootprint ||
                RectangleJunkSails.CanMigrateLegacyIndex(
                    RectangleJunkSails.LegacyNarrowIndex);
            bool migrateWide = hasLegacyModFootprint ||
                RectangleJunkSails.CanMigrateLegacyIndex(
                    RectangleJunkSails.LegacyWideIndex);
            int remapped = RemapLegacyIndices(
                data.sails,
                migrateNarrow,
                migrateWide);

            if (hasSceneIndex)
            {
                MarkCurrent(sceneIndex);
            }

            if (remapped > 0)
            {
                Plugin.LogSource?.LogInfo(
                    "Migrated " + remapped +
                    " Rectangle Junk sail index record(s) from 131/132 " +
                    "to 200/201" +
                    (hasSceneIndex
                        ? " for boat scene " + sceneIndex
                        : string.Empty) +
                    ".");
            }
        }

        internal static void MarkCurrent(
            SaveableBoatCustomization customization)
        {
            int sceneIndex;
            if (TryGetSceneIndex(customization, out sceneIndex))
            {
                MarkCurrent(sceneIndex);
            }
        }

        internal static int RemapLegacyIndices(
            IList<SaveSailData> sails,
            bool migrateNarrow,
            bool migrateWide)
        {
            if (sails == null)
            {
                return 0;
            }

            int remapped = 0;
            for (int i = 0; i < sails.Count; i++)
            {
                SaveSailData sail = sails[i];
                if (sail == null)
                {
                    continue;
                }

                if (migrateNarrow &&
                    sail.prefabIndex ==
                    RectangleJunkSails.LegacyNarrowIndex)
                {
                    sail.prefabIndex = RectangleJunkSails.NarrowIndex;
                    remapped++;
                }
                else if (migrateWide &&
                         sail.prefabIndex ==
                         RectangleJunkSails.LegacyWideIndex)
                {
                    sail.prefabIndex = RectangleJunkSails.WideIndex;
                    remapped++;
                }
            }

            return remapped;
        }

        private static bool TryGetSceneIndex(
            SaveableBoatCustomization customization,
            out int sceneIndex)
        {
            SaveableObject saveable = customization != null
                ? customization.GetComponent<SaveableObject>()
                : null;
            if (saveable != null)
            {
                sceneIndex = saveable.sceneIndex;
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

            string encoded;
            int version;
            return GameState.modData.TryGetValue(
                       GetLayoutKey(sceneIndex),
                       out encoded) &&
                   int.TryParse(encoded, out version) &&
                   version >= CurrentLayoutVersion;
        }

        private static bool HasLegacyModFootprint(int sceneIndex)
        {
            if (GameState.modData == null)
            {
                return false;
            }

            string prefix = Plugin.PluginGuid + "." + sceneIndex;
            return GameState.modData.ContainsKey(
                       prefix + ".activeMasts") ||
                   GameState.modData.ContainsKey(
                       prefix + ".indexLayoutVersion");
        }

        private static void MarkCurrent(int sceneIndex)
        {
            if (GameState.modData != null)
            {
                GameState.modData[GetLayoutKey(sceneIndex)] =
                    CurrentLayoutVersion.ToString();
            }
        }

        private static string GetLayoutKey(int sceneIndex)
        {
            return Plugin.PluginGuid + "." + sceneIndex +
                   ".rectangleSailIndexLayoutVersion";
        }
    }
}

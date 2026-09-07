using System;
using System.IO;
using UnityEngine;

namespace UnstayedJunkSailMast
{
    internal static class RectangleJunkAssets
    {
        private const string BundleFileName =
            "unstayedjunksailmast_assets";
        private const string NarrowPrefabPath =
            "assets/ujsmfinal/narrow_rectangle_junk.prefab";
        private const string WidePrefabPath =
            "assets/ujsmfinal/wide_rectangle_junk.prefab";

        internal static GameObject LoadNarrowPrefab()
        {
            if (narrowPrefab != null)
            {
                return narrowPrefab;
            }

            if (!EnsureBundleLoaded())
            {
                return null;
            }

            narrowPrefab = bundle.LoadAsset<GameObject>(NarrowPrefabPath);
            if (narrowPrefab == null)
            {
                Plugin.LogSource?.LogError(
                    "Could not find the final narrow rectangle Junk " +
                    "prefab in the Rectangle Junk AssetBundle.");
            }

            return narrowPrefab;
        }

        internal static GameObject LoadWidePrefab()
        {
            if (widePrefab != null)
            {
                return widePrefab;
            }

            if (!EnsureBundleLoaded())
            {
                return null;
            }

            widePrefab = bundle.LoadAsset<GameObject>(WidePrefabPath);
            if (widePrefab == null)
            {
                Plugin.LogSource?.LogError(
                    "Could not find the final wide rectangle Junk " +
                    "prefab in the Rectangle Junk AssetBundle.");
            }

            return widePrefab;
        }

        private static bool EnsureBundleLoaded()
        {
            if (bundle != null)
            {
                return true;
            }

            string bundlePath = Path.Combine(
                Plugin.PluginDirectory,
                "assets",
                BundleFileName);
            if (!File.Exists(bundlePath))
            {
                Plugin.LogSource?.LogError(
                    "Could not add Rectangle Junk sails: AssetBundle " +
                    "not found at " + bundlePath + ".");
                return false;
            }

            try
            {
                bundle = AssetBundle.LoadFromFile(bundlePath);
                if (bundle != null)
                {
                    return true;
                }

                Plugin.LogSource?.LogError(
                    "Could not load Rectangle Junk AssetBundle at " +
                    bundlePath + ".");
            }
            catch (Exception exception)
            {
                Plugin.LogSource?.LogError(
                    "Could not load final Rectangle Junk assets: " +
                    exception);
            }

            bundle = null;
            return false;
        }

        internal static void Reset()
        {
            narrowPrefab = null;
            widePrefab = null;
            bundle?.Unload(false);
            bundle = null;
        }

        private static AssetBundle bundle;
        private static GameObject narrowPrefab;
        private static GameObject widePrefab;
    }
}

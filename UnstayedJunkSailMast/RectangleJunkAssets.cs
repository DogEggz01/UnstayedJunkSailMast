using System;
using System.IO;
using UnityEngine;

namespace UnstayedJunkSailMast;

internal static class RectangleJunkAssets
{
	private const string BundleFileName = "unstayedjunksailmast_assets";

	private const string NarrowPrefabPath = "assets/ujsmfinal/narrow_rectangle_junk.prefab";

	private const string WidePrefabPath = "assets/ujsmfinal/wide_rectangle_junk.prefab";

	private static AssetBundle bundle;

	private static GameObject narrowPrefab;

	private static GameObject widePrefab;

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
		narrowPrefab = bundle.LoadAsset<GameObject>("assets/ujsmfinal/narrow_rectangle_junk.prefab");
		if (narrowPrefab == null)
		{
			Plugin.LogSource?.LogError("Could not find the final narrow rectangle Junk prefab in the Rectangle Junk AssetBundle.");
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
		widePrefab = bundle.LoadAsset<GameObject>("assets/ujsmfinal/wide_rectangle_junk.prefab");
		if (widePrefab == null)
		{
			Plugin.LogSource?.LogError("Could not find the final wide rectangle Junk prefab in the Rectangle Junk AssetBundle.");
		}
		return widePrefab;
	}

	private static bool EnsureBundleLoaded()
	{
		if (bundle != null)
		{
			return true;
		}
		string text = Path.Combine(Plugin.PluginDirectory, "assets", "unstayedjunksailmast_assets");
		if (!File.Exists(text))
		{
			Plugin.LogSource?.LogError("Could not add Rectangle Junk sails: AssetBundle not found at " + text + ".");
			return false;
		}
		try
		{
			bundle = AssetBundle.LoadFromFile(text);
			if (bundle != null)
			{
				return true;
			}
			Plugin.LogSource?.LogError("Could not load Rectangle Junk AssetBundle at " + text + ".");
		}
		catch (Exception ex)
		{
			Plugin.LogSource?.LogError("Could not load final Rectangle Junk assets: " + ex);
		}
		bundle = null;
		return false;
	}

	internal static void Reset()
	{
		narrowPrefab = null;
		widePrefab = null;
		bundle?.Unload(unloadAllLoadedObjects: false);
		bundle = null;
	}
}

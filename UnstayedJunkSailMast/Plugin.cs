using System.IO;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace UnstayedJunkSailMast;

[BepInPlugin("dogeggz.unstayedjunksailmast", "Unstayed Junk Sail Mast", "1.2.6")]
[BepInDependency("com.nandbrew.shipyardexpansion", BepInDependency.DependencyFlags.HardDependency)]
public sealed class Plugin : BaseUnityPlugin
{
	public const string PluginGuid = "dogeggz.unstayedjunksailmast";

	public const string PluginName = "Unstayed Junk Sail Mast";

	public const string PluginVersion = "1.2.6";

	public const string ShipyardExpansionGuid = "com.nandbrew.shipyardexpansion";

	private Harmony harmony;

	internal static ManualLogSource LogSource { get; private set; }

	internal static string PluginDirectory { get; private set; }

	private void Awake()
	{
		LogSource = base.Logger;
		PluginDirectory = Path.GetDirectoryName(base.Info.Location) ?? string.Empty;
		harmony = new Harmony("dogeggz.unstayedjunksailmast");
		harmony.PatchAll(typeof(Plugin).Assembly);
		base.Logger.LogInfo("Unstayed Junk Sail Mast 1.2.6 loaded; required Shipyard Expansion dependency is active.");
	}

	private void OnDestroy()
	{
		harmony?.UnpatchSelf();
		RectangleJunkSails.Reset();
		RectangleJunkAssets.Reset();
		UnstayedBoatRegistry.Clear();
		LogSource = null;
		PluginDirectory = null;
	}
}

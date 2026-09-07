using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.IO;

namespace UnstayedJunkSailMast
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(
        ShipyardExpansionGuid,
        BepInDependency.DependencyFlags.HardDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "dogeggz.unstayedjunksailmast";
        public const string PluginName = "Unstayed Junk Sail Mast";
        public const string PluginVersion = "1.2.7";
        public const string ShipyardExpansionGuid =
            "com.nandbrew.shipyardexpansion";

        internal static ManualLogSource LogSource { get; private set; }
        internal static string PluginDirectory { get; private set; }

        private Harmony harmony;

        private void Awake()
        {
            LogSource = Logger;
            PluginDirectory = Path.GetDirectoryName(Info.Location) ??
                              string.Empty;
            harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(Plugin).Assembly);
            Logger.LogInfo(
                PluginName + " " + PluginVersion + " loaded; " +
                "required Shipyard Expansion dependency is active.");
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
}

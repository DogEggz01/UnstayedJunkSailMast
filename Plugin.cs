using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;

namespace UnstayedJunkSailMast
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(
        ShipyardExpansionGuid,
        BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "dogeggz.unstayedjunksailmast";
        public const string PluginName = "Unstayed Junk Sail Mast";
        public const string PluginVersion = "1.1.0";
        public const string ShipyardExpansionGuid =
            "com.nandbrew.shipyardexpansion";

        internal static ManualLogSource LogSource { get; private set; }
        internal static bool ShipyardExpansionLoaded { get; private set; }

        private Harmony harmony;

        private void Awake()
        {
            LogSource = Logger;
            ShipyardExpansionLoaded =
                Chainloader.PluginInfos.ContainsKey(ShipyardExpansionGuid);
            harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(Plugin).Assembly);
            Logger.LogInfo(
                PluginName + " " + PluginVersion + " loaded; " +
                "Shipyard Expansion " +
                (ShipyardExpansionLoaded ? "detected." : "not detected."));
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
            UnstayedBoatRegistry.Clear();
            ShipyardExpansionLoaded = false;
            LogSource = null;
        }
    }
}

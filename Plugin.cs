using BepInEx;
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
        public const string PluginVersion = "1.0.4";
        public const string ShipyardExpansionGuid =
            "com.nandbrew.shipyardexpansion";

        internal static ManualLogSource LogSource { get; private set; }

        private Harmony harmony;

        private void Awake()
        {
            LogSource = Logger;
            harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(Plugin).Assembly);
            Logger.LogInfo(PluginName + " " + PluginVersion + " loaded.");
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
            UnstayedBoatRegistry.Clear();
            LogSource = null;
        }
    }
}

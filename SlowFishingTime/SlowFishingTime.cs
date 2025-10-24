using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using HarmonyLib;

namespace SlowFishingTime;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class SlowFishingTime : BasePlugin
{
    internal static new ManualLogSource Log;
    private static ConfigEntry<float> _fishingTimeScale;
    private static float _originalTimeScale;

    public override void Load()
    {
        _fishingTimeScale = Config.Bind("General", "FishingTimeScale", 30f, "Set the time scale while fishing. Game default is 60.");
        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        Harmony.CreateAndPatchAll(typeof(FishingTimePatch));
    }

    private class FishingTimePatch
    {
        /**
         * This is when the user uses the fishing rod
         */
        [HarmonyPatch(typeof(PlayerActionFishingRod), "Action")]
        [HarmonyPostfix]
        private static void OnFishingAction(PlayerActionFishingRod __instance)
        {
            // This ensure that it's compatible with custom timescale mods(Like my Time Freeze Inside)
            _originalTimeScale = DateManager.Instance.TimeScale;
            DateManager.Instance.TimeScale = _fishingTimeScale.Value;
        }
        
        /*
         * This is when the fishing action ends
         */
        [HarmonyPatch(typeof(PlayerActionFishingRod), "EndAction")]
        [HarmonyPostfix]
        private static void OnFishingActionEnd(PlayerActionFishingRod __instance)
        {
            DateManager.Instance.TimeScale = _originalTimeScale;
        }
    }
}
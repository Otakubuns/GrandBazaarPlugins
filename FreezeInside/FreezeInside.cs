using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using BokuMono.Data;
using BokuMono.Utility;
using HarmonyLib;

namespace FreezeInside;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class FreezeInside : BasePlugin
{
    internal static new ManualLogSource Log;
    private static ConfigEntry<float> _timeScaleValue;
    private static ConfigEntry<float> _timeInsideValue;

    public override void Load()
    {
        _timeScaleValue = Config.Bind("General", "Time Scale", 60.0f, "Set the time scale of the game(default is a second per in-game minute). Game default is 60.0.");
        _timeInsideValue = Config.Bind("General", "Inside Time Scale", 0f, "Sets the time scale while inside buildings. 0 to freeze time, 60 to keep normal speed.");
        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        Harmony.CreateAndPatchAll(typeof(TimeFreezePatch));
    }

    private class TimeFreezePatch
    {
        /*
         * Patches when a field is changed and changed depending on the field
         */
        [HarmonyPatch(typeof(FieldManager))]
        [HarmonyPatch("ChangeField", new Type[] {typeof(FieldMasterId), typeof(string), typeof(bool), typeof(Il2CppSystem.Action), typeof(Il2CppSystem.Action<uint>), typeof(bool) })]
        [HarmonyPostfix]
        private static void OnChangeField(FieldManager __instance, FieldMasterId fieldId)
        {
            var isIndoors = MasterDataManager.Instance.FieldMaster.GetData(fieldId).IsInDoor;
            UpdateTimeScale(isIndoors);
        }
        
        /**
         * Patches when a save is loaded to set the timescale correctly(since onStartGame the id is not setup yet)
         */
        [HarmonyPatch(typeof(FieldManager), "LoadFieldCacheFromSaveData")]
        [HarmonyPostfix]
        private static void OnLoadFieldCacheFromSaveData(FieldManager __instance)
        {
            try
            {
                var isIndoors = MasterDataManager.Instance.FieldMaster.GetData(__instance.CurrentFieldId).IsInDoor;
                UpdateTimeScale(isIndoors);
            }
            catch (Exception e)
            {
                // ignored
            }
        }
        
        /*
         * Helper function to update timescale(so im not doing too many if else's)
         */
        private static void UpdateTimeScale(bool isIndoors)
        {
            SingletonMonoBehaviour<DateManager>.Instance.TimeScale = isIndoors ? _timeInsideValue.Value : _timeScaleValue.Value;
        }
    }
}
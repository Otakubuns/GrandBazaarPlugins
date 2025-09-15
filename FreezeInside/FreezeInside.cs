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
    private static ConfigEntry<bool> _freezeInside;

    public override void Load()
    {
        _timeScaleValue = Config.Bind("General", "Time Scale", 60.0f, "Set the time scale of the game(default is a second per in-game minute). Game default is 60.0f.");
        _freezeInside = Config.Bind("General", "Freeze Inside", true, "Set to true to freeze time when indoors.");
        // Plugin startup logic
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
            SingletonMonoBehaviour<DateManager>.Instance.TimeScale =
                MasterDataManager.Instance.FieldMaster.GetData(fieldId).IsInDoor ? 0 : 60;
        }
        
        [HarmonyPatch(typeof(UITitleMainPage), "PlayTitleLogoAnimation")]
        [HarmonyPostfix]
        private static void OnPlayTitleLogoAnimation()
        {
            MasterDataManager.Instance.DropMaster.
            
            foreach (var itemMasterData in MasterDataManager.Instance.ItemMasterData)
            {
                if(itemMasterData.StackSize != 1) itemMasterData.StackSize = 999;
            }
        }

        [HarmonyPatch(typeof(DateManager), "OnStartGame")]
        [HarmonyPostfix]
        private static void OnStartGame(DateManager __instance)
        {
            try
            {
                if (_freezeInside.Value)
                {
                    SingletonMonoBehaviour<DateManager>.Instance.TimeScale =
                        MasterDataManager.Instance.FieldMaster
                            .GetData(SingletonMonoBehaviour<FieldManager>.Instance.CurrentFieldId).IsInDoor
                            ? 0
                            : _timeScaleValue.Value;

                }
                else
                {
                    SingletonMonoBehaviour<DateManager>.Instance.TimeScale = _timeScaleValue.Value;
                }
            }
            catch (Exception e)
            {
                // ignored
            }
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
                if (_freezeInside.Value)
                {
                    SingletonMonoBehaviour<DateManager>.Instance.TimeScale =
                        MasterDataManager.Instance.FieldMaster.GetData(__instance.CurrentFieldId).IsInDoor ? 0 : _timeScaleValue.Value;
                }
                else
                {
                    SingletonMonoBehaviour<DateManager>.Instance.TimeScale = _timeScaleValue.Value;
                }

            }
            catch (Exception e)
            {
                // ignored
            }
        }
    }
}
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using HarmonyLib;
using Il2CppSystem.Collections.Generic;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace SaveAnywhere;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class SaveAnywhere : BasePlugin
{
    static ManualLogSource _log;
    
    public override void Load()
    {
        _log = Log;
        // Plugin startup logic
        _log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        Harmony.CreateAndPatchAll(typeof(SavePatch));
    }
    
    //TODO: add maybe a json for location and load for the save so they can on save be there
    
    [HarmonyPatch]
    private class SavePatch
    {
        /**
         * This patch overrides the save button logic and opens the save/load menu instead of an error if not in the farmhouse
         */
        [HarmonyPatch(typeof(UIPausePage), "OnDecide")]
        [HarmonyPrefix]
        private static bool OnSaveButtonPressed(UIPausePage __instance, int __0, uint __1,
            List<StateErrorTextId> __2)
        {
            //if the save button(1130 text id) is pressed do custom logic, if not just return the original method
            //or index depdending on the circumstance
            //if(__0 != 0) return true;
            if (__1 != 1130) return true;
            __2?.Clear();
            var saveManager = SaveLoadManager.Instance;
            saveManager.PageType = SaveLoadManager.UIPageType.Save;
            var uiAccessor = UIAccessor.Instance;
            uiAccessor.RequestOpenMenu(UILoadKey.SaveLoad);
            return false;
        }

        /**
         * This patch removes the error id in dialogdata so the save button is always enabled
         */
        [HarmonyPatch(typeof(UIPausePage), "DialogChoices")]
        [HarmonyPostfix]
        private static void ChangeSaveButtonState(UIPausePage __instance, List<ChoiceData> __result)
        {
            foreach (var choice in __result)
            {
                choice.errorIds?.Clear();
            }
        }
    }
}
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using BokuMono.SaveData;
using HarmonyLib;
using Il2CppSystem.Collections.Generic;
using UnityEngine;

namespace SaveAnywhere;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class SaveAnywhere : BasePlugin
{
    public static ManualLogSource LOG;

    public override void Load()
    {
        LOG = Log;
        // Plugin startup logic
        LOG.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        Harmony.CreateAndPatchAll(typeof(SavePatch));
    }

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
            //or index depending on the circumstance
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
         * This patch removes the error id in DialogData so the save button is always enabled
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

        /*
         * This patch loads the player position from json when the spawn point is set
         */
        [HarmonyPatch(typeof(GameController), "SetSpawnPoint")]
        [HarmonyPrefix]
        private static void SetSpawnPointPatch(GameController __instance, ref FieldSpawnPoint fieldSpawnPoint)
        {
            if (fieldSpawnPoint == null) return;
            var slot = SaveDataManager.Instance.LoadSlot;
            var fieldId = FieldManager.Instance.CurrentFieldId;
            var saveFieldId = JsonUtility.LoadDataLocation(slot);
            //This is just to ensure that if SetSpawnPoint is called its only when it's in the same field as the save
            // it seems its only called on load save and not on scenes but this is just to be safe
            if (fieldId != saveFieldId) return;

            var position = JsonUtility.LoadDataJson(slot);
            if (position == new Vector3(0, 0, 0)) return;
            fieldSpawnPoint.transform.position = position;
        }
        
        /*
         * This patch saves the player position to json when a save is made, so it can be loaded on load
         */
        [HarmonyPatch(typeof(SaveLoadManager), "Save")]
        [HarmonyPostfix]
        private static void SaveLocation(SaveLoadManager __instance, int iSlot)
        {
            var player = GameController.Instance.playerCharacter;
            if (player == null) return;
            var position = player.transform.position;
            var locationId = FieldManager.Instance.CurrentFieldId;
            JsonUtility.SaveDataJson(position, iSlot, locationId);
        }
    }
}
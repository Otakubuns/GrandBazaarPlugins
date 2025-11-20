using System.Text.Json;
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
        // Track if a save has just been loaded & the current loaded save slot
        private static bool _isLoadedSave;
        private static int _currentSlot = -2;
        
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
         * This patch sets the players position after loading a save based on the saved json data
         * It checks if a save was loaded and the slot id first and only fires then
         */
        [HarmonyPatch(typeof(PlayerCharacter), "SetPlayerPosition")]
        [HarmonyPostfix]
        public static void SetPlayerPositionPatch(PlayerCharacter __instance)
        {
            if (!_isLoadedSave || _currentSlot < -1) return;
             
            try
            {
                if (__instance == null) return;
                
                var position = JsonUtility.LoadDataJson(_currentSlot);
                if(position == new Vector3(0, 0, 0)) return;
                SetPlayerPosition(__instance, position, Vector3.zero);
                _isLoadedSave = false;
            }
            catch (System.Exception e)
            {
                LOG.LogError("Failed to load position from json: " + e.Message);
            }
        }
        
        /*
         * This patch tracks when a save is loaded and the slot id and stores it for when the player position is set
         */
        [HarmonyPatch(typeof(SaveLoadManager), "LoadSlot")]
        [HarmonyPostfix]
        public static void LoadLocation(SaveLoadManager __instance, int iSlot)
        {
            _isLoadedSave = true;
            _currentSlot = iSlot;
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
            JsonUtility.SaveDataJson(position, iSlot);
        }
    }
    
    /*
     * This is a simple recreation of the PlayerCharacter SetPlayerPosition method to set the player position
     * The original was not working, so this is a replacement
     */
    private static void SetPlayerPosition(PlayerCharacter player, Vector3 position, Vector3 rotation)
    {
        if (position == new Vector3(0, 0, 0)) return;
        player.SetEnableMagicaCloth(true);

        var euler = Vector3.zero;
        var rot = Quaternion.Euler(euler);

        if (player.transformCache != null)
            player.transformCache.SetPositionAndRotation(position, rot);

        if (player.charaRigidbody == null) return;
        player.charaRigidbody.position = position;
        player.charaRigidbody.rotation = rot;

        if (player._Model_k__BackingField == null) return;

        var model = player._Model_k__BackingField;
        var tr = model.transform;
        if (tr != null)
            tr.SetPositionAndRotation(position, rot);

        player.SetPrevWaistPosition();

        if (player.actionCollider == null) return;
        player.actionCollider.ClearCache();
    }
}
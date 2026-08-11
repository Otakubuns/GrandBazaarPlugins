using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using HarmonyLib;

namespace GrowCropsAnytime;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class GrowCropsAnytime : BasePlugin
{
    internal static new ManualLogSource Log;
    private static ConfigEntry<bool> customCropSeasons;
    public static List<CropMasterJSON.CropConfig> _cropConfigs = [];

    public override void Load()
    {
        // Plugin startup logic
        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        
        customCropSeasons = Config.Bind("General", "CustomCropConfig", false, "If true, the mod will load a JSON file with custom crop seasons. If false, all crops will grow in all seasons.");
        if (customCropSeasons.Value)
        {
            // Load a JSON for all the custom set values for crop seasons.
            _cropConfigs = CropMasterJSON.LoadJsonFile();   
        }
        Harmony.CreateAndPatchAll(typeof(CropPatch));
    }

    public class CropPatch
    {
        [HarmonyPatch(typeof(UITitleMainPage), "PlayTitleLogoAnimation")]
        [HarmonyPostfix]
        public static void PatchCropSeasons()
        {
            var cropMaster = MasterDataManager.Instance.CropMaster;
            var customCrop = customCropSeasons.Value;

            foreach (var cropMasterData in cropMaster.list)
            {
                if(cropMasterData == null) continue;
                if (customCrop)
                {
                    // This way is easier since cropMasterData list in il2cpp which doesn't like LINQ
                    var customCropConfig = _cropConfigs.Find(x => x.CropId == cropMasterData.Id);

                    if (customCropConfig == null) continue;
                    cropMasterData.GrowInSpring = customCropConfig.GrowInSpring ? 1 : 0;
                    cropMasterData.GrowInSummer = customCropConfig.GrowInSummer ? 1 : 0;
                    cropMasterData.GrowInAutumn = customCropConfig.GrowInAutumn ? 1 : 0;
                    cropMasterData.GrowInWinter = customCropConfig.GrowInWinter ? 1 : 0;
                    continue;
                }
                
                cropMasterData.GrowInSpring = 1;
                cropMasterData.GrowInSummer = 1;
                cropMasterData.GrowInAutumn = 1;
                cropMasterData.GrowInWinter = 1;
            }
        }
    }
}
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using HarmonyLib;

namespace InfiniteCropRegrow;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class InfiniteCropRegrow : BasePlugin
{
    internal static new ManualLogSource Log;

    public override void Load()
    {
        // Plugin startup logic
        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        Harmony.CreateAndPatchAll(typeof(CropRegrowPatch));
    }


    public class CropRegrowPatch
    {
        [HarmonyPatch(typeof(UITitleMainPage), "PlayTitleLogoAnimation")]
        [HarmonyPostfix]
        public static void GetQualityUpperLimit()
        {
            var cropsMasterData = MasterDataManager.Instance.CropMaster.list;

            foreach (var cropMasterData in cropsMasterData)
            {
                if (!cropMasterData.IsRepeatedCultivation()) continue;

                cropMasterData.MaxRepeatCount = 100;
                // If you want to turn a crop that's not regrow-able into one you need to set:
                // TypeId to 3, 5 or 6. I tried with 3 for cabbage, and it worked but i'm not 100% which is the difference. For reference tomatoes use 3
                // RepeatedGrowthLevel This is the model stage of the crop it'll take when u harvest it. 2 is the one a lot of crops use. 4 is usually fully grown.
                // MaxRepeatCount needs to be more than 1, how many times it can be repeat harvested.
            }
        }
    }
}
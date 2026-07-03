using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using BokuMono.MiniGame;
using HarmonyLib;

namespace NoHorseRidingOutfit;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    internal static new ManualLogSource Log;
    private static ConfigEntry<bool> ShowOutfitOnDerby;

    public override void Load()
    {
        // Plugin startup logic
        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        ShowOutfitOnDerby = Config.Bind("Settings", "DerbyOutfit", false, "If true, the riding outfit will be shown in the derby mini-game");
        Harmony.CreateAndPatchAll(typeof(HorseRidingPatch));
    }

    public class HorseRidingPatch
    {
        [HarmonyPatch(typeof(PlayerCharacter), "ChangeIntoRidingClothes")]
        [HarmonyPrefix]
        static void PatchRidingClothes(ref bool set)
        {
            if (MiniGameManager.Instance.CurrentType == MiniGameType.HorseRacing && ShowOutfitOnDerby.Value) return;
            set = false;
        }
    }
}
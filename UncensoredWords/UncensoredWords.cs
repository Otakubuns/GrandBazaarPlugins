using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using HarmonyLib;

namespace UncensoredWords;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class UncensoredWords : BasePlugin
{
    internal static new ManualLogSource Log;

    public override void Load()
    {
        // Plugin startup logic
        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        Harmony.CreateAndPatchAll(typeof(UncensoredPatch));
    }


    private class UncensoredPatch
    {
        [HarmonyPatch(typeof(NGWordChecker), "CheckNGWord")]
        [HarmonyPrefix]
        static bool WordPatch()
        {
            //just return false to skip the checking entirely
            return false;
            
            
        }
    }
}
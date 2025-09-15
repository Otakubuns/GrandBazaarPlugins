using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using HarmonyLib;

namespace RunInBackground;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    internal static new ManualLogSource Log;

    /*
     * Port of Neko RunInBackground mod
     * https://fearlessrevolution.com/viewtopic.php?t=36278
     */
    public override void Load()
    {
        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        Harmony.CreateAndPatchAll(typeof(SuspendPatch));
    }

    private class SuspendPatch
    {
        [HarmonyPatch(typeof(AppManager), "OnFocusChanged")]
        [HarmonyPrefix]
        private static bool Prefix(AppManager __instance, bool focus)
        {
            bool flag = !focus;
            bool result;
            if (flag)
            {
                __instance.RequestSuspend(AppManager.SuspendState.Unsuspend);
                result = false;
            }
            else
            {
                result = true;
            }

            return result;
        }
    }
}
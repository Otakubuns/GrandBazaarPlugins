using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using HarmonyLib;

namespace FullGreeting;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class BetterGreeting : BasePlugin
{
    internal static new ManualLogSource Log;
    public static ConfigEntry<float> _pointsScale;

    public override void Load()
    {
        // Plugin startup logic
        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        _pointsScale = Config.Bind("General", "PointsScale", 1f,
            "Scale of points for greetings. Default is 1 (100 points).");
        Harmony.CreateAndPatchAll(typeof(GreetingPatch));
    }
}

public class GreetingPatch
{
    [HarmonyPatch(typeof(LikeabilitySetting), "GetGreetingPoint")]
    [HarmonyPrefix]
    private static bool PatchGreeting(LikeabilitySetting __instance, bool isBirthday, ref int __result)
    {
        __result = (int)(__instance.GetTalkPoint(isBirthday) * BetterGreeting._pointsScale.Value);
        return false;
    }
}
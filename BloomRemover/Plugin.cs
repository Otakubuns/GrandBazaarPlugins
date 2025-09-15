using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using HarmonyLib;

namespace BloomRemover;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    private static ConfigEntry<float> bloomIntensity;
    
    public override void Load()
    {
        // Plugin startup logic
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        Harmony.CreateAndPatchAll(typeof(BloomPatch));
        
        bloomIntensity = Config.Bind("General", "Bloom Intensity", 0f, "Sets the bloom intensity to this value (farm default is 1.7)");
    }
    
    [HarmonyPatch]
    public class BloomPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(LightControlManager), "Start")]
        public static void Postfix(LightControlManager __instance)
        {
            __instance.postProcessSetting.bloomIntensity = bloomIntensity.Value;
        }
    }
}
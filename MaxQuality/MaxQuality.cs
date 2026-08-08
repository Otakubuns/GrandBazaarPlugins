using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using HarmonyLib;

namespace MaxQuality;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class MaxQuality : BasePlugin
{
    internal static new ManualLogSource Log;
    internal static ConfigEntry<int> MaxQualityValueNone;
    internal static ConfigEntry<int> MaxQualityValueBronze;
    internal static ConfigEntry<int> MaxQualityValueOverSilver;

    public override void Load()
    {
        // Plugin startup logic
        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        MaxQualityValueNone = Config.Bind("Max Quality Value None", "MaxQualityValueNone", 730, "The maximum quality value if no festivals have been won.(set to max by default)");
        MaxQualityValueBronze = Config.Bind("Max Quality Value Bronze", "MaxQualityValueBronze", 730, "The maximum quality value if bronze has been won.(set to max by default)");
        MaxQualityValueOverSilver = Config.Bind("Max Quality Value Over Silver", "MaxQualityValueOverSilver", 730, "The maximum quality value if silver or more has been won.");
        Harmony.CreateAndPatchAll(typeof(QualityPatch));
    }

    public class QualityPatch
    {
        [HarmonyPatch(typeof(UITitleMainPage), "PlayTitleLogoAnimation")]
        [HarmonyPostfix]
        public static void GetQualityUpperLimit()
        {
            var qualitySetting = SettingAssetManager.Instance.QualitySetting;
            qualitySetting.MaxQualityValueNone = MaxQualityValueNone.Value;
            qualitySetting.MaxQualityValueBronze = MaxQualityValueBronze.Value;
            qualitySetting.MaxQualityValueOverSilver = MaxQualityValueOverSilver.Value;
            
            // Set the upper limit to the config values for quality using vanilla getQuality
            qualitySetting.QualityUpperLimitNone = qualitySetting.GetQuality(MaxQualityValueNone.Value);
            qualitySetting.QualityUpperLimitBronze = qualitySetting.GetQuality(MaxQualityValueBronze.Value);
            qualitySetting.QualityUpperLimitSilver = qualitySetting.GetQuality(MaxQualityValueOverSilver.Value);
        }
    }
}
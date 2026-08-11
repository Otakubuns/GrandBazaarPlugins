using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using HarmonyLib;

namespace GrowCropsAnytime;

public class CropMasterJSON
{
    public class CropConfig{
        public uint CropId { get; set; }
        public bool GrowInSpring { get; set; }
        public bool GrowInSummer { get; set; }
        public bool GrowInAutumn { get; set; }
        public bool GrowInWinter { get; set; }

        public CropConfig(uint cropId, bool growInSpring, bool growInSummer, bool growInAutumn, bool growInWinter)
        {
            CropId = cropId;
            GrowInSpring = growInSpring;
            GrowInSummer = growInSummer;
            GrowInAutumn = growInAutumn;
            GrowInWinter = growInWinter;
        }
    }
    
    public static List<CropConfig> LoadJsonFile()

    {
        var file = Directory.GetFiles(BepInEx.Paths.PluginPath, "GrowCropsAnytimeConfig.json",
            SearchOption.AllDirectories);

        if (file.Length <= 0)
        {
            GrowCropsAnytime.Log.LogWarning("No JSON file was found, no crops seasons will be changed.\n Creating example JSON.");
            CreateSampleJSON();
            return [];
        }

        try
        {
            var json = File.ReadAllText(file[0]);
            var cropConfigData = JsonSerializer.Deserialize<List<CropConfig>>(json);
            return cropConfigData ?? [];
        }
        catch (Exception e)
        {
            GrowCropsAnytime.Log.LogError($"Error reading/deseralizing JSON. \n{e}");
            return[];
        }
    }

    private static void CreateSampleJSON()
    {
        var filePath = Path.Combine(BepInEx.Paths.PluginPath, "GrowCropsAnytimeConfig.json");
        List<CropConfig> exampleJson =
        [
            new CropConfig(105000, true, false, false, false),
            new CropConfig(105019, false, true, true, false)
        ];
        
        var json = JsonSerializer.Serialize(exampleJson, new JsonSerializerOptions {WriteIndented = true});
        File.WriteAllText(filePath, json);
    }
}
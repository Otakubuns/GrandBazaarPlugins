using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using UnityEngine;

namespace CustomMusic;

public abstract class JsonConfig
{
    
    public class CueOverride
    {
        public string OldCueName { get; set; }
        public string NewCueName { get; set; }
    }

    public class FieldMusicOverride
    {
        public uint FieldID { get; set; }
        public string CueName { get; set; }
    }

    public class MusicConfig
    {
        public List<FieldMusicOverride> FieldMusicOverrides { get; set; } = [];
        public List<CueOverride> CueOverrides { get; set; } = [];

        public override string ToString()
        {
            string roomOverrides = "Room Music Overrides:\n";
            foreach (var CueOverride in FieldMusicOverrides)
            {
                roomOverrides += $"FieldID: {CueOverride.FieldID}, CueName: {CueOverride.CueName}\n";
            }

            string cueOverrides = "Cue Overrides:\n";
            foreach (var CueOverride in CueOverrides)
            {
                cueOverrides += $"OldCueName: {CueOverride.OldCueName}, NewCueName: {CueOverride.NewCueName}\n";
            }

            return roomOverrides + cueOverrides;
        }

        public bool IsOverride(uint fieldId)
        {
            return FieldMusicOverrides.FirstOrDefault(x => x.FieldID == fieldId) != null;
        }

        public string GetCueName(uint fieldId)
        {
            return FieldMusicOverrides.FirstOrDefault(x => x.FieldID == fieldId)?.CueName;
        }

        public string GetCueOverride(string oldCueName)
        {
            return CueOverrides.FirstOrDefault(x => x.OldCueName == oldCueName)?.NewCueName;
        }
    }

    public static MusicConfig ConfigOutput(string configPath)
    {
        var file = Directory.EnumerateFiles(configPath, "MusicConfig.json", SearchOption.AllDirectories)
            .FirstOrDefault();
        
        if (file == null)
            return null;
        
        try
        {
            var json = File.ReadAllText(file);
            var test = JsonSerializer.Deserialize<MusicConfig>(json);
            return test;
        }
        catch (Exception e)
        {
            CustomMusic.Log.LogError("Error reading MusicConfig: " + e);
            return null;
        }
    }
}
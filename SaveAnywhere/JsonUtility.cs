using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BepInEx;
using UnityEngine;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace SaveAnywhere;

/*
 * Utility class for saving and loading position data to/from a JSON file.
 */
public static class JsonUtility
{
    private static readonly string FolderPath = Path.Combine(Paths.BepInExRootPath, "plugins", "SaveAnywhere");
    private static readonly string FilePath = Path.Combine(FolderPath, "SaveAnywhere.json");

    /*
     * Loads the list of SaveData from the JSON file.
     * Returns an empty list if the file does not exist or if deserialization fails.
     */
    private static List<SaveData> LoadSaveDatas()
    {
        if (Directory.Exists(FolderPath) == false)
        {
            Directory.CreateDirectory(FolderPath);
        }

        //Check if folder exists
        if (!File.Exists(FilePath))
        {
            return [];
        }

        var json = File.ReadAllText(FilePath);
        var saveDataList = JsonSerializer.Deserialize<List<SaveData>>(json);
        return saveDataList ?? [];
    }

    /*
     * Saves the given position to the JSON file under the specified slot.
     * If the slot already exists, it updates the position; otherwise, it adds a new entry.
     */
    public static void SaveDataJson(Vector3 position, int slot)
    {
        try
        {
            // -1 slot is the auto save
            if (slot < -1) return;
            var saveDataList = LoadSaveDatas();

            // I could create a new class for the Vector to store better but tbh this is simpler and it's not big
            var existingData = saveDataList.FirstOrDefault(data => data.Slot == slot);
            var positionData = new Vector3Data(position.x, position.y, position.z);
            if (existingData != null)
            {
                existingData.Position = positionData;
            }
            else
            {
                saveDataList.Add(new SaveData {Slot = slot, Position = positionData});
            }

            // Ordering it by id so it's easier to read
            saveDataList = saveDataList.OrderBy(data => data.Slot).ToList();

            var json = JsonSerializer.Serialize(saveDataList, new JsonSerializerOptions {WriteIndented = true});
            File.WriteAllText(FilePath, json);
        }
        catch (Exception e)
        {
            SaveAnywhere.LOG.LogError("Failed to save data to JSON: " + e.Message);
        }
    }


    public static Vector3 LoadDataJson(int slot)
    {
        try
        {
            var saveDataList = LoadSaveDatas();
            var existingData = saveDataList.FirstOrDefault(data => data.Slot == slot);
            if (existingData != null)
            {
                var positionString = existingData.Position;
                return new Vector3(positionString.X, positionString.Y, positionString.Z);
            }
        }
        catch (Exception e)
        {
            SaveAnywhere.LOG.LogError("Failed to load data from JSON: " + e.Message);
        }

        return new Vector3(0, 0, 0);
    }
}

public class SaveData
{
    public int Slot { get; init; }
    public Vector3Data Position { get; set; }
}

public class Vector3Data
{
    public float X { get; }
    public float Y { get; }
    public float Z { get; }

    public Vector3Data(float x, float y, float z)
    {
        //round to 2 decimal places to reduce file size
        X = (float) Math.Round(x, 4);
        Y = (float) Math.Round(y, 4);
        Z = (float) Math.Round(z, 4);
    }
}
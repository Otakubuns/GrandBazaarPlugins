using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BokuMono;
using BokuMono.Data;
using BokuMono.Sound;
using CriWare;
using NAudio.Wave;
using UnityEngine;
using VGAudio.Containers.Hca;

namespace CustomMusic;

public abstract class MusicHelper
{
    static Dictionary<uint, string> seCueNameCache;
    public static Dictionary<string, string> musicCache;
    static JsonConfig.MusicConfig musicConfig;
    private static string folderPath;

    public static string GetSeCueName(uint id)
    {
        if (seCueNameCache != null) return seCueNameCache.GetValueOrDefault(id);
        
        seCueNameCache = new Dictionary<uint, string>();
        var items = MasterDataManager.Instance.SeMasterData._items;
        foreach (var item in items)
            if (item != null)
                seCueNameCache[item.Id] = item.CueName;

        return seCueNameCache.GetValueOrDefault(id);
    }

    public static void ConvertToHca(string folder, bool deleteOriginalMp3)
    {
        // if (hasRun) yield break; // Exit if already run
        // hasRun = true;
        folderPath = folder; 

        foreach (var file in Directory.GetFiles(folder, "*.mp3", SearchOption.AllDirectories)
                     .Concat(Directory.GetFiles(folder, "*.wav", SearchOption.AllDirectories)))
        {
            var hcaPath = Path.ChangeExtension(file, ".hca");
            if (File.Exists(hcaPath)) continue;

            try
            {
                byte[] wavData;

                if (Path.GetExtension(file) == ".mp3")
                {
                    using var mp3 = new Mp3FileReader(file);
                    using var pcmStream = WaveFormatConversionStream.CreatePcmStream(mp3);
                    using var memoryStream = new MemoryStream();
                    using (var writer = new WaveFileWriter(memoryStream, pcmStream.WaveFormat))
                        pcmStream.CopyTo(writer);
                    wavData = memoryStream.ToArray();
                }
                else
                {
                    wavData = File.ReadAllBytes(file);
                }

                File.WriteAllBytes(hcaPath,
                    new HcaWriter().GetFile(new VGAudio.Containers.Wave.WaveReader().Read(wavData)));


                if (File.Exists(hcaPath) && deleteOriginalMp3)
                    File.Delete(file);
            }
            catch (Exception e)
            {
                CustomMusic.Log.LogError($"Error converting {file} to HCA: {e}");
            }
        }

        SetUpMusicCache();
    }

    // this is for having the overrides done at the start so that it is taking up less time
    private static void SetUpMusicCache()
    {
        musicCache = Directory.EnumerateFiles(folderPath, "*.hca", SearchOption.AllDirectories)
            .ToDictionary(
                Path.GetFileNameWithoutExtension,
                file => file,
                StringComparer.OrdinalIgnoreCase
            );
        
        musicConfig = JsonConfig.ConfigOutput(folderPath);
    }

    public static void PlayCustomBGM(SoundBgmController __instance, string songPath, float fadeInTime = 1,
        float fadeOutTime = 1)
    {
        if(!File.Exists(songPath)) return;
        // CustomMusic.Log.LogInfo("Playing custom song: " + songPath);
        if (__instance.bgmPlayer != null && fadeOutTime > 0f)
        {
            __instance.bgmPlayer.FadeOut(fadeOutTime);
            __instance.bgmPlayer = null;
        }
        else
        {
            __instance.bgmPlayer?.Stop();
            __instance.bgmPlayer = null;
        }

        __instance.bgmPlayer ??= SoundManager.Instance!.GetSoundPlayer();

        __instance.bgmPlayer.player = new CriAtomExPlayer(256, 1);

        __instance.bgmPlayer.MasterData = new BgmMasterData();

        var player = __instance.bgmPlayer.player;
        player.SetFile(null, songPath);
        player.SetFormat(CriAtomEx.Format.HCA);
        player.SetNumChannels(2);
        player.SetSamplingRate(48000);
        player.AttachFader();
        player.SetFadeInTime((int) (fadeInTime * 2000));
        player.SetVoicePriority(255);
        player.Loop(true);

        player.Start();
        player.SetVolume(SoundManager.Instance.BgmMasterVolume);
        player.UpdateAll();
    }

    public static void PlayCustomAudio(SoundPlayer __instance, string songPath, float fadeInTime, float volume,
        bool isLoop = false)
    {
        // CustomMusic.Log.LogInfo("Playing custom song: " + songPath);
        __instance.Stop();

        __instance.player = new CriAtomExPlayer(256, 1);

        var player = __instance.player;
        player.SetFile(null, songPath);
        player.SetFormat(CriAtomEx.Format.HCA);
        player.SetNumChannels(2);
        player.SetSamplingRate(48000);
        player.AttachFader();
        player.SetFadeInTime((int) (fadeInTime * 1000f));
        player.Loop(isLoop);
        player.SetVolume(volume);
        player.UpdateAll();
        player.Start();
    }

    public static bool IsOverride(uint fieldId)
    {
        var cueName = musicConfig.GetCueName(fieldId);
        if(cueName == null) return false;

        return cueName.StartsWith("Vanilla_Cue_") || musicCache.TryGetValue(cueName, out var audioPath);
    }

    public static string GetAudioPath(string cueName, bool isRoomOverride = false)
    {
        if (!isRoomOverride)
            cueName = musicConfig.GetCueOverride(cueName) ?? cueName;

        // If they choose to have a vanilla cue play instead it will just return the original name and no path
        return cueName.StartsWith("Vanilla_Cue_") ? cueName : musicCache.GetValueOrDefault(cueName);
    }

    public static string GetOverrideRoomCueName(uint fieldId)
    {
        return musicConfig.GetCueName(fieldId);
    }
}
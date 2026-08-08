using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BepInEx.Unity.IL2CPP.Utils;
using BokuMono;
using BokuMono.Data;
using BokuMono.Sound;
using CriWare;
using HarmonyLib;
using Il2CppSystem.Collections.Generic;
using UnityEngine;

namespace CustomMusic;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public class CustomMusic : BasePlugin
{
    public new static ManualLogSource Log;
    private static ConfigEntry<string> musicFolderPath;
    private static ConfigEntry<bool> deleteOriginalMp3;
    private static string filePath;
    private static System.Collections.Generic.Dictionary<uint, string> roomOverride;

    // ok so soundplayer is made per new song
    //SoundManager -> SoundBgmController -> SoundPlayer
    // GetFieldBgm -> PlayFieldBgm -> Play

    public override void Load()
    {
        Log = base.Log; 
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        var defaultPath = Path.Combine(Paths.BepInExRootPath, "plugins", "CustomMusic");
        musicFolderPath = Config.Bind("General", "MusicFolderPath", defaultPath,
            "The folder in plugins where custom music files are stored(.mp3 files). Full Path is required.");
        deleteOriginalMp3 = Config.Bind("General", "DeleteOriginalMp3", false,
            "If true, original mp3 files will be deleted after conversion to HCA.");
        filePath = Path.Combine(Paths.BepInExRootPath, "plugins", musicFolderPath.Value);
        
        MusicHelper.ConvertToHca(filePath, deleteOriginalMp3.Value);
        Harmony.CreateAndPatchAll(typeof(SoundManagerPatches));
    }
    
    public class SoundManagerPatches
    {
        static string currentCueName = null;

        [HarmonyPatch(typeof(SoundBgmController))]
        [HarmonyPatch("PlayBgm", new Type[] {typeof(BgmMasterData), typeof(float), typeof(float), typeof(float)})]
        [HarmonyPrefix]
        public static void PlayBgm_Prefix(SoundBgmController __instance, ref BgmMasterData bgmData,
            float fadeInTime, float fadeOutTime, float volume)
        {
            var songPath = MusicHelper.GetAudioPath(bgmData.CueName);
            if (songPath == null)
                return;

            if (!songPath.StartsWith("Vanilla_Cue_")) return;
            
            var sheetName = songPath.Substring("Vanilla_Cue_".Length);
            var newBgmMasterData = MasterDataManager.Instance.BgmMasterData._items.FirstOrDefault(x => x.CueName == sheetName);
            if (newBgmMasterData == null) return;
            bgmData = newBgmMasterData;
        }

        [HarmonyPatch(typeof(SoundBgmController))]
        [HarmonyPatch("PlayBgm", new Type[] {typeof(BgmMasterData), typeof(float), typeof(float), typeof(float)})]
        [HarmonyPostfix]
        public static void PlayBgm_Postfix(SoundBgmController __instance, BgmMasterData bgmData,
            float fadeInTime, float fadeOutTime, float volume)
        {
            if (bgmData == null) return;
            if (__instance.bgmPlayer == null) return;

            if (bgmData.CueName == currentCueName) return;
            currentCueName = bgmData.CueName;
            
            var songPath = MusicHelper.GetAudioPath(bgmData.CueName);
            if (songPath == null)
            {
                currentCueName = null;
                return;
            }

            if (songPath.StartsWith("Vanilla_Cue_"))
                // Just to ensure that if it's a vanilla override that it just skips playing a custom song entirely
                return;
            
            MusicHelper.PlayCustomBGM(__instance, songPath, fadeInTime, fadeOutTime);
        }
        
        [HarmonyPatch(typeof(SoundManager), "PlayFieldBgm")]
        [HarmonyPostfix]
        public static void PlayFieldBgm(SoundManager __instance, uint fieldId, float fadeInSec, float fadeOutSec)
        {
            //To check if there is a custom track for specific field
            if(!MusicHelper.IsOverride(fieldId)) return;

            var cueName = MusicHelper.GetOverrideRoomCueName(fieldId);
            if (cueName == currentCueName)
                return;
            currentCueName = cueName;
            
            var songPath = MusicHelper.GetAudioPath(cueName, true);
            if (songPath == null)
            {
                //Log.LogInfo("No custom music found for cue name: " + cueName);
                currentCueName = null;
                return;
            }

            if (songPath.StartsWith("Vanilla_Cue_"))
            {
                var sheetName = songPath.Substring("Vanilla_Cue_".Length);
                var newBgmMasterData = MasterDataManager.Instance.BgmMasterData._items.FirstOrDefault(x => x.CueName == sheetName);
                if (newBgmMasterData == null) return;
                
                var setting = __instance.Setting;
                if (fadeInSec < 0f)
                    fadeInSec = setting.FieldChangeFadeTime;

                if (fadeOutSec < 0f)
                    fadeOutSec = setting.FieldChangeFadeTime;
                
                __instance.BgmController.PlayBgm(newBgmMasterData, fadeInSec, fadeOutSec, -1f);
            }
            MusicHelper.PlayCustomBGM(__instance.BgmController, songPath);
        }
        
        //NOTE: some use playse and some use play. stuff like bag never call play but menu does
        // [HarmonyPatch(typeof(SoundManager), "PlaySe",
        //     new Type[] {typeof(uint), typeof(float), typeof(bool), typeof(float), typeof(bool), typeof(bool)})]
        // [HarmonyPostfix]
        // public static void PatchSe(SoundManager __instance, uint id, float volume, SoundPlayer __result)
        // {
        //     if (__result == null) return;
        //
        //     var cueName = MusicHelper.GetSeCueName(id);
        //     if (cueName == null) return;
        //
        //     var songPath = MusicHelper.GetAudioPath(cueName);
        //     if (songPath == null) return;
        //
        //     __result.player.Stop();
        //     __result.player = new CriAtomExPlayer(256, 1);
        //     var p = __result.player;
        //
        //     p.SetFile(null, Path.Combine(filePath, songPath));
        //     p.SetFormat(CriAtomEx.Format.HCA);
        //     p.SetNumChannels(2);
        //     p.SetSamplingRate(48000);
        //     p.SetVolume(SoundManager.Instance.SeMasterVolume);
        //     p.UpdateAll();
        //     p.Start();
        // }
        //
        //  [HarmonyPatch(typeof(SoundManager))]
        //  [HarmonyPatch("Play")]
        //  [HarmonyPostfix]
        //  public static void PatchPlay(SoundManager __instance, uint id, SoundManager.Category category, float volume,
        //      ref SoundPlayer __result)
        //  {
        //      if (__result == null) return;
        //
        //      var cueName = MusicHelper.GetSeCueName(id);
        //      if (cueName == null) return;
        //
        //      var songPath = MusicHelper.GetAudioPath(cueName);
        //      if (songPath == null) return;
        //
        //      __result.player.Stop();
        //      __result.player = new CriAtomExPlayer(256, 1);
        //      var p = __result.player;
        //      
        //      p.SetFile(null, Path.Combine(filePath, songPath));
        //      p.SetFormat(CriAtomEx.Format.HCA);
        //      p.SetNumChannels(2);
        //      p.SetSamplingRate(48000);
        //      //TODO: check the type of sound, and get the volume accordingly
        //      p.SetVolume(volume < 0 ? 1.0f : volume);
        //      p.UpdateAll();
        //      p.Start();
        // }
        
        /*
         * These are functions for the new player object as remaking it will not allow the game to properly control it
         */
        [HarmonyPatch(typeof(SoundBgmController), "FadeVolume")]
        [HarmonyPostfix]
        public static void FadeVolume_Postfix(SoundBgmController __instance, bool isLower, float fadeTime)
        {
            if (currentCueName == null) return;
            var player = __instance.bgmPlayer?.player;
            if (player == null) return;

            player.SetFadeOutTime((int) (fadeTime * 1000f));

            if (isLower)
                player.SetVolume(SoundManager.Instance.BgmMasterVolume / 2f);
            else
                player.SetVolume(SoundManager.Instance.BgmMasterVolume);
            player.UpdateAll();
        }
        
        [HarmonyPatch(typeof(SoundManager), "ApplyOption")]
        [HarmonyPostfix]
        public static void ApplyOption_Postfix(SoundManager __instance, bool isCache)
        {
            if (!isCache) return;
            __instance.BgmController.bgmPlayer.player.SetVolume(__instance.BgmMasterVolume);
            __instance.BgmController.bgmPlayer.player.UpdateAll();
        }


        [HarmonyPatch(typeof(SoundManager), "PauseBgm")]
        [HarmonyPostfix]
        public static void PauseBgm_Postfix(SoundManager __instance)
        {
            __instance.GetSoundPlayer()?.player?.Pause();
        }


        [HarmonyPatch(typeof(SoundManager), "ResumeBgm")]
        [HarmonyPostfix]
        public static void ResumeBgm_Postfix(SoundManager __instance)
        {
            __instance.GetSoundPlayer()?.player?.Resume(CriAtomEx.ResumeMode.AllPlayback);
        }

        [HarmonyPatch(typeof(SoundManager), "StopBgm")]
        [HarmonyPostfix]
        public static void StopBgm_Postfix(SoundManager __instance)
        {
            __instance.GetSoundPlayer()?.player?.Stop();
        }

        [HarmonyPatch(typeof(SoundManager), "set_BgmMasterVolume")]
        [HarmonyPostfix]
        public static void SetBgmMasterVolume(SoundManager __instance, float value)
        {
            __instance.BgmController.bgmPlayer?.player?.SetVolume(value);
            __instance.BgmController.bgmPlayer?.player?.UpdateAll();
        }
    }
}
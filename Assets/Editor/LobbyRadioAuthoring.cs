using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using HauntedFish.Multiplayer;
using UnityEditor;
using UnityEngine;

public static class LobbyRadioAuthoring
{
    const string Workspace = "C:/Users/wkeif/Documents/Codex/2026-10-10/task-6";
    const string Request = Workspace + "/radio-authoring-request.txt";
    const string PrefabPath = "Assets/Prefabs/Radio/LobbyRadioPlayback.prefab";
    static readonly string[] ClipNames = { "HFH_Radio_Ozon_Hair_Spray", "HFH_Radio_Dentu_Creme", "HFH_Radio_409_Cleaner", "HFH_Radio_Loop_A_Lot", "HFH_Radio_Blahs", "HFH_Radio_Dristan", "HFH_Radio_Professional_Overeaters", "HFH_Radio_Thunderstreak", "HFH_Radio_Trik_Trak", "HFH_Radio_Gilbert_Slot_Racers", "HFH_Radio_Micrin_Mouthwash" };
    static bool Finished;
    static void CheckIdle()
    {
        if (Finished || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (Application.dataPath.Replace('\\', '/') != "C:/Users/wkeif/Documents/GitHub/Haunted Fish Hotel/Assets") return;
        var clips = ClipNames.Select(name => AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/RadioAds/" + name + ".wav")).ToArray();
        if (clips.Any(clip => !clip)) return;
        Finished = true;
        
        GameObject root = null;
        try
        {
            // Prefab isolation only. No loaded gameplay scene is opened, dirtied or saved.
            if (!LobbyStormRepairTask.RequireSafeEditMode()) return;
            root = PrefabUtility.LoadPrefabContents(PrefabPath);
            var source = root.GetComponent<AudioSource>();
            var player = root.GetComponent<LobbyRadioPlayback>();
            var music = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/lobby - no master effects.mp3");
            Require(source && player && music, "Authored source, player and original lobby music exist.");
            var low = root.GetComponent<AudioLowPassFilter>(); if (!low) low = root.AddComponent<AudioLowPassFilter>();
            var high = root.GetComponent<AudioHighPassFilter>(); if (!high) high = root.AddComponent<AudioHighPassFilter>();
            var distortion = root.GetComponent<AudioDistortionFilter>(); if (!distortion) distortion = root.AddComponent<AudioDistortionFilter>();
            low.cutoffFrequency = 3200; high.cutoffFrequency = 350; distortion.distortionLevel = .16f;
            var serialized = new SerializedObject(player);
            serialized.FindProperty("_Source").objectReferenceValue = source;
            serialized.FindProperty("_Music").objectReferenceValue = music;
            var ads = serialized.FindProperty("_Advertisements"); ads.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++) ads.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (!LobbyStormRepairTask.RequireSafeEditMode(false)) throw new InvalidOperationException("Editor state changed; prefab save cancelled.");
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root); root = null;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            source = prefab.GetComponent<AudioSource>(); player = prefab.GetComponent<LobbyRadioPlayback>();
            Require(prefab.GetComponentsInChildren<AudioSource>(true).Length == 1, "Exactly one authored radio source.");
            Require(!source.playOnAwake && source.loop && source.clip == music, "Existing looping music remains authored with automatic playback off.");
            Require(Mathf.Approximately(source.volume,.5f) && Mathf.Approximately(source.spatialBlend,1) && Mathf.Approximately(source.minDistance,1.5f) && Mathf.Approximately(source.maxDistance,22) && source.rolloffMode == AudioRolloffMode.Linear && source.dopplerLevel == 0, "Original spatial audio settings retained.");
            Require(prefab.GetComponent<AudioLowPassFilter>() && prefab.GetComponent<AudioHighPassFilter>() && prefab.GetComponent<AudioDistortionFilter>(), "Vintage filters authored in prefab.");
            serialized = new SerializedObject(player);
            Require(serialized.FindProperty("_Source").objectReferenceValue == source && serialized.FindProperty("_Music").objectReferenceValue == music && serialized.FindProperty("_Advertisements").arraySize == ClipNames.Length, "Playback references and eleven default clips authored.");
            foreach (var clip in clips) Require(clip.channels == 1 && clip.frequency == 22050 && clip.length > 0 && clip.loadState != AudioDataLoadState.Failed, "Imported valid mono 22.05 kHz clip: " + clip.name);
            var evidence = "PASS native Unity " + Application.unityVersion + ": radio scripts compiled, prefab imported and saved in prefab isolation, one authored spatial source, original music and volume/falloff retained, three authored vintage filters, eleven nonzero mono 22.05kHz AudioClips bound. No Lobby.unity or other gameplay scene saved.\n";
            foreach (var clip in clips) evidence += clip.name + ": " + clip.length.ToString("F3") + "s, " + clip.samples + " samples\n";
            foreach (var script in new[] { "LobbyRadioPlayback.cs", "RadioAdvertisementSelection.cs", "LobbyCursorManager.cs" }) evidence += script + " SHA256 " + Hash(Application.dataPath + "/Scripts/Multiplayer/Lobby/" + script) + "\n";
            evidence += "Audio decode/import and prefab references are verified. Actual listening, live radio presses, remote multiplayer delivery, and scene exit/return in Play mode have not been performed. Source-designated public domain is not guaranteed worldwide or underlying-music clearance.\n";
            File.WriteAllText(Workspace + "/radio-native-import-evidence.txt", evidence);
            File.Delete(Request);
        }
        catch (Exception error) { File.WriteAllText(Workspace + "/radio-native-import-error.txt", error.ToString()); Debug.LogException(error); }
        finally { if (root) PrefabUtility.UnloadPrefabContents(root); }
    }
    [MenuItem("Tools/Haunted Hotel/Verify radio prefab")]
    public static void VerifyAgain() { if (!LobbyStormRepairTask.RequireSafeEditMode()) return; File.WriteAllText(Request, "Verify the expanded radio prefab only."); Finished = false; CheckIdle(); }
    static void Require(bool ok, string reason) { if (!ok) throw new Exception(reason); }
    static string Hash(string path) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
}

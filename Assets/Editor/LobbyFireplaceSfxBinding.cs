// STAGED ONLY. Native authoring call after settings bundle publication and exclusive Lobby grant.
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
public static class LobbyFireplaceSfxBinding {
 public static void Bind(AudioSource source){
 const string path="Assets/Resources/HotelAudio.mixer",expected="6cc882a789454a5aabbc3a05e5d3ec03";
 if(!source)throw new Exception("Authored fireplace source missing");
 if(AssetDatabase.AssetPathToGUID(path)!=expected||!AssetDatabase.LoadAssetAtPath<AudioMixer>(path))throw new Exception("Publish settings-owned mixer/dependencies first; no dangling reference saved");
 var group=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AudioMixerGroup>().SingleOrDefault(x=>AssetDatabase.TryGetGUIDAndLocalFileIdentifier(x,out string guid,out long id)&&guid==expected&&id==24300002);
 if(!group)throw new Exception("Exact published SFX group24300002 not imported");
 source.outputAudioMixerGroup=group;PrefabUtility.RecordPrefabInstancePropertyModifications(source);EditorUtility.SetDirty(source);
 // Caller owns backup/save and dependency compile validation. Source calibration is untouched.
 }
}


using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Explicit read-only inspection: no update subscriptions and no importer changes.
public static class StormAudioImportDiagnosis {
 const string E="C:/Users/wkeif/Documents/Codex/2026-10-09/task/LobbyAtmosphereEvidence";
 [MenuItem("Tools/Haunted Fish/Inspect Storm Audio Import")]
 public static void Run(){
 if(!LobbyStormRepairTask.RequireSafeEditMode())return;
 var lines=new List<string>{"Unity="+Application.unityVersion+" target="+EditorUserBuildSettings.activeBuildTarget};
 try{
 var platform=SupportedPlatformName(EditorUserBuildSettings.activeBuildTarget);
 if(platform==null){lines.Add("SKIP unknown/no-target/unsupported platform: no importer queries or mutations.");Directory.CreateDirectory(E);File.WriteAllLines(E+"/audio-import-manual-inspection.txt",lines);return;}
 foreach(var file in Directory.GetFiles(Application.dataPath+"/Art/Storm","*.wav",SearchOption.AllDirectories)){
 if(!LobbyStormRepairTask.RequireSafeEditMode())return;
 var path="Assets"+file.Substring(Application.dataPath.Length).Replace('\\','/');
 var importer=AssetImporter.GetAtPath(path) as AudioImporter;var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);
 if(!importer||!clip)throw new Exception("Imported storm asset missing: "+path);
 var setting=importer.defaultSampleSettings;
 lines.Add(path+" Hz="+clip.frequency+" channels="+clip.channels+" seconds="+clip.length+" GUID="+AssetDatabase.AssetPathToGUID(path)+" default="+setting.sampleRateSetting);
 if(platform!=null&&importer.ContainsSampleSettingsOverride(platform)){var s=importer.GetOverrideSampleSettings(platform);lines.Add("OVERRIDE "+platform+" rate="+s.sampleRateSetting+" Hz="+s.sampleRateOverride);}
 ValidateSource(file,clip,lines);
 }
 Directory.CreateDirectory(E);File.WriteAllLines(E+"/audio-import-manual-inspection.txt",lines);
 Debug.Log("Storm audio inspected without importer mutation or callbacks.");
 }catch(Exception error){Directory.CreateDirectory(E);File.WriteAllLines(E+"/audio-import-manual-error.txt",lines);File.AppendAllText(E+"/audio-import-manual-error.txt",error.ToString());Debug.LogException(error);}
 }
 public static string SupportedPlatformName(BuildTarget target){
 if(target==BuildTarget.NoTarget)return null;
 switch(BuildPipeline.GetBuildTargetGroup(target)){
 case BuildTargetGroup.Standalone:return "Standalone";
 case BuildTargetGroup.WebGL:return "WebGL";
 case BuildTargetGroup.Android:return "Android";
 case BuildTargetGroup.iOS:return "iOS";
 default:return null;
 }
 }
 static void ValidateSource(string file,AudioClip clip,List<string> lines){
 using var reader=new BinaryReader(File.OpenRead(file));var stream=reader.BaseStream;
 if(new string(reader.ReadChars(4))!="RIFF")throw new Exception("Not RIFF WAV");reader.ReadUInt32();if(new string(reader.ReadChars(4))!="WAVE")throw new Exception("Not WAVE");
 int rate=0,channels=0,align=0;uint data=0;
 while(stream.Position+8<=stream.Length){var tag=new string(reader.ReadChars(4));uint size=reader.ReadUInt32();long end=stream.Position+size+(size%2);if(tag=="fmt "){int format=reader.ReadUInt16();channels=reader.ReadUInt16();rate=reader.ReadInt32();reader.ReadUInt32();align=reader.ReadUInt16();int bits=reader.ReadUInt16();if(format!=1||bits!=16)throw new Exception("PCM16 source expected");}else if(tag=="data")data=size;stream.Position=end;}
 if(rate!=48000||channels!=2||align<=0||data==0)throw new Exception("Stereo48k source header changed: "+file);
 double duration=(double)data/align/rate;bool web=EditorUserBuildSettings.activeBuildTarget==BuildTarget.WebGL;
 if(clip.channels!=2||(clip.frequency!=48000&&!(web&&clip.frequency==44100))||Math.Abs(clip.length-duration)>.002)throw new Exception("Source/import timing mismatch: "+file);
 lines.Add("SOURCE stereo48k duration="+duration+" native delta="+Math.Abs(clip.length-duration));
 }
}

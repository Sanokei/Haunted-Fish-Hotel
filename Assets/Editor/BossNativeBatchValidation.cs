#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class BossNativeBatchValidation
{
    const string Marker="BossNativeBatchValidation.Armed";
    static string Summary=>SessionState.GetBool("BossNativeBatchValidation.Segmented",false)?"Tools/BossFightTests/Evidence/segmented-boss-native/batch-results.txt":"Tools/BossFightTests/Evidence/2026-10-11-native-batch/results.txt";
    static string Result(string name)=>System.IO.Path.Combine(System.IO.Path.GetTempPath(),name+".txt");
    static void RequireOwnedBatch()
    {
        if(!Application.isBatchMode||!Environment.GetCommandLineArgs().Contains("--boss-native-validation"))
            throw new InvalidOperationException("Only explicitly invoked owned native batch may run this entry point.");
    }
    public static void RunSegmented()
    {
        RequireOwnedBatch();SessionState.SetBool("BossNativeBatchValidation.Segmented",true);Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Summary));
        try
        {
            BossSegmentedAuthoring.Run();
            BossSegmentedNativeValidation.Run();
            string report=File.ReadAllText("Tools/BossFightTests/Evidence/segmented-boss-native/results.txt");
            if(report.Contains("FAIL "))throw new Exception("Segmented variant/render validation failed: "+report);
            Run();
        }
        catch(Exception error){Complete(false,error.ToString());}
    }
    public static void Run()
    {
        RequireOwnedBatch();Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Summary));
        SessionState.SetBool(Marker,true);SessionState.SetFloat("BossNativeBatchValidation.Deadline",(float)EditorApplication.timeSinceStartup+240);
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        try
        {
            BossPresentationNativeValidation.Run();
            string presentation=File.ReadAllText(Result("HauntedFishBossPresentationOcclusionNative-20261010"));
            if(presentation.Contains("FAIL "))throw new Exception("Native occlusion validation failed: "+presentation);
            BossArenaValidation.Run();
            if(!SessionState.GetBool("BossArenaValidation.Armed",false))throw new Exception("Physics runner did not arm");
        }
        catch(Exception error){Complete(false,error.ToString());}
    }
    [InitializeOnLoadMethod]
    static void ResumeExplicitRun()
    {
        if(!SessionState.GetBool(Marker,false))return;
        RequireOwnedBatch();EditorApplication.update-=Tick;EditorApplication.update+=Tick;
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Marker,false))return;
        if(EditorApplication.timeSinceStartup>SessionState.GetFloat("BossNativeBatchValidation.Deadline",0))
        {Complete(false,"Timeout waiting for native physical match result");return;}
        string file=Result("HauntedFishBossArenaPlayNative-20261010-rerun");
        if(EditorApplication.isPlayingOrWillChangePlaymode||SessionState.GetBool("BossArenaValidation.Armed",false)||SessionState.GetBool("BossArenaValidation.Restore",false)||!File.Exists(file))return;
        string outcome=File.ReadAllText(file);Complete(outcome.StartsWith("PASS "),outcome);
    }
    static void Complete(bool pass,string message)
    {
        RequireOwnedBatch();SessionState.SetBool(Marker,false);EditorApplication.update-=Tick;
        File.WriteAllText(Summary,(pass?"PASS ":"FAIL ")+"owned native batch; "+message);
        Debug.Log("BOSS_NATIVE_BATCH "+(pass?"PASS ":"FAIL ")+message);
        EditorApplication.Exit(pass?0:1);
    }
}
#endif
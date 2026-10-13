#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using HauntedFish.Multiplayer;
public static class GameIntroPrefabValidationTask
{
    static void RequireIdleEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Requires idle Edit mode outside prefab editing.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save or discard scene changes before explicitly running this tool.");
    }

    const string Evidence="C:/Users/wkeif/Documents/Codex/2026-10-09/task-3/NativeIntroPrefabValidation";
    static void Require(bool value,string text) { if(!value) throw new InvalidOperationException(text); }
    [MenuItem("Tools/Haunted Fish/Validate Game intro prefabs")]
    public static void Run()
    {
        RequireIdleEditMode();
        if(!Application.dataPath.Replace('\\','/').EndsWith("/Haunted Fish Hotel/Assets",StringComparison.Ordinal)) return;
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            PrefabStageUtility.GetCurrentPrefabStage()!=null || HotelPlayer.ActivePlayers.Count!=0 || GameSceneController.Current) return;
        for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
            if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) return;

        Directory.CreateDirectory(Evidence);
        try
        {
            var attic=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/AtticRoundCutscene.prefab");
            Require(attic,"Imported authored attic missing");
            var assets=attic.GetComponent<AtticTransitionAssets>(); var story=attic.GetComponent<Monologue.Dialogue.StoryUI>();
            Require(assets && story && assets.FishRoot && assets.FishPrefab && assets.StreakRoot,"Native authored transition bindings missing");
            var wall=story.Find("hallway_wall"); var floor=story.Find("hallway_floor");
            Require(wall && floor && assets.FishRoot.parent==floor.parent && wall.parent==floor.parent,"Native fish/hallway hierarchy mismatch");
            Require(assets.Streaks.Length==18 && assets.Streaks.All(x=>x && x.transform.IsChildOf(assets.StreakRoot)),"Native authored streak count/hierarchy mismatch");
            Require(assets.FishPrefab.Root && assets.FishPrefab.Shadow && assets.FishPrefab.Pieces.Length>=2 && assets.FishPrefab.Pieces.All(x=>x),"Native fish artwork slots missing");
            Require(assets.FishPrefab.GetComponentsInChildren<HotelPlayer>(true).Length==0 &&
                assets.FishPrefab.GetComponentsInChildren<Mirage.NetworkIdentity>(true).Length==0,"Visual prefab contains duplicated network actor");
            Require(assets.FishHowToPrefab && assets.GhostHowToPrefab && assets.FishHowToPrefab!=assets.GhostHowToPrefab,"Role pages must be distinct authored prefabs");
            bool clips=true;
            foreach(var page in new[]{assets.FishHowToPrefab,assets.GhostHowToPrefab})
            {
                var fields=new SerializedObject(page);
                Require(fields.FindProperty("_Continue").objectReferenceValue && fields.FindProperty("_Status").objectReferenceValue &&
                    fields.FindProperty("_Canvas").objectReferenceValue && fields.FindProperty("_MissingRecording").objectReferenceValue,"Native ready/page bindings missing");
                var video=page.GetComponent<VideoPlayer>(); var raw=page.GetComponentInChildren<RawImage>(true);
                Require(video && raw && video.renderMode==VideoRenderMode.RenderTexture && video.targetTexture && raw.texture==video.targetTexture &&
                    video.targetTexture.width==640 && video.targetTexture.height==360,"Native video box/target configuration invalid");
                Require(!video.playOnAwake && video.isLooping,"Native video playback should start only when page begins and loop");
                clips &= video.clip;
            }
            var grace=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/GraceCountdown.prefab").GetComponent<GraceCountdownPresentation>();
            Require(grace && grace.Digit && grace.Digit.font && grace.Digit.font.name.Contains("Modak") && grace.Digit.fontSize==160 &&
                grace.Digit.rectTransform.sizeDelta.y>=300 && grace.Digit.GetComponent<Outline>(),"Native bubbly grace prefab/font/outline invalid");
            var storm=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/GameStormAtmosphere.prefab");
            Require(storm && storm.GetComponent<GameStormSceneBinding>().ActivationApproved,"Native Game storm activation binding invalid");
            var lightning=storm.GetComponentInChildren<LightningEffectManager>(true);
            Require(lightning && lightning.ValidateAuthoredBindings(out _),"Shared native Game ValidateAuthoredBindings rejected serialized window passes/flash");
            var audio=storm.GetComponentInChildren<LobbyStormAudio>(true);
            Require(audio && new SerializedObject(audio).FindProperty("_Lightning").objectReferenceValue==lightning,"Native Game audio does not share the single lightning owner");
            File.WriteAllText(Evidence+"/result.txt","PASS native asset import, serialized intro prefabs and shared Game storm ValidateAuthoredBindings; NO runtime/render/network pass. Genuine clips="+clips);
        }
        catch(Exception error) { File.WriteAllText(Evidence+"/result.txt","FAIL native prefab asset validation\n"+error); }
    }
}
#endif

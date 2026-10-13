#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using HauntedFish.Theatre;
public static class TheatreForegroundFraming
{
    // Staged only. Run after an explicit editor grant; does not open/launch Unity.
    // This is the current preview-media correction. Inspect again after recorder replacement.
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||PrefabStageUtility.GetCurrentPrefabStage()!=null)throw new Exception("Exclusive idle Unity ownership required.");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Preserve dirty user scenes first.");
        var before=EditorSceneManager.GetSceneManagerSetup();var selection=Selection.objects;
        try
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/theatre.unity",OpenSceneMode.Single);var root=scene.GetRootGameObjects().Single(r=>r.GetComponent<TheatrePresentation>());
            var transforms=root.GetComponentsInChildren<Transform>(true);
            var own=transforms.Single(t=>t.name=="Owned black fish silhouette");
            var walker=own.parent;var walkerBefore=walker.localPosition;
            var exit=transforms.Single(t=>t.name.StartsWith("EXIT"));var exitBefore=exit.localPosition;var collider=exit.GetComponent<BoxCollider>();var colliderCenter=collider.center;var colliderSize=collider.size;
            var presentation=root.GetComponent<TheatrePresentation>();var serialized=new SerializedObject(presentation);
            var heading=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/HalloweenUI/Modak-Headings.asset");var body=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/HalloweenUI/VarelaRound-Body.asset");if(!heading||!body)throw new Exception("Recorder must publish and native-bake shared typography assets first.");
            var status=(UnityEngine.Object)transforms.Single(t=>t.name=="Playback status").GetComponent<Text>();var countdown=(UnityEngine.Object)transforms.Single(t=>t.name=="Countdown numeral").GetComponent<Text>();
            var converted=new Dictionary<UnityEngine.Object,TMP_Text>();
            foreach(var old in root.GetComponentsInChildren<Text>(true))
            {
                var go=old.gameObject;string text=old.text;Color color=old.color;bool raycast=old.raycastTarget;var align=old.alignment;bool isStatus=old==status;bool isCountdown=old==countdown;bool display=!isStatus&&!isCountdown&&old.fontSize>=28;float size=isStatus?20:isCountdown?36:display?30:24;
                var key=(UnityEngine.Object)old;UnityEngine.Object.DestroyImmediate(old);var t=go.AddComponent<TextMeshProUGUI>();t.text=text;t.color=color;t.raycastTarget=raycast;t.font=display?heading:body;t.fontSharedMaterial=t.font.material;t.fontSize=size;t.enableAutoSizing=false;t.textWrappingMode=TextWrappingModes.Normal;t.overflowMode=TextOverflowModes.Overflow;t.lineSpacing=2;t.paragraphSpacing=4;
                t.alignment=align==TextAnchor.MiddleCenter?TextAlignmentOptions.Center:align==TextAnchor.UpperCenter?TextAlignmentOptions.Top:align==TextAnchor.LowerCenter?TextAlignmentOptions.Bottom:TextAlignmentOptions.MidlineLeft;converted[key]=t;
            }
            serialized.FindProperty("_Status").objectReferenceValue=transforms.Single(t=>t.name=="Playback status").GetComponent<TMP_Text>();serialized.FindProperty("_Countdown").objectReferenceValue=transforms.Single(t=>t.name=="Countdown numeral").GetComponent<TMP_Text>();
            foreach(var role in new[]{"Fish","Ghost"}){var button=transforms.Single(t=>t.name==(role=="Fish"?"Player tutorial":"Ghost tutorial"));serialized.FindProperty("_"+role+"TabBackground").objectReferenceValue=button.GetComponent<UnityEngine.UI.Image>();serialized.FindProperty("_"+role+"TabLabel").objectReferenceValue=button.GetComponentInChildren<TMP_Text>();}serialized.ApplyModifiedPropertiesWithoutUndo();
            // Lift ONLY artwork beneath the horizontal lane. Lane and exit remain identical.
            var ownPosition=own.localPosition;ownPosition.y=3.0f;own.localPosition=ownPosition;
            foreach(string name in new[]{"Seated fish audience left","Seated ghost audience right"})
            {
                var actor=transforms.Single(t=>t.name==name);var position=actor.localPosition;position.y=5.8f;actor.localPosition=position;
            }
            foreach(var renderer in new[]{own.GetComponent<SpriteRenderer>(),transforms.Single(t=>t.name=="Seated fish audience left").GetComponent<SpriteRenderer>(),transforms.Single(t=>t.name=="Seated ghost audience right").GetComponent<SpriteRenderer>()})
            {
                if(renderer.color!=Color.black)throw new Exception("Keep silhouettes pure black; no glow/outline material change.");
                if(renderer.sharedMaterial==null)throw new Exception("Preserve authored silhouette material.");
            }
            if(walker.localPosition!=walkerBefore||exit.localPosition!=exitBefore||collider.center!=colliderCenter||collider.size!=colliderSize)throw new Exception("Movement/exit integration changed; abort.");
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/Theatre/TheatrePresentation.prefab",out bool saved);if(!saved)throw new Exception("Native prefab save failed");
            if(!EditorSceneManager.SaveScene(scene))throw new Exception("Native scene save failed");
            Debug.Log("THEATRE foreground artwork lifted into current illuminated preview frame. Lane/exit/colliders unchanged. Native decoded-frame screenshot required; inspect again for replacement recordings.");
        }
        finally
        {
            if(before.Length>0&&before.All(s=>!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(before);else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            Selection.objects=selection;
        }
    }
}
#endif
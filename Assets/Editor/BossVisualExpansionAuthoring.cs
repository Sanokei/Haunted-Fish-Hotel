#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using HauntedFish.BossFight;
public static class BossVisualExpansionAuthoring
{
    // Staged only; Fonts supplied by shared UI owner. Does not touch Game/Lobby/HotelNetworkPlayer.
    public static void Apply(Font heading=null,Font body=null)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)throw new Exception("Explicit idle exclusive Boss Unity grant required");
        const string path="Assets/Resources/BossFight/BossFightArena.prefab";var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var rig=root.GetComponentInChildren<BossSegmentedRig>(true);var parentScale=rig.transform.parent.lossyScale;rig.transform.localScale=new Vector3(1.7f/parentScale.x,1.7f/parentScale.y,1.7f/parentScale.z);
            // Absolute desired world scale; repeat application does not accumulate scaling.
            foreach(var head in rig.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.name=="Head"))head.transform.localScale=Vector3.one*1.12f;
            // Arm motion is authored in Assets/Animations/BossFight/Arms; the old idle-sway tuning fields no longer exist.
            var views=root.GetComponentInChildren<BossArenaView>(true);var viewSerialized=new SerializedObject(views);viewSerialized.FindProperty("_GhostFraming").objectReferenceValue=rig;viewSerialized.ApplyModifiedPropertiesWithoutUndo();
            foreach(var camera in root.GetComponentsInChildren<Camera>(true))camera.rect=new Rect(0,.12f,1,.88f);
            var managerSerialized=new SerializedObject(root.GetComponentInChildren<BossArenaManager>(true));var legacyScore=(TextMesh)managerSerialized.FindProperty("_Score").objectReferenceValue;if(legacyScore)legacyScore.gameObject.SetActive(false);
            var hud=root.GetComponentInChildren<BossHudPresentation>(true);var hudSerialized=new SerializedObject(hud);
            var lives=(Text)hudSerialized.FindProperty("_Lives").objectReferenceValue;
            var hintObject=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Control and result hint").gameObject;
            var legacy=hintObject.GetComponent<Text>();var hint=hintObject.GetComponent<TextMeshProUGUI>();
            var bodyAsset=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/HalloweenUI/VarelaRound-Body.asset");if(!bodyAsset)throw new Exception("Recorder must publish and native-bake shared Varela Round body first.");
            if(legacy){UnityEngine.Object.DestroyImmediate(legacy);hint=hintObject.AddComponent<TextMeshProUGUI>();}
            if(!hint)throw new Exception("Authored hint text missing");hint.font=bodyAsset;hint.fontSharedMaterial=bodyAsset.material;hint.fontSize=24;hint.enableAutoSizing=false;hint.textWrappingMode=TextWrappingModes.Normal;hint.overflowMode=TextOverflowModes.Overflow;hint.alignment=TextAlignmentOptions.MidlineLeft;hint.raycastTarget=false;hint.color=new Color32(248,234,205,255);
            hudSerialized.FindProperty("_Hint").objectReferenceValue=hint;hudSerialized.ApplyModifiedPropertiesWithoutUndo();
            SetRect(lives.rectTransform,new Vector2(.015f,.012f),new Vector2(.29f,.10f));SetRect(hint.rectTransform,new Vector2(.31f,.012f),new Vector2(.985f,.10f));
            lives.alignment=TextAnchor.MiddleLeft; // Keep the existing gameplay score font.
            var backing=root.GetComponentsInChildren<RectTransform>(true).Single(t=>t.name=="Lives meter backing");SetRect(backing,Vector2.zero,new Vector2(1,.11f));
            PrefabUtility.SaveAsPrefabAsset(root,path,out bool saved);if(!saved)throw new Exception("Native boss prefab save failed");
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    static void SetRect(RectTransform transform,Vector2 min,Vector2 max){transform.anchorMin=min;transform.anchorMax=max;transform.offsetMin=new Vector2(12,4);transform.offsetMax=new Vector2(-12,-4);}
}
#endif
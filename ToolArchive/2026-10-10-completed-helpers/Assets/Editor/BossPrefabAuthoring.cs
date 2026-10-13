using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using HauntedFish.BossFight;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Native prefab authoring only. Never rewrites scene YAML, switches the user's scene,
// opens Play mode, simulates physics, saves user scenes or generates runtime UI/actors.
[InitializeOnLoad]
public static class BossPrefabAuthoring
{
    const string Source="Assets/Scenes/BossFight.unity";
    const string Target="Assets/Resources/BossFight/BossFightArena.prefab";
    static string Prefix=>System.IO.Path.Combine(System.IO.Path.GetTempPath(),"HauntedFishBossPerspectiveAuthoring-20261010");
    static BossPrefabAuthoring(){EditorApplication.delayCall+=AutoRun;}
    static void AutoRun(){if(File.Exists(Prefix+".request")&&!File.Exists(Prefix+".txt")) Run();}
    [MenuItem("Tools/Haunted Hotel/Author boss prefab from saved scene (isolated)")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)
        {File.WriteAllText(Prefix+".txt","SKIPPED: requires idle Edit mode; source and user scenes unchanged.");return;}
        var active=SceneManager.GetActiveScene();var selection=Selection.objects;
        var setup=EditorSceneManager.GetSceneManagerSetup();
        var dirty=Enumerable.Range(0,SceneManager.sceneCount).ToDictionary(i=>SceneManager.GetSceneAt(i).handle,i=>SceneManager.GetSceneAt(i).isDirty);
        string before=Convert.ToBase64String(SHA256.Create().ComputeHash(File.ReadAllBytes(Source)));
        Scene preview=default;GameObject wrapper=null;bool loadedPrefab=false;
        var lines=new System.Collections.Generic.List<string>();int checks=0;lines.Add("SOURCE_SHA256 "+before);
        void Check(bool valid,string label){if(!valid)throw new InvalidOperationException(label);checks++;lines.Add("PASS "+label);}
        try
        {
            if(File.Exists(Target))
            {
                wrapper=PrefabUtility.LoadPrefabContents(Target);preview=wrapper.scene;loadedPrefab=true;
            }
            else
            {
                preview=EditorSceneManager.OpenPreviewScene(Source);
                var sceneRoots=preview.GetRootGameObjects();
                wrapper=EditorUtility.CreateGameObjectWithHideFlags("BossFight Arena (authored prefab)",HideFlags.HideAndDontSave);
                SceneManager.MoveGameObjectToScene(wrapper,preview);wrapper.hideFlags=HideFlags.None;
                foreach(var root in sceneRoots)root.transform.SetParent(wrapper.transform,true);
            }
            var manager=wrapper.GetComponentsInChildren<BossArenaManager>(true).Single();
            Configure(wrapper,manager,preview);
            int rigidbodies=wrapper.GetComponentsInChildren<Rigidbody>(true).Length;
            int colliders=wrapper.GetComponentsInChildren<Collider>(true).Length;
            int sprites=wrapper.GetComponentsInChildren<SpriteRenderer>(true).Length;
            var authored=PrefabUtility.SaveAsPrefabAsset(wrapper,Target,out bool saved);
            Check(saved&&authored,"native Unity SaveAsPrefabAsset completed");
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Target);
            Check(asset&&PrefabUtility.IsPartOfPrefabAsset(asset),"arena is a native imported prefab asset");
            Check(asset.GetComponentsInChildren<Rigidbody>(true).Length==rigidbodies,"all authored Rigidbody objects retained");
            Check(asset.GetComponentsInChildren<Collider>(true).Length==colliders,"all authored collider objects retained");
            Check(asset.GetComponentsInChildren<SpriteRenderer>(true).Length==sprites,"all authored sprite objects retained");
            var arena=asset.GetComponentsInChildren<BossArenaManager>(true).Single();var serialized=new SerializedObject(arena);
            foreach(string field in new[]{"_Puck","_FishPaddle","_GhostPaddle","_Reactions","_FishSpawn","_GhostSpawn","_Camera","_Score","_Views","_Hud"})
            {var reference=serialized.FindProperty(field).objectReferenceValue;Check(reference&&AssetDatabase.GetAssetPath(reference)==Target,"internal authored prefab binding "+field);}
            var characters=asset.GetComponentsInChildren<BossCharacterPresentation>(true).Single();
            Check(characters.FishActor.GetComponentsInChildren<SpriteRenderer>(true).Length==1&&characters.GhostActor.GetComponentsInChildren<SpriteRenderer>(true).Length==1,"one standing contestant each side in native prefab");
            Check(asset.GetComponentsInChildren<BossGoal>(true).Length==2,"both goal triggers retained in native prefab");
            var idObject=asset.transform;
            Check(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(idObject,out string guid,out long rootId),"native prefab root identifier resolved");
            lines.Add("PREFAB "+Target+" guid="+guid+" rootTransformFileID="+rootId);
            lines.Add("SOURCE scene unchanged; native scene-instance integration remains a separate guarded operation.");
        }
        catch(Exception error){lines.Add("FAIL "+error);}
        finally
        {
            if(loadedPrefab&&wrapper)PrefabUtility.UnloadPrefabContents(wrapper);
            else if(preview.IsValid())EditorSceneManager.ClosePreviewScene(preview);
            try
            {
                Check(Convert.ToBase64String(SHA256.Create().ComputeHash(File.ReadAllBytes(Source)))==before,"saved boss scene bytes preserved");
                Check(SceneManager.GetActiveScene()==active,"active user scene preserved");
                var after=EditorSceneManager.GetSceneManagerSetup();
                Check(setup.Length==after.Length&&setup.Select(s=>s.path+"|"+s.isLoaded+"|"+s.isActive).SequenceEqual(after.Select(s=>s.path+"|"+s.isLoaded+"|"+s.isActive)),"loaded user scene setup preserved");
                Check(Enumerable.Range(0,SceneManager.sceneCount).All(i=>dirty.TryGetValue(SceneManager.GetSceneAt(i).handle,out bool value)&&value==SceneManager.GetSceneAt(i).isDirty),"user scene dirty flags preserved");
                Check(selection.SequenceEqual(Selection.objects),"user selection preserved");
            }
            catch(Exception error){lines.Add("FAIL cleanup "+error);}
            lines.Insert(0,"NATIVE PREFAB AUTHORING checks="+checks);
            File.WriteAllLines(Prefix+".txt",lines);
        }
    }
    static void Set(UnityEngine.Object owner,string field,UnityEngine.Object value)
    {var obj=new SerializedObject(owner);obj.FindProperty(field).objectReferenceValue=value;obj.ApplyModifiedPropertiesWithoutUndo();}
    static GameObject Make(string name,Scene scene,Transform parent,params Type[] types)
    {
        var obj=EditorUtility.CreateGameObjectWithHideFlags(name,HideFlags.HideAndDontSave,types);
        SceneManager.MoveGameObjectToScene(obj,scene);obj.hideFlags=HideFlags.None;
        obj.transform.SetParent(parent,false);return obj;
    }
    static void Configure(GameObject root,BossArenaManager manager,Scene scene)
    {
        var data=new SerializedObject(manager);
        var fish=(Camera)data.FindProperty("_Camera").objectReferenceValue;
        var characters=(BossCharacterPresentation)data.FindProperty("_Reactions").objectReferenceValue;
        var views=root.GetComponentsInChildren<BossArenaView>(true).FirstOrDefault();
        if(!views)views=Make("Boss perspective camera rigs",scene,root.transform,typeof(BossArenaView)).GetComponent<BossArenaView>();
        Transform focus=views.transform.Find("Table focus");if(!focus)focus=Make("Table focus",scene,views.transform).transform;
        focus.position=new Vector3(1000,1.7f,0);
        fish.transform.SetParent(views.transform,true);fish.name="Fish end perspective camera";
        Camera Camera(string name,float yaw,float pitch)
        {
            var existing=views.transform.Find(name);
            var camera=existing?existing.GetComponent<UnityEngine.Camera>():null;
            if(!camera){var clone=UnityEngine.Object.Instantiate(fish.gameObject,views.transform);clone.name=name;camera=clone.GetComponent<UnityEngine.Camera>();}
            camera.orthographic=false;camera.fieldOfView=55;camera.nearClipPlane=.1f;camera.farClipPlane=100;
            camera.rect=new Rect(.12f,0,.88f,1);camera.transform.rotation=Quaternion.Euler(pitch,yaw,0);camera.gameObject.SetActive(false);return camera;
        }
        fish.orthographic=false;fish.fieldOfView=55;fish.nearClipPlane=.1f;fish.farClipPlane=100;fish.rect=new Rect(.12f,0,.88f,1);fish.transform.rotation=Quaternion.Euler(35,0,0);fish.gameObject.SetActive(false);
        var ghost=Camera("Ghost end perspective camera",180,35);var spectator=Camera("Spectator perspective camera",0,60);
        Set(views,"_Fish",fish);Set(views,"_Ghost",ghost);Set(views,"_Spectator",spectator);Set(views,"_Focus",focus);Set(views,"_Characters",characters);
        var framing=new SerializedObject(views);framing.FindProperty("_FrameSize").vector3Value=new Vector3(7.3f,.7f,11.6f);framing.FindProperty("_FrameCentreY").floatValue=-.05f;
        foreach(var item in new[]{("_FishNearWalls",false),("_GhostNearWalls",true)})
        {
            var walls=root.GetComponentsInChildren<Renderer>(true).Where(r=>r.name=="Outside end wall"&&(r.transform.position.z>focus.position.z)==item.Item2).ToArray();
            var field=framing.FindProperty(item.Item1);field.arraySize=walls.Length;for(int i=0;i<walls.Length;i++)field.GetArrayElementAtIndex(i).objectReferenceValue=walls[i];
        }
        framing.ApplyModifiedPropertiesWithoutUndo();
        characters.FishActor.GetComponentInChildren<SpriteRenderer>(true).transform.localScale=Vector3.one*.6f;
        characters.GhostActor.GetComponentInChildren<SpriteRenderer>(true).transform.localScale=Vector3.one*2.2f;
        var actors=new SerializedObject(characters);
        foreach(var item in new[]{("_FishArtwork",characters.FishActor),("_GhostArtwork",characters.GhostActor)})
        {
            var renderers=item.Item2.GetComponentsInChildren<Renderer>(true);var field=actors.FindProperty(item.Item1);field.arraySize=renderers.Length;
            for(int i=0;i<renderers.Length;i++)field.GetArrayElementAtIndex(i).objectReferenceValue=renderers[i];
        }
        actors.ApplyModifiedPropertiesWithoutUndo();characters.SetViewer(BossViewerSide.Spectator);
        var hud=root.GetComponentsInChildren<BossHudPresentation>(true).FirstOrDefault();
        if(!hud)
        {
            var obj=Make("Boss lives HUD",scene,root.transform,typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(BossHudPresentation));
            hud=obj.GetComponent<BossHudPresentation>();var canvas=obj.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=20;canvas.enabled=false;
            var scaler=obj.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var backing=Make("Lives meter backing",scene,obj.transform,typeof(RectTransform),typeof(Image));var backingRect=(RectTransform)backing.transform;
            backingRect.anchorMin=Vector2.zero;backingRect.anchorMax=new Vector2(.12f,1);backingRect.offsetMin=backingRect.offsetMax=Vector2.zero;
            var image=backing.GetComponent<Image>();image.color=new Color32(27,24,34,255);image.raycastTarget=false;
            var font=((TextMesh)data.FindProperty("_Score").objectReferenceValue).font;
            Text Label(string name,float offset,float height,int size)
            {
                var label=Make(name,scene,obj.transform,typeof(RectTransform),typeof(Text)).GetComponent<Text>();var rect=(RectTransform)label.transform;
                rect.anchorMin=new Vector2(0,1);rect.anchorMax=new Vector2(.12f,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(12,-offset);rect.sizeDelta=new Vector2(-24,height);
                label.font=font;label.fontSize=size;label.alignment=TextAnchor.UpperLeft;label.resizeTextForBestFit=true;label.resizeTextMinSize=12;label.resizeTextMaxSize=size;label.color=new Color32(248,234,205,255);label.raycastTarget=false;return label;
            }
            Set(hud,"_Canvas",canvas);Set(hud,"_Lives",Label("Lives",24,230,28));Set(hud,"_Hint",Label("Control and result hint",280,240,18));
        }
        var lightningRoot=root.GetComponentsInChildren<LightningEffectManager>(true).FirstOrDefault();
        GameObject lightObject;
        if(!lightningRoot)
        {
            var template=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Storm/StormLightningAttic.prefab");
            if(!template)throw new InvalidOperationException("Authored attic lightning template missing");
            lightObject=(GameObject)PrefabUtility.InstantiatePrefab(template,scene);lightObject.transform.SetParent(root.transform,false);lightningRoot=lightObject.GetComponent<LightningEffectManager>();
        }
        else lightObject=lightningRoot.gameObject;
        lightObject.transform.position=new Vector3(1000,0,0);lightObject.SetActive(false);
        var lightData=new SerializedObject(lightningRoot);
        if(lightData.FindProperty("_AuthoredRoomLights")==null)throw new InvalidOperationException("Shared authored lightning extension must be imported before serialization");
        var roomPulse=(Light)lightData.FindProperty("_AuthoredRoomLights").GetArrayElementAtIndex(0).objectReferenceValue;
        var silhouette=(SpriteRenderer)lightData.FindProperty("_OpponentShadow").objectReferenceValue;
        silhouette.sortingOrder=-10;silhouette.color=new Color(0,0,0,.8f);silhouette.enabled=false;
        var fishArt=characters.FishActor.GetComponentInChildren<SpriteRenderer>(true);var ghostArt=characters.GhostActor.GetComponentInChildren<SpriteRenderer>(true);
        Set(lightningRoot,"_OpponentRenderer",ghostArt);
        var audio=root.GetComponentsInChildren<HauntedFish.Multiplayer.LobbyStormAudio>(true).FirstOrDefault();
        GameObject audioObject;
        if(!audio)
        {
            var template=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Storm/StormAudioAttic.prefab");
            if(!template)throw new InvalidOperationException("Authored less-muffled attic audio template missing");
            audioObject=(GameObject)PrefabUtility.InstantiatePrefab(template,scene);audioObject.transform.SetParent(root.transform,false);audio=audioObject.GetComponent<HauntedFish.Multiplayer.LobbyStormAudio>();
        }
        else audioObject=audio.gameObject;
        audioObject.transform.position=new Vector3(1000,0,0);audioObject.SetActive(false);Set(audio,"_Lightning",lightningRoot);
        var shadows=root.GetComponentsInChildren<BossOpponentShadow>(true).FirstOrDefault();
        if(!shadows)shadows=Make("Opponent storm wall anchors",scene,root.transform,typeof(BossOpponentShadow)).GetComponent<BossOpponentShadow>();
        Transform Anchor(string name,bool positive)
        {
            var anchor=shadows.transform.Find(name);if(!anchor)anchor=Make(name,scene,shadows.transform).transform;
            var wall=root.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r=>r.name=="Outside end wall"&&(r.transform.position.z>focus.position.z)==positive);
            float z=wall?(positive?wall.bounds.min.z-.05f:wall.bounds.max.z+.05f):(positive?9.7f:-9.7f);
            anchor.SetPositionAndRotation(new Vector3(1000,wall?wall.bounds.center.y:3.5f,z),Quaternion.Euler(0,positive?0:180,0));return anchor;
        }
        Set(shadows,"_FishWallAnchor",Anchor("Fish opponent wall",false));Set(shadows,"_GhostWallAnchor",Anchor("Ghost opponent wall",true));
        Set(shadows,"_Lightning",lightningRoot);Set(shadows,"_RoomPulse",roomPulse);Set(shadows,"_Shadow",silhouette);Set(shadows,"_FishOpponent",fishArt);Set(shadows,"_GhostOpponent",ghostArt);Set(views,"_StormShadows",shadows);
        var storm=root.GetComponentsInChildren<BossStormPresentation>(true).FirstOrDefault();
        if(!storm)storm=Make("Boss storm room lifecycle",scene,root.transform,typeof(BossStormPresentation)).GetComponent<BossStormPresentation>();
        Set(storm,"_Lightning",lightningRoot);Set(storm,"_Audio",audio);Set(storm,"_LightningRoot",lightObject);Set(storm,"_AudioRoot",audioObject);Set(views,"_Storm",storm);
        Set(manager,"_Views",views);Set(manager,"_Hud",hud);
        var oldScore=(TextMesh)data.FindProperty("_Score").objectReferenceValue;oldScore.gameObject.SetActive(false);
        views.Select(BossViewerSide.Ghost,false);views.Select(BossViewerSide.Spectator,false);views.Select(BossViewerSide.Fish,false);
        Set(lightningRoot,"_OpponentRenderer",ghostArt);
        if(!lightningRoot.ValidateAuthoredBindings(out string bindingError))throw new InvalidOperationException("Boss attic authored bindings: "+bindingError);
        if(!roomPulse||!silhouette||!fishArt||!ghostArt||!lightData.FindProperty("_FlashCanvas").objectReferenceValue)
            throw new InvalidOperationException("Boss attic requires room light, flash Canvas, one shadow and both opponent artwork bindings");
        if(root.GetComponentsInChildren<LightningEffectManager>(true).Length!=1||root.GetComponentsInChildren<HauntedFish.Multiplayer.LobbyStormAudio>(true).Length!=1)
            throw new InvalidOperationException("Boss attic requires exactly one strike owner and one audio owner");
        if(new SerializedObject(audio).FindProperty("_Lightning").objectReferenceValue!=lightningRoot)
            throw new InvalidOperationException("Boss attic audio must share its visual strike owner");
    }}
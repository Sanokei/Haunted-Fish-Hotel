using System;
using System.Collections.Generic;
using System.IO;
using IOPath = System.IO.Path;
using System.Linq;
using System.Reflection;
using HauntedFish.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// One-shot isolated native checks in the existing editor. Never opens a scene
// into the main hierarchy, enters Play mode, saves assets, or exits the editor.
public static class TrapPlacementIsolatedValidation
{
    static void RequireIdleEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Requires idle Edit mode outside prefab editing.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save or discard scene changes before explicitly running this tool.");
    }

    static readonly string Folder=IOPath.Combine(IOPath.GetTempPath(),"hfh-trap-validation");
    static object Get(object value,string name)=>value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);
    static void Set(object value,string name,object data)=>value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(value,data);
    static void Call(object value,string name,params object[] args)=>value.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(value,args);
    [MenuItem("Tools/Haunted Hotel/Validate traps in isolated preview scene")]
    public static void Run()
    {
        RequireIdleEditMode();
        var mainScene=SceneManager.GetActiveScene();bool dirty=mainScene.isDirty;
        var gameField=typeof(GameSceneController).GetField("<Current>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic);
        var oldGame=gameField.GetValue(null);
        var cameraField=typeof(HotelViewCamera).GetField("_Current",BindingFlags.Static|BindingFlags.NonPublic);
        var oldCamera=cameraField.GetValue(null);
        Scene preview=default;GhostPlacementWorld world=null;GhostPlacementController controls=null;
        int count=0;var results=new List<string>();
        void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);count++;results.Add("PASS "+message);}
        try
        {
            preview=EditorSceneManager.OpenPreviewScene("Assets/Scenes/Game.unity");
            var roots=preview.GetRootGameObjects();
            var scene=roots.SelectMany(r=>r.GetComponentsInChildren<GameSceneController>(true)).Single();
            world=roots.SelectMany(r=>r.GetComponentsInChildren<GhostPlacementWorld>(true)).Single();
            controls=world.GetComponent<GhostPlacementController>();
            gameField.SetValue(null,scene);
            controls.Configure(null,scene);world.BindControls(controls);
            var camera=roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).First(c=>c.name=="Main Camera");
            cameraField.SetValue(null,camera);
            world.enabled=true;
            world.InjectDefinitions(new[]{AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Traps/ShoppingCartTrap.prefab").GetComponent<GhostTrap>(),AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Traps/FallingChandelierTrap.prefab").GetComponent<GhostTrap>()});
            world.BeginRound("editor-preview","isolated-trap-check",1);
            var cart=world.Definition("cart");var chandelier=world.Definition("chandelier");
            Check(cart&&chandelier&&world.HasGraceZone,"Authored scene exposes real catalog and grace prohibition");
            Check(!world.PlacementPoint(new Vector3(-10,2,0),"cart",out _),"Native canonical preview rejects grace area");
            Check(world.PlacementPoint(new Vector3(6,3,0),"chandelier",out var basePoint),"Native canonical ceiling placement is clear");
            var center=basePoint+chandelier.ArmedOffset+chandelier.ColliderOffset;
            Check(Mathf.Abs(center.y+chandelier.BodyHalfSize.y-scene.HallwayCeilingY)<.002f,"Final ceiling body fits authored hallway ceiling");
            Check(Mathf.Abs(basePoint.y+chandelier.ColliderOffset.y-chandelier.BodyHalfSize.y-scene.HallwayBounds.max.y)<.002f,"Final landing body fits authored hallway floor");
            var sprite=(SpriteRenderer)Get(controls,"_PreviewSprite");
            sprite.sprite=chandelier.Icon;
            Check(sprite.sprite,"Authored chandelier artwork is available");
            var size=sprite.sprite.bounds.size;
            float scale=Mathf.Min(chandelier.ObjectSize.x/Mathf.Max(.001f,size.x),Mathf.Min(chandelier.ObjectSize.y,chandelier.BodyHalfSize.y*2)/Mathf.Max(.001f,size.y));
            sprite.transform.localScale=new Vector3(scale,scale,1);
            sprite.transform.localPosition=new Vector3(0,chandelier.BodyHalfSize.y-sprite.sprite.bounds.max.y*scale,0);
            var carried=(GameObject)Get(controls,"_Preview");carried.transform.position=new Vector3(6,2,0);carried.SetActive(true);
            Call(controls,"PresentPlacementPreview",chandelier,basePoint,true);
            var destination=(GameObject)Get(controls,"_PlacementPreview");
            var destinationSprite=(SpriteRenderer)Get(controls,"_PlacementPreviewSprite");
            Check(destination.activeInHierarchy&&Vector3.Distance(destination.transform.position,center)<.002f,"Real destination presenter uses canonical final body center");
            var opaque=chandelier.GetComponentInChildren<GhostTrapAreaPresentation>(true).OpaqueArtBounds;
            float opaqueTop=destinationSprite.sprite.bounds.min.y+destinationSprite.sprite.bounds.size.y*opaque.yMax;
            var top=destinationSprite.transform.TransformPoint(new Vector3(0,opaqueTop,0));
            Check(Mathf.Abs(top.y-scene.HallwayCeilingY)<.002f,"Actual destination opaque-art top is ceiling snapped");
            Check(carried.transform.position==new Vector3(6,2,0),"Carried artwork remains attached to ghost flight");
            Call(controls,"PresentPlacementPreview",chandelier,basePoint,false);
            Check(destinationSprite.color.r==HotelPalette.Rust.r&&destinationSprite.color.a>0,"Invalid destination uses rejected-placement color");
            Call(controls,"HidePlacementPreview");Check(!destination.activeSelf,"Destination cleanup hides preview");
            // Actual offline-authority objects exercise production TryPlace; no
            // transport, timing, input, or play-mode lifecycle is simulated here.
            var avatar=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/HotelNetworkPlayer.prefab"),preview);
            var player=avatar.GetComponent<HotelPlayer>();player.Networked=false;
            Set(player,"_Movement",avatar.GetComponent<HotelPlayerMovement>());
            avatar.GetComponent<GameSideScrollMotor>().Bind(scene);
            Set(player,"_EditorRole",1);player.RoundIntroComplete=true;player.RoundStateKey="isolated-trap-check";player.RoundVersion=1;
            avatar.GetComponent<CharacterController>().enabled=false;
            Check(world.Hold(player,"cart",new Vector3(2,1,0)),"Real offline authority holds inventory");
            Check(world.PlacementPoint(new Vector3(2,1,0),"cart",out var point),"First request begins with a clear canonical preview");
            Check(world.TryPlace(player,point,"cart")&&world.Count==1&&player.HeldTrapFamily=="","First authoritative placement commits and spends its held resource");
            var trap=world.Trap(0);
            Check(!world.PlacementPoint(point,"cart",out _),"Canonical preview rejects committed footprint without physics synchronization");
            Check(world.Hold(player,"cart",point),"Second request retains a held resource");
            Set(world,"_NextPlacement",0f);
            Check(!world.TryPlace(player,point,"cart")&&world.Count==1&&player.HeldTrapFamily=="cart","Competing authoritative request rejects overlap independently of cooldown and retains inventory");
            var edge=point+Vector3.right*(cart.BodyHalfSize.x*2);
            Check(world.TrapFootprintClear(cart,edge),"Native registry allows adjacent footprints");
            Check(!world.TrapFootprintClear(cart,edge-Vector3.right*.001f),"Native registry rejects full-body thin intersections");
            Check(!world.TrapFootprintClear(cart,point,90),"Native registry rejects rotated intersections");
            Check(world.TrapFootprintClear(cart,point,0,trap.Index),"Reset validator excludes its own footprint");
            var presentation=trap.GetComponent<GhostTrapAreaPresentation>();
            if(!presentation)presentation=trap.GetComponentInChildren<GhostTrapAreaPresentation>(true);
            Call(presentation,"Awake");
            Call(presentation,"LateUpdate");
            var art=(SpriteRenderer[])Get(presentation,"_WorldArt");
            Check(art.Length>=2&&art.All(a=>a.sortingLayerID==0&&a.sortingOrder<=-10),"Actual trap world renderers sort below authored fish body and fin");
            var root=(GameObject)Get(presentation,"_Root");
            Check(root.GetComponentsInChildren<Image>(true).All(i=>!i.enabled&&!i.raycastTarget),"Overlay trap art is disabled and does not intercept UI");
            Call(presentation,"OnDisable");Check(art.All(a=>!a.enabled),"Disabling presentation hides all owned world renderers");
            Check(SceneManager.GetActiveScene()==mainScene&&mainScene.isDirty==dirty,"Main editor scene and dirty state remain unchanged");
            results.Add("PASS "+count+" isolated native edit-mode checks. Full host/transport/play-mode and pixel QA not executed.");
        }
        finally
        {
            gameField.SetValue(null,oldGame);cameraField.SetValue(null,oldCamera);
            if(preview.IsValid())
            {
                // Edit-mode teardown must be immediate; production owns deferred
                // destruction in Play mode. Clear test-only handles first.
                foreach(var presentation in preview.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GhostTrapAreaPresentation>(true)))
                {
                    var white=(Sprite)Get(presentation,"_WhiteSprite");if(white)UnityEngine.Object.DestroyImmediate(white);
                    Set(presentation,"_WhiteSprite",null);Set(presentation,"_WorldArt",null);
                }
                if(controls)
                {
                    var destination=(GameObject)Get(controls,"_PlacementPreview");
                    Set(controls,"_PlacementPreview",null);if(destination)UnityEngine.Object.DestroyImmediate(destination);
                }
                if(world)
                {
                    ((Dictionary<int,GhostTrap>)Get(world,"_Objects")).Clear();
                    ((GhostCubeSnapshot)Get(world,"_State")).Cubes.Clear();
                }
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }
        Directory.CreateDirectory(Folder);File.WriteAllLines(IOPath.Combine(Folder,"native-result.txt"),results);Debug.Log(results.Last());
    }
}
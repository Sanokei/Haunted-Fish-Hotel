#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using HauntedFish.BossFight;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;

public static class BossSegmentedAuthoring
{
    const string Art="Assets/Art/Boss/",RigAsset="Assets/Resources/BossFight/BossSegmentedOpponent.prefab",ArenaAsset="Assets/Resources/BossFight/BossFightArena.prefab";
    const float Ppu=512;
    static readonly int[] LeftOrder={3,0,2,1},RightOrder={2,3,1,0};
    static readonly float[][] Joints={new[]{1166.61f,602.5f,1267.49f,941.5f,844f,1378.99f,1210.5f,1073.5f},new[]{1214.5f,598.7f,1247.5f,948.06f,825f,1397f,1211f,1094.5f}};
    static void Set(UnityEngine.Object owner,string field,UnityEngine.Object value)
    {var data=new SerializedObject(owner);data.FindProperty(field).objectReferenceValue=value;data.ApplyModifiedPropertiesWithoutUndo();}
    static void Refs(UnityEngine.Object owner,string field,UnityEngine.Object[] values)
    {var data=new SerializedObject(owner);var list=data.FindProperty(field);list.arraySize=values.Length;for(int i=0;i<values.Length;i++)list.GetArrayElementAtIndex(i).objectReferenceValue=values[i];data.ApplyModifiedPropertiesWithoutUndo();}
    static Sprite Import(string name,float pivotX,float pivotY=0)
    {
        var importer=(TextureImporter)AssetImporter.GetAtPath(Art+name);
        if(!importer)throw new InvalidOperationException("Boss source art is not imported: "+name);
        importer.GetSourceTextureWidthAndHeight(out int width,out int height);
        int croppedHeight=Mathf.Min(1714,height);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit=Ppu;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.maxTextureSize=4096;importer.textureCompression=TextureImporterCompression.CompressedHQ;
        var factories=new SpriteDataProviderFactories();factories.Init();
        var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
        var prior=provider.GetSpriteRects();string spriteName=System.IO.Path.GetFileNameWithoutExtension(name);
        var rect=new SpriteRect{name=spriteName,rect=new Rect(0,height-croppedHeight,width,croppedHeight),alignment=SpriteAlignment.Custom,pivot=new Vector2(pivotX,pivotY),spriteID=prior.Length==1?prior[0].spriteID:UnityEditor.GUID.Generate()};
        provider.SetSpriteRects(new[]{rect});var names=provider.GetDataProvider<ISpriteNameFileIdDataProvider>();names.SetNameFileIdPairs(new[]{new SpriteNameFileIdPair(rect.name,rect.spriteID)});provider.Apply();importer.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(Art+name).OfType<Sprite>().Single();
    }
    static SpriteRenderer Part(string name,Transform parent,Sprite sprite,Material material,int sorting)
    {
        var obj=new GameObject(name,typeof(SpriteRenderer));obj.transform.SetParent(parent,false);
        var render=obj.GetComponent<SpriteRenderer>();render.sprite=sprite;render.sharedMaterial=material;render.sortingOrder=sorting;render.shadowCastingMode=ShadowCastingMode.Off;render.receiveShadows=false;return render;
    }
    [MenuItem("Tools/Haunted Hotel/Author segmented boss opponent (explicit)")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||PrefabStageUtility.GetCurrentPrefabStage()!=null)throw new InvalidOperationException("Requires exclusively owned idle Edit mode.");
        var setup=EditorSceneManager.GetSceneManagerSetup();
        for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Preserve unsaved user scenes; authoring refused.");
        if(File.Exists(RigAsset))throw new InvalidOperationException("Segmented boss prefab already exists: preserve it and review before another authoring pass.");
        var loaded=PrefabUtility.LoadPrefabContents(ArenaAsset);GameObject root=null;var preview=default(UnityEngine.SceneManagement.Scene);
        try
        {
            var characters=loaded.GetComponentInChildren<BossCharacterPresentation>(true);var arena=loaded.GetComponentInChildren<BossArenaManager>(true);
            var old=characters.GhostActor.GetComponentsInChildren<SpriteRenderer>(true);
            if(old.Length!=1)throw new InvalidOperationException("Saved ghost presentation changed; refusing broad replacement.");
            var material=old[0].sharedMaterial;
            int bodies=loaded.GetComponentsInChildren<Rigidbody>(true).Length,colliders=loaded.GetComponentsInChildren<Collider>(true).Length;
            preview=EditorSceneManager.NewPreviewScene();root=new GameObject("Segmented Boss Opponent",typeof(BossSegmentedRig));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,preview);
            var rig=root.GetComponent<BossSegmentedRig>();var variants=new BossGhostVariant[3];var all=new System.Collections.Generic.List<Renderer>();
            for(int v=0;v<3;v++)
            {
                string prefix=v==2?"BOSS-v3-god-":"BOSS-v1v2-";
                string headName=v==0?"BOSS-v1-neutral-head.png":v==1?"BOSS-v2-angry-head.png":"BOSS-v3-god-head.png";
                var variant=new GameObject("v"+(v+1)+" - "+(3-v)+" ghost lives");variant.transform.SetParent(root.transform,false);
                var head=Part("Head",variant.transform,Import(headName,.5f),material,5);
                var arms=new Transform[8];var artwork=new Renderer[9];artwork[0]=head;
                for(int a=0;a<8;a++)
                {
                    bool left=a<4;int number=a%4+1;float joint=Joints[v==2?1:0][a];
                    var part=Part((left?"Left":"Right")+" tentacle "+number,variant.transform,Import(prefix+"arm-"+(left?"left":"right")+"-"+number+".png",joint/2048),material,1+(left?LeftOrder:RightOrder)[number-1]);
                    part.transform.localPosition=new Vector3((left?joint-2048:joint)/Ppu,0,(a+1)*.0002f);arms[a]=part.transform;artwork[a+1]=part;
                }
                variants[v]=new BossGhostVariant{Root=variant,Head=head.transform,Arms=arms,Artwork=artwork};all.AddRange(artwork);
            }
            var shadow2=Import("BOSS-v2-angry-full.png",.5f,.5f);var shadow3=Import("BOSS-v3-god-full.png",.5f,.5f);
            var source=Part("Silhouette reference - never camera rendered",root.transform,shadow2,material,-10);source.forceRenderingOff=true;source.transform.localPosition=Vector3.up*(1714/(2*Ppu));
            var serialized=new SerializedObject(rig);var array=serialized.FindProperty("_Variants");array.arraySize=3;
            for(int v=0;v<3;v++)
            {
                var item=array.GetArrayElementAtIndex(v);item.FindPropertyRelative("Root").objectReferenceValue=variants[v].Root;item.FindPropertyRelative("Head").objectReferenceValue=variants[v].Head;
                var arms=item.FindPropertyRelative("Arms");arms.arraySize=8;for(int j=0;j<8;j++)arms.GetArrayElementAtIndex(j).objectReferenceValue=variants[v].Arms[j];
                var art=item.FindPropertyRelative("Artwork");art.arraySize=9;for(int j=0;j<9;j++)art.GetArrayElementAtIndex(j).objectReferenceValue=variants[v].Artwork[j];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();Set(rig,"_ShadowSource",source);Refs(rig,"_ShadowSprites",new UnityEngine.Object[]{shadow2,shadow2,shadow3});Refs(rig,"_AllArtwork",all.Cast<UnityEngine.Object>().ToArray());rig.SetLifeStage(0);rig.SetVisible(true);
            if(!rig.ValidateAuthoredBindings(out string error))throw new Exception(error);
            var asset=PrefabUtility.SaveAsPrefabAsset(root,RigAsset,out bool saved);if(!saved)throw new Exception("Native segmented prefab save failed");
            foreach(var renderer in old)
            {
                var go=renderer.gameObject;
                if(go.GetComponents<Component>().Length==2&&go.transform.childCount==0)UnityEngine.Object.DestroyImmediate(go);
                else UnityEngine.Object.DestroyImmediate(renderer); // Retain any additional user components.
            }
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(asset,loaded.scene);instance.transform.SetParent(characters.GhostActor,false);
            var parentScale=characters.GhostActor.lossyScale;instance.transform.localScale=new Vector3(1/parentScale.x,1/parentScale.y,1/parentScale.z);
            var centre=arena.Bounds.center;instance.transform.SetPositionAndRotation(new Vector3(centre.x,centre.y,Mathf.Max(characters.GhostActor.position.z,arena.Bounds.max.z+1.5f)),Quaternion.identity);
            var authored=instance.GetComponent<BossSegmentedRig>();authored.SetLifeStage(0);authored.SetVisible(true);
            Set(characters,"_GhostRig",authored);Refs(characters,"_GhostArtwork",authored.AllArtwork.Cast<UnityEngine.Object>().ToArray());
            var shadowAdapter=loaded.GetComponentInChildren<BossOpponentShadow>(true);Set(shadowAdapter,"_GhostRig",authored);Set(shadowAdapter,"_GhostOpponent",authored.ShadowSource);
            var lightning=loaded.GetComponentInChildren<LightningEffectManager>(true);Set(lightning,"_OpponentRenderer",authored.ShadowSource);
            if(bodies!=loaded.GetComponentsInChildren<Rigidbody>(true).Length||colliders!=loaded.GetComponentsInChildren<Collider>(true).Length)throw new Exception("Boss physics changed during visual authoring");
            File.Copy(ArenaAsset,System.IO.Path.Combine(System.IO.Path.GetTempPath(),"HauntedFishBossArena.before-segmented.prefab"),true);
            PrefabUtility.SaveAsPrefabAsset(loaded,ArenaAsset,out saved);if(!saved)throw new Exception("Native arena prefab save failed");
            Debug.Log("SEGMENTED BOSS AUTHORING PASS: three authored variants, nine visible parts each; unchanged physics and scene files.");
        }
        finally
        {
            if(root)UnityEngine.Object.DestroyImmediate(root);if(preview.IsValid())EditorSceneManager.ClosePreviewScene(preview);PrefabUtility.UnloadPrefabContents(loaded);
            if(!setup.Select(s=>s.path+"|"+s.isLoaded+"|"+s.isActive).SequenceEqual(EditorSceneManager.GetSceneManagerSetup().Select(s=>s.path+"|"+s.isLoaded+"|"+s.isActive)))throw new Exception("User scene setup changed during authoring");
        }
    }
}
#endif
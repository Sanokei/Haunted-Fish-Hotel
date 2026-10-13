#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using HauntedFish.BossFight;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class BossSegmentedNativeValidation
{
    static T Get<T>(object owner,string field)=>(T)owner.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);
    [MenuItem("Tools/Haunted Hotel/Validate segmented boss variants (isolated)")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||PrefabStageUtility.GetCurrentPrefabStage()!=null)throw new InvalidOperationException("Requires exclusively owned idle Edit mode.");
        var before=EditorSceneManager.GetSceneManagerSetup();var selection=Selection.objects;
        string folder="Tools/BossFightTests/Evidence/segmented-boss-native";Directory.CreateDirectory(folder);
        var lines=new List<string>();int checks=0;GameObject root=null;
        void Check(bool pass,string label){if(!pass)throw new Exception(label);checks++;lines.Add("PASS "+label);}
        try
        {
            root=PrefabUtility.LoadPrefabContents("Assets/Resources/BossFight/BossFightArena.prefab");
            var arena=root.GetComponentInChildren<BossArenaManager>(true);var characters=root.GetComponentInChildren<BossCharacterPresentation>(true);var rig=characters.GhostActor.GetComponentInChildren<BossSegmentedRig>(true);
            Check(rig&&rig.ValidateAuthoredBindings(out string error),"complete authored segmented rig");
            Check(root.GetComponentsInChildren<BossSegmentedRig>(true).Length==1,"one boss opponent rig");
            Check(rig.GetComponentsInChildren<Collider>(true).Length==0&&rig.GetComponentsInChildren<Rigidbody>(true).Length==0,"art does not add gameplay colliders or bodies");
            var view=root.GetComponentInChildren<BossArenaView>(true);int hierarchy=root.GetComponentsInChildren<Transform>(true).Length;
            var data=new BossArenaSnapshot{Epoch=100,Revision=1,FishId=101,GhostId=202,Active=true,ParticipantsReady=true,PuckPosition=new Vector3(1000,1.8f,0),FishPaddlePosition=new Vector3(1000,1.7f,-3.6f),GhostPaddlePosition=new Vector3(1000,1.7f,3.6f)};
            arena.SetAuthority(false);
            foreach(int goals in new[]{0,1,2})
            {
                data.FishScore=goals;data.Revision++;Check(arena.ApplySnapshot(JsonUtility.ToJson(data)),"authoritative snapshot applies ghost life stage "+goals);
                Check(rig.VariantIndex==goals,"snapshot life stage maps to v"+(goals+1));
                Check(rig.ActiveArtwork.Length==9&&rig.AllArtwork.Length==27,"nine segments in selected variant; 27 authored once");
                var visibleHead=rig.ActiveArtwork.OfType<SpriteRenderer>().Single(r=>r.name=="Head");
                string head=goals==0?"BOSS-v1-neutral-head":goals==1?"BOSS-v2-angry-head":"BOSS-v3-god-head";
                Check(visibleHead.sprite.name==head,"correct supplied head source "+head);
                Check(rig.ShadowSource.sprite.name==(goals==2?"BOSS-v3-god-full":"BOSS-v2-angry-full"),"full-reference silhouette matches stage");
                foreach(uint viewer in new uint[]{101,202,303})
                {
                    arena.SetLocalViewer(viewer,true);
                    int visible=rig.AllArtwork.Count(r=>r.enabled&&r.gameObject.activeInHierarchy&&!r.forceRenderingOff);
                    Check(visible==(viewer==202?0:9),"every segment follows local ownership visibility "+viewer);
                    Check(rig.ShadowSource.forceRenderingOff,"full-reference source never duplicates visible opponent");
                    if(viewer==202)continue;
                    foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(720,1280)})
                    {
                        var camera=arena.Camera;var rt=new RenderTexture(size.x,size.y,24);rt.Create();camera.targetTexture=rt;camera.scene=root.scene;view.Fit();
                        var canvas=Get<Canvas>(root.GetComponentInChildren<BossHudPresentation>(true),"_Canvas");var previousMode=canvas.renderMode;var previousCamera=canvas.worldCamera;
                        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.5f;
                        var old=RenderTexture.active;var texture=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
                        try
                        {
                            foreach(var render in rig.ActiveArtwork)Check(render.bounds.min.z>arena.Bounds.max.z+.8f,"segment remains behind far goal plane");
                            camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,size.x,size.y),0,0);texture.Apply();var visiblePixels=texture.GetPixels32();
                            string name="v"+(goals+1)+"-"+(viewer==101?"Fish":"Spectator")+"-"+size.x+"x"+size.y;
                            File.WriteAllBytes(System.IO.Path.Combine(folder,name+".png"),texture.EncodeToPNG());
                            rig.SetVisible(false);camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,size.x,size.y),0,0);texture.Apply();var baseline=texture.GetPixels32();rig.SetVisible(true);
                            int changed=0,sampled=0;
                            foreach(float x in new[]{-2f,0,2f})foreach(float z in new[]{-3.6f,0,3.6f})
                            {
                                var point=camera.WorldToScreenPoint(new Vector3(1000+x,1.65f,z));
                                for(int dy=-10;dy<=10;dy++)for(int dx=-10;dx<=10;dx++)
                                {
                                    int px=Mathf.Clamp(Mathf.RoundToInt(point.x)+dx,0,size.x-1),py=Mathf.Clamp(Mathf.RoundToInt(point.y)+dy,0,size.y-1),i=py*size.x+px;sampled++;
                                    if(Mathf.Abs(visiblePixels[i].r-baseline[i].r)>2||Mathf.Abs(visiblePixels[i].g-baseline[i].g)>2||Mathf.Abs(visiblePixels[i].b-baseline[i].b)>2)changed++;
                                }
                            }
                            lines.Add("OCCLUSION "+name+" boardChanged="+changed+" sampled="+sampled);Check(changed<=4,"rendered playable board patches remain unobstructed "+name);
                        }
                        finally{canvas.renderMode=previousMode;canvas.worldCamera=previousCamera;rig.SetVisible(true);RenderTexture.active=old;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(rt);}
                    }
                }
            }
            Check(root.GetComponentsInChildren<Transform>(true).Length==hierarchy,"life/view changes construct no hierarchy");
            lines.Add("LIMIT: isolated native snapshot/view/render checks; run physical and shared-storm regressions separately.");
        }
        catch(Exception error){lines.Add("FAIL "+error);}
        finally
        {
            if(root)PrefabUtility.UnloadPrefabContents(root);
            Check(before.Select(s=>s.path+"|"+s.isLoaded+"|"+s.isActive).SequenceEqual(EditorSceneManager.GetSceneManagerSetup().Select(s=>s.path+"|"+s.isLoaded+"|"+s.isActive)),"user scene setup preserved");
            Check(selection.SequenceEqual(Selection.objects),"user selection preserved");
            lines.Insert(0,"SEGMENTED BOSS NATIVE checks="+checks);File.WriteAllLines(System.IO.Path.Combine(folder,"results.txt"),lines);
        }
    }
}
#endif
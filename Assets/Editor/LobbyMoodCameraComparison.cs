// STAGED ONLY. Manual invocation after exclusive Unity grant. No auto callbacks.
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
public static class LobbyMoodCameraComparison {
 const string E=@"C:\Users\wkeif\Documents\Codex\2026-10-09\task\LobbyMoodPreparation";
 public static void Before(){Run("before");} public static void After(){Run("after");}
 static void Run(string phase){
 if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)throw new Exception("Idle clean Edit Mode required");
 for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Preserve unsaved scenes first");
 string prior=SceneManager.GetActiveScene().path;
 try{
 var scene=EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity",OpenSceneMode.Single);
 var all=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).ToArray();
 var c=all.Select(x=>x.GetComponent<Camera>()).Single(x=>x&&x.name=="Main Camera");var d=c.GetComponent<UniversalAdditionalCameraData>();if(!d)throw new Exception("Missing URP camera data");
 var world=all.SelectMany(x=>x.GetComponents<Component>()).Single(x=>x&&x.GetType().Name=="LobbyIntroductionWorldSequence");var s=new SerializedObject(world);
 var entry=(Transform)s.FindProperty("_EntryCamera").objectReferenceValue;var reception=(Transform)s.FindProperty("_ReceptionCamera").objectReferenceValue;if(!entry||!reception)throw new Exception("Missing authored anchors");
 var r=new StringBuilder("Edit Mode authored-camera renders; gameplay Play still requires separate acceptance.\n");
 r.AppendLine("PP="+d.renderPostProcessing+" VolumeMask="+d.volumeLayerMask.value+" HDR="+c.allowHDR+" renderer="+d.scriptableRenderer.GetType().Name);
 r.AppendLine("Fog="+RenderSettings.fog+" mode="+RenderSettings.fogMode+" start="+RenderSettings.fogStartDistance+" end="+RenderSettings.fogEndDistance);
 foreach(var v in all.Select(x=>x.GetComponent<Volume>()).Where(x=>x))r.AppendLine("Volume="+v.name+" active="+v.isActiveAndEnabled+" weight="+v.weight+" layer="+v.gameObject.layer+" profile="+AssetDatabase.GetAssetPath(v.sharedProfile));
 foreach(var g in all.SelectMany(x=>x.GetComponents<Renderer>()).SelectMany(x=>x.sharedMaterials).Where(x=>x).GroupBy(x=>x.shader.name))r.AppendLine("Shader="+g.Key+" slots="+g.Count());
 Directory.CreateDirectory(E);var p=c.transform.position;var q=c.transform.rotation;float fov=c.fieldOfView;bool fog=RenderSettings.fog;
 try{
 Shot(c,phase,"gameplay-saved",r); View(c,new Vector3(0,1.7f,-3),new Vector3(0,1.1f,6),phase,"normal-entry",r); View(c,new Vector3(0,1.7f,2),new Vector3(0,1.1f,6),phase,"normal-reception",r); View(c,new Vector3(-3,1.7f,-1),new Vector3(-7,1.4f,-.4f),phase,"normal-radio",r); View(c,new Vector3(3,1.7f,0),new Vector3(6.5f,3,1.3f),phase,"normal-grandma",r); View(c,new Vector3(-2,1.7f,4),new Vector3(-6,2,10),phase,"normal-stairs",r); View(c,new Vector3(1,1.7f,3),new Vector3(6.2f,1.8f,6.7f),phase,"normal-theatre",r);
 var vc=s.FindProperty("_Camera").objectReferenceValue;if(!vc)throw new Exception("Missing Cinemachine camera");var vs=new SerializedObject(vc);var lens=vs.FindProperty("Lens.FieldOfView")??vs.FindProperty("m_Lens.FieldOfView");if(lens==null)throw new Exception("Resolve authored Cinemachine lens");c.fieldOfView=lens.floatValue;
 c.transform.SetPositionAndRotation(entry.position,entry.rotation);Shot(c,phase,"intro-entry",r);
 c.transform.SetPositionAndRotation(reception.position,reception.rotation);Shot(c,phase,"intro-reception",r);
 }finally{c.transform.SetPositionAndRotation(p,q);c.fieldOfView=fov;RenderSettings.fog=fog;}
 File.WriteAllText(System.IO.Path.Combine(E,phase+"-camera-compatibility.txt"),r.ToString());
 // No SaveScene or SaveAssets: diagnostic poses and toggles never persist.
 }finally{if(!string.IsNullOrEmpty(prior)&&prior!="Assets/Scenes/Lobby.unity")EditorSceneManager.OpenScene(prior,OpenSceneMode.Single);}
 }
 static void View(Camera c,Vector3 p,Vector3 target,string phase,string view,StringBuilder r){c.transform.position=p;c.transform.LookAt(target);Shot(c,phase,view,r);}
 static void Shot(Camera c,string phase,string view,StringBuilder r){r.AppendLine(view+" pos="+c.transform.position+" rot="+c.transform.rotation+" FOV="+c.fieldOfView);Capture(c,phase+"-"+view+".png");bool fog=RenderSettings.fog;try{RenderSettings.fog=false;Capture(c,phase+"-"+view+"-fog-off.png");RenderSettings.fog=true;Capture(c,phase+"-"+view+"-fog-on.png");}finally{RenderSettings.fog=fog;}}
 static void Capture(Camera c,string name){const int w=1600,h=900;var rt=new RenderTexture(w,h,24,RenderTextureFormat.ARGBHalf);rt.Create();var old=c.targetTexture;var active=RenderTexture.active;Texture2D t=null;try{c.targetTexture=rt;c.Render();RenderTexture.active=rt;t=new Texture2D(w,h,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,w,h),0,0);t.Apply();File.WriteAllBytes(System.IO.Path.Combine(E,name),t.EncodeToPNG());}finally{c.targetTexture=old;RenderTexture.active=active;if(t)UnityEngine.Object.DestroyImmediate(t);rt.Release();UnityEngine.Object.DestroyImmediate(rt);}}
}


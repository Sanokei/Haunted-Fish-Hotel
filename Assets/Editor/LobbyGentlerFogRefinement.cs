// STAGED ONLY after independent visual review. Publish/run only after next exclusive Lobby grant.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class LobbyGentlerFogRefinement {
 const string E=@"C:\Users\wkeif\Documents\Codex\2026-10-09\task\LobbyMoodPreparation";
 public static void Apply(){
 if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)throw new Exception("Idle Edit Mode required");
 for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Preserve unrelated unsaved scenes first");
 const string path="Assets/Scenes/Lobby.unity";File.Copy(path,System.IO.Path.Combine(E,"Lobby-before-gentler-fog.unity"),true);
 var prior=SceneManager.GetActiveScene().path;
 try{
 var scene=EditorSceneManager.OpenScene(path);var root=scene.GetRootGameObjects().Single(x=>x.name=="Lobby indoor practical fill");
 var reception=root.GetComponentsInChildren<Light>(true).Single(x=>x.name=="Reception warm pool");reception.intensity=6.5f;EditorUtility.SetDirty(reception);
 RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=6;RenderSettings.fogEndDistance=26;
 // Proposed local threshold lights: native views must confirm their placement and intensity.
 Point(root,"Stair arch threshold",new Vector3(-5,2.7f,5.8f),new Color(.88f,.8f,.57f),1.5f,3.3f);
 Point(root,"Theatre arch threshold",new Vector3(5.3f,2.5f,5.5f),new Color(.75f,.78f,.6f),1.4f,3.4f);
 EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Lobby save failed");
 File.WriteAllText(System.IO.Path.Combine(E,"gentler-fog-authored.txt"),"Proposed scene fog6..26, reception pool6.5, local arch threshold pools authored. Acceptance requires fresh actual Play renders; stair void must stay dark while desk/lamp/bell/treads/exits remain readable. No profile, pipeline, camera pose, geometry or runtime code edits.");
 }finally{if(!string.IsNullOrEmpty(prior)&&prior!=path)EditorSceneManager.OpenScene(prior);}
 }
 static void Point(GameObject root,string name,Vector3 position,Color color,float intensity,float range){var found=root.transform.Find(name);GameObject go=found?found.gameObject:new GameObject(name);if(!found)go.transform.SetParent(root.transform);go.transform.position=position;var light=go.GetComponent<Light>();if(!light)light=go.AddComponent<Light>();light.type=LightType.Point;light.lightmapBakeType=LightmapBakeType.Realtime;light.color=color;light.intensity=intensity;light.range=range;light.shadows=LightShadows.None;EditorUtility.SetDirty(light);}
}

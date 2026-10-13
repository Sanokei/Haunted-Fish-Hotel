#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class GameAtticDoorArtAuthoring
{
 const string Art="Assets/Art/Hallway/HALLWAY-Attic-Door-Open-panel.png";
 const string ScenePath="Assets/Scenes/Game.unity";
 const string Evidence="C:/Users/wkeif/Documents/Codex/2026-10-09/task-3/attic-door-art-native";
 public static void Run()
 {
  if(!Application.isBatchMode||!Environment.GetCommandLineArgs().Contains("--game-attic-door-art"))throw new Exception("Explicit owned editor command required");
  Directory.CreateDirectory(Evidence);
  for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new Exception("Refusing dirty editor scene");
  var importer=(TextureImporter)AssetImporter.GetAtPath(Art);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=100;importer.spritePivot=Vector2.one*.5f;var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.SaveAndReimport();
  var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Art);if(!sprite)throw new Exception("Requested authored sprite import required");
  var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
  var stairs=all.Single(t=>t.name=="Hallway end stairs to ghost challenge");var colliders=stairs.GetComponentsInChildren<Collider>(true);var before=colliders.Select(c=>EditorJsonUtility.ToJson(c)+EditorJsonUtility.ToJson(c.transform)).ToArray();
  foreach(var renderer in stairs.GetComponentsInChildren<MeshRenderer>(true))renderer.enabled=false;
  if(stairs.Find("Authored open attic door panel"))throw new Exception("Already bound; do not create duplicate");
  var go=new GameObject("Authored open attic door panel");go.transform.SetParent(stairs,false);
  // Preserve the 1172x1054 source aspect. Its opaque X bounds are 296..890,
  // centered seven pixels to the right of the source canvas midpoint.
  const float height=4;float pixelScale=height/1054f;go.transform.localPosition=new Vector3(-7*pixelScale,height*.5f,1.88f);
  go.transform.localScale=Vector3.one*(height/(1054f/100));
  var view=go.AddComponent<SpriteRenderer>();view.sprite=sprite;view.color=Color.white;view.sortingOrder=0;
  view.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
  if(!view.sharedMaterial){var door=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/HallwayDoor.prefab").GetComponentInChildren<SpriteRenderer>();view.sharedMaterial=door.sharedMaterial;}
  for(int i=0;i<colliders.Length;i++)if(before[i]!=EditorJsonUtility.ToJson(colliders[i])+EditorJsonUtility.ToJson(colliders[i].transform))throw new Exception("Stair collision geometry changed");
  if(Mathf.Abs(stairs.position.x-51.8f)>.001f)throw new Exception("Current 68-unit end position changed");
  EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Scene save failed");
  File.WriteAllText(System.IO.Path.Combine(Evidence,"binding-result.txt"),"PASS requested attic-door panel authored at end; uniform source scale, 4m high; opaque center X51.8; original stair transforms/colliders unchanged; cream mesh renderers hidden; entrance trigger and cursor untouched. Native render acceptance still required.");
 }
 static void Render()
 {
  var scene=EditorSceneManager.OpenPreviewScene(ScenePath);
  try{
   var roots=scene.GetRootGameObjects();var all=roots.SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();var camera=all.SelectMany(t=>t.GetComponents<Camera>()).First(c=>c.name=="Main Camera");camera.scene=scene;
   Capture(camera,new Vector3(51.4f,2.055f,-10),2.5f,1280,720,"attic-door-end.png");
   Capture(camera,new Vector3(19,2.055f,-10),2.9f,3200,256,"hallway-final.png");
  }finally{EditorSceneManager.ClosePreviewScene(scene);}
 }
 static void Capture(Camera camera,Vector3 point,float size,int width,int height,string name)
 {
  camera.transform.SetPositionAndRotation(point,Quaternion.identity);camera.orthographic=true;camera.orthographicSize=size;
  var rt=new RenderTexture(width,height,24);var image=new Texture2D(width,height,TextureFormat.RGB24,false);var previous=RenderTexture.active;
  try{camera.targetTexture=rt;camera.aspect=(float)width/height;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(System.IO.Path.Combine(Evidence,name),image.EncodeToPNG());}
  finally{camera.targetTexture=null;RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);}
 }
 public static void VerifyFinalAuthoredBatch(){try{Render();GameHallwayNativeValidation.Run();}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
 public static void VerifyAllBatch(){try{Run();Render();ActiveTrapJumpDiagnostic.Run();GameHallwayNativeValidation.Run();}catch(Exception e){Directory.CreateDirectory(Evidence);File.WriteAllText(System.IO.Path.Combine(Evidence,"error.txt"),e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}}
 public static void RunBatch(){try{Run();EditorApplication.Exit(0);}catch(Exception e){Directory.CreateDirectory(Evidence);File.WriteAllText(System.IO.Path.Combine(Evidence,"error.txt"),e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}}
}
#endif
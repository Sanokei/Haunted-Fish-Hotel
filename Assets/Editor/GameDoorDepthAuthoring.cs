#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
public static class GameDoorDepthAuthoring
{
 const string MeshPath="Assets/Art/BlenderDepthImports/HallwayDoorTopUnity.asset";
 const string PrefabPath="Assets/Resources/HallwayDoor.prefab";
 const string Materials="Assets/Art/Materials/GameDoorDepth";
 const string Evidence=@"C:\Users\wkeif\Documents\Codex\2026-10-09\task-3\door-depth-native";
 static void Require(bool value,string message){if(!value)throw new Exception(message);}
 public static void Run()
 {
  Require(Application.isBatchMode&&Environment.GetCommandLineArgs().Contains("--game-door-depth"),"Explicit owned editor command required");
  Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Idle Edit Mode required");
  Directory.CreateDirectory(Evidence);
  var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
  Require(mesh&&mesh.subMeshCount==3,"Shared imported canonical door mesh required");
  var b=mesh.bounds;
  Require(Mathf.Abs(b.min.z)<.001f&&Mathf.Abs(b.max.z-.08f)<.001f,"Shared owner must normalize front Z0, depth +Z first");
  Require(Mathf.Abs(b.size.x-1.516106f)<.001f&&Mathf.Abs(b.size.y-.410943f)<.001f,"Natural trimmed wooden contour dimensions");
  var vertices=mesh.vertices;
  var fronts=Enumerable.Range(0,3).Where(s=>mesh.GetTriangles(s).All(i=>Mathf.Abs(vertices[i].z)<.001f)).ToArray();
  Require(fronts.Length==1,"One verified planar front material slot");
  if(!AssetDatabase.IsValidFolder(Materials))AssetDatabase.CreateFolder("Assets/Art/Materials","GameDoorDepth");
  var shader=Shader.Find("Universal Render Pipeline/Lit");Require(shader,"URP Lit shader");
  var wood=MaterialAt("Crown_WarmWood",shader);
  wood.SetColor("_BaseColor",new Color(.29803922f,.20392157f,.14509804f,1));
  wood.SetFloat("_Metallic",0);wood.SetFloat("_Smoothness",.2f);
  var front=MaterialAt("Crown_FrontRetainedSprite",shader);
  front.SetColor("_BaseColor",new Color(1,1,1,0));front.SetFloat("_Surface",1);front.SetFloat("_Blend",0);
  front.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);front.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
  front.SetFloat("_ZWrite",0);front.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
  front.SetOverrideTag("RenderType","Transparent");front.renderQueue=3000;front.SetShaderPassEnabled("ShadowCaster",false);
  var root=PrefabUtility.LoadPrefabContents(PrefabPath);
  try
  {
   var sprite=root.GetComponentsInChildren<SpriteRenderer>(true).Single();
   Require(sprite.enabled&&AssetDatabase.GetAssetPath(sprite.sprite)=="Assets/Art/Hallway/HALLWAY-Door-full.png","Retained original full door front art");
   string original=EditorJsonUtility.ToJson(sprite);var originalTransform=EditorJsonUtility.ToJson(sprite.transform);
   var socket=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name.StartsWith("Door top mesh attachment"));
   Require(socket.childCount==0,"Unbound authored crown socket required; do not duplicate existing binding");
   socket.localPosition=new Vector3(.01987259335f,.427845245f,.0001f);
   var depth=new GameObject("Door crown solid depth - original front retained");depth.transform.SetParent(socket,false);
   depth.transform.localPosition=new Vector3(-.01636566511085181f,0,0);
   depth.transform.localScale=Vector3.one;depth.transform.localRotation=Quaternion.identity;
   depth.AddComponent<MeshFilter>().sharedMesh=mesh;
   var renderer=depth.AddComponent<MeshRenderer>();var slots=new[]{wood,wood,wood};slots[fronts[0]]=front;
   renderer.sharedMaterials=slots;renderer.shadowCastingMode=ShadowCastingMode.TwoSided;renderer.receiveShadows=true;
   Require(EditorJsonUtility.ToJson(sprite)==original&&EditorJsonUtility.ToJson(sprite.transform)==originalTransform,"Original sprite properties and scale preserved");
   PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
   File.WriteAllText(System.IO.Path.Combine(Evidence,"binding-result.txt"),"PASS authored single shared crown binding inherited by 13 hallway door instances; original sprite unchanged; unit mesh scale; seven source pixel alignment correction; mesh front +.0001 behind sprite; front slot="+fronts[0]+"; mesh bounds="+b+"\nNative scene/render checks still required.");
   AssetDatabase.SaveAssets();
  }
  finally{PrefabUtility.UnloadPrefabContents(root);}
 }
 static Material MaterialAt(string name,Shader shader)
 {
  string path=Materials+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
  if(!material){material=new Material(shader){name=name};AssetDatabase.CreateAsset(material,path);}EditorUtility.SetDirty(material);return material;
 }
 static void ValidateRender()
 {
  var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/Game.unity");
  try {
   var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
   var crowns=all.Where(t=>t.name=="Door crown solid depth - original front retained").OrderBy(t=>t.position.x).ToArray();Require(crowns.Length==13,"Thirteen authored crowns inherited by Game");
   foreach(var crown in crowns){Require(crown.localScale==Vector3.one,"Unit crown scale");Require(crown.GetComponent<MeshFilter>().sharedMesh==AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath),"Shared mesh reused");var b=crown.GetComponent<Renderer>().bounds;Require(b.max.z<2&&b.min.z>1.9f,"Crown fits sprite-to-wall gap");}
   Require(all.SelectMany(t=>t.GetComponents<AudioListener>()).Count(l=>l.enabled&&l.gameObject.activeInHierarchy)==1,"One existing active Game listener");
   var cam=all.SelectMany(t=>t.GetComponents<Camera>()).First(c=>c.name=="Main Camera");cam.scene=scene;cam.enabled=false;
   var first=crowns[0];var center=first.GetComponent<Renderer>().bounds.center;
   Capture(cam,center+new Vector3(0,0,-3),center,"door-front.png",true,.65f);
   foreach(var c in crowns)c.GetComponent<Renderer>().enabled=false;
   Capture(cam,center+new Vector3(0,0,-3),center,"door-front-original.png",true,.65f);
   foreach(var c in crowns)c.GetComponent<Renderer>().enabled=true;
   Capture(cam,center+new Vector3(1.4f,.3f,-2),center,"door-oblique.png",false,.65f);
   Capture(cam,new Vector3(19,2.055f,-10),new Vector3(19,2.055f,2),"hallway-68-door-depth.png",true,2.9f,3200,256);
   File.WriteAllText(System.IO.Path.Combine(Evidence,"scene-result.txt"),"PASS 13 shared authored crowns; unit scale; all solid depth fits behind source art before wall; one active listener; native front/original/oblique/full hallway renders. Protected serialized assets checked externally.");
  }finally{EditorSceneManager.ClosePreviewScene(scene);}
 }
 static void Capture(Camera camera,Vector3 position,Vector3 target,string name,bool ortho,float size,int width=1200,int height=700)
 {
  camera.transform.position=position;camera.transform.LookAt(target);camera.orthographic=ortho;camera.orthographicSize=size;camera.fieldOfView=38;camera.transparencySortMode=TransparencySortMode.CustomAxis;camera.transparencySortAxis=Vector3.forward;camera.nearClipPlane=.05f;camera.farClipPlane=200;
  var rt=new RenderTexture(width,height,24);var texture=new Texture2D(width,height,TextureFormat.RGB24,false);var previous=RenderTexture.active;
  try{camera.targetTexture=rt;camera.aspect=(float)width/height;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();File.WriteAllBytes(System.IO.Path.Combine(Evidence,name),texture.EncodeToPNG());}
  finally{camera.targetTexture=null;RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(rt);}
 }
 public static void RenderBatch(){try{ValidateRender();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
 public static void RunBatch(){try{Run();ValidateRender();EditorApplication.Exit(0);}catch(Exception e){Directory.CreateDirectory(Evidence);File.WriteAllText(System.IO.Path.Combine(Evidence,"binding-error.txt"),e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}}
}
#endif
#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using HauntedFish.BossFight;
// Inert staged native authoring. Called only after confirmed idle ownership and safe unpack.
public static class BossTrayArtworkAuthoring
{
 const string City="Assets/Art/UrbanCity/";
 public static GameObject Building(Transform parent,bool broken)
 {
  var root=new GameObject(broken?"Broken architectural wall and roof sections":"Miniature haunted building - actual UrbanCity art");root.transform.SetParent(parent,false);
  for(int i=0;i<4;i++)
  {
   var wall=Source(i==0?"wallA_door.obj":"wallA_flatWindow.obj",root.transform,.58f,.62f);
   wall.transform.localRotation=Quaternion.Euler(broken?25+i*13:0,i*90,broken?(i%2==0?20:-20):0);
   wall.transform.localPosition=new Vector3(i==1?.29f:i==3?-.29f:0,broken?.02f:0,i==0?-.29f:i==2?.29f:0);
   if(broken)wall.transform.localPosition*=1.35f;
  }
  var roof=Source("roofMetal_typeA.obj",root.transform,.62f,.18f);roof.transform.localPosition=broken?new Vector3(.38f,.02f,.38f):new Vector3(0,.62f,0);if(broken)roof.transform.localRotation=Quaternion.Euler(70,30,25);
  return root;
 }
 public static GameObject Tray(Transform parent,bool ghost,Material timber,Material trim)
 {
  var root=new GameObject(ghost?"Ghost side tray - three authored cells":"Fish side tray - three authored cells");root.transform.SetParent(parent,false);root.transform.position=new Vector3(1000+(ghost?4.15f:-4.15f),1.62f,ghost?2:-2);
  Cube("Raised tray deck",root.transform,new Vector3(0,-.10f,0),new Vector3(1.05f,.18f,3.3f),timber);
  foreach(float x in new[]{-.53f,.53f})Cube("Outer tray lip",root.transform,new Vector3(x,.015f,0),new Vector3(.065f,.11f,3.35f),trim);
  foreach(float z in new[]{-1.64f,-.5f,.5f,1.64f})Cube("Cell divider",root.transform,new Vector3(0,.015f,z),new Vector3(1.08f,.11f,.065f),trim);
  foreach(float x in new[]{-.38f,.38f})foreach(float z in new[]{-1.25f,1.25f})Cube("Raised side-platform leg",root.transform,new Vector3(x,-.46f,z),new Vector3(.12f,.7f,.12f),timber);
  return root;
 }
 public static void Guard()
 {if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)throw new Exception("Confirmed idle editor ownership required");for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Preserve user unsaved scenes before authoring");}
 static GameObject Source(string file,Transform parent,float width,float height)
 {
  var source=AssetDatabase.LoadAssetAtPath<GameObject>(City+file);if(!source)throw new Exception("Missing verified art: "+City+file);var go=(GameObject)PrefabUtility.InstantiatePrefab(source);go.transform.SetParent(parent,false);go.transform.localPosition=Vector3.zero;
  foreach(var collider in go.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(collider);foreach(var body in go.GetComponentsInChildren<Rigidbody>(true))UnityEngine.Object.DestroyImmediate(body);
  var renders=go.GetComponentsInChildren<Renderer>(true);if(renders.Length==0)throw new Exception("Imported art has no renderer: "+file);var bounds=renders[0].bounds;foreach(var render in renders)bounds.Encapsulate(render.bounds);
  float scale=Mathf.Min(width/Mathf.Max(bounds.size.x,bounds.size.z,.001f),height/Mathf.Max(bounds.size.y,.001f));var centre=bounds.center-go.transform.position;float bottom=bounds.min.y-go.transform.position.y;go.transform.localScale*=scale;go.transform.localPosition=new Vector3(-centre.x*scale,-bottom*scale,-centre.z*scale);return go;
 }
 static void Cube(string name,Transform parent,Vector3 position,Vector3 size,Material material)
 {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;}
}
#endif

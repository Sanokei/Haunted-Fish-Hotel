#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using HauntedFish.BossFight;
using HauntedFish.Multiplayer;
public static class BossDefenseAuthoring
{
 const string ArenaPath="Assets/Resources/BossFight/BossFightArena.prefab";
 const string DefenseFolder="Assets/Resources/BossFight/Defenses";
 static readonly string[] Sources={"Assets/Art/LiquorBottles/Assets/BeerBottle_SM.fbx","Assets/Art/CanOfTuna/CanOfTuna.fbx","Assets/Art/BlenderDepthImports/LobbyRadioWoodDepth.prefab"};
 public static void Apply()
 {
  Guard();if(!AssetDatabase.IsValidFolder(DefenseFolder))AssetDatabase.CreateFolder("Assets/Resources/BossFight","Defenses");
  var black=MaterialAsset("Crack",Color.black);var green=MaterialAsset("Valid",new Color(.2f,.9f,.4f));var red=MaterialAsset("Invalid",new Color(.95f,.2f,.15f));
  var bounce=AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(DefenseFolder+"/DefenseBounce.physicMaterial");
  if(!bounce){bounce=new PhysicsMaterial("Defense bounce");AssetDatabase.CreateAsset(bounce,DefenseFolder+"/DefenseBounce.physicMaterial");}
  bounce.bounciness=1;bounce.dynamicFriction=bounce.staticFriction=0;bounce.bounceCombine=PhysicsMaterialCombine.Maximum;EditorUtility.SetDirty(bounce);
  var root=PrefabUtility.LoadPrefabContents(ArenaPath);
  try
  {
   var manager=root.GetComponentInChildren<BossArenaManager>(true);var m=new SerializedObject(manager);
   var puck=(Rigidbody)m.FindProperty("_Puck").objectReferenceValue;var fish=(Rigidbody)m.FindProperty("_FishPaddle").objectReferenceValue;var ghost=(Rigidbody)m.FindProperty("_GhostPaddle").objectReferenceValue;
   fish.constraints=ghost.constraints=RigidbodyConstraints.FreezePositionY|RigidbodyConstraints.FreezeRotation;
   puck.constraints=RigidbodyConstraints.FreezePositionY|RigidbodyConstraints.FreezeRotationX|RigidbodyConstraints.FreezeRotationZ;
   var prior=root.transform.Find("Authored defense rack");if(prior)UnityEngine.Object.DestroyImmediate(prior.gameObject);
   var rack=new GameObject("Authored defense rack");rack.transform.SetParent(root.transform,false);
   var board=rack.AddComponent<BossDefenseBoard>();var drag=rack.AddComponent<BossDragInput>();var pieces=new BossDefensePiece[6];
   for(int i=0;i<6;i++)
   {
    int kind=i%3;float radius=kind==0?.28f:kind==1?.4f:.45f;var model=AssetDatabase.LoadAssetAtPath<GameObject>(Sources[kind]);if(!model)throw new Exception("Missing actual 3D Arts source: "+Sources[kind]);
    var pieceRoot=new GameObject((i<3?"Fish ":"Ghost ")+((BossDefenseKind)kind)+" defense");pieceRoot.transform.SetParent(rack.transform,false);pieceRoot.transform.position=new Vector3(1000+(i<3?-3.85f:3.85f),1.62f,(i<3?-1:1)*(1+kind));
    var rb=pieceRoot.AddComponent<Rigidbody>();rb.isKinematic=true;rb.useGravity=false;rb.constraints=RigidbodyConstraints.FreezePositionY|RigidbodyConstraints.FreezeRotation;
    var shape=pieceRoot.AddComponent<CapsuleCollider>();shape.radius=radius;shape.height=1;shape.center=new Vector3(0,.42f,0);shape.sharedMaterial=bounce;shape.enabled=false;
    var pickup=pieceRoot.AddComponent<CapsuleCollider>();pickup.radius=radius+.08f;pickup.height=1.15f;pickup.center=shape.center;pickup.isTrigger=true;
    var healthy=Model(model,"Healthy source 3D art",pieceRoot.transform,radius);var crack1=Crack("Crack stage one",pieceRoot.transform,radius,black,0);var crack2=Crack("Crack stage two",pieceRoot.transform,radius,black,1);
    var broken=new GameObject("Broken 3D fragments");broken.transform.SetParent(pieceRoot.transform,false);
    // Mesh-bearing source art supplies every fracture fragment; all states exist in the prefab before gameplay.
    for(int f=0;f<3;f++){var fragment=Model(model,"Source art fragment "+f,broken.transform,radius*.42f);fragment.transform.localPosition=new Vector3((f-1)*radius*.9f,.03f,(f%2==0?1:-1)*radius*.45f);fragment.transform.localRotation=Quaternion.Euler(70+f*17,23+f*49,25+f*28);}
    var valid=Model(model,"Valid placement preview",pieceRoot.transform,radius);Tint(valid,green);var invalid=Model(model,"Invalid placement preview",pieceRoot.transform,radius);Tint(invalid,red);
    foreach(var state in new[]{crack1,crack2,broken,valid,invalid})state.SetActive(false);
    var piece=pieceRoot.AddComponent<BossDefensePiece>();var s=new SerializedObject(piece);s.FindProperty("_Slot").intValue=i;Ref(s,"_Body",rb);Ref(s,"_Collision",shape);Ref(s,"_Pickup",pickup);Ref(s,"_Healthy",healthy);Ref(s,"_Crack1",crack1);Ref(s,"_Crack2",crack2);Ref(s,"_Broken",broken);Ref(s,"_ValidPreview",valid);Ref(s,"_InvalidPreview",invalid);s.ApplyModifiedPropertiesWithoutUndo();pieces[i]=piece;
   }
   var b=new SerializedObject(board);Ref(b,"_Puck",puck);Ref(b,"_FishPaddle",fish);Ref(b,"_GhostPaddle",ghost);var a=b.FindProperty("_Pieces");a.arraySize=6;for(int i=0;i<6;i++)a.GetArrayElementAtIndex(i).objectReferenceValue=pieces[i];b.ApplyModifiedPropertiesWithoutUndo();
   var d=new SerializedObject(drag);Ref(d,"_Arena",manager);Ref(d,"_Board",board);Ref(d,"_FishStriker",fish);Ref(d,"_GhostStriker",ghost);d.ApplyModifiedPropertiesWithoutUndo();var v=new SerializedObject(root.GetComponentInChildren<BossArenaView>(true));Ref(v,"_DefenseFraming",rack.transform);v.ApplyModifiedPropertiesWithoutUndo();Ref(m,"_Defenses",board);Ref(m,"_DragInput",drag);m.ApplyModifiedPropertiesWithoutUndo();
   if(!board.ValidateAuthoredBindings(out string error)||!drag.ValidateAuthoredBindings(out error))throw new Exception(error);
   PrefabUtility.SaveAsPrefabAsset(root,ArenaPath,out bool saved);if(!saved)throw new Exception("Boss prefab save failed");
  }finally{PrefabUtility.UnloadPrefabContents(root);}AssetDatabase.SaveAssets();
 }
 public static void AppendOwnedPlayerAdapter()
 {
  Guard();const string path="Assets/Resources/HotelNetworkPlayer.prefab";var root=PrefabUtility.LoadPrefabContents(path);
  try{var before=root.GetComponents<Component>().ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));if(!root.GetComponent<BossDefenseRequest>())root.AddComponent<BossDefenseRequest>();foreach(var pair in before)if(EditorJsonUtility.ToJson(pair.Key)!=pair.Value)throw new Exception("Existing shared player component changed");PrefabUtility.SaveAsPrefabAsset(root,path,out bool saved);if(!saved)throw new Exception("Player adapter append failed");}finally{PrefabUtility.UnloadPrefabContents(root);}
 }
 static GameObject Model(GameObject source,string name,Transform parent,float radius)
 {
  var go=(GameObject)PrefabUtility.InstantiatePrefab(source);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=Vector3.zero;go.transform.localRotation=Quaternion.identity;
  foreach(var c in go.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);foreach(var r in go.GetComponentsInChildren<Rigidbody>(true))UnityEngine.Object.DestroyImmediate(r);
  var renderers=go.GetComponentsInChildren<Renderer>(true);if(renderers.Length==0)throw new Exception("3D source has no renderers");var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
  float scale=radius*1.65f/Mathf.Max(bounds.size.x,bounds.size.z,.001f);scale=Mathf.Min(scale,.88f/Mathf.Max(bounds.size.y,.001f));var offset=bounds.center-go.transform.position;go.transform.localScale*=scale;go.transform.localPosition=new Vector3(-offset.x*scale,-(bounds.min.y-go.transform.position.y)*scale,-offset.z*scale);return go;
 }
 static GameObject Crack(string name,Transform parent,float radius,Material black,int stage)
 {
  var root=new GameObject(name);root.transform.SetParent(parent,false);for(int i=0;i<3;i++){var line=GameObject.CreatePrimitive(PrimitiveType.Cube);line.name="Authored crack contour "+i;line.transform.SetParent(root.transform,false);line.transform.localPosition=new Vector3((i-1)*radius*.3f,.2f+i*.16f,-radius*.82f);line.transform.localScale=new Vector3(.022f,.27f,.022f);line.transform.localRotation=Quaternion.Euler(0,stage*60,(i%2==0?1:-1)*(30+stage*16));UnityEngine.Object.DestroyImmediate(line.GetComponent<Collider>());line.GetComponent<Renderer>().sharedMaterial=black;}return root;
 }
 static void Tint(GameObject go,Material material){foreach(var r in go.GetComponentsInChildren<Renderer>(true))r.sharedMaterials=Enumerable.Repeat(material,r.sharedMaterials.Length).ToArray();}
 static Material MaterialAsset(string name,Color color){string path=DefenseFolder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){var shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");m=new Material(shader);AssetDatabase.CreateAsset(m,path);}m.color=color;EditorUtility.SetDirty(m);return m;}
 static void Ref(SerializedObject s,string field,UnityEngine.Object value){var p=s.FindProperty(field);if(p==null)throw new Exception("Missing compiled authored field "+field);p.objectReferenceValue=value;}
 static void Guard(){if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)throw new Exception("Explicit idle exclusive Boss Unity grant required");}
}
#endif

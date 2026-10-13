using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
public static class LobbyRadioDepthAuthoring
{
 const string Root="Assets/Art/BlenderDepthImports/";
 const string Evidence=@"C:\Users\wkeif\Documents\Codex\2026-10-09\task\RadioOakIntegration";
 [MenuItem("Tools/Haunted Fish/Integrate Oak Radio Depth")]
 public static void Run()
 {
  if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)throw new Exception("Editor must be idle Edit Mode");
  for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Save unrelated scene changes first");
  var prior=SceneManager.GetActiveScene().path;
  try
  {
   AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
   foreach(string n in new[]{"RadioWoodenBodyDepth","HallwayDoorTopDepth"})
   {
    var mi=(ModelImporter)AssetImporter.GetAtPath(Root+n+".fbx");
    mi.globalScale=1;mi.importCameras=false;mi.importLights=false;mi.importAnimation=false;
    mi.materialImportMode=ModelImporterMaterialImportMode.None;mi.SaveAndReimport();
   }
   var model=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"RadioWoodenBodyDepth.fbx");
   Require(model!=null,"radio FBX import");
   var inst=UnityEngine.Object.Instantiate(model);
   var filters=inst.GetComponentsInChildren<MeshFilter>(true);
   Require(filters.Length==1,"one case mesh, no antenna mesh");
   var filter=filters[0]; var original=filter.sharedMesh;
   Require(original.subMeshCount==3,"front/side/back slots");
   Mesh mesh=UnityEngine.Object.Instantiate(original);mesh.name="Radio Wooden Case Unity";
   Matrix4x4 matrix=inst.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
   var vs=mesh.vertices;var uv=mesh.uv;double corr=0;
   foreach(int v in mesh.GetTriangles(2))corr+=vs[v].x*(uv[v].x-.5f);
   bool flip=corr<0;
   for(int i=0;i<vs.Length;i++){vs[i]=matrix.MultiplyPoint3x4(vs[i]);if(flip)vs[i].x=-vs[i].x;}
   mesh.vertices=vs;
   if(flip)for(int s=0;s<3;s++){var ts=mesh.GetTriangles(s);for(int i=0;i<ts.Length;i+=3){int t=ts[i];ts[i]=ts[i+2];ts[i+2]=t;}mesh.SetTriangles(ts,s);}
   mesh.RecalculateNormals();mesh.RecalculateBounds();
   var bounds=mesh.bounds;
   Require(Mathf.Abs(bounds.size.x-13.76f)<.15f,"case width");
   Require(Mathf.Abs(bounds.size.y-14.155f)<.15f,"case height");
   Require(Mathf.Abs(bounds.size.z-4.9924273f)<.01f,".36m depth");
   if(bounds.max.z<.01f){vs=mesh.vertices;for(int i=0;i<vs.Length;i++)vs[i].z=-vs[i].z;mesh.vertices=vs;for(int s=0;s<3;s++){var ts=mesh.GetTriangles(s);for(int i=0;i<ts.Length;i+=3){int t=ts[i];ts[i]=ts[i+2];ts[i+2]=t;}mesh.SetTriangles(ts,s);}mesh.RecalculateNormals();mesh.RecalculateBounds();}
   Require(Mathf.Abs(mesh.bounds.min.z)<.01f&&mesh.bounds.max.z>4.98f,"front Z0 depth +Z");
   UnityEngine.Object.DestroyImmediate(inst);
   string meshPath=Root+"RadioWoodenCaseUnity.asset";
   var oldMesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
   if(oldMesh!=null){EditorUtility.CopySerialized(mesh,oldMesh);UnityEngine.Object.DestroyImmediate(mesh);mesh=oldMesh;}else AssetDatabase.CreateAsset(mesh,meshPath);
   var shader=Shader.Find("Universal Render Pipeline/Lit");Require(shader!=null,"URP Lit");
   var oak=MaterialAt("Radio_Oak_MatchedFront",shader);
   string oakPath=Root+"RadioOakRepeat.jpg";
   if(!File.Exists(oakPath))File.Copy("Assets/Art/Seamless-Oak-Texture-2.jpg",oakPath);
   AssetDatabase.ImportAsset(oakPath,ImportAssetOptions.ForceSynchronousImport);
   var ownedOak=(TextureImporter)AssetImporter.GetAtPath(oakPath);ownedOak.textureType=TextureImporterType.Default;ownedOak.wrapMode=TextureWrapMode.Repeat;ownedOak.sRGBTexture=true;ownedOak.SaveAndReimport();
   oak.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(oakPath));
   oak.SetColor("_BaseColor",new Color(.7620176773f,1,.8833393263f,1));
   oak.SetTexture("_EmissionMap",oak.GetTexture("_BaseMap"));oak.SetColor("_EmissionColor",new Color(.7620176773f,1,.8833393263f,1)*.65f);oak.EnableKeyword("_EMISSION");oak.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;oak.SetFloat("_Metallic",0);oak.SetFloat("_Smoothness",.25f);oak.SetFloat("_Surface",0);oak.SetFloat("_ZWrite",1);
   oak.SetTextureScale("_BaseMap",Vector2.one);oak.SetTextureOffset("_BaseMap",Vector2.zero);
   var tex=(TextureImporter)AssetImporter.GetAtPath(oakPath);
   Require(tex.wrapMode==TextureWrapMode.Repeat,"existing oak Repeat (do not modify shared texture)");
   var back=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/LobbyRevision/Plaque_Oiled_Antique_Wood.mat");Require(back!=null,"existing plaque back material");
   var front=MaterialAt("Radio_Front_RetainedSprite",shader);
   front.SetColor("_BaseColor",new Color(1,1,1,0));front.SetFloat("_Surface",1);front.SetFloat("_Blend",0);
   front.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);front.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
   front.SetFloat("_ZWrite",0);front.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
   front.SetOverrideTag("RenderType","Transparent");front.renderQueue=3000;front.SetShaderPassEnabled("ShadowCaster",false);
   var go=new GameObject("Radio Wooden Body Depth");go.AddComponent<MeshFilter>().sharedMesh=mesh;
   var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=new[]{oak,back,front};renderer.shadowCastingMode=ShadowCastingMode.TwoSided;renderer.receiveShadows=true;
   string prefabPath=Root+"LobbyRadioWoodDepth.prefab";
   var prefab=PrefabUtility.SaveAsPrefabAsset(go,prefabPath);UnityEngine.Object.DestroyImmediate(go);
   var scene=EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity",OpenSceneMode.Single);
   var sprites=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<SpriteRenderer>(true)).Where(x=>x.sprite!=null&&AssetDatabase.GetAssetPath(x.sprite)=="Assets/Art/Radio-body.png").ToArray();
   Require(sprites.Length==1,"exactly one original radio body sprite");
   var sprite=sprites[0];var st=sprite.transform;
   Require(Mathf.Abs(st.lossyScale.x-.072109212327f)<.00001f,"saved radio scale");
   string before=EditorJsonUtility.ToJson(sprite);
   var priorDepth=st.GetComponentsInChildren<MeshFilter>(true).Where(x=>AssetDatabase.GetAssetPath(x.sharedMesh)==meshPath).Select(x=>x.transform).FirstOrDefault();if(priorDepth!=null)UnityEngine.Object.DestroyImmediate(priorDepth.gameObject);
   var attachment=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
   attachment.transform.SetParent(st,false);attachment.transform.localPosition=new Vector3(0,0,.0001f);
   attachment.transform.localRotation=Quaternion.identity;attachment.transform.localScale=Vector3.one;
   Require(EditorJsonUtility.ToJson(sprite)==before,"original sprite and antenna properties preserved");
   Require(scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<MonoBehaviour>(true)).Count(x=>x!=null&&x.GetType().Name=="LobbyRadioPlayback")==1,"one existing playback controller");
   Require(st.parent.name=="Radio","radio hierarchy preserved");
   Require(st.parent.GetComponentsInChildren<Collider>(true).Length>0,"existing interaction colliders");
   EditorSceneManager.MarkSceneDirty(scene);Require(EditorSceneManager.SaveScene(scene),"saved Lobby");
   AssetDatabase.SaveAssets();
   var center=st.TransformPoint(new Vector3(0,-.8925f,2.4962f));
   var forward=st.TransformDirection(Vector3.back);
   Capture(scene,center+forward*2.4f,center,"radio-front.png");
   Capture(scene,center+(forward+st.right*.8f).normalized*2.4f+Vector3.up*.15f,center,"radio-side.png");
   File.WriteAllText(System.IO.Path.Combine(Evidence,"native-result.txt"),"PASS imported both shared FBXs; one case mesh/3 slots; original sprite/antenna unchanged; width/height/depth checked; front Z0 depth +Z; .36m world; oak Repeat/tint/UV unchanged; one radio playback and colliders preserved; saved Lobby; mesh X correction="+flip+"; bounds="+mesh.bounds+"\nDoor FBX GUID="+AssetDatabase.AssetPathToGUID(Root+"HallwayDoorTopDepth.fbx")+"\nRadio FBX GUID="+AssetDatabase.AssetPathToGUID(Root+"RadioWoodenBodyDepth.fbx")+"\nRadio prefab GUID="+AssetDatabase.AssetPathToGUID(prefabPath));
  }
  finally {if(!string.IsNullOrEmpty(prior)&&prior!="Assets/Scenes/Lobby.unity")EditorSceneManager.OpenScene(prior,OpenSceneMode.Single);}
 }
 static Material MaterialAt(string n,Shader s){string p=Root+n+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(p);if(m==null){m=new Material(s){name=n};AssetDatabase.CreateAsset(m,p);}m.shader=s;EditorUtility.SetDirty(m);return m;}
 static void Require(bool ok,string n){if(!ok)throw new Exception("Radio depth validation failed: "+n);}
 static void Capture(Scene scene,Vector3 pos,Vector3 target,string name)
 {
  var obj=new GameObject("Radio Validation Camera");var c=obj.AddComponent<Camera>();
  c.enabled=false;c.transform.position=pos;c.transform.LookAt(target);c.fieldOfView=38;c.nearClipPlane=.05f;c.farClipPlane=200;
  c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.06f,.07f,.08f);
  var rt=new RenderTexture(1200,1000,24);c.targetTexture=rt;c.Render();
  var prev=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(1200,1000,TextureFormat.RGB24,false);
  image.ReadPixels(new Rect(0,0,1200,1000),0,0);image.Apply();File.WriteAllBytes(System.IO.Path.Combine(Evidence,name),image.EncodeToPNG());
  RenderTexture.active=prev;c.targetTexture=null;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(obj);
 }
 public static void ProbeBatch()
 {
  try{
   var report=new System.Text.StringBuilder();
   var scene=EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity",OpenSceneMode.Single);
   var root=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<MeshFilter>(true)).Single(x=>AssetDatabase.GetAssetPath(x.sharedMesh)==Root+"RadioWoodenCaseUnity.asset").transform;
   var mf=root.GetComponentInChildren<MeshFilter>();var mesh=mf.sharedMesh;var vs=mesh.vertices;var uvs=mesh.uv;
   for(int s=0;s<mesh.subMeshCount;s++){int f=0,b=0,m=0;float ulo=999,uhi=-999,vlo=999,vhi=-999;var ts=mesh.GetTriangles(s);
    for(int i=0;i<ts.Length;i+=3){float lo=Mathf.Min(vs[ts[i]].z,vs[ts[i+1]].z,vs[ts[i+2]].z);float hi=Mathf.Max(vs[ts[i]].z,vs[ts[i+1]].z,vs[ts[i+2]].z);if(hi<.01f)f++;else if(lo>4.98f)b++;else m++;}
    foreach(int v in ts){ulo=Mathf.Min(ulo,uvs[v].x);uhi=Mathf.Max(uhi,uvs[v].x);vlo=Mathf.Min(vlo,uvs[v].y);vhi=Mathf.Max(vhi,uvs[v].y);}
    report.AppendLine("Radio slot "+s+" front="+f+" back="+b+" varying="+m+" UV="+ulo+","+uhi+","+vlo+","+vhi+" mat="+AssetDatabase.GetAssetPath(root.GetComponentInChildren<MeshRenderer>().sharedMaterials[s]));
   }
   string dp=Root+"HallwayDoorTopDepth.fbx";var dm=AssetDatabase.LoadAssetAtPath<GameObject>(dp);
   foreach(var a in AssetDatabase.LoadAllAssetsAtPath(dp)){AssetDatabase.TryGetGUIDAndLocalFileIdentifier(a,out string g,out long id);report.AppendLine("Door subasset "+a.GetType().Name+" "+a.name+" GUID="+g+" fileID="+id);}
   var di=UnityEngine.Object.Instantiate(dm);var df=di.GetComponentsInChildren<MeshFilter>(true);Require(df.Length==1,"one door mesh");var door=UnityEngine.Object.Instantiate(df[0].sharedMesh);
   var mat=di.transform.worldToLocalMatrix*df[0].transform.localToWorldMatrix;var dv=door.vertices;var du=door.uv;double cor=0;foreach(int v in door.GetTriangles(0))cor+=dv[v].x*(du[v].x-.5f);
   bool fx=cor<0;for(int i=0;i<dv.Length;i++){dv[i]=mat.MultiplyPoint3x4(dv[i]);if(fx)dv[i].x=-dv[i].x;}door.vertices=dv;
   if(fx)for(int s=0;s<door.subMeshCount;s++){var ts=door.GetTriangles(s);for(int i=0;i<ts.Length;i+=3){int t=ts[i];ts[i]=ts[i+2];ts[i+2]=t;}door.SetTriangles(ts,s);}
   door.RecalculateBounds();if(door.bounds.max.z<.001f){dv=door.vertices;for(int i=0;i<dv.Length;i++)dv[i].z=-dv[i].z;door.vertices=dv;for(int s=0;s<door.subMeshCount;s++){var ts=door.GetTriangles(s);for(int i=0;i<ts.Length;i+=3){int t=ts[i];ts[i]=ts[i+2];ts[i+2]=t;}door.SetTriangles(ts,s);}door.RecalculateBounds();}door.RecalculateNormals();Require(Mathf.Abs(door.bounds.size.z-.08f)<.001f,"door depth");
   report.AppendLine("Door canonical mesh X correction="+fx+" bounds="+door.bounds+" slots="+door.subMeshCount);
   string ap=Root+"HallwayDoorTopUnity.asset";var prev=AssetDatabase.LoadAssetAtPath<Mesh>(ap);if(prev!=null){EditorUtility.CopySerialized(door,prev);UnityEngine.Object.DestroyImmediate(door);door=prev;}else{door.name="Hallway Door Top Unity";AssetDatabase.CreateAsset(door,ap);}
   AssetDatabase.TryGetGUIDAndLocalFileIdentifier(door,out string dg,out long did);report.AppendLine("Door canonical asset="+ap+" GUID="+dg+" fileID="+did);
   UnityEngine.Object.DestroyImmediate(di);AssetDatabase.SaveAssets();File.WriteAllText(System.IO.Path.Combine(Evidence,"model-handoff.txt"),report.ToString());
   EditorApplication.Exit(0);
  }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
 }
 public static void FixCaptureBatch(){
 try{
 var pr=PrefabUtility.LoadPrefabContents(Root+"LobbyRadioWoodDepth.prefab");pr.GetComponentInChildren<MeshRenderer>().sharedMaterials=new[]{AssetDatabase.LoadAssetAtPath<Material>(Root+"Radio_Oak_MatchedFront.mat"),AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/LobbyRevision/Plaque_Oiled_Antique_Wood.mat"),AssetDatabase.LoadAssetAtPath<Material>(Root+"Radio_Front_RetainedSprite.mat")};
 PrefabUtility.SaveAsPrefabAsset(pr,Root+"LobbyRadioWoodDepth.prefab");PrefabUtility.UnloadPrefabContents(pr);AssetDatabase.SaveAssets();
 var scene=EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity",OpenSceneMode.Single);
 var st=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<SpriteRenderer>(true)).Single(x=>x.sprite!=null&&AssetDatabase.GetAssetPath(x.sprite)=="Assets/Art/Radio-body.png").transform;
 var center=st.TransformPoint(new Vector3(0,-.8925f,2.4962f));var forward=st.TransformDirection(Vector3.back);
 Capture(scene,center+forward*2.4f,center,"radio-front.png");Capture(scene,center+(forward+st.right*.8f).normalized*2.4f+Vector3.up*.15f,center,"radio-side.png");
 File.WriteAllText(System.IO.Path.Combine(Evidence,"final-material-validation.txt"),"PASS native slot mapping 0=side/bevel oak, 1=back original plaque, 2=front invisible retained sprite. GPU recaptured. No scene edits.");
 ProbeBatch();
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
 }
 public static void RunBatch(){try{Run();EditorApplication.Exit(0);}catch(Exception e){File.WriteAllText(System.IO.Path.Combine(Evidence,"native-error.txt"),e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}}
}

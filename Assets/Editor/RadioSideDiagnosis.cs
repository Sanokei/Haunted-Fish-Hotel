using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class RadioSideDiagnosis {
 const string E=@"C:\Users\wkeif\Documents\Codex\2026-10-09\task\RadioOakIntegration";
 const string R="Assets/Art/BlenderDepthImports/";
 public static void RunBatch(){try{
 if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit mode");
 var scene=EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity");
 var all=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).ToArray();
 var mesh=all.Select(x=>x.GetComponent<MeshFilter>()).Where(x=>x&&AssetDatabase.GetAssetPath(x.sharedMesh)==R+"RadioWoodenCaseUnity.asset").Single();
 var mr=mesh.GetComponent<MeshRenderer>();var mats=mr.sharedMaterials;var side=mats[0];var st=mesh.transform.parent;
 var main=all.Select(x=>x.GetComponent<Camera>()).Single(x=>x&&x.name=="Main Camera");
 var center=st.TransformPoint(new Vector3(0,-.8925f,2.4962f));var f=st.TransformDirection(Vector3.back);
 var test=new GameObject("Radio diagnosis camera").AddComponent<Camera>();test.enabled=false;test.fieldOfView=38;test.nearClipPlane=.05f;test.farClipPlane=200;test.clearFlags=CameraClearFlags.SolidColor;test.backgroundColor=new Color(.06f,.07f,.08f);
 test.transform.position=center+(f+st.right*.8f).normalized*2.4f+Vector3.up*.15f;test.transform.LookAt(center);
 Capture(main,"lobby-main-before.png");Capture(test,"radio-lit-before.png");
 var unlit=new Material(Shader.Find("Universal Render Pipeline/Unlit"));unlit.SetTexture("_BaseMap",side.GetTexture("_BaseMap"));unlit.SetColor("_BaseColor",side.GetColor("_BaseColor"));mats[0]=unlit;mr.sharedMaterials=mats;
 Capture(main,"lobby-main-unlit-test.png");Capture(test,"radio-unlit-test.png");
 mats[0]=side;mr.sharedMaterials=mats;
 var light=new GameObject("Radio diagnosis light").AddComponent<Light>();light.type=LightType.Point;light.range=2;light.intensity=1.5f;light.color=new Color(1,.85f,.65f);light.transform.position=center+st.right*.9f+f*.5f;
 Capture(main,"lobby-main-fill-test.png");Capture(test,"radio-fill-test.png");
 File.WriteAllText(System.IO.Path.Combine(E,"side-diagnosis.txt"),"Material="+side.shader.name+" texture="+AssetDatabase.GetAssetPath(side.GetTexture("_BaseMap"))+" tint="+side.GetColor("_BaseColor")+"\nMain camera="+main.transform.position+" rotation="+main.transform.eulerAngles+"\nAmbient="+RenderSettings.ambientMode+" sky="+RenderSettings.ambientSkyColor+" equator="+RenderSettings.ambientEquatorColor+" ground="+RenderSettings.ambientGroundColor+"\nScene lights="+string.Join("\n",all.Select(x=>x.GetComponent<Light>()).Where(x=>x).Select(x=>x.name+" type="+x.type+" bake="+x.lightmapBakeType+" intensity="+x.intensity+" enabled="+x.enabled+" pos="+x.transform.position)));
 UnityEngine.Object.DestroyImmediate(light.gameObject);UnityEngine.Object.DestroyImmediate(unlit);UnityEngine.Object.DestroyImmediate(test.gameObject);
 EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
 static void Capture(Camera c,string n){var rt=new RenderTexture(1600,1000,24);var old=c.targetTexture;c.targetTexture=rt;c.Render();var prev=RenderTexture.active;RenderTexture.active=rt;var t=new Texture2D(1600,1000,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1600,1000),0,0);t.Apply();File.WriteAllBytes(System.IO.Path.Combine(E,n),t.EncodeToPNG());RenderTexture.active=prev;c.targetTexture=old;UnityEngine.Object.DestroyImmediate(t);UnityEngine.Object.DestroyImmediate(rt);}
}

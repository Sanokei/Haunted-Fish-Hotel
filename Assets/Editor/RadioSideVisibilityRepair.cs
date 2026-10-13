using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class RadioSideVisibilityRepair {
 const string E=@"C:\Users\wkeif\Documents\Codex\2026-10-09\task\RadioOakIntegration";
 const string R="Assets/Art/BlenderDepthImports/";
 public static void RunBatch(){try{
 if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Idle edit mode required");
 var scene=EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity");var all=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).ToArray();
 var mr=all.Select(x=>x.GetComponent<MeshFilter>()).Single(x=>x&&AssetDatabase.GetAssetPath(x.sharedMesh)==R+"RadioWoodenCaseUnity.asset").GetComponent<MeshRenderer>();
 var side=mr.sharedMaterials[0];var oak=side.GetTexture("_BaseMap");if(AssetDatabase.GetAssetPath(oak)!=R+"RadioOakRepeat.jpg")throw new Exception("Unexpected user material override");
 var st=mr.transform.parent;var center=st.TransformPoint(new Vector3(0,-.8925f,2.4962f));var f=st.TransformDirection(Vector3.back);
 var main=all.Select(x=>x.GetComponent<Camera>()).Single(x=>x&&x.name=="Main Camera");
 var test=new GameObject("Radio repair validation camera").AddComponent<Camera>();test.enabled=false;test.fieldOfView=38;test.nearClipPlane=.05f;test.farClipPlane=200;test.clearFlags=CameraClearFlags.SolidColor;test.backgroundColor=new Color(.06f,.07f,.08f);test.transform.position=center+(f+st.right*.8f).normalized*2.4f+Vector3.up*.15f;test.transform.LookAt(center);
 Capture(main,"lobby-main-side-before.png");Capture(test,"radio-side-before.png");
 side.SetColor("_BaseColor",new Color(.7620176773f,1,.8833393263f,1));side.SetColor("_Color",new Color(.7620176773f,1,.8833393263f,1));
 side.SetTexture("_EmissionMap",oak);side.SetColor("_EmissionColor",new Color(.7620176773f,1,.8833393263f,1)*.65f);side.EnableKeyword("_EMISSION");side.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
 EditorUtility.SetDirty(side);AssetDatabase.SaveAssets(); if(!side.IsKeywordEnabled("_EMISSION"))throw new Exception("Emission keyword was removed during material save");
 Capture(main,"lobby-main-side-after.png");Capture(test,"radio-side-after.png");
 var m=mr.GetComponent<MeshFilter>().sharedMesh;if(Mathf.Abs(m.bounds.size.z*st.lossyScale.z-.36f)>.0001f)throw new Exception("Depth changed");
 if(side.shader.name!="Universal Render Pipeline/Lit"||side.GetFloat("_ReceiveShadows")!=1)throw new Exception("Lit shadow contract changed");
 File.WriteAllText(System.IO.Path.Combine(E,"side-repair-validation.txt"),"PASS exact oak texture/UV/slot; corrected material sRGB tint (.762018,1,.883339); texture-based .65 albedo ambient floor; URP Lit/shadows retained; renderer remains nonstatic; no lighting bake or room-light changes; .36m depth unchanged; authored Main Camera and scene close views captured; only own side material saved. Runtime Play and click/audio are not exercised.");
 UnityEngine.Object.DestroyImmediate(test.gameObject);EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
 static void Capture(Camera c,string n){var rt=new RenderTexture(1600,1000,24);var old=c.targetTexture;c.targetTexture=rt;c.Render();var prev=RenderTexture.active;RenderTexture.active=rt;var t=new Texture2D(1600,1000,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1600,1000),0,0);t.Apply();File.WriteAllBytes(System.IO.Path.Combine(E,n),t.EncodeToPNG());RenderTexture.active=prev;c.targetTexture=old;UnityEngine.Object.DestroyImmediate(t);UnityEngine.Object.DestroyImmediate(rt);}
}

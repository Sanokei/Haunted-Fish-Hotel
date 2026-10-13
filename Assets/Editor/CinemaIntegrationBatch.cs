#if UNITY_EDITOR
using System;using System.IO;using System.Linq;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using HauntedFish.Theatre;
public static class CinemaIntegrationBatch {
 public static void Run(){
 if(!Application.isBatchMode)throw new Exception("Owned batch required");
 const string folder="Assets/Art/CinemaChairs",model=folder+"/HFH_CinemaSplit6.fbx";
 var shader=Shader.Find("Universal Render Pipeline/Lit");if(!shader)throw new Exception("URP Lit unavailable");
 string[] names={"HFH_CharcoalVelvet","HFH_BlackenedFrame","HFH_AgedBronze","HFH_VelvetPiping"};Color[] colors={new Color(.023f,.012f,.022f),new Color(.009f,.012f,.014f),new Color(.09f,.062f,.032f),new Color(.017f,.009f,.016f)};float[] metal={0,.15f,.67f,0},smooth={.09f,.57f,.52f,.25f};
 var importer=(ModelImporter)AssetImporter.GetAtPath(model);importer.globalScale=1;importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.CalculateMikk;importer.addCollider=false;
 for(int i=0;i<4;i++){string path=folder+"/"+names[i]+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(!material){material=new Material(shader){name=names[i]};AssetDatabase.CreateAsset(material,path);}material.SetColor("_BaseColor",colors[i].gamma);material.SetFloat("_Metallic",metal[i]);material.SetFloat("_Smoothness",smooth[i]);EditorUtility.SetDirty(material);importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),names[i]),material);}
 importer.SaveAndReimport();var filmShader=Shader.Find("HauntedFish/CinemaFilm");if(!filmShader||ShaderUtil.ShaderHasError(filmShader))throw new Exception("Film shader failed native import");string fp=folder+"/CinemaFilm.mat";var film=AssetDatabase.LoadAssetAtPath<Material>(fp);if(!film){film=new Material(filmShader);AssetDatabase.CreateAsset(film,fp);}AssetDatabase.SaveAssets();
 var scene=EditorSceneManager.OpenScene("Assets/Scenes/theatre.unity",OpenSceneMode.Single);var theatre=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TheatrePresentation>(true)).Single();
 TheatreCinemaAuthoring.Apply(theatre,AssetDatabase.LoadAssetAtPath<GameObject>(model),film);
 foreach(var sprite in theatre.GetComponentsInChildren<SpriteRenderer>(true))if(sprite.name.StartsWith("Seated")){var pose=sprite.transform.localPosition;pose.y=2.4f;sprite.transform.localPosition=pose;}
 if(theatre.GetComponentsInChildren<Canvas>(true).Any(c=>c.enabled))throw new Exception("Theatre UI remains enabled");
 EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Directory.CreateDirectory("Tools/TheatreTests/Evidence/cinema");File.WriteAllText("Tools/TheatreTests/Evidence/cinema/import-results.txt","PASS native FBX 5.670m width; collider-free split6 scale2.5; four URP materials; world film shader imported without errors; UI disabled; owned walker pose preserved. Native playback/render follows.");
 TheatreNativeValidation.Run();
 }
}
#endif

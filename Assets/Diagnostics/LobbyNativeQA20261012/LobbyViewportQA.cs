using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HauntedFish.Multiplayer;
using Monologue.Dialogue;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
public sealed class LobbyViewportQA : MonoBehaviour {
 public LobbyManager Manager;public StoryUI UI;public LightningEffectManager Storm;public LobbyStormAudio StormAudio;public LobbyFireplaceFlicker Fireplace;public AudioSource FireSource;public LobbyRadioPlayback Radio;public AudioMixer Mixer;
 const string E=@"C:\Users\wkeif\Documents\Codex\2026-10-09\task\LobbyMoodPreparation\StandaloneQA";
 readonly List<string> results=new List<string>();readonly List<float> captured=new List<float>();readonly object audioLock=new object();int checks,channels=2;bool completed,record;
 void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;results.Add("PASS "+label);}
 void Awake(){Application.runInBackground=true;Application.targetFrameRate=60;Directory.CreateDirectory(E);}
 void OnAudioFilterRead(float[] data,int channelCount){if(!record)return;lock(audioLock){channels=channelCount;captured.AddRange(data);}}
 IEnumerator Start(){yield return null;var routine=Run();while(true){object next;try{if(!routine.MoveNext())break;next=routine.Current;}catch(Exception e){Finish(false,e.ToString());yield break;}yield return next;}Finish(true,"real standalone overlay/frame/source tests completed");}
 IEnumerator Run(){
 Check(Manager.IntroductionPlaying,"authored intro begins on scene activation");Check(Manager.IntroductionPresentationReady,"authored presentation ready without runtime UI spawn");
 yield return new WaitForSecondsRealtime(2.4f);Check(Storm.AmbientActive,"storm starts at opening Ink weather cue");Check(Get<AudioSource>(StormAudio,"_RainSource").isPlaying,"actual rain source playing during intro");
 int objects=SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).Count();
 bool talking=false;float deadline=Time.unscaledTime+65;
 while(Manager.IntroductionPlaying){var world=FindFirstObjectByType<LobbyIntroductionWorldSequence>();var voice=Get<AudioSource>(world,"_CaptionVoice");talking|=voice&&voice.isPlaying;if(Time.unscaledTime>deadline)throw new Exception("Full intro timed out");yield return null;}
 Check(talking,"authored talking source played at LobbyLine");Check(UI!=null,"authored UI retained after full intro");
 var screen=FindFirstObjectByType<HotelConnectionScreen>();var overlay=Get<CanvasGroup>(screen,"_Overlay");yield return null;Check(overlay.alpha==1,"already-played intro shows connecting cover while session not ready");
 GetMethod(Manager,"BeginIntroduction").Invoke(Manager,null);yield return new WaitForSecondsRealtime(.5f);UI.RequestSkip();yield return null;Check(!Manager.IntroductionPlaying,"actual authored Skip button event cancels replay");Check(!Get<AudioSource>(FindFirstObjectByType<LobbyIntroductionWorldSequence>(),"_CaptionVoice").isPlaying,"skip stops talking source");
 int now=SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).Count();Check(now==objects,"replay/skip creates no new intro UI/fish/audio objects");Storm.StopAmbient();
 foreach(var v in new[]{new[]{1280,720},new[]{1152,720},new[]{960,720},new[]{1260,540}}){
 Screen.SetResolution(v[0],v[1],FullScreenMode.Windowed);for(int i=0;i<5;i++)yield return null;Check(Screen.width==v[0]&&Screen.height==v[1],"actual render resolution "+v[0]+"x"+v[1]);UI.Prepare();Canvas.ForceUpdateCanvases();var white=Get<GameObject>(UI,"_WhiteFrame");var canvas=white.GetComponentInParent<Canvas>(true);Check(canvas.isRootCanvas&&canvas.renderMode==RenderMode.ScreenSpaceOverlay,"independent root overlay "+v[0]+"x"+v[1]);
 string prefix=v[0]+"x"+v[1];yield return new WaitForEndOfFrame();Capture(prefix+"-before.png",false);StartCoroutine(UI.Play(new StorySequenceStep(StorySequenceStepKind.FlashFrames,count:2)));int whiteFrames=0;
 for(int frame=0;frame<4;frame++){yield return new WaitForEndOfFrame();bool active=white.activeSelf;Capture(prefix+"-frame"+frame+".png",active);if(active)whiteFrames++;yield return null;}
 Check(whiteFrames==2,"exactly two full-white rendered frames "+prefix);Check(!white.activeSelf,"white frame restored inactive "+prefix);UI.Close();
 }
 var light=Get<Light>(Fireplace,"_Hearth");float minimum=float.MaxValue,maximum=0;float end=Time.unscaledTime+3;while(Time.unscaledTime<end){minimum=Mathf.Min(minimum,light.intensity);maximum=Mathf.Max(maximum,light.intensity);yield return null;}Check(maximum-minimum>.01f&&minimum>=14*.82f&&maximum<=14*1.18f,"restrained irregular hearth flicker bounds");Fireplace.enabled=false;Check(Mathf.Abs(light.intensity-14)<.001f,"disable restores authored hearth intensity");Fireplace.enabled=true;yield return new WaitForSecondsRealtime(1);
 var cursor=FindFirstObjectByType<LobbyCursorManager>();if(cursor)cursor.enabled=false;Radio.SetReady(true);
 HotelSettings.Save(1,1,0,FullScreenMode.Windowed);yield return new WaitForSecondsRealtime(1);Check(Mixer.GetFloat("SoundEffectsVolumeDb",out float fx)&&fx<=-79&&Get<AudioSource>(Radio,"_Source").isPlaying,"SFX mute leaves radio music playing");
 HotelSettings.Save(1,0,1,FullScreenMode.Windowed);yield return new WaitForSecondsRealtime(1);Check(Mixer.GetFloat("MusicVolumeDb",out float music)&&music<=-79&&FireSource.isPlaying,"Music mute leaves authored fire playing");HotelSettings.Save(0,1,1,FullScreenMode.Windowed);yield return null;Check(AudioListener.volume==0,"Master zero applied on listener");HotelSettings.Save(1,1,1,FullScreenMode.Windowed);yield return null;
 Check(FireSource.outputAudioMixerGroup&&FireSource.outputAudioMixerGroup.name=="Sound Effects"&&Mathf.Abs(FireSource.volume-.8f)<.001f&&FireSource.minDistance==6&&FireSource.maxDistance==28,"fire SFX routing and source calibration preserved");
 record=true;yield return new WaitForSecondsRealtime(3);record=false;float[] audio;lock(audioLock){audio=captured.ToArray();captured.Clear();}if(audio.Length>0){WriteWave(audio,channels,"engine-mix-audition.wav");double rms=Math.Sqrt(audio.Select(x=>(double)x*x).Average());results.Add("MEASURE actual listener PCM samples="+audio.Length+" channels="+channels+" rms="+rms);}else results.Add("UNVERIFIED listener capture callback unavailable; perceptual listening not claimed");
 results.Add("UNVERIFIED connected two-peer theatre RPC; isolated authored QA scene does not start network server");
 }
 void Capture(string name,bool requireWhite){var rt=new RenderTexture(Screen.width,Screen.height,0,RenderTextureFormat.ARGB32);rt.Create();var active=RenderTexture.active;var texture=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);try{ScreenCapture.CaptureScreenshotIntoRenderTexture(rt);RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);texture.Apply();File.WriteAllBytes(System.IO.Path.Combine(E,name),texture.EncodeToPNG());if(requireWhite){var pixels=texture.GetPixels32();int bad=pixels.Count(x=>x.r<254||x.g<254||x.b<254);Check(bad==0,"all "+pixels.Length+" rendered pixels white "+name+" bad="+bad);}}finally{RenderTexture.active=active;Destroy(texture);rt.Release();Destroy(rt);}}
 static T Get<T>(object o,string name){if(o==null)return default;var f=o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance);return f==null?default:(T)f.GetValue(o);}
 static MethodInfo GetMethod(object o,string name)=>o.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance);
 void WriteWave(float[] audio,int c,string name){using(var stream=File.Create(System.IO.Path.Combine(E,name)))using(var w=new BinaryWriter(stream)){int bytes=audio.Length*2;w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(bytes+36);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)c);w.Write(AudioSettings.outputSampleRate);w.Write(AudioSettings.outputSampleRate*c*2);w.Write((short)(c*2));w.Write((short)16);w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(bytes);foreach(float sample in audio)w.Write((short)Mathf.Clamp(Mathf.RoundToInt(sample*32767),-32768,32767));}}
 void Finish(bool pass,string message){if(completed)return;completed=true;results.Insert(0,(pass?"PASS":"FAIL")+" checks="+checks+" "+message);File.WriteAllLines(System.IO.Path.Combine(E,"results.txt"),results);Application.Quit(pass?0:1);}
}

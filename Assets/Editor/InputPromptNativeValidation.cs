#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HauntedFish.InputPrompts;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.DualShock.LowLevel;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Explicit, synchronous native-input validation for an idle disposable Editor project.
// Synthetic devices exercise the real Input System. This is not a physical-controller test.
// The method is authored and API-compiled; running it requires a working native Unity Editor.
public static class InputPromptNativeValidation
{
    const string CatalogPath = "Assets/Resources/InputPrompts/HotelInputGlyphs.asset";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);
    static void Set(object target,string name,object value)=>target.GetType().GetField(name,Private).SetValue(target,value);
    static void Call(object target,string name)=>target.GetType().GetMethod(name,Private).Invoke(target,null);
    static void ResetTracker()=>typeof(InputPromptDevices).GetMethod("Reset",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
    static string ExpectedText(InputPromptSpriteText view,InputPromptFamily family)
    {
        var catalog=Get<InputPromptCatalog>(view,"_Catalog");var sprites=Get<TMP_SpriteAsset>(view,"_SpriteAsset");var actions=Get<InputActionAsset>(view,"_ActionAsset");
        return Regex.Replace(Get<string>(view,"_Template")??"",@"\{input:([^}]+)\}",token=>
        {
            string path=token.Groups[1].Value;
            var prompt=path.StartsWith("<")?InputPromptResolver.ResolvePaths(path,"",family):InputPromptResolver.Resolve(InputPromptActions.Find(path,actions),family);
            string[] names=prompt.GlyphPaths.Select(p=>catalog.FindSpriteName(family,p)).ToArray();
            if(names.Length==0||names.Any(name=>string.IsNullOrEmpty(name)||sprites.GetSpriteIndexFromName(name)<0))return prompt.DisplayText.Replace("<","\u2039").Replace(">","\u203a");
            return string.Concat(names.Select(name=>"<sprite name=\""+name+"\" tint=1>"));
        });
    }
    static void RequireIdle()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage()!=null)
            throw new InvalidOperationException("Run in idle Edit mode, outside Prefab Mode, in a disposable validation project.");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)
            throw new InvalidOperationException("Save or discard scene edits before running validation.");
        if(typeof(InputPromptDevices).GetField("_Changed",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null)!=null)
            throw new InvalidOperationException("Active prompt listeners exist; use an empty scene in a disposable validation project.");
    }
    static void Key(Keyboard keyboard,bool down)
    {InputSystem.QueueStateEvent(keyboard,down?new KeyboardState(UnityEngine.InputSystem.Key.Space):new KeyboardState());InputSystem.Update();}
    static void Tap(Keyboard keyboard) {Key(keyboard,false);Key(keyboard,true);}
    static void Pad(Gamepad pad,bool down)
    {InputSystem.QueueStateEvent(pad,down?new GamepadState().WithButton(GamepadButton.South):new GamepadState());InputSystem.Update();}
    static void Tap(Gamepad pad) {Pad(pad,false);Pad(pad,true);}
    static void Sense(DualSenseGamepadHID pad,bool down)
    {
        InputSystem.QueueStateEvent(pad,new DualSenseHIDInputReport{leftStickX=128,leftStickY=128,rightStickX=128,rightStickY=128,buttons0=(byte)(8|(down?32:0))});
        InputSystem.Update();
    }
    static void Tap(DualSenseGamepadHID pad) {Sense(pad,false);Sense(pad,true);}
    static T Add<T>(List<InputDevice> created) where T:InputDevice
    {T device=InputSystem.AddDevice<T>();created.Add(device);return device;}
    static Gamepad AddPad(string layout,List<InputDevice> created)
    {var device=(Gamepad)InputSystem.AddDevice(layout);created.Add(device);return device;}

    [MenuItem("Tools/Haunted Hotel/Validate native input prompts (isolated)")]
    public static void Run()
    {
        RequireIdle();
        var lines=new List<string>();int checks=0;
        void Check(bool value,string label){if(!value)throw new InvalidOperationException(label);checks++;lines.Add("PASS "+label);}
        var created=new List<InputDevice>();var prefabs=new List<GameObject>();var imageViews=new List<InputGlyphPrompt>();var textViews=new List<InputPromptSpriteText>();
        var originalSettings=InputSystem.settings;InputSettings temporarySettings=null;GameObject root=null;InputActionAsset actions=null, gameplayActions=null;
        Action listener=null;int notifications=0;
        string report=Path.Combine(Path.GetTempPath(),"HauntedFish-InputPrompts-NativeValidation.txt");
        try
        {
            temporarySettings=Object.Instantiate(originalSettings);InputSystem.settings=temporarySettings;
            temporarySettings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;
            ResetTracker();
            var keyboard=Add<Keyboard>(created);var mouse=Add<Mouse>(created);
            var xbox=AddPad("XInputController",created);var dualshock=Add<DualShockGamepad>(created);
            var dualsense=Add<DualSenseGamepadHID>(created);var ds4hid=Add<DualShock4GamepadHID>(created);var generic=Add<Gamepad>(created);
            listener=()=>notifications++;InputPromptDevices.Listen(listener);
            Tap(keyboard);Check(InputPromptDevices.ActiveDevice==keyboard,"native keyboard event selects keyboard/mouse");
            var catalog=AssetDatabase.LoadAssetAtPath<InputPromptCatalog>(CatalogPath);
            Check(catalog,"imported catalog loads");
            foreach(var family in new[]{InputPromptFamily.Xbox,InputPromptFamily.PlayStation,InputPromptFamily.GenericGamepad})
                Check(catalog.Find(family,"buttonSouth"),"imported South sprite exists for "+family);
            Check(catalog.Find(InputPromptFamily.Xbox,"buttonSouth")!=catalog.Find(InputPromptFamily.GenericGamepad,"buttonSouth"),"unknown controller artwork differs from Xbox A");

            actions=ScriptableObject.CreateInstance<InputActionAsset>();var map=new InputActionMap("PromptTest");actions.AddActionMap(map);
            var jump=map.AddAction("Jump",InputActionType.Button);jump.AddBinding("<Keyboard>/space",groups:"Keyboard");jump.AddBinding("<Gamepad>/buttonSouth",groups:"Gamepad");
            var move=map.AddAction("Move",InputActionType.Value);move.AddCompositeBinding("2DVector").With("Up","<Keyboard>/w",groups:"Keyboard").With("Down","<Keyboard>/s",groups:"Keyboard").With("Left","<Keyboard>/a",groups:"Keyboard").With("Right","<Keyboard>/d",groups:"Keyboard");move.AddBinding("<Gamepad>/leftStick",groups:"Gamepad");
            map.Enable();
            InputPromptActions.Register(map);Check(InputPromptActions.Find("PromptTest/Jump",null)==jump,"registered runtime map resolves actual native action");
            root=new GameObject("Input prompt validation (temporary)");root.SetActive(false);root.hideFlags=HideFlags.HideAndDontSave;
            var glyph=root.AddComponent<Image>();var fallback=new GameObject("Fallback",typeof(RectTransform),typeof(Text));fallback.transform.SetParent(root.transform,false);
            var view=root.AddComponent<InputGlyphPrompt>();Set(view,"_Catalog",catalog);Set(view,"_Glyph",glyph);Set(view,"_Fallback",fallback.GetComponent<Text>());view.SetAction(jump);
            Call(view,"OnDisable");Call(view,"OnEnable");imageViews.Add(view);
            Check(glyph.sprite==catalog.Find(InputPromptFamily.KeyboardMouse,"<Keyboard>/space")&&glyph.enabled,"production Image view starts on keyboard sprite");
            Tap(xbox);Check(InputPromptDevices.Family==InputPromptFamily.Xbox,"real XInput layout selected by queued state");
            Check(glyph.sprite==catalog.Find(InputPromptFamily.Xbox,"buttonSouth"),"event refreshes production Image to Xbox A");
            Tap(dualshock);Check(InputPromptDevices.Family==InputPromptFamily.PlayStation,"real DualShock layout selected by queued state");
            Check(glyph.sprite==catalog.Find(InputPromptFamily.PlayStation,"buttonSouth"),"event refreshes production Image to PlayStation Cross");
            using(StateEvent.From(ds4hid,out var stateEvent))
            {ds4hid.buttonSouth.WriteValueIntoEvent(1f,stateEvent);InputSystem.QueueEvent(stateEvent);}InputSystem.Update();
            Check(InputPromptDevices.ActiveDevice==ds4hid&&InputPromptDevices.Family==InputPromptFamily.PlayStation,"real DualShock4 HID control state selects PlayStation");
            Tap(dualsense);Check(InputPromptDevices.ActiveDevice==dualsense&&InputPromptDevices.Family==InputPromptFamily.PlayStation,"real DualSense HID report selects PlayStation");
            Tap(generic);Check(InputPromptDevices.Family==InputPromptFamily.GenericGamepad,"bare Gamepad selects generic family");
            Check(glyph.sprite==catalog.Find(InputPromptFamily.GenericGamepad,"buttonSouth"),"event refreshes production Image to positional South");
            Tap(keyboard);Pad(xbox,false);Check(InputPromptDevices.ActiveDevice==keyboard,"release-only state does not take keyboard ownership");
            for(int i=1;i<=20;i++){InputSystem.QueueDeltaStateEvent(xbox.leftStick,new Vector2(i*.01f,0));InputSystem.Update();}
            Check(InputPromptDevices.ActiveDevice==keyboard,"real delta events plus processors ignore idle drift");
            for(int i=21;i<=85;i++){InputSystem.QueueDeltaStateEvent(xbox.leftStick,new Vector2(i*.01f,0));InputSystem.Update();}
            Check(InputPromptDevices.ActiveDevice==xbox,"slow meaningful tilt eventually switches using real dead-zone processing");
            Tap(keyboard);for(int i=0;i<20;i++){InputSystem.QueueDeltaStateEvent(xbox.leftStick,new Vector2(.851f+(i%2)*.002f,0));InputSystem.Update();}
            Check(InputPromptDevices.ActiveDevice==keyboard,"held stick noise does not reclaim after keyboard");
            InputSystem.QueueDeltaStateEvent(xbox.leftStick,Vector2.zero);InputSystem.Update();InputSystem.QueueDeltaStateEvent(xbox.leftStick,new Vector2(.9f,0));InputSystem.Update();
            Check(InputPromptDevices.ActiveDevice==xbox,"neutral rearms deliberate stick input");
            InputSystem.QueueStateEvent(xbox,new GamepadState());InputSystem.Update();
            InputSystem.QueueStateEvent(xbox,new GamepadState{leftStick=new Vector2(.9f,0),rightStick=new Vector2(.9f,0)});InputSystem.Update();Tap(keyboard);
            InputSystem.QueueStateEvent(xbox,new GamepadState{leftStick=new Vector2(.901f,0),rightStick=new Vector2(.901f,0)});InputSystem.Update();
            Check(InputPromptDevices.ActiveDevice==keyboard,"full state disarms both sticks without short-circuit leak");
            Tap(xbox);InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(1,1)});InputSystem.Update();Check(InputPromptDevices.ActiveDevice==xbox,"native mouse jitter ignored");
            InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(6,0)});InputSystem.Update();Check(InputPromptDevices.ActiveDevice==mouse,"meaningful native mouse movement switches");
            Tap(keyboard);Tap(xbox);InputSystem.RemoveDevice(xbox);Check(InputPromptDevices.ActiveDevice==keyboard,"native removed active device falls back to most recent keyboard");
            InputSystem.AddDevice(xbox);Check(InputPromptDevices.ActiveDevice==keyboard,"native re-add does not steal ownership");Tap(xbox);Check(InputPromptDevices.ActiveDevice==xbox,"native re-added controller input switches again");

            int before=notifications;jump.ApplyBindingOverride(1,"<Gamepad>/buttonWest");
            Check(notifications>before,"real ApplyBindingOverride raises BoundControlsChanged presentation event");
            Check(glyph.sprite==catalog.Find(InputPromptFamily.Xbox,"buttonWest"),"rebinding refreshes actual Image without device switch");
            Tap(dualshock);Check(glyph.sprite==catalog.Find(InputPromptFamily.PlayStation,"buttonWest"),"rebound West becomes Square rather than Cross");
            jump.ApplyBindingOverride(1,"");Check(!glyph.enabled&&fallback.GetComponent<Text>().enabled,"disabled gamepad binding clears stale image");
            Check(fallback.GetComponent<Text>().text=="Keyboard required","disabled pad binding truthful text");
            jump.RemoveAllBindingOverrides();Tap(keyboard);view.SetAction(move);
            Check(!glyph.enabled&&fallback.GetComponent<Text>().text.Length>0,"one slot uses full composite text instead of truncating WASD");
            var slots=new Image[3];for(int i=0;i<slots.Length;i++){var child=new GameObject("Glyph "+i,typeof(RectTransform),typeof(Image));child.transform.SetParent(root.transform,false);slots[i]=child.GetComponent<Image>();}
            Set(view,"_AdditionalGlyphs",slots);view.Refresh();Check(glyph.enabled&&slots.All(slot=>slot.enabled),"four authored-style slots render complete WASD");
            before=notifications;move.ApplyBindingOverride(1,"<Keyboard>/e");Check(notifications>before&&glyph.sprite==catalog.Find(InputPromptFamily.KeyboardMouse,"<Keyboard>/e"),"composite override event refreshes its first slot");
            move.ApplyBindingOverride(2,"");Check(!glyph.enabled&&slots.All(slot=>!slot.enabled)&&fallback.GetComponent<Text>().enabled,"disabled composite part clears every stale glyph");move.RemoveAllBindingOverrides();
            jump.bindingMask=InputBinding.MaskByGroup("Gamepad");Check(!InputPromptResolver.Resolve(jump,InputPromptFamily.KeyboardMouse).Available,"actual action binding mask excludes keyboard");jump.bindingMask=null;
            map.bindingMask=InputBinding.MaskByGroup("Keyboard");Check(!InputPromptResolver.Resolve(jump,InputPromptFamily.Xbox).Available,"actual map binding mask excludes controller");map.bindingMask=null;
            actions.bindingMask=InputBinding.MaskByGroup("Gamepad");Check(!InputPromptResolver.Resolve(jump,InputPromptFamily.KeyboardMouse).Available,"actual asset binding mask excludes keyboard");actions.bindingMask=null;
            map.devices=new InputDevice[]{keyboard};Check(!InputPromptResolver.Resolve(jump,InputPromptFamily.Xbox).Available,"actual map device restriction excludes controller");map.devices=null;
            map.devices=new InputDevice[]{mouse};Check(!InputPromptResolver.Resolve(jump,InputPromptFamily.KeyboardMouse).Available,"native Mouse-only restriction cannot advertise Keyboard input");map.devices=null;
            jump.ApplyBindingOverride(0,"<Keyboard>/definitelyMissingControl");Check(!InputPromptResolver.Resolve(jump,InputPromptFamily.KeyboardMouse).Available,"actual control-layout lookup rejects nonexistent keyboard control");jump.RemoveAllBindingOverrides();
            Check(!InputPromptResolver.ResolvePaths("<Keyboard>/definitelyMissingControl","",InputPromptFamily.KeyboardMouse).Available,"actual fixed-path lookup rejects nonexistent control");
            var importedActions=AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Resources/HotelMultiplayerActions.inputactions");
            Check(importedActions,"actual imported multiplayer action asset loads");gameplayActions=Object.Instantiate(importedActions);
            var ghost=gameplayActions.FindActionMap("Ghost",true);var discard=ghost.FindAction("Dispose",true);ghost.devices=new InputDevice[]{xbox};
            Check(discard.bindings.Any(binding=>binding.effectivePath=="<Gamepad>/buttonNorth"),"actual Ghost/Dispose has controller North binding");
            Check(!ghost.actions.Where(action=>action!=discard).SelectMany(action=>action.bindings).Any(binding=>binding.effectivePath=="<Gamepad>/buttonNorth"),"controller North has no competing Ghost action binding");
            Check(InputPromptResolver.Resolve(discard,InputPromptFamily.Xbox).DisplayText=="Y","controller discard resolves Xbox Y");ghost.devices=null;
            Check(InputPromptResolver.Resolve(discard,InputPromptFamily.PlayStation).DisplayText=="Triangle","controller discard resolves PlayStation Triangle");
            ghost.devices=new InputDevice[]{xbox};int discarded=0;discard.performed+=_=>discarded++;ghost.Enable();
            InputSystem.QueueStateEvent(xbox,new GamepadState());InputSystem.Update();
            InputSystem.QueueStateEvent(xbox,new GamepadState().WithButton(GamepadButton.North));InputSystem.Update();
            Check(discarded==1&&discard.WasPressedThisFrame(),"native North event performs actual Dispose action once and exposes pressed-this-frame");
            InputSystem.QueueStateEvent(xbox,new GamepadState());InputSystem.Update();Check(discarded==1&&!discard.WasPressedThisFrame(),"native North release does not perform discard again");ghost.Disable();
            view.SetAction(jump);jump.Disable();bool rebound=false;
            using(var operation=jump.PerformInteractiveRebinding(0).WithControlsHavingToMatchPath("<Keyboard>/*").OnComplete(_=>rebound=true))
            {operation.Start();InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.E));InputSystem.Update();}
            jump.Enable();Check(rebound&&jump.bindings[0].effectivePath.EndsWith("/e",StringComparison.OrdinalIgnoreCase),"real interactive rebind selects E");
            Check(glyph.sprite==catalog.Find(InputPromptFamily.KeyboardMouse,"<Keyboard>/e"),"interactive rebind refreshes actual glyph");

            // Validate imported, authored prompt objects in isolated prefab contents; never save them.
            foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Resources"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(!asset || (asset.GetComponentsInChildren<InputGlyphPrompt>(true).Length==0&&asset.GetComponentsInChildren<InputPromptSpriteText>(true).Length==0))continue;
                var contents=PrefabUtility.LoadPrefabContents(path);prefabs.Add(contents);
                foreach(var authored in contents.GetComponentsInChildren<InputGlyphPrompt>(true))
                {Check(authored.HasBindings,"imported Image slot has authored refs: "+path+" / "+authored.name);Call(authored,"OnDisable");Call(authored,"OnEnable");imageViews.Add(authored);}
                foreach(var authored in contents.GetComponentsInChildren<InputPromptSpriteText>(true))
                {Check(authored.HasBindings,"imported TMP slot has authored refs: "+path+" / "+authored.name);Call(authored,"OnDisable");Call(authored,"OnEnable");textViews.Add(authored);}
            }
            Check(imageViews.Count>1,"at least one shipped prefab contains authored Image prompts");
            Check(textViews.Count>0,"at least one shipped prefab contains authored TMP prompts");
            foreach(var family in new[]{InputPromptFamily.KeyboardMouse,InputPromptFamily.Xbox,InputPromptFamily.PlayStation,InputPromptFamily.GenericGamepad})
            {
                if(family==InputPromptFamily.KeyboardMouse)Tap(keyboard);else if(family==InputPromptFamily.Xbox)Tap(xbox);else if(family==InputPromptFamily.PlayStation)Tap(dualshock);else Tap(generic);
                foreach(var authored in imageViews.Skip(1))
                {
                    var actionAsset=Get<InputActionAsset>(authored,"_ActionAsset");string actionPath=Get<string>(authored,"_ActionPath");
                    var expected=actionAsset?InputPromptResolver.Resolve(actionAsset.FindAction(actionPath,false),family):InputPromptResolver.ResolvePaths(Get<string>(authored,"_KeyboardPath"),Get<string>(authored,"_GamepadPath"),family);
                    var slot=Get<Image>(authored,"_Glyph");var text=Get<Text>(authored,"_Fallback");var extra=Get<Image[]>(authored,"_AdditionalGlyphs");var authoredCatalog=Get<InputPromptCatalog>(authored,"_Catalog");
                    bool drawable=expected.Available&&expected.GlyphPaths.Length>0&&expected.GlyphPaths.Length<=1+extra.Length&&expected.GlyphPaths.All(p=>authoredCatalog.Find(family,p));
                    Check(slot.enabled==drawable,"authored slot visibility refresh: "+authored.name+" / "+family);
                    if(drawable)
                    {
                        for(int i=0;i<=extra.Length;i++)
                        {
                            var image=i==0?slot:extra[i-1];Check(image,"authored composite slot exists: "+authored.name+" / "+i);
                            bool visible=i<expected.GlyphPaths.Length;
                            Check(image.enabled==visible&&image.sprite==(visible?authoredCatalog.Find(family,expected.GlyphPaths[i]):null),"authored slot family sprite refresh: "+authored.name+" / "+family+" / "+i);
                        }
                    }
                    else Check(text.enabled&&text.text==expected.DisplayText,"authored truthful fallback: "+authored.name+" / "+family);
                }
                foreach(var authored in textViews)
                {
                    var text=Get<TMP_Text>(authored,"_Text");Check(!text.text.Contains("{input:"),"authored TMP tokens resolved: "+authored.name+" / "+family);
                    Check(text.text==ExpectedText(authored,family),"authored TMP exact binding and family refresh: "+authored.name+" / "+family);
                    if(family==InputPromptFamily.GenericGamepad)Check(!text.text.Contains("xbox_",StringComparison.OrdinalIgnoreCase),"generic authored TMP contains no Xbox glyph names");
                }
            }
            foreach(var authored in imageViews)Call(authored,"OnDisable");foreach(var authored in textViews)Call(authored,"OnDisable");
            InputPromptDevices.Unlisten(listener);listener=null;before=notifications;
            jump.ApplyBindingOverride(0,"<Keyboard>/space");Tap(generic);Check(notifications==before,"removed public listener receives no later native input/rebind events");
            Check(!(bool)typeof(InputPromptDevices).GetField("_Listening",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null),"last presenter disable removes shared native subscriptions");
            typeof(InputPromptDevices).GetMethod("TrackSession",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
            Tap(keyboard);Tap(xbox);Check(InputPromptDevices.ActiveDevice==xbox,"session owner records controller activity with every prompt hidden");
            Call(view,"OnEnable");Check(glyph.sprite==catalog.Find(InputPromptFamily.Xbox,"buttonSouth"),"reopened presenter reflects device used while all UI was hidden");Call(view,"OnDisable");
            Check((bool)typeof(InputPromptDevices).GetField("_Listening",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null),"session owner remains subscribed while views hide");
            lines.Insert(0,"PASS "+checks+" native synthetic-input and imported authored-presentation checks. Physical hardware and rendered visual quality are separate acceptance checks.");
        }
        catch(Exception ex){lines.Insert(0,"FAIL native input prompt validation: "+ex);throw;}
        finally
        {
            foreach(var view in imageViews)if(view)Call(view,"OnDisable");foreach(var view in textViews)if(view)Call(view,"OnDisable");
            if(listener!=null)InputPromptDevices.Unlisten(listener);
            foreach(var prefab in prefabs)if(prefab)PrefabUtility.UnloadPrefabContents(prefab);
            if(gameplayActions){gameplayActions.Disable();Object.DestroyImmediate(gameplayActions);}
            if(root)Object.DestroyImmediate(root);if(actions){foreach(var map in actions.actionMaps)InputPromptActions.Unregister(map);actions.Disable();Object.DestroyImmediate(actions);}
            foreach(var device in created)if(device.added)InputSystem.RemoveDevice(device);
            ResetTracker();InputSystem.settings=originalSettings;if(temporarySettings)Object.DestroyImmediate(temporarySettings);
            File.WriteAllLines(report,lines);Debug.Log("Input prompt native validation report: "+report+"\n"+string.Join("\n",lines));
        }
    }
}
#endif

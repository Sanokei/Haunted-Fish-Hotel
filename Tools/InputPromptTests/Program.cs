// Runs the actual production classes with explicit input/UI boundary doubles.
// These checks do not establish Unity callback order, dead-zone processing, native import or hardware behavior.
using System;
using System.Linq;
using System.Reflection;
using HauntedFish.InputPrompts;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.LowLevel;
using TMPro;
static class Program
{
    static int checks, failures;
    static void Check(bool condition,string label)
    { checks++;if(!condition){failures++;Console.Error.WriteLine("FAIL "+label);} }
    static void Eq<T>(T actual,T expected,string label)=>Check(Equals(actual,expected),label+" (expected "+expected+", got "+actual+")");
    static void Set(object target,string field,object value)=>target.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(target,value);
    static void Call(object target,string method)=>target.GetType().GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(target,null);
    static void Reset()
    { typeof(InputPromptDevices).GetMethod("Reset",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);typeof(InputPromptActions).GetMethod("Reset",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);InputSystem.Clear(); }
    static InputAction Action(params string[] paths)
    {var a=new InputAction("Jump");foreach(var p in paths)a.bindings.Add(new InputBinding{path=p});return a;}
    static InputAction Composite()
    {
        var a=Action();a.bindings.Add(new InputBinding{path="2DVector",isComposite=true});
        foreach(var p in new[]{"up:w","down:s","left:a","right:d"}) {var bits=p.Split(':');a.bindings.Add(new InputBinding{name=bits[0],path="<Keyboard>/"+bits[1],isPartOfComposite=true,groups="Keyboard"});}
        a.bindings.Add(new InputBinding{path="<Gamepad>/leftStick",groups="Gamepad"});return a;
    }
    static void Tap(Keyboard k)
    {InputSystem.Emit(k,k.spaceKey,0f);InputSystem.Emit(k,k.spaceKey,1f);}
    static void Tap(Gamepad p)
    {InputSystem.Emit(p,p.buttonSouth,0f);InputSystem.Emit(p,p.buttonSouth,1f);}
    static void ResolveChecks()
    {
        var a=Action("<Keyboard>/space","<Gamepad>/buttonSouth");
        foreach(var pair in new[]{(InputPromptFamily.KeyboardMouse,"Space"),(InputPromptFamily.Xbox,"A"),(InputPromptFamily.PlayStation,"Cross"),(InputPromptFamily.GenericGamepad,"South button")})
        {var r=InputPromptResolver.Resolve(a,pair.Item1);Check(r.Available,"standard binding available "+pair.Item1);Eq(r.DisplayText,pair.Item2,"family-correct display "+pair.Item1);Eq(r.GlyphPaths[0],pair.Item1==InputPromptFamily.KeyboardMouse?"<Keyboard>/space":"buttonSouth","physical key "+pair.Item1);}
        a.Override(1,"<Gamepad>/buttonWest");
        Eq(InputPromptResolver.Resolve(a,InputPromptFamily.Xbox).DisplayText,"X","Xbox rebound West");
        Eq(InputPromptResolver.Resolve(a,InputPromptFamily.PlayStation).DisplayText,"Square","PlayStation rebound West does not confuse Xbox X and Cross");
        a.Override(0,"<Keyboard>/e");Eq(InputPromptResolver.Resolve(a,InputPromptFamily.KeyboardMouse).GlyphPaths[0],"<Keyboard>/e","keyboard override path");
        var b=a.bindings[0];b.display="Z";a.bindings[0]=b;
        Eq(InputPromptResolver.Resolve(a,InputPromptFamily.KeyboardMouse).GlyphPaths[0],"<Keyboard>/z","layout-aware keyboard display");
        a.Override(0,"");Check(!InputPromptResolver.Resolve(a,InputPromptFamily.KeyboardMouse).Available,"disabled keyboard override not shown");
        a.Override(1,"");Eq(InputPromptResolver.Resolve(a,InputPromptFamily.Xbox).DisplayText,"Unbound","all disabled bindings unbound");
        Check(!InputPromptResolver.Resolve(null,InputPromptFamily.KeyboardMouse).Available,"missing action unbound");
        Eq(InputPromptResolver.Resolve(Action("<Mouse>/leftButton"),InputPromptFamily.Xbox).DisplayText,"Mouse required","mouse-only action truthful on controller");
        Eq(InputPromptResolver.Resolve(Action("<Keyboard>/e"),InputPromptFamily.PlayStation).DisplayText,"Keyboard required","keyboard-only action truthful on controller");
        Eq(InputPromptResolver.ResolvePaths("<Mouse>/leftButton","",InputPromptFamily.GenericGamepad).DisplayText,"Mouse required","fixed mouse path truthful");
        Eq(InputPromptResolver.ResolvePaths("","",InputPromptFamily.KeyboardMouse).DisplayText,"Unbound","missing fixed path unbound");
        Check(!InputPromptResolver.Resolve(Action(""),InputPromptFamily.KeyboardMouse).Available,"empty path skipped");
        Check(!InputPromptResolver.Resolve(Action("this is not a control"),InputPromptFamily.KeyboardMouse).Available,"malformed action path skipped");
        Check(!InputPromptResolver.Resolve(Action("<Keyboard>/definitelyMissingControl"),InputPromptFamily.KeyboardMouse).Available,"nonexistent control rejected despite plausible layout");
        Check(!InputPromptResolver.ResolvePaths("<Keyboard>/definitelyMissingControl","",InputPromptFamily.KeyboardMouse).Available,"nonexistent fixed path rejected");
        Check(InputPromptResolver.Resolve(Action("<Keyboard>/f13"),InputPromptFamily.KeyboardMouse).Available,"valid uncatalogued F13 remains available for text fallback");
        Check(!InputPromptResolver.Resolve(Action("<Gamepad>/definitelyMissingControl"),InputPromptFamily.Xbox).Available,"nonexistent gamepad control rejected");
        var typed=Action("<DualShockGamepad>/buttonEast","<XInputController>/buttonWest");
        Eq(InputPromptResolver.Resolve(typed,InputPromptFamily.PlayStation).DisplayText,"Circle","family-specific PS binding");
        Eq(InputPromptResolver.Resolve(typed,InputPromptFamily.Xbox).DisplayText,"X","family-specific Xbox binding");
        Check(!InputPromptResolver.Resolve(typed,InputPromptFamily.GenericGamepad).Available,"unknown pad does not use Xbox-specific binding");
        foreach(var control in new[]{"buttonSouth","buttonEast","buttonWest","buttonNorth"})
        {var label=InputPromptResolver.GamepadLabel(control,InputPromptFamily.GenericGamepad,"A");Check(label.Contains("button"),"generic positional face label "+control);}
        var c=Composite();var r2=InputPromptResolver.Resolve(c,InputPromptFamily.KeyboardMouse);
        Eq(string.Join(",",r2.GlyphPaths),"<Keyboard>/w,<Keyboard>/a,<Keyboard>/s,<Keyboard>/d","WASD physical order");
        Eq(InputPromptResolver.Resolve(c,InputPromptFamily.Xbox).DisplayText,"Left stick","controller skips keyboard composite");
        c.Override(1,"<Keyboard>/upArrow");Eq(InputPromptResolver.Resolve(c,InputPromptFamily.KeyboardMouse).GlyphPaths[0],"<Keyboard>/upArrow","composite rebound part");
        c.Override(2,"");Check(!InputPromptResolver.Resolve(c,InputPromptFamily.KeyboardMouse).Available,"partial WASD after disabled part rejected");
        var chord=Action();chord.bindings.Add(new InputBinding{path="ButtonWithOneModifier",isComposite=true});
        chord.bindings.Add(new InputBinding{name="modifier",path="<Keyboard>/leftShift",isPartOfComposite=true});
        chord.bindings.Add(new InputBinding{name="button",path="<Keyboard>/e",isPartOfComposite=true});
        Eq(InputPromptResolver.Resolve(chord,InputPromptFamily.KeyboardMouse).GlyphPaths.Length,2,"complete modifier chord");
        chord.Override(1,"");Check(!InputPromptResolver.Resolve(chord,InputPromptFamily.KeyboardMouse).Available,"disabled modifier must not imply bare key triggers action");
        var masked=Composite();var map=new InputActionMap{name="Game"};map.Add(masked);var asset=new InputActionAsset();map.asset=asset;asset.maps.Add(map);
        masked.bindingMask=InputBinding.MaskByGroup("Gamepad");Check(!InputPromptResolver.Resolve(masked,InputPromptFamily.KeyboardMouse).Available,"action mask excludes keyboard");
        Check(InputPromptResolver.Resolve(masked,InputPromptFamily.Xbox).Available,"action mask retains pad");masked.bindingMask=null;
        map.bindingMask=InputBinding.MaskByGroup("Keyboard");Check(!InputPromptResolver.Resolve(masked,InputPromptFamily.Xbox).Available,"map mask excludes pad");map.bindingMask=null;
        asset.bindingMask=InputBinding.MaskByGroup("Gamepad");Check(!InputPromptResolver.Resolve(masked,InputPromptFamily.KeyboardMouse).Available,"asset mask excludes keyboard");asset.bindingMask=null;
        map.devices=new InputDevice[]{new Keyboard()};Check(!InputPromptResolver.Resolve(masked,InputPromptFamily.Xbox).Available,"map device restriction excludes Xbox");
        Check(InputPromptResolver.Resolve(masked,InputPromptFamily.KeyboardMouse).Available,"map device restriction allows keyboard");
        map.devices=new InputDevice[]{new Mouse()};Check(!InputPromptResolver.Resolve(masked,InputPromptFamily.KeyboardMouse).Available,"Mouse-only device restriction cannot permit keyboard composite");
        var pointerAction=map.Add(Action("<Mouse>/leftButton"));Check(InputPromptResolver.Resolve(pointerAction,InputPromptFamily.KeyboardMouse).Available,"Mouse-only restriction permits actual mouse binding");
        map.devices=new InputDevice[]{new Mouse{enabled=false}};Check(!InputPromptResolver.Resolve(pointerAction,InputPromptFamily.KeyboardMouse).Available,"disabled restricted device cannot supply prompt");
        map.devices=new InputDevice[]{new Mouse{added=false}};Check(!InputPromptResolver.Resolve(pointerAction,InputPromptFamily.KeyboardMouse).Available,"removed restricted device cannot supply prompt");
        map.devices=Array.Empty<InputDevice>();Check(!InputPromptResolver.Resolve(masked,InputPromptFamily.KeyboardMouse).Available,"empty device restriction allows no device");
    }
    static void DeviceChecks()
    {
        Reset();var k=InputSystem.Add(new Keyboard());var m=InputSystem.Add(new Mouse());
        var xbox=InputSystem.Add(new Gamepad{layout="XInputController"});var ps=InputSystem.Add(new DualShockGamepad());
        var dualsense=InputSystem.Add(new Gamepad{layout="DualSenseGamepadHID"});var unknown=InputSystem.Add(new Gamepad{layout="CustomPad"});
        Eq(InputPromptDevices.Classify(xbox),InputPromptFamily.Xbox,"real XInput layout family");
        Eq(InputPromptDevices.Classify(new Gamepad{layout="CustomXInput"}),InputPromptFamily.Xbox,"derived XInput layout family");
        Eq(InputPromptDevices.Classify(ps),InputPromptFamily.PlayStation,"DualShock base family");
        Eq(InputPromptDevices.Classify(dualsense),InputPromptFamily.PlayStation,"DualSense layout family");
        Eq(InputPromptDevices.Classify(unknown),InputPromptFamily.GenericGamepad,"unknown remains generic");
        Eq(InputPromptDevices.Classify(new Gamepad()),InputPromptFamily.GenericGamepad,"bare Gamepad never Xbox");
        int updates=0,secondUpdates=0;System.Action listener=()=>updates++,second=()=>secondUpdates++;
        InputPromptDevices.Listen(listener);InputPromptDevices.Listen(second);
        Eq(InputSystem.EventListeners,1,"multiple prompts use one input event listener");Eq(InputSystem.DeviceListeners,1,"one device listener");Eq(InputSystem.ActionListeners,1,"one action listener");
        Eq(InputPromptDevices.ActiveDevice,k,"startup prefers keyboard without controller input");
        Tap(xbox);Eq(InputPromptDevices.Family,InputPromptFamily.Xbox,"keyboard to Xbox press");
        int snapshot=updates;InputSystem.Emit(xbox,xbox.buttonSouth,0f);Eq(updates,snapshot,"release does not notify");
        Tap(k);Eq(InputPromptDevices.Family,InputPromptFamily.KeyboardMouse,"Xbox to keyboard");
        InputSystem.Emit(xbox,xbox.buttonSouth,0f);Eq(InputPromptDevices.ActiveDevice,k,"release-only event cannot take keyboard ownership");
        Tap(ps);Eq(InputPromptDevices.Family,InputPromptFamily.PlayStation,"keyboard to DualShock");
        Tap(dualsense);Eq(InputPromptDevices.ActiveDevice,dualsense,"DualShock to DualSense updates active device");
        Tap(unknown);Eq(InputPromptDevices.Family,InputPromptFamily.GenericGamepad,"unknown activity is generic");
        snapshot=updates;InputSystem.Emit(m,m.delta,new Vector2(1,1));Eq(updates,snapshot,"mouse jitter ignored");
        InputSystem.Emit(m,m.delta,new Vector2(4,0));Eq(InputPromptDevices.ActiveDevice,m,"meaningful mouse movement claims keyboard/mouse");
        Tap(xbox);InputSystem.Emit(m,m.scroll,new Vector2(0,1));Eq(InputPromptDevices.ActiveDevice,m,"mouse scrolling claims family");
        Tap(xbox);InputSystem.Emit(m,m.leftButton,1f);Eq(InputPromptDevices.ActiveDevice,m,"mouse click claims family");
        Tap(k);for(int i=1;i<=25;i++)InputSystem.Emit(xbox,xbox.leftStick,new Vector2(i*.01f,0));Eq(InputPromptDevices.ActiveDevice,k,"idle drift below threshold ignored");
        for(int i=26;i<=60;i++)InputSystem.Emit(xbox,xbox.leftStick,new Vector2(i*.01f,0));Eq(InputPromptDevices.ActiveDevice,xbox,"slow deliberate tilt crosses hysteresis threshold");
        Tap(k);for(int i=0;i<30;i++)InputSystem.Emit(xbox,xbox.leftStick,new Vector2(.61f+(i%2)*.01f,0));Eq(InputPromptDevices.ActiveDevice,k,"held stick noise does not reclaim after keyboard");
        InputSystem.Emit(xbox,xbox.leftStick,new Vector2(.1f,0));InputSystem.Emit(xbox,xbox.leftStick,new Vector2(.7f,0));Eq(InputPromptDevices.ActiveDevice,xbox,"return neutral rearms stick");
        InputSystem.Emit(xbox,xbox.leftStick,new Vector2(),xbox.rightStick,new Vector2());Tap(k);
        InputSystem.Emit(xbox,xbox.leftStick,new Vector2(.8f,0),xbox.rightStick,new Vector2(.8f,0));Tap(k);
        InputSystem.Emit(xbox,xbox.leftStick,new Vector2(.81f,0),xbox.rightStick,new Vector2(.81f,0));Eq(InputPromptDevices.ActiveDevice,k,"both sticks disarm even when one already triggers");
        InputSystem.Emit(xbox,xbox.leftStick,new Vector2(-.8f,0));Eq(InputPromptDevices.ActiveDevice,xbox,"deliberate large direction change accepted");
        Tap(k);InputSystem.Emit(xbox,xbox.leftTrigger,.1f);Eq(InputPromptDevices.ActiveDevice,k,"small trigger noise ignored");
        InputSystem.Emit(xbox,xbox.leftTrigger,.8f);Eq(InputPromptDevices.ActiveDevice,xbox,"trigger crossing accepted");
        Tap(k);InputSystem.Emit(xbox,xbox.leftTrigger,.81f);Eq(InputPromptDevices.ActiveDevice,k,"held trigger noise ignored");
        InputSystem.Emit(xbox,xbox.dpad.right,1f);Eq(InputPromptDevices.ActiveDevice,xbox,"D-pad accepted");
        InputSystem.Change(xbox,InputDeviceChange.Disconnected);Eq(InputPromptDevices.ActiveDevice,k,"disconnect falls back to most recent available device");
        InputSystem.Change(xbox,InputDeviceChange.Reconnected);Eq(InputPromptDevices.ActiveDevice,k,"reconnect without activity does not switch");
        Tap(xbox);Eq(InputPromptDevices.ActiveDevice,xbox,"reconnected activity switches again");
        InputSystem.Change(xbox,InputDeviceChange.Disabled);Eq(InputPromptDevices.ActiveDevice,k,"disabled current device falls back");
        InputSystem.Emit(xbox,xbox.buttonEast,1f);Eq(InputPromptDevices.ActiveDevice,k,"disabled device input ignored");
        InputSystem.Change(xbox,InputDeviceChange.Enabled);Eq(InputPromptDevices.ActiveDevice,k,"enable without input does not switch");
        var unsupported=InputSystem.Add(new InputDevice());InputSystem.Emit(unsupported,k.enterKey,1f);Eq(InputPromptDevices.ActiveDevice,k,"unsupported devices ignored");
        var nonState=new InputEventPtr{eventType=typeof(string)};nonState.values[xbox.buttonEast]=1f;InputSystem.Emit(xbox,nonState);Eq(InputPromptDevices.ActiveDevice,k,"non-state events ignored");
        snapshot=updates;InputSystem.BindingChanged(new object());Eq(updates,snapshot+1,"BoundControlsChanged refreshes prompts");
        snapshot=updates;InputSystem.ActionChanged(new object(),InputActionChange.ActionEnabled);Eq(updates,snapshot,"unrelated action event does not churn prompts");
        InputSystem.Change(k,InputDeviceChange.ConfigurationChanged);Eq(updates,snapshot+1,"keyboard configuration change refreshes layout glyphs");
        Eq(updates,secondUpdates,"all listeners observe same events");InputPromptDevices.Unlisten(listener);Eq(InputSystem.EventListeners,1,"remaining prompt keeps shared subscription");
        InputPromptDevices.Unlisten(second);Eq(InputSystem.EventListeners,0,"last prompt removes event subscription");Eq(InputSystem.DeviceListeners,0,"last prompt removes device subscription");Eq(InputSystem.ActionListeners,0,"last prompt removes action subscription");
        snapshot=updates;InputSystem.BindingChanged(new object());Eq(updates,snapshot,"removed listener receives no events");
        InputPromptDevices.Listen(listener);Eq(InputSystem.EventListeners,1,"re-enable registers exactly once");Reset();Eq(InputSystem.EventListeners,0,"subsystem reset removes old subscriptions");
    }
    static InputPromptCatalog Catalog()
    {
        var c=new InputPromptCatalog();var glyphs=new System.Collections.Generic.List<InputPromptCatalog.Glyph>();
        foreach(var family in (InputPromptFamily[])Enum.GetValues(typeof(InputPromptFamily)))
        foreach(var key in family==InputPromptFamily.KeyboardMouse?new[]{"<Keyboard>/space","<Keyboard>/e","<Keyboard>/w","<Keyboard>/a","<Keyboard>/s","<Keyboard>/d"}:new[]{"buttonSouth","buttonWest","leftStick"})
        {string name=family+"_"+key;glyphs.Add(new InputPromptCatalog.Glyph{Family=family,ControlPath=key,Sprite=new Sprite{name=name},SpriteName=name});}
        Set(c,"_Glyphs",glyphs.ToArray());return c;
    }
    static void PresentationChecks()
    {
        Reset();var k=InputSystem.Add(new Keyboard());var xbox=InputSystem.Add(new Gamepad{layout="XInputController"});var ps=InputSystem.Add(new DualShockGamepad());var generic=InputSystem.Add(new Gamepad());
        var catalog=Catalog();var glyph=new Image();var additional=new[]{new Image(),new Image(),new Image()};var fallback=new Text();var view=new InputGlyphPrompt();
        Set(view,"_Catalog",catalog);Set(view,"_Glyph",glyph);Set(view,"_AdditionalGlyphs",additional);Set(view,"_Fallback",fallback);
        var action=Action("<Keyboard>/space","<Gamepad>/buttonSouth");view.SetAction(action);Call(view,"OnEnable");
        Check(glyph.enabled&&!fallback.enabled,"slot shows keyboard glyph");Eq(glyph.sprite,catalog.Find(InputPromptFamily.KeyboardMouse,"<Keyboard>/space"),"correct initial sprite");
        Tap(xbox);Eq(glyph.sprite,catalog.Find(InputPromptFamily.Xbox,"buttonSouth"),"authored Image refreshes on Xbox event");
        Tap(ps);Eq(glyph.sprite,catalog.Find(InputPromptFamily.PlayStation,"buttonSouth"),"authored Image refreshes on PS event");
        Tap(generic);Eq(glyph.sprite,catalog.Find(InputPromptFamily.GenericGamepad,"buttonSouth"),"authored Image shows generic positional artwork");
        action.Override(1,"<Gamepad>/buttonWest");Eq(glyph.sprite,catalog.Find(InputPromptFamily.GenericGamepad,"buttonWest"),"binding-change event refreshes image without device switch");
        action.Override(1,"");Check(!glyph.enabled&&glyph.sprite==null&&fallback.enabled,"disabled binding clears stale image");Eq(fallback.text,"Keyboard required","disabled pad binding truthful text");
        Tap(k);view.SetAction(Composite());Check(glyph.enabled&&additional.All(i=>i.enabled),"four authored slots show complete composite");
        view.SetAction(action);Check(additional.All(i=>!i.enabled&&i.sprite==null),"single-key action clears surplus composite slots");
        var insufficient=new InputGlyphPrompt();var only=new Image();var label=new Text();Set(insufficient,"_Catalog",catalog);Set(insufficient,"_Glyph",only);Set(insufficient,"_Fallback",label);insufficient.SetAction(Composite());
        Check(!only.enabled&&label.enabled&&label.text.Contains("W"),"insufficient slots use complete text rather than partial composite");
        view.SetAction(Action("<Keyboard>/f13"));Check(!glyph.enabled&&fallback.enabled&&fallback.text=="f13","unmapped control uses truthful text fallback");
        Call(view,"OnDisable");Eq(InputSystem.EventListeners,0,"disabled Image presenter removes subscription");
        var previous=fallback.text;Tap(xbox);Eq(fallback.text,previous,"disabled Image presenter stays untouched");
        var sparse=new InputPromptCatalog();var neutral=new Sprite();Set(sparse,"_Glyphs",new[]{new InputPromptCatalog.Glyph{Family=InputPromptFamily.GenericGamepad,ControlPath="buttonSouth",Sprite=neutral,SpriteName="neutral"},new InputPromptCatalog.Glyph{Family=InputPromptFamily.Xbox,ControlPath="buttonWest",Sprite=new Sprite(),SpriteName="xbox_x"}});
        Eq(sparse.Find(InputPromptFamily.PlayStation,"buttonSouth"),neutral,"missing family artwork uses generic fallback");
        Check(sparse.Find(InputPromptFamily.GenericGamepad,"buttonWest")==null,"unknown controller never falls through to Xbox sprite");
        Check(sparse.FindSpriteName(InputPromptFamily.GenericGamepad,"buttonWest")==null,"unknown controller never falls through to Xbox TMP glyph");
        var tmp=new TMP_Text();var spriteAsset=new TMP_SpriteAsset();foreach(var family in new[]{InputPromptFamily.KeyboardMouse,InputPromptFamily.Xbox,InputPromptFamily.PlayStation,InputPromptFamily.GenericGamepad})
        foreach(var key in family==InputPromptFamily.KeyboardMouse?new[]{"<Keyboard>/space","<Keyboard>/e"}:new[]{"buttonSouth","buttonWest"})spriteAsset.names.Add(catalog.FindSpriteName(family,key));
        var map=new InputActionMap{name="Game"};var jump=map.Add(Action("<Keyboard>/space","<Gamepad>/buttonSouth"));var textView=new InputPromptSpriteText();Set(textView,"_Catalog",catalog);Set(textView,"_SpriteAsset",spriteAsset);Set(textView,"_Text",tmp);textView.SetActionMap(map);textView.SetTemplate("Jump {input:Game/Jump}; select {input:<Mouse>/leftButton}");Call(textView,"OnEnable");
        Tap(k);Check(tmp.text.Contains("KeyboardMouse_")&&tmp.text.Contains("Left Button"),"TMP mixes glyph and fixed-control text fallback");
        Tap(xbox);Check(tmp.text.Contains("Xbox_buttonSouth")&&tmp.text.Contains("Mouse required"),"TMP live gamepad switch keeps mouse-only instruction truthful");
        jump.Override(1,"<Gamepad>/buttonWest");Check(tmp.text.Contains("Xbox_buttonWest"),"TMP responds to BoundControlsChanged");
        Tap(ps);Check(tmp.text.Contains("PlayStation_buttonWest"),"TMP PS West differs from Xbox X");
        Tap(generic);Check(tmp.text.Contains("GenericGamepad_buttonWest")&&!tmp.text.Contains("Xbox_"),"TMP unknown uses positional glyph");
        spriteAsset.names.Clear();textView.Refresh();Check(tmp.text.Contains("West button")&&!tmp.text.Contains("<sprite"),"missing TMP character falls back completely");
        jump.Override(1,"");Check(tmp.text.Contains("Keyboard required"),"TMP disabled binding removes stale glyph");
        textView.SetTemplate("{input:Game/Missing}");Eq(tmp.text,"Unbound","missing TMP action path safe");
        Call(textView,"OnDisable");Eq(InputSystem.EventListeners,0,"disabled TMP presenter unsubscribes");Reset();
    }
    static void RegistryAndSessionChecks()
    {
        Reset();var keyboard=InputSystem.Add(new Keyboard());var xbox=InputSystem.Add(new Gamepad{layout="XInputController"});
        typeof(InputPromptDevices).GetMethod("TrackSession",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
        Eq(InputSystem.EventListeners,1,"session tracker subscribes with no visible UI");
        Tap(xbox);Eq(InputPromptDevices.ActiveDevice,xbox,"hidden-UI controller activity remembered");
        int count=0;System.Action changed=()=>count++;InputPromptDevices.Listen(changed);Eq(InputSystem.EventListeners,1,"visible UI reuses session subscription");
        InputPromptDevices.Unlisten(changed);Eq(InputSystem.EventListeners,1,"hiding all UI retains session tracking");
        Tap(keyboard);Eq(InputPromptDevices.ActiveDevice,keyboard,"hidden-UI keyboard activity remembered");
        InputPromptDevices.Listen(changed);int before=count;
        var originalAsset=new InputActionAsset();var originalMap=new InputActionMap{name="Game",asset=originalAsset};originalAsset.maps.Add(originalMap);var original=originalMap.Add(Action("<Keyboard>/space"));
        Eq(InputPromptActions.Find("Game/Jump",originalAsset),original,"unregistered map uses authored action");
        var cloneMap=new InputActionMap{name="Game"};var clone=cloneMap.Add(Action("<Keyboard>/e"));InputPromptActions.Register(cloneMap);
        Eq(count,before+1,"register cloned map refreshes all prompts");Eq(InputPromptActions.Find("Game/Jump",originalAsset),clone,"runtime clone supersedes authored action");
        Eq(InputPromptActions.Find("Game/Jump",null),clone,"runtime action resolves without authored asset");
        InputPromptActions.Register(cloneMap);Eq(count,before+1,"repeat same registration is idempotent");
        var newerMap=new InputActionMap{name="Game"};var newer=newerMap.Add(Action("<Keyboard>/w"));InputPromptActions.Register(newerMap);InputPromptActions.Unregister(cloneMap);
        Eq(InputPromptActions.Find("Game/Jump",originalAsset),newer,"older owner cannot unregister replacement runtime map");
        Check(InputPromptActions.Find("Game/Missing",originalAsset)==null,"missing action in active runtime map does not silently use asset");
        Check(InputPromptActions.Find(null,originalAsset)==null,"missing action path safe");
        var catalog=Catalog();var image=new Image();var text=new Text();var view=new InputGlyphPrompt();Set(view,"_Catalog",catalog);Set(view,"_Glyph",image);Set(view,"_Fallback",text);Set(view,"_ActionAsset",originalAsset);Set(view,"_ActionPath","Game/Jump");Call(view,"OnEnable");
        Eq(image.sprite,catalog.Find(InputPromptFamily.KeyboardMouse,"<Keyboard>/w"),"authored Image consumes registered local runtime clone");
        newer.Override(0,"<Keyboard>/e");Eq(image.sprite,catalog.Find(InputPromptFamily.KeyboardMouse,"<Keyboard>/e"),"clone rebinding refreshes authored presenter");
        InputPromptActions.Unregister(newerMap);Eq(image.sprite,catalog.Find(InputPromptFamily.KeyboardMouse,"<Keyboard>/space"),"unregister restores authored action and refreshes immediately");
        Call(view,"OnDisable");InputPromptDevices.Unlisten(changed);Eq(InputSystem.EventListeners,1,"disabled views do not remove session owner");
        Reset();Eq(InputSystem.EventListeners,0,"subsystem reset removes session owner");
    }
    public static int Main()
    {
        ResolveChecks();DeviceChecks();PresentationChecks();RegistryAndSessionChecks();
        Console.WriteLine((failures==0?"PASS":"FAIL")+" "+checks+" production input-prompt source checks; "+failures+" failed. Boundary doubles only; NOT native Unity/hardware/render evidence.");
        return failures==0?0:1;
    }
}

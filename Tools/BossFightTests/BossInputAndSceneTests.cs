using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HauntedFish.BossFight;
static class BossInputAndSceneTests
{
    public static void Run(Action<bool,string> check)
    {
        check(BossPaddleInputPolicy.OwnsPaddle(1,1,2,true,true), "fish pointer ownership");
        check(BossPaddleInputPolicy.OwnsPaddle(2,1,2,true,true), "ghost pointer ownership");
        check(!BossPaddleInputPolicy.OwnsPaddle(3,1,2,true,true), "spectator pointer denied");
        check(!BossPaddleInputPolicy.OwnsPaddle(0,1,2,true,true), "zero pointer identity denied");
        check(!BossPaddleInputPolicy.OwnsPaddle(1,1,1,true,true), "duplicate contestants denied");
        check(!BossPaddleInputPolicy.OwnsPaddle(1,1,2,false,true), "inactive/practice match pointer denied");
        check(!BossPaddleInputPolicy.OwnsPaddle(1,1,2,true,false), "loading pointer denied");
        check(BossPaddleInputPolicy.InputAllowed(false,1), "unblocked boss controls");
        check(!BossPaddleInputPolicy.InputAllowed(true,1), "menu/dialogue focus blocks boss and practice controls");
        check(!BossPaddleInputPolicy.InputAllowed(false,0), "paused time blocks controls");
        check(!BossPaddleInputPolicy.InputAllowed(false,float.NaN), "invalid time blocks controls");
        bool Axis(float tx,float tz,float px,float pz,bool ghost,out float x,out float z,float interval=.05f) =>
            BossPaddleInputPolicy.TryAxis(tx,tz,px,pz,1000,0,3,5,.48f,7,interval,ghost,out x,out z);
        check(Axis(1002,-2,1000,-3.6f,false,out float x,out float z)&&x>0&&z>0&&Math.Abs(x*x+z*z-1)<.00001, "fish mouse direction and speed bound");
        check(Axis(1000,-3.6f,1000,-3.6f,false,out x,out z)&&x==0&&z==0, "pointer at paddle stops movement");
        check(Axis(1000,-3.565f,1000,-3.6f,false,out x,out z)&&Math.Abs(z-.1f)<.0001, "near pointer approaches without overshoot");
        check(Axis(1000,100,1000,-.55f,false,out x,out z)&&z==0, "fish cannot cross table centre");
        check(Axis(1000,-100,1000,.55f,true,out x,out z)&&z==0, "ghost cannot cross table centre");
        check(Axis(99999,-2,1002.52f,-2,false,out x,out z)&&Math.Abs(x)<.0001, "pointer outside rail clamps to paddle-radius inset");
        check(Axis(1000,-999,1000,-4.52f,false,out x,out z)&&Math.Abs(z)<.0001, "fish stays inside end rail");
        check(Axis(1000,999,1000,4.52f,true,out x,out z)&&Math.Abs(z)<.0001, "ghost stays inside end rail");
        check(!Axis(float.NaN,0,1000,-2,false,out x,out z)&&x==0&&z==0, "nonfinite pointer rejected");
        check(!Axis(1000,-2,float.PositiveInfinity,-2,false,out x,out z), "nonfinite paddle rejected");
        check(!Axis(1000,-2,1000,-3,false,out x,out z,0), "zero interval rejected");
        check(!Axis(1000,-2,1000,-3,false,out x,out z,-1), "negative interval rejected");
        check(!BossPaddleInputPolicy.TryAxis(0,0,0,0,0,0,3,5,.48f,0,.05f,false,out x,out z), "zero speed rejected");
        check(!BossPaddleInputPolicy.TryAxis(0,0,0,0,0,0,.2f,5,.48f,7,.05f,false,out x,out z), "invalid authored width rejected");
        string scenePath=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../Assets/Scenes/BossFight.unity"));
        string scene=File.ReadAllText(scenePath);
        var sourcePrefab=Regex.Match(scene,@"m_SourcePrefab: \{fileID: 100100000, guid: ([a-f0-9]+), type: 3\}");
        if(sourcePrefab.Success)
        {
            string prefabPath=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../Assets/Resources/BossFight/BossFightArena.prefab"));
            check(File.ReadAllText(prefabPath+".meta").Contains(sourcePrefab.Groups[1].Value),"scene references native authored arena prefab GUID");
            check(!scene.Contains("GameObject:"),"boss scene does not duplicate authored runtime hierarchy");
            scene=File.ReadAllText(prefabPath);
        }
        var docs=Regex.Matches(scene,@"(?ms)^--- !u!(\d+) &(\d+)(?: stripped)?\r?\n(.*?)(?=^--- !u!|\z)");
        var ids=new Dictionary<string,string>();
        foreach(Match doc in docs) check(ids.TryAdd(doc.Groups[2].Value,doc.Groups[3].Value),"unique scene fileID "+doc.Groups[2].Value);
        foreach(Match reference in Regex.Matches(scene,@"\{fileID: (-?\d+)\}"))
            check(reference.Groups[1].Value=="0"||ids.ContainsKey(reference.Groups[1].Value),"serialized scene reference "+reference.Groups[1].Value);
        var actorRefs=new[]{Regex.Match(scene,@"_FishActor: \{fileID: (\d+)\}").Groups[1].Value,Regex.Match(scene,@"_GhostActor: \{fileID: (\d+)\}").Groups[1].Value};
        bool segmented=Regex.IsMatch(scene,@"_GhostRig: \{fileID: [1-9]\d*\}");
        foreach(string pivot in actorRefs)
        {
            if(segmented&&pivot==actorRefs[1])
            {
                string rigPath=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(scenePath),"../Resources/BossFight/BossSegmentedOpponent.prefab"));
                string rig=File.ReadAllText(rigPath),meta=File.ReadAllText(rigPath+".meta");
                string guid=Regex.Match(meta,@"guid: ([a-f0-9]+)").Groups[1].Value;
                check(scene.Contains(guid),"arena references dedicated segmented opponent prefab");
                check(Regex.Matches(rig,@"(?m)^SpriteRenderer:").Count==28,"27 authored visible parts plus one nonrendering silhouette source");
                check(!Regex.IsMatch(rig,@"(?m)^(Rigidbody|BoxCollider|SphereCollider|CapsuleCollider|MeshCollider):"),"segmented art adds no physics objects");
                check(Regex.Matches(rig,@"m_Name: Head\r?\n").Count==3,"one head per life variant");
                check(Regex.Matches(rig,@"m_Name: Left tentacle").Count==12&&Regex.Matches(rig,@"m_Name: Right tentacle").Count==12,"four left/four right tentacles per authored variant");
                continue;
            }
            var children=Regex.Matches(ids[pivot],@"(?m)^  - \{fileID: (\d+)\}$");
            check(children.Count==1,"one standing artwork beneath reaction pivot "+pivot);
            string child=children[0].Groups[1].Value;
            string go=Regex.Match(ids[child],@"m_GameObject: \{fileID: (\d+)\}").Groups[1].Value;
            var parts=Regex.Matches(ids[go],@"component: \{fileID: (\d+)\}").Cast<Match>();
            check(parts.Count(p=>ids[p.Groups[1].Value].StartsWith("SpriteRenderer:"))==1,"one contestant SpriteRenderer "+pivot);
        }
        check(ids.Values.Where(value=>value.StartsWith("GameObject:")&&value.Contains("paddle character badge")).All(value=>value.Contains("m_IsActive: 0")),"paddle character badges disabled");
    }
}
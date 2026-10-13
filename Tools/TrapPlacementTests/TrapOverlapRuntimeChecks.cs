using System;
using System.Linq;
using System.Reflection;
using HauntedFish.Multiplayer;
using UnityEngine;
using UnityEngine.UI;
// Called by the real host runner after its first cart placement. Not a mock world.
public static class TrapOverlapRuntimeChecks
{
    public static void Run(GhostPlacementWorld world, HotelPlayer player, GhostTrap placed, Action<bool,string> check)
    {
        var definition=world.Definition("cart");
        check(!world.TrapFootprintClear(definition,placed.Position),"Registry rejects identical placement footprint");
        var edge=placed.Position+Vector3.right*(definition.BodyHalfSize.x*2);
        check(world.TrapFootprintClear(definition,edge),"Registry permits exactly adjacent body footprints");
        check(!world.TrapFootprintClear(definition,edge-Vector3.right*.001f),"Registry rejects thin intersection omitted by shrunken physics query");
        check(!world.TrapFootprintClear(definition,placed.Position,90),"Registry rejects rotated footprint intersection");
        var visual=placed.GetComponent<GhostTrapAreaPresentation>();
        if(!visual)visual=placed.GetComponentInChildren<GhostTrapAreaPresentation>(true);
        check(visual,"Placed trap has presentation");
        var worldArt=visual.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.name.EndsWith(" world art")).ToArray();
        check(worldArt.Length>=2&&worldArt.All(r=>r.sortingLayerID==0&&r.sortingOrder<=-10),"World trap renderers sort below authored fish body and fin");
        var root=(GameObject)typeof(GhostTrapAreaPresentation).GetField("_Root",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(visual);
        check(root.GetComponentsInChildren<Image>(true).All(i=>!i.enabled&&!i.raycastTarget),"No overlay trap images draw over fish or capture menu clicks");
        // Two requests both start from a clear preview; the second must observe the
        // first's registry insertion immediately and retain its held resource.
        var candidate=placed.Position+Vector3.right*4;
        check(world.PlacementPoint(candidate,"cart",out var point),"Competing request starts with a valid canonical preview");
        typeof(GhostPlacementController).GetField("_Position",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(world.Controls,point);
        check(world.Hold(player,"cart",point),"Authority holds first competing placement resource");
        player.RequestPlaceHeldTrap(point,"cart",player.RoundStateKey,player.RoundVersion);
        check(player.GhostPlacementAccepted&&world.Count==2,"First request commits its registry reservation");
        var second=UnityEngine.Object.FindObjectsByType<GhostTrap>(FindObjectsSortMode.None).Single(t=>t.World==world&&t.Index!=placed.Index);
        check(world.Hold(player,"cart",point),"Authority holds second competing placement resource");
        // Call the canonical validator directly as well: cooldown must not hide
        // a missing registry check when requests arrive in the same frame.
        check(!world.PlacementPoint(candidate,"cart",out _),"Second canonical preview sees committed footprint before physics sync");
        typeof(GhostPlacementWorld).GetField("_NextPlacement",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(world,0f);
        player.RequestPlaceHeldTrap(point,"cart",player.RoundStateKey,player.RoundVersion);
        check(!player.GhostPlacementAccepted&&world.Count==2&&player.HeldTrapFamily=="cart","Competing owned request is rejected without spending held resource");
        check(world.Dispose(player,-1),"Rejected request retains disposable inventory");
        world.Possess(player,second.Index,point);
        check(world.Dispose(player,second.Index)&&world.Count==1,"Disposal removes competing footprint reservation");
        check(world.TrapFootprintClear(definition,point),"Disposed footprint is reusable immediately");
        check(!second.gameObject.activeInHierarchy,"Disposal hides owned world sprite children immediately");
    }
}
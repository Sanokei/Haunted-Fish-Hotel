using System;
using System.IO;
using HauntedFish.Multiplayer;
int checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
var a = new TrapFootprint(0, 0, 1, .5, 0);
Check(a.Overlaps(a), "Identical footprint overlaps");
Check(a.Overlaps(new TrapFootprint(1.999, 0, 1, .5, 0)), "Thin intersecting strip rejected without collider shrink");
Check(!a.Overlaps(new TrapFootprint(2, 0, 1, .5, 0)), "Exact horizontal adjacency allowed");
Check(!a.Overlaps(new TrapFootprint(0, 1, 1, .5, 0)), "Exact depth adjacency allowed");
Check(!a.Overlaps(new TrapFootprint(2.01, 0, 1, .5, 0)), "Separated bodies allowed");
Check(a.Overlaps(new TrapFootprint(0, 0, .1, .1, 73)), "Contained rotated footprint rejected");
Check(a.Overlaps(new TrapFootprint(1.49, 0, 1, .5, 90)), "Quarter-turn intersection rejected");
Check(!a.Overlaps(new TrapFootprint(1.5, 0, 1, .5, 90)), "Quarter-turn adjacency allowed");
var diagonal = new TrapFootprint(0, 0, 1, .1, 45);
Check(!diagonal.Overlaps(new TrapFootprint(.4, .4, 1, .1, 45)), "Separated parallel rotated bodies with overlapping AABBs allowed");
Check(diagonal.Overlaps(new TrapFootprint(.05, .05, 1, .1, 45)), "Parallel rotated intersection rejected");
var random = new Random(734);
for (int i = 0; i < 1000; i++)
{
    var left = new TrapFootprint(random.NextDouble()*6,random.NextDouble()*4,.2+random.NextDouble(),.1+random.NextDouble(),random.NextDouble()*360);
    var right = new TrapFootprint(random.NextDouble()*6,random.NextDouble()*4,.2+random.NextDouble(),.1+random.NextDouble(),random.NextDouble()*360);
    Check(left.Overlaps(right)==right.Overlaps(left), "Overlap symmetry");
    var repeated = new TrapFootprint(left.X,left.Z,left.HalfX,left.HalfZ,Math.Atan2(left.Sin,left.Cos)*180/Math.PI+360);
    Check(left.Overlaps(right)==repeated.Overlaps(right), "Full-turn rotation invariance");
}
// These are integration source checks, not runtime network or render QA.
string root = Directory.GetCurrentDirectory();
string Read(string file) => File.ReadAllText(Path.Combine(root,"Assets/Scripts/Multiplayer",file));
var world = Read("GhostPlacementWorld.cs");
var controller = Read("GhostPlacementController.cs");
var visual = Read("GhostTrapAreaPresentation.cs");
var trap = Read("GhostTrap.cs");
Check(world.Contains("TrapFootprintClear(prefab, grounded + prefab.ArmedOffset)"),"Canonical placement checks registry");
Check(controller.Contains("_World.PlacementPoint(target, _Family, out var placement)"),"Preview uses canonical placement");
Check(world.IndexOf("!PlacementPoint(position, family, out var grounded)") >= 0 && world.IndexOf("!PlacementPoint(position, family, out var grounded)") < world.IndexOf("_State.Cubes.Add(record)"),"Authority revalidates before publishing or consuming inventory");
Check(world.Contains("_Objects.Clear();") && world.Contains("_Objects.Remove(id);"),"Round reset and disposal remove reservations");
Check(world.Contains("state.Position, state.Rotation")&&world.Contains("state.Origin + trap.ArmedOffset, state.Rotation"),"Current and reset positions retain rotated reservations");
Check(trap.Contains("TrapFootprintClear(this, target, _State.Rotation, _State.Id)"),"Reset revalidates other reservations and excludes itself");
Check(visual.Contains("image.enabled = false;")&&visual.Contains("art.sortingOrder = image == _CartImage ? -10 : -20;"),"Overlay art replaced by world sprites below fish body and fin");
Check(visual.Contains("art.transform.SetParent(transform, false);")&&visual.Contains("if (art) art.enabled = false;")&&visual.Contains("Destroy(_WhiteSprite)"),"World visual ownership, disable, and generated sprite cleanup");
Check(controller.Contains("placement + definition.ArmedOffset + definition.ColliderOffset"),"Snapped destination uses exact final body center");
Check(controller.Contains("if (ceilingPlacement && !_Pending) PresentPlacementPreview(heldDefinition, placement, clear);"),"Destination validity comes from the same canonical footprint preview");
Check(controller.Contains("_Preview.transform.SetPositionAndRotation(_Position, Quaternion.identity);"),"Carried artwork retains flight position");
Check(controller.Contains("HidePlacementPreview();")&&controller.Contains("if (_PlacementPreview) Destroy(_PlacementPreview);"),"Destination hides on lifecycle boundaries and is destroyed with owner");
Console.WriteLine($"PASS {checks} geometry and integration source checks; no Unity runtime QA performed.");
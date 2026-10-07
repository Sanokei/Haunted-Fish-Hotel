using UnityEngine;
namespace HauntedFish.Multiplayer
{
    public enum TrapInputMode { Movement, MouseClick, Space }
    public enum TrapInputKind { Move, Click, Activate, Reset }
    public enum TrapBehaviour { Slide, Pulse, Fall }
    public enum TrapPhase { Armed, Active, Latched }
    public readonly struct TrapInput
    {
        public readonly TrapInputKind Kind;
        public readonly float Axis;
        public readonly Vector3 Point;
        public TrapInput(TrapInputKind kind, float axis = 0, Vector3 point = default)
        { Kind = kind; Axis = axis; Point = point; }
    }
    // The world owns authority and replication; each authored prefab owns behavior and tuning.
    public interface IGhostTrap
    {
        string FamilyTag { get; }
        TrapInputMode InputMode { get; }
        Vector3 Position { get; }
        bool AcceptInput(TrapInput input, float now);
        bool Simulate(float seconds, float now);
        GhostCubePlacement CaptureState();
        void ApplyState(GhostCubePlacement state);
    }
}

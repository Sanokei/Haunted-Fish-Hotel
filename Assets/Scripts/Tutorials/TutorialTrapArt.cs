using HauntedFish.Multiplayer;
using UnityEngine;
namespace HauntedFish.Tutorials
{
    // Animates an authored sprite from real trap phase; creates no presentation objects.
    public sealed class TutorialTrapArt : MonoBehaviour
    {
        public GhostTrap Trap;
        public SpriteRenderer Art;
        Vector3 scale;
        void Awake() { scale = Art.transform.localScale; }
        void LateUpdate()
        {
            float pulse = Trap.InputMode == TrapInputMode.MouseClick && Trap.Phase == TrapPhase.Active
                ? 1 + .1f * Mathf.Sin(Trap.CaptureState().Progress * Mathf.PI) : 1;
            Art.transform.localScale = scale * pulse;
        }
    }
}

// Stand-ins for sibling boss-fight components the arena manager only calls into.
using HauntedFish.BossFight;
using UnityEngine;
namespace HauntedFish.BossFight
{
    public enum BossViewerSide { Fish, Ghost, Spectator }
    public sealed class BossCharacterPresentation : MonoBehaviour
    {
        public void ResetPose() { }
        public void SetGhostLifeStage(int fishGoals) { }
        public void Present(BossArenaSnapshot state) { }
        public float ReactionDuration(bool final) => 0;
        public void PresentFuse(bool armed, float heat, float seconds) { }
        public void PresentBlast(bool onBossSide) { }
    }
    public sealed class BossArenaView : MonoBehaviour
    {
        public Camera CurrentCamera => null;
        public void Select(BossViewerSide side, bool watching) { }
        public void Fit() { }
    }
    public sealed class BossHudPresentation : MonoBehaviour
    {
        public void Present(BossArenaSnapshot state, BossViewerSide side, bool watching, bool practice) { }
    }
    public sealed class BossDragInput : MonoBehaviour { public void Cancel() { } }
}

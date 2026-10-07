using UnityEngine;
namespace HauntedFish.Multiplayer
{
    // Editable sequence reference and cutscene pacing live with the reusable authored prefab.
    public sealed class RoundIntroductionSettings : MonoBehaviour
    {
        public TextAsset Sequence;
        [Min(0.1f)] public float TimingScale = 1;
    }
}

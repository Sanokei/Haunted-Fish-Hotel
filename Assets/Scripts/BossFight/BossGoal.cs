using UnityEngine;

namespace HauntedFish.BossFight
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class BossGoal : MonoBehaviour
    {
        [SerializeField]
        BossArenaManager _Arena;
        [SerializeField]
        bool _FishScores;
        void OnTriggerEnter(Collider other)
        {
            if (_Arena && other.attachedRigidbody == _Arena.Puck)
                _Arena.RegisterGoal(_FishScores);
        }
    }
}

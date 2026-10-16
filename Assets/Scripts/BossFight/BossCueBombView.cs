using UnityEngine;

namespace HauntedFish.BossFight
{
    // Presentation only. Everything seen here is authored: an Animator on the cue ball holds the burning-fuse and glow
    // states, a second Animator on a free-standing blast object holds the explosion, and a stun marker animator sits on
    // each paddle. This component just selects states, so it has no per-frame code. The authority drives it from bomb
    // events and clients drive it from snapshots, through the same three calls.
    public sealed class BossCueBombView : MonoBehaviour
    {
        [SerializeField] Animator _Fuse, _Blast, _FishStun, _GhostStun;
        [SerializeField] Transform _BlastRoot;
        // Flipbook fireball (Kenney smoke-particles explosion sheet) under the blast root, burst once per detonation.
        [SerializeField] ParticleSystem _Fireball;
        static readonly int Idle = Animator.StringToHash("Idle");
        static readonly int Burn = Animator.StringToHash("Burn");
        static readonly int Boom = Animator.StringToHash("Boom");
        static readonly int Stun = Animator.StringToHash("Stun");

        public bool ValidateAuthoredBindings(out string error)
        {
            error = null;
            if (!_Fuse || !_Blast || !_BlastRoot || !_FishStun || !_GhostStun) { error = "Assign the fuse, blast and both stun animators and the blast root."; return false; }
            return true;
        }

        // The Burn state is one authored second of glow and spark that gets hotter as it plays. Starting it at the
        // current heat and slowing it to the fuse length makes it finish exactly at detonation.
        public void Light(float heat, float fuseSeconds)
        {
            if (!_Fuse || !float.IsFinite(heat) || !float.IsFinite(fuseSeconds) || fuseSeconds <= 0) return;
            _Fuse.speed = 1f / fuseSeconds;
            _Fuse.Play(Burn, 0, Mathf.Clamp01(heat));
        }

        public void Defuse()
        {
            if (!_Fuse) return;
            _Fuse.speed = 1;
            _Fuse.Play(Idle, 0, 0);
        }

        public void Detonate(Vector3 position, BossSide victim)
        {
            Defuse();
            if (_BlastRoot && _Blast)
            {
                _BlastRoot.position = position;
                _Blast.Play(Boom, 0, 0);
                if (_Fireball) { _Fireball.Clear(true); _Fireball.Play(true); }
            }
            var marker = victim == BossSide.Fish ? _FishStun : victim == BossSide.Ghost ? _GhostStun : null;
            if (marker) marker.Play(Stun, 0, 0);
        }

        void OnDisable() => Defuse();
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using Monologue.Dialogue;
using UnityEngine;

namespace HauntedFish.Multiplayer
{
    // Lifecycle only. All visuals, bindings and animation destinations are authored in the prefab.
    public sealed class LobbyIntroductionPresentation : MonoBehaviour
    {
        [SerializeField] StoryUI _UIPrefab;
        StoryUI _UI;
        public StoryUI UI => _UI;
        public void Prepare()
        {
            if (!_UI)
            {
                if (!_UIPrefab) throw new InvalidOperationException("Assign the authored introduction UI prefab.");
                _UI = Instantiate(_UIPrefab, transform, false);
            }
            _UI.Prepare();
        }
        public bool Supports(StorySequenceStep step) => _UIPrefab && _UIPrefab.Supports(step);
        public void ValidateSequence(IReadOnlyList<StorySequenceStep> steps)
        {
            if (!_UIPrefab) throw new InvalidOperationException("Assign the authored introduction UI prefab.");
            _UIPrefab.ValidateSequence(steps);
        }
        public IEnumerator Play(StorySequenceStep step) { Prepare(); yield return _UI.Play(step); }
        public void SetSkipProgress(float progress) { if (_UI) _UI.SetSkipProgress(progress); }
        public void Close()
        {
            if (!_UI) return;
            _UI.Close();
            Destroy(_UI.gameObject);
            _UI = null;
        }
        void OnDisable() => Close();
    }
}

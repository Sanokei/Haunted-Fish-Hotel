using System.Collections;
using Monologue.Dialogue;
using UnityEngine;

namespace HauntedFish.Multiplayer
{
    // Ink uses # animation: grandma_rimshot on the punchline. Never hook per-character sound.
    public sealed class GrandmaFishPunchlineAudio : MonoBehaviour
    {
        [SerializeField] AudioSource _Source;
        [SerializeField] AudioClip _Rimshot;
        Coroutine _Pending;
        Ink.Runtime.Story _LastStory;
        string _LastLine;
        int _LastTurn = -1;

        void OnEnable()
        {
            StoryFunctions.OnAnimationEvent += Cue;
            DialogueManager.OnDialogueEndEvent += Cancel;
        }
        void Cue(string tag)
        {
            if (tag != "grandma_rimshot") return;
            var manager = DialogueManager.Instance;
            if (!manager || manager.CurrentStory == null || !manager._DialoguePanel) return;
            var story = manager.CurrentStory;
            var line = manager._DialoguePanel.DialogueText;
            var turn = story.state.currentTurnIndex;
            if (_LastStory == story && _LastTurn == turn && _LastLine == line) return;
            Cancel();
            _LastStory = story; _LastTurn = turn; _LastLine = line;
            _Pending = StartCoroutine(AfterReveal(manager, story, line));
        }
        IEnumerator AfterReveal(DialogueManager manager, Ink.Runtime.Story story, string line)
        {
            // Wait one frame for the world canvas to initialize its typewriter.
            yield return null;
            while (manager && manager.ActiveDialoguePanel && manager.CurrentStory == story &&
                   manager._DialoguePanel && manager._DialoguePanel.DialogueText == line)
            {
                var text = manager._DialoguePanel.SpeechText;
                if (!text || text.maxVisibleCharacters >= text.textInfo.characterCount)
                {
                    if (_Source && _Rimshot) _Source.PlayOneShot(_Rimshot);
                    _Pending = null;
                    yield break;
                }
                yield return null;
            }
            _Pending = null; // Advancing/closing before the punchline is revealed cancels it.
        }
        void Cancel()
        {
            if (_Pending != null) StopCoroutine(_Pending);
            _Pending = null;
            if (_Source) _Source.Stop();
        }
        void OnDisable()
        {
            StoryFunctions.OnAnimationEvent -= Cue;
            DialogueManager.OnDialogueEndEvent -= Cancel;
            Cancel();
            _LastStory = null; _LastLine = null; _LastTurn = -1;
        }
    }
}
                   
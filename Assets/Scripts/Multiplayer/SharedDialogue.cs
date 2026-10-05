using System;
using System.Linq;
using Mirage;
using Monologue.Dialogue;
using Monologue.StoryInput;
using UnityEngine;

namespace HauntedFish.Multiplayer
{
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class SharedDialogue : NetworkBehaviour
    {
        public static SharedDialogue Instance { get; private set; }

        [SyncVar]
        string _SnapshotJson = "";
        [SyncVar]
        public uint SpeakerPlayerId;
        DialogueCatalog _Catalog;
        string _LastApplied = "", _LastContent = "";
        int _Revision;
        bool _PublishPending;
        bool _SharedWasActive;
        public int Revision => _Revision;
        public static bool Applying { get; private set; }

        void Awake()
        {
            Instance = this;
            _Catalog = Resources.Load<DialogueCatalog>("MultiplayerDialogueCatalog");
        }

        void OnEnable()
        {
            StoryFunctions.OnSpeakerEvent += Speaker;
            StoryFunctions.OnCameraSetEvent += CameraCue;
            StoryFunctions.OnMoveToEvent += MoveCue;
            StoryFunctions.OnEmojiEvent += EmojiCue;
            StoryFunctions.OnAnimationEvent += AnimationCue;
            DialogueManager.OnDialogueStartEvent += RequestPublish;
            DialogueManager.OnDialogueContinuedEvent += RequestPublish;
            DialogueManager.OnDialogueEndEvent += RequestPublish;
            StoryInputTextFieldManager.OnStoryInputStartEvent += RequestPublish;
            StoryInputTextFieldManager.OnStoryInputEndEvent += RequestPublish;
        }

        void OnDisable()
        {
            StoryFunctions.OnSpeakerEvent -= Speaker;
            StoryFunctions.OnCameraSetEvent -= CameraCue;
            StoryFunctions.OnMoveToEvent -= MoveCue;
            StoryFunctions.OnEmojiEvent -= EmojiCue;
            StoryFunctions.OnAnimationEvent -= AnimationCue;
            DialogueManager.OnDialogueStartEvent -= RequestPublish;
            DialogueManager.OnDialogueContinuedEvent -= RequestPublish;
            DialogueManager.OnDialogueEndEvent -= RequestPublish;
            StoryInputTextFieldManager.OnStoryInputStartEvent -= RequestPublish;
            StoryInputTextFieldManager.OnStoryInputEndEvent -= RequestPublish;
        }

        void SendCue(SharedWorldCue cue)
        {
            RequestPublish();
            if (IsServer && DialogueManager.Instance && DialogueManager.Instance.IsSharedDialogue)
                Server.SendToAll(cue, authenticatedOnly: true, excludeLocalPlayer: true);
        }

        void Speaker(string speaker) => SendCue(new SharedWorldCue { Type = 0, First = speaker });
        void CameraCue(string camera, bool back) => SendCue(new SharedWorldCue { Type = 1, First = camera, Flag = back });
        void MoveCue(string character, int x, int y, float delay, bool disappear) => SendCue(new SharedWorldCue { Type = 2, First = character, X = x, Y = y, Delay = delay, Flag = disappear });
        void EmojiCue(string emoji, string character) => SendCue(new SharedWorldCue { Type = 3, First = emoji, Second = character });
        void AnimationCue(string animation) => SendCue(new SharedWorldCue { Type = 4, First = animation });
        void OnDestroy()
        {
            if (Instance != this)
                return;
            if (DialogueManager.Instance && DialogueManager.Instance.IsSharedDialogue)
                Apply(DialogueManager.Instance.ExitDialogMode);
            Instance = null;
            Applying = false;
        }

        public static void CloseForTravel()
        {
            var manager = DialogueManager.Instance;
            if (manager && (manager.ActiveDialoguePanel || manager.IsSharedDialogue || (StoryInputTextFieldManager.Instance && StoryInputTextFieldManager.Instance.ActiveInputPanel)))
                Apply(manager.ExitDialogMode);
        }

        public bool Open(TextAsset story, Vector3 anchor)
        {
            var player = LocalPlayer();
            if (!Identity.IsSpawned || !player)
                return false;
            player.RequestDialogue(0, DialogueCatalog.Key(story), anchor, -1, _Revision, "", "");
            return true;
        }

        public bool Route(int action, int choice = -1, string key = "", string value = "")
        {
            if (Applying || !Identity.IsSpawned || !DialogueManager.Instance || !DialogueManager.Instance.IsSharedDialogue)
                return false;
            var player = LocalPlayer();
            if (player)
                player.RequestDialogue(action, "", Vector3.zero, choice, _Revision, key, value);
            return true;
        }

        HotelPlayer LocalPlayer() => HotelPlayer.LocalPlayer && HotelPlayer.LocalPlayer.IsLocalPlayer
            ? HotelPlayer.LocalPlayer : null;

        public void HandleRequest(HotelPlayer player, int action, string storyKey, Vector3 anchor, int choice, int expectedRevision, string inputKey, string inputValue)
        {
            if (!IsServer || !player || !player.Identity.IsSpawned || !DialogueManager.Instance || !_Catalog)
                return;
            var manager = DialogueManager.Instance;
            if (action == 0)
            {
                var story = _Catalog.Find(storyKey);
                if (!story || manager.ActiveDialoguePanel || manager.IsSharedDialogue)
                    return;
                bool source = FindObjectsByType<EnterDialogueMode>(FindObjectsSortMode.None).Any(t =>
                    t.Audience == DialogueAudience.Everyone && t.InkAsset == story &&
                    Vector3.Distance(t.transform.position, anchor) < .25f);
                source |= FindObjectsByType<TriggerDialogue>(FindObjectsSortMode.None).Any(t =>
                    t.Audience == DialogueAudience.Everyone && t.InkAsset == story &&
                    Vector3.Distance(t.transform.position, anchor) < .25f);
                if (!source || Vector3.Distance(player.transform.position, anchor) > 6)
                    return;
                SpeakerPlayerId = player.NetId;
                Apply(() => manager.BeginSharedDialogue(story, player.transform.position));
            }
            else
            {
                // Reject concurrent or stale clicks against an already advanced story.
                if (!manager.IsSharedDialogue || expectedRevision != _Revision || manager.IsWaiting)
                    return;
                if (action == 1 && manager.CurrentStory?.currentChoices.Count == 0)
                    Apply(manager.ContinueStory);
                else if (action == 2 && manager.CurrentStory != null && choice >= 0 && choice < manager.CurrentStory.currentChoices.Count)
                    Apply(() => manager.ChoiceSelected(choice));
                else if (action == 3)
                    Apply(manager.ExitDialogMode);
                else if (action == 4 && inputValue != null && inputValue.Length <= 256 && StoryInputTextFieldManager.Instance && StoryInputTextFieldManager.Instance.ActiveInputPanel && inputKey == StoryInputTextFieldManager.Instance.CurrentKey)
                    Apply(() => StoryInputTextFieldManager.Instance.SubmitShared(inputKey, inputValue));
                else
                    return;
            }

            Publish();
        }

        static void Apply(Action action)
        {
            Applying = true;
            try
            {
                action();
            }
            finally
            {
                Applying = false;
            }
        }

        void RequestPublish()
        {
            if (IsServer && (_SharedWasActive || (DialogueManager.Instance && DialogueManager.Instance.IsSharedDialogue)))
                _PublishPending = true;
        }

        void LateUpdate()
        {
            if (!Identity.IsSpawned)
                return;
            if (IsServer && _PublishPending)
            {
                Publish();
            }

            if (!IsServer)
                ApplyReceivedSnapshot();
        }

        // Clients render the server's snapshot without executing Ink story effects again.
        void ApplyReceivedSnapshot()
        {
            if (_SnapshotJson != "" && _SnapshotJson != _LastApplied && DialogueManager.Instance && _Catalog)
            {
                var snapshot = JsonUtility.FromJson<DialogueSnapshot>(_SnapshotJson);
                var story = _Catalog.Find(snapshot.storyKey);
                if (snapshot.active && !story)
                {
                    Debug.LogError("Shared dialogue missing from catalog. Rebuild catalog on all clients.");
                    return;
                }

                _Revision = snapshot.revision;
                Apply(() => DialogueManager.Instance.ApplySharedSnapshot(snapshot, story));
                _LastApplied = _SnapshotJson;
            }
        }

        void Publish()
        {
            var manager = DialogueManager.Instance;
            if (!manager)
                return;
            _PublishPending = false;
            _SharedWasActive = manager.IsSharedDialogue;
            var input = StoryInputTextFieldManager.Instance;
            var data = new DialogueSnapshot
            {
                active = manager.IsSharedDialogue && (manager.ActiveDialoguePanel || (input && input.ActiveInputPanel)),
                storyKey = DialogueCatalog.Key(manager.CurrentAsset),
                storyState = manager.CurrentStory?.state.ToJson() ?? "",
                text = manager._DialoguePanel.DialogueText,
                speaker = manager._DialoguePanel.DialogueDisplayName,
                choices = manager.CurrentStory?.currentChoices.Select(c => c.text).ToArray() ?? Array.Empty<string>(),
                anchor = manager.DialogueAnchor,
                inputActive = input && input.ActiveInputPanel,
                inputQuestion = input ? input.CurrentQuestion : "",
                inputKey = input ? input.CurrentKey : ""
            };
            var content = JsonUtility.ToJson(data);
            if (content == _LastContent)
                return;
            _LastContent = content;
            data.revision = ++_Revision;
            _SnapshotJson = JsonUtility.ToJson(data);
        }
    }
}

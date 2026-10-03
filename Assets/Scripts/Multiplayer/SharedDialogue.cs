using System;
using System.Linq;
using Mirage;
using Monologue.Dialogue;
using Monologue.StoryInput;
using UnityEngine;
namespace HauntedFish.Multiplayer
{
    // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
    [RequireComponent(typeof(NetworkIdentity))]
    // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
    public sealed class SharedDialogue : NetworkBehaviour
    {
        // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
        public static SharedDialogue Instance { get; private set; }
        // Mirage serializes this authoritative server value to observers; clients use the received state for presentation.
        [SyncVar] string snapshotJson = "";
        // Mirage serializes this authoritative server value to observers; clients use the received state for presentation.
        [SyncVar] public uint SpeakerPlayerId;
        DialogueCatalog catalog;
        // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
        string lastApplied = "", lastContent = "";
        // Require the currently displayed story revision so duplicated or stale clicks cannot advance the shared story twice.
        int revision;
        float nextCapture;
        // Require the currently displayed story revision so duplicated or stale clicks cannot advance the shared story twice.
        public int Revision => revision;
        // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
        public static bool Applying { get; private set; }
        // Resolve the authored dependencies early; the scene and prefab data determine what exists.
        void Awake()
        {
            // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
            Instance = this;
            // Resolve an existing authored resource asset by name; resource lookup does not construct scene dependencies.
            catalog = Resources.Load<DialogueCatalog>("MultiplayerDialogueCatalog");
        }
        // Subscribe while this existing component is active so presentation reacts to the current story or scene.
        void OnEnable()
        {
            // Apply the story system's existing authored effects through the selected private or shared authority path.
            StoryFunctions.OnSpeakerEvent += Speaker;
            // Apply the story system's existing authored effects through the selected private or shared authority path.
            StoryFunctions.OnCameraSetEvent += CameraCue;
            // Apply the story system's existing authored effects through the selected private or shared authority path.
            StoryFunctions.OnMoveToEvent += MoveCue;
            // Apply the story system's existing authored effects through the selected private or shared authority path.
            StoryFunctions.OnEmojiEvent += EmojiCue;
            // Apply the story system's existing authored effects through the selected private or shared authority path.
            StoryFunctions.OnAnimationEvent += AnimationCue;
        }
        // Unsubscribe on deactivation to prevent stale listeners from acting on a later scene/session.
        void OnDisable()
        {
            // Apply the story system's existing authored effects through the selected private or shared authority path.
            StoryFunctions.OnSpeakerEvent -= Speaker;
            // Apply the story system's existing authored effects through the selected private or shared authority path.
            StoryFunctions.OnCameraSetEvent -= CameraCue;
            // Apply the story system's existing authored effects through the selected private or shared authority path.
            StoryFunctions.OnMoveToEvent -= MoveCue;
            // Apply the story system's existing authored effects through the selected private or shared authority path.
            StoryFunctions.OnEmojiEvent -= EmojiCue;
            // Apply the story system's existing authored effects through the selected private or shared authority path.
            StoryFunctions.OnAnimationEvent -= AnimationCue;
        }
        // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
        void SendCue(SharedWorldCue cue)
        {
            // Only the authoritative server changes shared simulation; remote clients consume the resulting state.
            if (IsServer && DialogueManager.Instance && DialogueManager.Instance.IsSharedDialogue)
                // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
                Server.SendToAll(cue, authenticatedOnly: true, excludeLocalPlayer: true);
        }
        // Keep this small operation on the existing component so callers share one state transition.
        void Speaker(string speaker) => SendCue(new SharedWorldCue { Type=0, First=speaker });
        // Keep this small operation on the existing component so callers share one state transition.
        void CameraCue(string camera, bool back) => SendCue(new SharedWorldCue { Type=1, First=camera, Flag=back });
        // Keep this small operation on the existing component so callers share one state transition.
        void MoveCue(string character, int x, int y, float delay, bool disappear) =>
            // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
            SendCue(new SharedWorldCue { Type=2, First=character, X=x, Y=y, Delay=delay, Flag=disappear });
        // Keep this small operation on the existing component so callers share one state transition.
        void EmojiCue(string emoji, string character) => SendCue(new SharedWorldCue { Type=3, First=emoji, Second=character });
        // Keep this small operation on the existing component so callers share one state transition.
        void AnimationCue(string animation) => SendCue(new SharedWorldCue { Type=4, First=animation });
        // Clean up this existing object or duplicate so obsolete presentation/session state does not survive into the next session.
        void OnDestroy()
        {
            // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
            if (Instance != this) return;
            // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
            if (DialogueManager.Instance && DialogueManager.Instance.IsSharedDialogue)
                // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
                Apply(DialogueManager.Instance.ExitDialogMode);
            // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
            Instance = null; Applying = false;
        }
        // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
        public static void CloseForTravel()
        {
            // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
            var manager=DialogueManager.Instance;
            // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
            if(manager&&(manager.ActiveDialoguePanel||manager.IsSharedDialogue||(StoryInputTextFieldManager.Instance&&StoryInputTextFieldManager.Instance.ActiveInputPanel)))
                // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
                Apply(manager.ExitDialogMode);
        }
        // Refer to compiled authored Ink data; clients cannot supply arbitrary executable story content.
        public bool Open(TextAsset story, Vector3 anchor)
        {
            // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
            var player = LocalPlayer();
            // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
            if (!Identity.IsSpawned || !player) return false;
            // Require the currently displayed story revision so duplicated or stale clicks cannot advance the shared story twice.
            player.RequestDialogue(0, DialogueCatalog.Key(story), anchor, -1, revision, "", "");
            // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
            return true;
        }
        // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
        public bool Route(int action, int choice = -1, string key = "", string value = "")
        {
            // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
            if (Applying || !Identity.IsSpawned || !DialogueManager.Instance || !DialogueManager.Instance.IsSharedDialogue) return false;
            // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
            var player = LocalPlayer();
            // Require the currently displayed story revision so duplicated or stale clicks cannot advance the shared story twice.
            if (player) player.RequestDialogue(action, "", Vector3.zero, choice, revision, key, value);
            // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
            return true;
        }
        // Mirage marks the character assigned to this connection as local; other spawned characters remain observers.
        HotelPlayer LocalPlayer() => FindObjectsByType<HotelPlayer>(FindObjectsSortMode.None).FirstOrDefault(p => p.IsLocalPlayer);
        // Snapshots carry a monotonic story revision that clients return with their next action.
        public void HandleRequest(HotelPlayer player, int action, string storyKey, Vector3 anchor, int choice, int expectedRevision, string inputKey, string inputValue)
        {
            // Only the authoritative server changes shared simulation; remote clients consume the resulting state.
            if (!IsServer || !player || !player.Identity.IsSpawned || !DialogueManager.Instance || !catalog) return;
            // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
            var manager = DialogueManager.Instance;
            // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
            if (action == 0)
            {
                // Only scene-authored shared sources are allowed; clients cannot choose arbitrary story/location.
                // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
                var story = catalog.Find(storyKey);
                // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
                if (!story || manager.ActiveDialoguePanel || manager.IsSharedDialogue) return;
                // Look up objects already present in the loaded scenes; ownership and scene checks select the appropriate one.
                bool source = FindObjectsByType<EnterDialogueMode>(FindObjectsSortMode.None).Any(t =>
                    // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
                    t.Audience == DialogueAudience.Everyone && t.InkAsset == story && Vector3.Distance(t.transform.position,anchor)<.25f);
                // Look up objects already present in the loaded scenes; ownership and scene checks select the appropriate one.
                source |= FindObjectsByType<TriggerDialogue>(FindObjectsSortMode.None).Any(t =>
                    // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
                    t.Audience == DialogueAudience.Everyone && t.InkAsset == story && Vector3.Distance(t.transform.position,anchor)<.25f);
                // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
                if (!source || Vector3.Distance(player.transform.position, anchor)>6) return;
                // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
                SpeakerPlayerId=player.NetId;
                // Keep this small operation on the existing component so callers share one state transition.
                Apply(() => manager.BeginSharedDialogue(story,player.transform.position));
            }
            else
            {
                // Revision prevents simultaneous clicks from skipping two lines/choices.
                // Require the currently displayed story revision so duplicated or stale clicks cannot advance the shared story twice.
                if (!manager.IsSharedDialogue || expectedRevision != revision || manager.IsWaiting) return;
                // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
                if (action==1 && manager.CurrentStory?.currentChoices.Count==0) Apply(manager.ContinueStory);
                // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
                else if (action==2 && manager.CurrentStory != null && choice>=0 && choice<manager.CurrentStory.currentChoices.Count)
                    // Keep this small operation on the existing component so callers share one state transition.
                    Apply(() => manager.ChoiceSelected(choice));
                // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
                else if (action==3) Apply(manager.ExitDialogMode);
                // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
                else if (action==4 && inputValue != null && inputValue.Length<=256 &&
                         StoryInputTextFieldManager.Instance && StoryInputTextFieldManager.Instance.ActiveInputPanel &&
                         // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
                         inputKey==StoryInputTextFieldManager.Instance.CurrentKey)
                    // Keep this small operation on the existing component so callers share one state transition.
                    Apply(() => StoryInputTextFieldManager.Instance.SubmitShared(inputKey,inputValue));
                // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
                else return;
            }
            // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
            Publish();
        }
        // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
        static void Apply(Action action)
        {
            // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
            Applying = true;
            // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
            try { action(); } finally { Applying = false; }
        }
        // Service local presentation and authoritative simulation each frame, with ownership/readiness checks inside the path.
        void Update()
        {
            // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
            if (!Identity.IsSpawned) return;
            // Only the authoritative server changes shared simulation; remote clients consume the resulting state.
            if (IsServer && Time.unscaledTime>=nextCapture)
            {
                // Use real session time so UI pauses or time-scale changes cannot defeat networking deadlines.
                nextCapture=Time.unscaledTime+.05f;
                // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
                if (DialogueManager.Instance && DialogueManager.Instance.IsSharedDialogue) Publish();
            }
            // Only the authoritative server changes shared simulation; remote clients consume the resulting state.
            if (!IsServer && snapshotJson!="" && snapshotJson!=lastApplied && DialogueManager.Instance && catalog)
            {
                // Publish or apply a complete authoritative dialogue view so late joiners see the same current prompt and choices.
                var snapshot=JsonUtility.FromJson<DialogueSnapshot>(snapshotJson);
                // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
                var story=catalog.Find(snapshot.storyKey);
                // Report a missing authored dependency instead of adding objects or components at runtime.
                if (snapshot.active && !story) { Debug.LogError("Shared dialogue missing from catalog. Rebuild catalog on all clients."); return; }
                // Require the currently displayed story revision so duplicated or stale clicks cannot advance the shared story twice.
                revision=snapshot.revision;
                // Publish or apply a complete authoritative dialogue view so late joiners see the same current prompt and choices.
                Apply(() => DialogueManager.Instance.ApplySharedSnapshot(snapshot,story));
                // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
                lastApplied=snapshotJson;
            }
        }
        // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
        void Publish()
        {
            // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
            var manager=DialogueManager.Instance;
            // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
            if (!manager) return;
            // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
            var input=StoryInputTextFieldManager.Instance;
            // Publish or apply a complete authoritative dialogue view so late joiners see the same current prompt and choices.
            var data=new DialogueSnapshot {
                // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
                active=manager.IsSharedDialogue && (manager.ActiveDialoguePanel || (input && input.ActiveInputPanel)),
                // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
                storyKey=DialogueCatalog.Key(manager.CurrentAsset),
                // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
                storyState=manager.CurrentStory?.state.ToJson() ?? "",
                // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
                text=manager._DialoguePanel.DialogueText,
                // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
                speaker=manager._DialoguePanel.DialogueDisplayName,
                // Keep this small operation on the existing component so callers share one state transition.
                choices=manager.CurrentStory?.currentChoices.Select(c=>c.text).ToArray() ?? Array.Empty<string>(),
                // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
                anchor=manager.DialogueAnchor,
                // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
                inputActive=input && input.ActiveInputPanel,
                // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
                inputQuestion=input ? input.CurrentQuestion : "", inputKey=input ? input.CurrentKey : ""
            };
            // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
            var content=JsonUtility.ToJson(data);
            // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
            if (content==lastContent) return;
            // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
            lastContent=content;
            // Require the currently displayed story revision so duplicated or stale clicks cannot advance the shared story twice.
            data.revision=++revision;
            // Only the server executes shared Ink. Clients request actions with revisions, then render the authoritative snapshot instead of running story effects twice.
            snapshotJson=JsonUtility.ToJson(data);
        }
    }
}


using Monologue.StoryInput;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using HauntedFish.Multiplayer;
using System.Collections.Generic;

namespace Monologue.Dialogue
{
    public class TriggerDialogue : MonoBehaviour
    {
        [Header("Visual Cue")]
        [SerializeField] private GameObject _VisualCue;
        [Header("Ink JSON")]
        [SerializeField] TextAsset _InkJSON;
        public DialogueAudience Audience = DialogueAudience.RelevantPlayer;
        public TextAsset InkAsset => _InkJSON;
        readonly HashSet<Component> players = new HashSet<Component>();
        Collider volume;
        // FIXME: FLAG
        bool _IsPlayerInRange;
        private void Awake()
        {
            volume=GetComponent<Collider>();
            if (_VisualCue) _VisualCue.SetActive(false);
        }

        void Update()
        {
            if (!DialogueManager.Instance || !StoryInputTextFieldManager.Instance) return;
            players.RemoveWhere(c => !c);
            _IsPlayerInRange = players.Count > 0;
            // Client controllers are disabled; test the authored trigger volume against
            // the owned player's synchronized pose instead of requiring physics callbacks.
            var player = HotelPlayer.LocalPlayer;
            if (volume && volume.enabled && volume.isTrigger && player)
                {
                    var controller=player.GetComponent<CharacterController>();
                    var point=player.transform.TransformPoint(controller.center);
                    if (Vector3.Distance(volume.ClosestPoint(point),point)<=controller.radius+.05f)
                    { _IsPlayerInRange=true; }
                }
            var blocked = DialogueManager.Instance.ActiveDialoguePanel || StoryInputTextFieldManager.Instance.ActiveInputPanel ||
                (player && player.InputBlocked);
            var showCue = !blocked && _IsPlayerInRange;
            if (_VisualCue && _VisualCue.activeSelf != showCue) _VisualCue.SetActive(showCue);
            
            if (blocked || !_IsPlayerInRange)
                return;

            var keyboard = Keyboard.current;
            if(keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame || keyboard.fKey.wasPressedThisFrame))
                DialogueManager.Instance.EnterDialogMode(_InkJSON, Audience, transform.position);
        }
        void OnDisable() { players.Clear(); _IsPlayerInRange = false; }
        void OnTriggerEnter(Collider playerCollider) { EnterRange(playerCollider); }
        void OnTriggerExit(Collider playerCollider) { players.Remove(playerCollider); }
        private void OnTriggerEnter2D(Collider2D playerCollider) { EnterRange(playerCollider); }
        void EnterRange(Component playerCollider)
        {
            if (!playerCollider.CompareTag("Player")) return;
            var owner = playerCollider.GetComponentInParent<HotelPlayer>();
            if (!owner || owner.IsRelevantPlayer) players.Add(playerCollider);
        }

        private void OnTriggerExit2D(Collider2D playerCollider)
        {
            players.Remove(playerCollider);
        }
    }
}

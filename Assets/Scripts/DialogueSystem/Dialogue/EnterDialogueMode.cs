using System.Collections;
using System.Collections.Generic;
using Ink.Parsed;
using UnityEngine;
using Monologue.Dialogue;
using HauntedFish.Multiplayer;

public class EnterDialogueMode : MonoBehaviour
{
    [SerializeField] TextAsset _InkJSON;
    public DialogueAudience Audience = DialogueAudience.RelevantPlayer;
    public TextAsset InkAsset => _InkJSON;
    public void EnterDialogue()
    {
        if (DialogueManager.Instance) DialogueManager.Instance.EnterDialogMode(_InkJSON, Audience, transform.position);
    }
}

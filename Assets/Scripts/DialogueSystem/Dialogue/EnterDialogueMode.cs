using System.Collections;
using System.Collections.Generic;
using Ink.Parsed;
using UnityEngine;
using Monologue.Dialogue;

public class EnterDialogueMode : MonoBehaviour
{
    [SerializeField] TextAsset _InkJSON;
    public void EnterDialogue()
    {
        DialogueManager.Instance.EnterDialogMode(_InkJSON);
    }
}

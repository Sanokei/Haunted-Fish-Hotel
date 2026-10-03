using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using Ink.Runtime;
using BuildingBlocks.DataTypes;
using Monologue.Dialogue;
using UnityEngine.Playables;

namespace Monologue.Character
{
    public class EmojiTelegraph : MonoBehaviour
    {
        [SerializeField] Animator EmojiAnimator;
        ICharacter _self;
        void Awake()
        {
            if(_self.Equals(null))
                _self = gameObject.GetComponent<ICharacter>();
        }
        void OnEnable()
        {
            Variables.OnGlobalsChangeEvent += SetEmojiOnVariableChange;
            StoryFunctions.OnEmojiEvent += SetEmojiOnCall;
        }
        void OnDisable()
        {
            Variables.OnGlobalsChangeEvent -= SetEmojiOnVariableChange;
            StoryFunctions.OnEmojiEvent -= SetEmojiOnCall;
        }

        void SetEmojiOnVariableChange(string key, Ink.Runtime.Object value, Ink.Runtime.Object previousValue)
        {
            if(key.Equals($"{_self.CharacterTag}_love"))
            {
                int pval = (int)((Value<int>)previousValue).valueObject;
                int val = (int)((Value<int>)value).valueObject;
                if(pval > val)
                    SetEmoji("HeartBroken");
                if(val > pval)
                    if(val - pval > 1)
                        SetEmoji("HeartMultiple");
                    else
                        SetEmoji("HeartSingle");
            }
            // ...
        }

        void SetEmojiOnCall(string emojiName, string characterTag)
        {
            if(characterTag.Equals(_self.CharacterTag))
                SetEmoji(emojiName);
        }

        public void SetEmoji(string emojiName)
        {
            EmojiAnimator.Play(emojiName);
        }
    }
}
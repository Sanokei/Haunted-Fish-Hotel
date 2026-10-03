using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Monologue.Character
{
    public class DefaultCharacter : MonoBehaviour, ICharacter
    {
        [SerializeField] string _CharacterTag;
        public string CharacterTag
        {
            get
            {
                return _CharacterTag;
            }

            set
            {
                _CharacterTag = value;
            }
        }
    }
}
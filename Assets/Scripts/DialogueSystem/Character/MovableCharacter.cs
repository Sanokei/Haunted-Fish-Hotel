using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Lean.Transition;

using Monologue.Dialogue;

namespace Monologue.Character
{
    public class MovableCharacter : MonoBehaviour
    {
        ICharacter _self;
        void Awake()
        {
            if(_self.Equals(null))
                _self = gameObject.GetComponent<ICharacter>();
        }
        void OnEnable()
        {
            StoryFunctions.OnMoveToEvent += MoveCharacter;
        }

        void OnDisable()
        {
            StoryFunctions.OnMoveToEvent -= MoveCharacter;
        }

        void MoveCharacter(string characterTag, int x, int y, float delay, bool disappear)
        {
            if(characterTag.Equals(_self.CharacterTag))
            {
                transform.localPositionTransition(new Vector3(x,y,0), delay)
                    .JoinTransition()
                    .EventTransition(() => 
                    {
                        if(disappear)
                            Destroy(gameObject);
                    });
            }
        }

    }
}
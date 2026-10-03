using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Lean.Transition;
using HauntedFish.Multiplayer;

using Monologue.Dialogue;

namespace Monologue.Character
{
    public class MovableCharacter : MonoBehaviour
    {
        ICharacter _self;
        void Awake()
        {
            if(_self == null)
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
            var network = GetComponent<NetworkWorldObject>();
            if (network && !network.CanSimulate) return;
            if (_self == null) return;
            if(characterTag.Equals(_self.CharacterTag))
            {
                transform.localPositionTransition(new Vector3(x,y,0), delay)
                    .JoinTransition()
                    .EventTransition(() => 
                    {
                        if(disappear)
                        {
                            if (network) network.Despawn(); else Destroy(gameObject);
                        }
                    });
            }
        }

    }
}
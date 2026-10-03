using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Monologue
{
    public class DontDestroyHelper : MonoBehaviour
    {
        [SerializeField] int _TotalNotDestroyable;
        int _NotDestroyableCount;

        public delegate void NotDestroyedHelper();
        public static event NotDestroyedHelper NotDestroyedHelperEvent; 

        bool _calledHelperEvent = false;
        void OnEnable()
        {
            Monologue.DontDestroyOnLoad.NotDestroyedEvent += AddToCounter;
        }

        void OnDisable()
        {
            Monologue.DontDestroyOnLoad.NotDestroyedEvent -= AddToCounter;
        }

        void AddToCounter()
        {
            _NotDestroyableCount++;
        }

        // void Start

        void Update()
        {
            if(!_calledHelperEvent && _NotDestroyableCount >= _TotalNotDestroyable)
            {
                NotDestroyedHelperEvent?.Invoke();
                _calledHelperEvent = true;
            }
        }
    }
}

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
            NotifyWhenReady();
        }

        // void Start

        void Start() => NotifyWhenReady();

        void NotifyWhenReady()
        {
            if(!_calledHelperEvent && _NotDestroyableCount >= _TotalNotDestroyable)
            {
                _calledHelperEvent = true;
                StartCoroutine(NotifyNextFrame());
            }
        }
        IEnumerator NotifyNextFrame()
        {
            // Start callbacks must finish moving their objects into the persistent scene.
            yield return null;
            NotDestroyedHelperEvent?.Invoke();
        }
    }
}

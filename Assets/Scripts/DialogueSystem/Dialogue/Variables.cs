using System.Collections.Generic;
using UnityEngine;
using Ink.Runtime;
using System.Linq;

namespace Monologue.Dialogue
{
    public class Variables
    {
        public delegate void OnGlobalsChange(string key, Ink.Runtime.Object value, Ink.Runtime.Object previousValue);
        public static event OnGlobalsChange OnGlobalsChangeEvent;
        Story m_GlobalVarsStory;

        public VariablesState Globals
        {
            get
            {
                return m_GlobalVarsStory.variablesState;
            }
            private set
            {
                // .NET collections doesnt support being enumerated and modified at the same time
                    // dictonaries can via .Remove() and .Clear()
                // VariableState has IEnumable<string> that returns KEY values
                List<string> keys = new(Globals);
                foreach(string key in keys)
                    Globals[key] = value[key];
            }
        }

        public StoryState State
        {
            get
            {
                return m_GlobalVarsStory.state;
            }
        }

        public object this[string key]
        {
            get
            {
                return Globals[key];
            }
            set
            {
                SetGlobalVariable(key,value);
            }
        }
        public Variables(TextAsset globalsJSON)
        {
            m_GlobalVarsStory = new(globalsJSON.text);
        }
        // enable and disable event listeners for the ink story that is currently loaded
        public void StartListening(Story story)
        {
            SetVariableState(story);
            story.variablesState.variableChangedEvent += SetGlobalVariable;
        }
        public void StopListening(Story story)
        {
            story.variablesState.variableChangedEvent -= SetGlobalVariable;
        }
        
        // Set the story to the global
        public void SetVariableState(Story story)
        {
            SetVariableState(story.variablesState);
        }
        
        public void SetVariableState(VariablesState value)
        {
            List<string> keys = new(value);
            foreach(string key in keys)
                value[key] = Globals[key];
        }

        // Set global to the story
        public void SetGlobalVariable(string key, Ink.Runtime.Object value)
        { 
            OnGlobalsChangeEvent?.Invoke(key,value,Value.Create(Globals[key]));
            Globals.SetGlobal(key,value);
        }
        public void SetGlobalVariable(string key, object value)
        {
            SetGlobalVariable(key, Value.Create(value));
        }
    }
}
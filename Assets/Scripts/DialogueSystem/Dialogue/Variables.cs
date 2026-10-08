using System;
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
        readonly Story m_GlobalVarsStory;
        readonly HashSet<Story> listeningStories = new();

        public VariablesState Globals
        {
            get
            {
                return m_GlobalVarsStory.variablesState;
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
        public Variables(TextAsset globalsJSON) : this(globalsJSON.text)
        {
        }

        public Variables(string globalsJSON)
        {
            m_GlobalVarsStory = new(globalsJSON);
        }
        // enable and disable event listeners for the ink story that is currently loaded
        public void StartListening(Story story)
        {
            if (story == null) throw new ArgumentNullException(nameof(story));
            if (listeningStories.Contains(story)) return;
            SetVariableState(story);
            story.variablesState.variableChangedEvent += OnStoryVariableChanged;
            listeningStories.Add(story);
        }
        public void StopListening(Story story)
        {
            if (story == null || !listeningStories.Remove(story)) return;
            story.variablesState.variableChangedEvent -= OnStoryVariableChanged;
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
                if (Globals.Contains(key)) value[key] = Globals[key];
        }

        void OnStoryVariableChanged(string key, Ink.Runtime.Object value)
        {
            // Ink also reports variables declared only in this story. They are not
            // persistent globals and must neither overwrite nor extend the global store.
            if (Globals.Contains(key)) SetGlobalVariable(key, value);
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

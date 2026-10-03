using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

using Monologue.StoryInput;

using Ink.Runtime;

using System.Linq;

namespace Monologue.Dialogue
{
    public class StoryFunctions
    {
        public delegate void OnEmoji(string emojiName, string characterTag);
        public static event OnEmoji OnEmojiEvent;

        public delegate void OnMoveTo(string characterTag, int x, int y, float delay, bool disappear);
        public static event OnMoveTo OnMoveToEvent;

        public delegate void OnCameraSet(string cameraTag, bool goBack);
        public static event OnCameraSet OnCameraSetEvent;

        public delegate void OnSpeaker(string speaker);
        public static event OnSpeaker OnSpeakerEvent;

        public delegate void OnAnimation(string animation);
        public static event OnAnimation OnAnimationEvent;

        public static List<string> TagtoList(string tagValue)
        {
            if(tagValue.ToCharArray().Count() == 0)
                return new List<string>();

            if(tagValue[0] == '{' && tagValue[tagValue.Count()-1] =='}')
                return tagValue.Remove(0).Remove(tagValue.Count()-1).Split(',').ToList();
            else
                return new List<string>(){tagValue};
                
        }
        // Custom Ink handling
        public static void HandleTags(Story story)
        {
            foreach(string tag in story.currentTags)
            {
                string[] tagSplit = tag.Split(':');
                if(tagSplit.Length != 2)
                    Debug.LogError("Tag is not formatted correctly");

                string tagValue = tagSplit[1].Trim();

                switch(tagSplit[0].Trim())
                {
                    case "speaker":
                        DialogueManager.Instance._DialoguePanel.DialogueDisplayName = tagValue;
                        OnSpeakerEvent?.Invoke(tagValue);
                    break;

                    case "animation":
                        OnAnimationEvent?.Invoke(tagValue);
                    break;

                    case "image":
                        DialogueManager.Instance._DialoguePanel.ProfileIncluded = true;
                        DialogueManager.Instance._DialoguePanel.ProfileImage.sprite = Resources.Load<Sprite>($"Characters/Sample");// Resources.Load<Image>($"Characters/{tagValue}");
                    break;

                    // Format strings
                    // my name is: <name> #format:name
                    case "format":
                        List<string> listOfFormat = TagtoList(tagValue);
                        string text = story.currentText;
                        foreach(string vars in listOfFormat)
                            text = text?.Replace($"<{vars}>",DialogueManager.Instance.GlobalVars[vars].ToString());
                        DialogueManager.Instance._DialoguePanel.DialogueText = text;
                    break;

                    case "cutscene":

                    break;
                }
            }
        }

        // Used in Ink
        // EXTERNAL name(params)
        // e.g EXTERNAL InputText("What IS \"up dog\"?", false)

        public static void BindFunctions(Story story)
        {
            // lambda optional parameters aren't avaiable until C# 12.0. Fucking Unity.
            // and because the bind stores everything to a Dictonary I cannot create variant functions
            story.BindExternalFunction("InputText",(string question, string key, string profile) =>
            {
                StoryInputTextFieldManager.Instance.EnterInputMode(question, key, profile);
            });

            story.BindExternalFunction("Emoji",(string emoteName, string characterTag) =>
            {
                List<string> listOfEmotes = TagtoList(emoteName);
                List<string> listOfCharacters = TagtoList(characterTag);
                
                // It is only an issue if the number of emotes is more than the number of characters
                if(listOfEmotes.Count() > listOfCharacters.Count())
                    return;

                for(int i = 0; i < listOfCharacters.Count(); i++)
                    // if the character list is larger than the emote list, then just play the same emote for all of them.
                    OnEmojiEvent?.Invoke( i >= listOfEmotes.Count() ? listOfEmotes[^1] : listOfEmotes[i], listOfCharacters[i]);
            });

            // story.BindExternalFunction("CreateQuest",(int id, string questName, string questBody, string iconName, string color) => 
            // {
            //     QuestManager.Instance._QuestPanel.Add(id,questName,questBody,iconName,color);
            // });

            // story.BindExternalFunction("SetCamera", (string cameraTag, bool goBack) => 
            // {
            //     OnCameraSetEvent?.Invoke( cameraTag, goBack );
            // });

            story.BindExternalFunction("SetCamera", (string cameraTag) => 
            {
                // FIXME: Because it doesnt wait for the anaimation to end before continuing, it can reach ends where it tries to look for camera null
                OnCameraSetEvent?.Invoke( cameraTag, false ); // will fix later
            });

            story.BindExternalFunction("MoveTo", (string characterTag, int x, int y, float delay, bool disappear) => 
            {
                OnMoveToEvent?.Invoke(characterTag,x,y,delay,disappear);
            });

            story.BindExternalFunction("ChangeScene", (string sceneName) =>
            {
                SceneManager.LoadScene(sceneName);
            });
        }

        public static void UnbindFunctions(Story story)
        {
            story.UnbindExternalFunction("InputText");
            story.UnbindExternalFunction("Emoji");
            // story.UnbindExternalFunction("CreateQuest");
            story.UnbindExternalFunction("SetCamera");
            story.UnbindExternalFunction("MoveTo");
            story.UnbindExternalFunction("ChangeScene");
        }

    }
}
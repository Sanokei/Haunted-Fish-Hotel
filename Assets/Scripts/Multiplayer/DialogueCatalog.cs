using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
namespace HauntedFish.Multiplayer
{
    public sealed class DialogueCatalog : ScriptableObject
    {
        public TextAsset[] Stories = Array.Empty<TextAsset>();
        public TextAsset Find(string key)
        {
            foreach (var asset in Stories) if (asset && Key(asset) == key) return asset;
            return null;
        }
        public static string Key(TextAsset asset)
        {
            if (!asset) return "";
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(asset.text))).Replace("-", "");
        }
    }
    [Serializable] public sealed class DialogueSnapshot
    {
        public int revision;
        public bool active, inputActive;
        public string storyKey, storyState, text, speaker, inputQuestion, inputKey;
        public string[] choices;
        public Vector3 anchor;
    }
}


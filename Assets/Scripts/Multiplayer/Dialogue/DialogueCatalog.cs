using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
namespace HauntedFish.Multiplayer
{
    // Stable content-derived keys let peers refer to the same authored Ink asset without trusting arbitrary client story content.
    public sealed class DialogueCatalog : ScriptableObject
    {
        // Refer to compiled authored Ink data; clients cannot supply arbitrary executable story content.
        public TextAsset[] Stories = Array.Empty<TextAsset>();
        // Refer to compiled authored Ink data; clients cannot supply arbitrary executable story content.
        public TextAsset Find(string key)
        {
            // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
            foreach (var asset in Stories) if (asset && Key(asset) == key) return asset;
            // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
            return null;
        }
        // Refer to compiled authored Ink data; clients cannot supply arbitrary executable story content.
        public static string Key(TextAsset asset)
        {
            // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
            if (!asset) return "";
            using (var sha = SHA256.Create())
                // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(asset.text))).Replace("-", "");
        }
    }
    // Publish or apply a complete authoritative dialogue view so late joiners see the same current prompt and choices.
    [Serializable] public sealed class DialogueSnapshot
    {
        // Require the currently displayed story revision so duplicated or stale clicks cannot advance the shared story twice.
        public int revision;
        // Stable content-derived keys let peers refer to the same authored Ink asset without trusting arbitrary client story content.
        public bool active, inputActive;
        // Stable content-derived keys let peers refer to the same authored Ink asset without trusting arbitrary client story content.
        public string storyKey, storyState, text, speaker, inputQuestion, inputKey;
        // Stable content-derived keys let peers refer to the same authored Ink asset without trusting arbitrary client story content.
        public string[] choices;
        // Stable content-derived keys let peers refer to the same authored Ink asset without trusting arbitrary client story content.
        public Vector3 anchor;
    }
}


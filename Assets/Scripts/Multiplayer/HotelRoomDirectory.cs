using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace HauntedFish.Multiplayer
{
    internal static class HotelRoomDirectory
    {
        public static IEnumerator Request(string service, string token, string path, string method,
            string json, Action<Room, string> completed)
        {
            using (var request = new UnityWebRequest(service.TrimEnd('/') + path, method))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                if (json != null) request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrEmpty(token)) request.SetRequestHeader("Authorization", "Bearer " + token);
                request.timeout = 15;
                request.useHttpContinue = false;
                yield return request.SendWebRequest();
                Room result = null;
                try { result = JsonUtility.FromJson<Room>(request.downloadHandler.text); }
                catch (ArgumentException) { }
                if (request.result != UnityWebRequest.Result.Success)
                    completed(null, (!string.IsNullOrEmpty(result?.error) ? result.error : "Lobby directory unavailable. Check its URL.")
                        + " (HTTP " + request.responseCode + ", " + request.result + ": " + request.error + ").");
                else if (result == null) completed(null, "The lobby directory returned an invalid response.");
                else completed(result, null);
            }
        }
    }
}

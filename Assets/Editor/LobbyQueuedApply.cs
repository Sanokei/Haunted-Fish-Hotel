using System;
using UnityEditor;
using UnityEngine;
public static class LobbyQueuedApply {
 public static void Run(){try{LobbyMoodCameraComparison.Before();LobbyIntroRegressionAuthoring.Apply();LobbyGentlerFogRefinement.Apply();LobbyFireplaceAuthoring.Apply();LobbyMoodCameraComparison.After();Debug.Log("LOBBY_QUEUED_APPLY_PASS");EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}

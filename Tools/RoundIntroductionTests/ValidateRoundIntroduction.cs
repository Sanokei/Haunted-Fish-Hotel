using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using HauntedFish.Multiplayer;
using Monologue.Dialogue;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
public static class ValidateRoundIntroduction {
 public static void Run() {
  var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  new GameObject("Validation runner").AddComponent<RoundValidationRunner>();
  EditorSceneManager.SaveScene(scene,"Assets/Validation.unity");EditorApplication.EnterPlaymode();
 }
}

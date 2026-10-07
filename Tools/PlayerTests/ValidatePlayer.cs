using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HauntedFish.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ValidatePlayer
{
    public static void Run()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var runner = new GameObject("Player validation").AddComponent<PlayerValidationRunner>();
        EditorSceneManager.SaveScene(scene, "Assets/Validation.unity");
        EditorApplication.EnterPlaymode();
    }
}

public sealed class PlayerValidationRunner : MonoBehaviour
{
    int _Checks;

    void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        _Checks++;
    }

    static T Field<T>(object target, string name) => (T)target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    static void Call(object target, string name) => target.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

    IEnumerator Start()
    {
        var tests = Tests();
        while (true)
        {
            object next;
            try
            {
                if (!tests.MoveNext()) break;
                next = tests.Current;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
                yield break;
            }
            yield return next;
        }
        Debug.Log($"PLAYER VALIDATION PASSED: {_Checks} checks");
        EditorApplication.Exit(0);
    }

    IEnumerator Tests()
    {
        var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(0, 0, -10);
        camera.orthographic = true;
        camera.orthographicSize = 1.25f;
        camera.backgroundColor = new Color(.12f, .16f, .2f);
        camera.clearFlags = CameraClearFlags.SolidColor;

        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.position = new Vector3(0, -.5f, 0);
        floor.transform.localScale = new Vector3(50, 1, 50);
        floor.GetComponent<Renderer>().enabled = false;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HotelNetworkPlayer.prefab");
        Check(prefab, "Prefab imports");
        var player = Instantiate(prefab, new Vector3(0, 1.1f, 0), Quaternion.identity);
        var other = Instantiate(prefab, new Vector3(5, 1.1f, 0), Quaternion.identity);
        player.AddComponent<HotelPlayer>();
        other.AddComponent<HotelPlayer>();
        var movement = player.GetComponent<HotelPlayerMovement>();
        var otherMovement = other.GetComponent<HotelPlayerMovement>();
        Check(movement && player.GetComponent<GameSideScrollMotor>(), "Authored movement components resolve");
        movement.SetSimulationAuthority(true);
        otherMovement.SetSimulationAuthority(true);
        movement.SetControlState(true, true, false);
        otherMovement.SetControlState(true, true, false);
        yield return null;
        yield return null;
        yield return null;

        Check(movement.SimulationReady, "Authoritative controller settles");
        Check(movement.ActiveInputMap == "Lobby", "Lobby map selected");
        movement.SetControlState(true, false, false);
        Check(movement.ActiveInputMap == "" && otherMovement.ActiveInputMap == "Lobby", "Owned input maps are isolated");
        movement.SetControlState(true, true, false);
        var before = player.transform.position;
        // AcceptInput receives world XZ axes; camera-relative conversion happens before sending.
        movement.AcceptInput(Vector2.right * 10, false, Vector3.zero);
        movement.Simulate(.1f);
        Check(Mathf.Abs(player.transform.position.x - before.x - .45f) < .03f,
            "Lobby input is clamped to walking speed");
        Check(!movement.FacingLeft, "Right movement selects right body direction");

        movement.SetSimulationAuthority(false);
        before = player.transform.position;
        movement.AcceptInput(Vector2.one, true, Vector3.zero);
        movement.Simulate(.1f);
        Check(player.transform.position == before && !player.GetComponent<CharacterController>().enabled,
            "Observers cannot simulate collision");
        movement.Teleport(new Vector3(0, 1.1f, 0));
        Check(!player.GetComponent<CharacterController>().enabled, "Teleport preserves observer controller state");
        movement.SetSimulationAuthority(true);
        movement.SetControlState(false, true, false);
        before = player.transform.position;
        movement.AcceptInput(Vector2.one, true, Vector3.zero);
        movement.Simulate(.1f);
        Check(player.transform.position == before && movement.ActiveInputMap == "", "Loading gates input and physics");

        var game = new GameObject("Game controller").AddComponent<GameSceneController>();
        Check(movement.GameActive && otherMovement.GameActive, "Game entry binds existing players");
        var latePlayer = Instantiate(prefab, new Vector3(10, 1.1f, 0), Quaternion.identity);
        latePlayer.AddComponent<HotelPlayer>();
        var lateMovement = latePlayer.GetComponent<HotelPlayerMovement>();
        Check(lateMovement.GameActive, "Game entry also binds players enabled later");
        latePlayer.SetActive(false);
        Check(!lateMovement.GameActive, "Disabled player releases game behavior");
        latePlayer.SetActive(true);
        Check(lateMovement.GameActive, "Re-enabled player reacquires game behavior");
        movement.SetMode(HotelControlMode.Fish);
        movement.SetControlState(true, true, false);
        yield return null;
        yield return null;
        yield return null;
        Check(movement.ActiveInputMap == "Game", "Game map replaces Lobby map");
        player.GetComponent<CharacterController>().Move(Vector3.down * .2f);
        before = player.transform.position;
        movement.AcceptInput(Vector2.left * 10, true, new Vector3(999, 999, 999));
        movement.Simulate(.1f);
        Check(Mathf.Abs(player.transform.position.x - before.x + .45f) < .03f && movement.FacingLeft,
            "Game movement clamps speed and selects left direction");
        Check(player.transform.position.y > before.y, "Grounded buffered jump lifts player");
        Check(Mathf.Abs(player.transform.position.z) < .0001f, "Game movement remains on its 2D plane");
        Check(movement.LampPosition == new Vector3(15, 16, -3), "Authoritative pointer input respects Game's bounds and depth");
        movement.SharedDialogueLocked = true;
        before = player.transform.position;
        movement.Simulate(.1f);
        Check(Mathf.Abs(player.transform.position.x - before.x) < .0001f && !movement.Walking,
            "Shared dialogue stops already accepted movement");
        movement.SharedDialogueLocked = false;
        movement.AcceptInput(Vector2.right, false, Vector3.zero);
        yield return new WaitForSecondsRealtime(.35f);
        before = player.transform.position;
        movement.Simulate(.1f);
        Check(Mathf.Abs(player.transform.position.x - before.x) < .0001f, "Stale commands stop movement");
        game.Exit();
        movement.SetMode(HotelControlMode.Lobby);
        Check(!movement.GameActive && !otherMovement.GameActive && !lateMovement.GameActive,
            "Game exit unbinds every persistent player");
        movement.SetControlState(true, true, false);
        Check(movement.ActiveInputMap == "Lobby", "Game exit restores Lobby input");
        Check(player.GetComponent<GameSideScrollMotor>().ReadMouse() == Vector3.zero,
            "Unbound motor safely ignores mouse input");
        game.Enter(null);
        Check(movement.GameActive && lateMovement.GameActive, "Game can re-enter without stale bindings");
        Destroy(game.gameObject);
        yield return null;
        Check(!movement.GameActive && !lateMovement.GameActive, "Unloading Game releases player dependencies");
        latePlayer.SetActive(false);

        var visual = player.GetComponentInChildren<HotelFishSprite>();
        var body = Field<SpriteRenderer>(visual, "_Body");
        var fin = Field<SpriteRenderer>(visual, "_Fin");
        var pivot = Field<Transform>(visual, "_FinPivot");
        Check(body && fin && pivot, "Body and fin prefab references resolve");
        Check(fin.sortingOrder < body.sortingOrder, "Fin renders behind body");
        Check(Mathf.Abs(fin.sprite.pivot.x - 957) < 1 && Mathf.Abs(fin.sprite.pivot.y - 548) < 1,
            "Fin sprite pivots at its top attachment");
        var anchor = pivot.localPosition;
        var material = new Material(Shader.Find("Sprites/Default"));
        body.sharedMaterial = fin.sharedMaterial = material;
        player.transform.position = Vector3.zero;
        other.SetActive(false);
        visual.Present(false, false, false);
        Call(visual, "LateUpdate");
        Check(body.sprite.name == "FISH-right", "Right authored body selected");
        Capture(camera, "right.png");
        visual.Present(true, false, false);
        Call(visual, "LateUpdate");
        Check(body.sprite.name == "FISH-left" && Mathf.Abs(pivot.localPosition.x + anchor.x) < .001f,
            "Left authored body mirrors fin attachment");
        Capture(camera, "left.png");
        visual.Present(false, true, false);
        yield return new WaitForSecondsRealtime(.1f);
        Call(visual, "LateUpdate");
        Check(pivot.localPosition == anchor && Mathf.Abs(pivot.localEulerAngles.z) > .1f,
            "Fin swings about a fixed top anchor");
        Capture(camera, "fin-swing.png");
    }

    static void Capture(Camera camera, string name)
    {
        var target = new RenderTexture(700, 700, 24);
        camera.targetTexture = target;
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var texture = new Texture2D(700, 700, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, 700, 700), 0, 0);
        texture.Apply();
        var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Output"));
        Directory.CreateDirectory(output);
        File.WriteAllBytes(Path.Combine(output, name), texture.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = previous;
        Destroy(texture);
        Destroy(target);
    }
}

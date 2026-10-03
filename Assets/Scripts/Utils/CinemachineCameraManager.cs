using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

using BuildingBlocks.DataTypes;
using Monologue.Dialogue;
using SimpleMan.CoroutineExtensions;


public class CinemachineCameraManager : MonoBehaviour
{
    public static CinemachineCameraManager Instance { get; private set;}
    void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    public InspectableDictionary<string,CinemachineCamera> _Cameras = new();
    KeyValuePair<string, CinemachineCamera> _CurrentCamera;
    string _SavedCamera;
    void OnEnable()
    {
        SceneManager.activeSceneChanged += AddCameras;
        StoryFunctions.OnCameraSetEvent += SetCamFromDialogue;
    }
    void OnDisable()
    {
        SceneManager.activeSceneChanged -= AddCameras;
        StoryFunctions.OnCameraSetEvent -= SetCamFromDialogue;
    }
    void AddCameras(Scene scene0, Scene scene1)
    {
        _Cameras = new();
        List<CinemachineCamera> _AllCameras = FindObjectsOfType<CinemachineCamera>(true).ToList();
        foreach(var cam in _AllCameras)
            _Cameras.Add(cam.name, cam);
    }
    void SetCamFromDialogue(string camName, bool goBack)
    {
        _SavedCamera = _CurrentCamera.Key;
        SetCam(camName);
        // FIXME: this is bad
        DialogueManager.Instance.IsWaiting = true;
        this.Delay(1f, () => DialogueManager.Instance.IsWaiting = false);
        if(goBack)
            DialogueManager.OnDialogueTryingToContinueEvent += CallbackCam;
    }
    void CallbackCam()
    {
        DialogueManager.OnDialogueTryingToContinueEvent -= CallbackCam;
        SetCam(_SavedCamera);
    }
    public void SetCam(int idx)
    {
        _Cameras.Values.ToList().ForEach(cam => cam.Priority = 0);
        _Cameras[idx].Priority = 1;
        _CurrentCamera = new KeyValuePair<string, CinemachineCamera>(_Cameras[idx].name, _Cameras[idx]);
    }
    public void SetCam(string idx)
    {
        _Cameras.Values.ToList().ForEach(cam => cam.Priority = 0);
        _Cameras[idx].Priority = 1;
        _CurrentCamera = new KeyValuePair<string, CinemachineCamera>(_Cameras[idx].name, _Cameras[idx]);
    }
    public void SetCam(CinemachineCamera idx)
    {
        _Cameras.Values.ToList().ForEach(cam => cam.Priority = 0);
        idx.Priority = 1;
        _CurrentCamera = new KeyValuePair<string, CinemachineCamera>(idx.name, idx);
    }
}
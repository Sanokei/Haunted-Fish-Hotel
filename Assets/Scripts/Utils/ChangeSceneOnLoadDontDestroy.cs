using System.Collections;
using System.Collections.Generic;
using Monologue;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ChangeSceneOnLoadDontDestroy : MonoBehaviour
{
    public static ChangeSceneOnLoadDontDestroy Instance {get; private set;}
    [SerializeField] string _SceneName;
    [SerializeField] int _TotalChangeSceneCount;
    int _ChangeSceneCount;
    bool _transitionStarted;
    void Awake()
    {
        if (!Instance)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void NextScene()
    {
        _ChangeSceneCount++;
        LoadWhenReady();
    }
    void Start() => LoadWhenReady();
    void LoadWhenReady()
    {
        if(!_transitionStarted && _ChangeSceneCount >= _TotalChangeSceneCount)
        {
            _transitionStarted=true;
            StartCoroutine(LoadNextFrame());
        }
    }
    IEnumerator LoadNextFrame()
    {
        yield return null;
        // Existing Helper serializes the destination with literal quotes.
        BubbleSceneTransition.Load(_SceneName.Trim().Trim('"'));
    }
}

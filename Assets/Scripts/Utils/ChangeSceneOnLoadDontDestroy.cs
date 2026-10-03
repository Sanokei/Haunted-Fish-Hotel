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
    }
    void Update()
    {
        if(!_transitionStarted && _ChangeSceneCount >= _TotalChangeSceneCount)
        {
            _transitionStarted=true;
            // Existing Helper serializes the destination with literal quotes.
            SceneManager.LoadScene(_SceneName.Trim().Trim('"'));
        }
    }
}

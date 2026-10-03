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
        if(_TotalChangeSceneCount == _ChangeSceneCount)
            SceneManager.LoadScene(_SceneName);
    }
}

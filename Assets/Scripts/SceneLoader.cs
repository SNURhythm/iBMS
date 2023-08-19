using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public string nextScene;
    
    // Start is called before the first frame update
    void Start()
    {   
        StartCoroutine(LoadScene());
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    IEnumerator LoadScene()
    {
        var asyncOperation = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(nextScene);
        while (!asyncOperation.isDone)
        {
            Logger.Log(asyncOperation.progress);
            yield return null;
        }
    }

    private void OnGUI()
    {
        
    }
}

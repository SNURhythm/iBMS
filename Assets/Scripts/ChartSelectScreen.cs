using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class ChartSelectScreen : MonoBehaviour
{
    [SerializeField]
    private GameObject ChartSelectButtonPrefab;
    [SerializeField]
    private GameObject ChartSelectButtonParent;
    
    private GameObject[] ChartSelectButtons;
    // Start is called before the first frame update
    void Start()
    {

    }

    private void OnEnable()
    {
        // NOTE: This would not work on Android but it's fine for now since we'll not be using StreamingAssets on release
        var info = new DirectoryInfo(Application.persistentDataPath);
        var fileInfo = info.GetDirectories();
        ChartSelectButtons = new GameObject[fileInfo.Length];
        for (var i = 0; i < fileInfo.Length; i++)
        {
            var file = fileInfo[i];

            if (file.Name.EndsWith(".meta")) continue;
            var button = Instantiate(ChartSelectButtonPrefab, ChartSelectButtonParent.transform);
            ChartSelectButtons[i] = button;
            button.GetComponent<ChartSelectButton>().ChartTitle.text = file.Name;
            button.GetComponent<Button>().onClick.AddListener(() =>
            {
                // peek the directory and add buttons of *.bms, *.bme, *.bml, *.pms
                var chartInfo = new DirectoryInfo(file.FullName);
                var chartFileInfo = chartInfo.GetFiles();
                foreach (var chartFile in chartFileInfo)
                {
                    if (!new [] { ".bms", ".bme", ".bml" }.Contains(chartFile.Extension)) continue;
                    var chartButton = Instantiate(ChartSelectButtonPrefab, ChartSelectButtonParent.transform);
                    chartButton.GetComponent<ChartSelectButton>().ChartTitle.text = chartFile.Name;
                    chartButton.GetComponent<Button>().onClick.AddListener(() =>
                    {
                        Debug.Log("Load chart: " + chartFile.FullName);
                        GameManager.Instance.BmsPath = chartFile.FullName;
                        // load scene
                        UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("LoadingScene");
                    });
                }
            });
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

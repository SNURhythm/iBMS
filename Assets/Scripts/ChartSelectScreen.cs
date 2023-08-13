using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using B83.Image.BMP;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using Touch = UnityEngine.Touch;


public class ChartSelectScreen : MonoBehaviour
{
    [SerializeField]
    private GameObject ChartSelectButtonPrefab;
    [SerializeField]
    private GameObject ChartSelectButtonParent;
    [SerializeField]
    private RawImage Background;
    [SerializeField] private Toggle AutoToggle;
    [SerializeField] private Toggle KeySoundToggle;
    private GameObject[] ChartSelectButtons;
    // Start is called before the first frame update
    void Start()
    {
        var bmpLoader = new BMPLoader();
        var image = bmpLoader.LoadBMP(Application.persistentDataPath + "/take003/bga_take_bg.bmp");
        var texture = image.ToTexture2D();
        Background.texture = texture;
    }

    private void OnEnable()
    {

        TouchSimulation.Enable();
        //
        // var info = new DirectoryInfo(Application.persistentDataPath);
        // var fileInfo = info.GetDirectories();
        // ChartSelectButtons = new GameObject[fileInfo.Length];
        // for (var i = 0; i < fileInfo.Length; i++)
        // {
        //     var file = fileInfo[i];
        //
        //     if (file.Name.EndsWith(".meta")) continue;
        //     var button = Instantiate(ChartSelectButtonPrefab, ChartSelectButtonParent.transform);
        //     ChartSelectButtons[i] = button;
        //     button.GetComponent<ChartSelectButton>().ChartTitle.text = file.Name;
        //     button.GetComponent<Button>().onClick.AddListener(() =>
        //     {
        //         // peek the directory and add buttons of *.bms, *.bme, *.bml, *.pms
        //         var chartInfo = new DirectoryInfo(file.FullName);
        //         var chartFileInfo = chartInfo.GetFiles();
        //         foreach (var chartFile in chartFileInfo)
        //         {
        //             if (!new [] { ".bms", ".bme", ".bml" }.Contains(chartFile.Extension)) continue;
        //             var chartButton = Instantiate(ChartSelectButtonPrefab, ChartSelectButtonParent.transform);
        //             chartButton.GetComponent<ChartSelectButton>().ChartTitle.text = chartFile.Name;
        //             chartButton.GetComponent<Button>().onClick.AddListener(() =>
        //             {
        //                 Debug.Log("Load chart: " + chartFile.FullName);
        //                 GameManager.Instance.BmsPath = chartFile.FullName;
        //                 GameManager.Instance.AutoPlay = AutoToggle.isOn;
        //                 GameManager.Instance.KeySound = KeySoundToggle.isOn;
        //                 // load scene
        //                 UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("LoadingScene");
        //             });
        //         }
        //     });
        // }
    }

    // Update is called once per frame
    void Update()
    {

    }
}

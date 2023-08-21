using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Drawing;

using B83.Image.BMP;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using Touch = UnityEngine.Touch;
using FFmpeg.AutoGen;
using UnityEngine.Video;

using DirectShowLib;
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
        if (Application.platform == RuntimePlatform.IPhonePlayer)
        {
            ffmpeg.RootPath = Application.dataPath + "/Binaries/iOS/ffmpeg";
        }
        else
        {
            ffmpeg.RootPath = Application.dataPath + "/Binaries/Windows/ffmpeg";
        }
    }

    private void OnEnable()
    {

        TouchSimulation.Enable();
    }

    // Update is called once per frame
    void Update()
    {

    }


}

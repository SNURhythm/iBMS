using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Drawing;

using UnityEngine;
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

    [SerializeField] private Toggle AutoToggle;
    [SerializeField] private Toggle KeySoundToggle;
    private GameObject[] ChartSelectButtons;
    // Start is called before the first frame update
    void Start()
    {
        Video
        DirectShowLib.reader
        if (Application.platform == RuntimePlatform.IPhonePlayer)
        {
            ffmpeg.RootPath = Application.dataPath + "/Binaries/iOS/ffmpeg";
            DecodeAllFramesToImages(AVHWDeviceType.AV_HWDEVICE_TYPE_NONE);
        }
        else
        {
            ffmpeg.RootPath = Application.dataPath + "/Binaries/Windows/ffmpeg";
            DecodeAllFramesToImages(AVHWDeviceType.AV_HWDEVICE_TYPE_NONE);
        }
        VideoFileReader reader = new VideoFileReader();
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
                        GameManager.Instance.AutoPlay = AutoToggle.isOn;
                        GameManager.Instance.KeySound = KeySoundToggle.isOn;
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

    private static unsafe void DecodeAllFramesToImages(AVHWDeviceType HWDevice)
    {
        // decode all frames from url, please not it might local resorce, e.g. string url = "../../sample_mpeg4.mp4";
        
        var url = "http://clips.vorwaerts-gmbh.de/big_buck_bunny.mp4"; // be advised this file holds 1440 frames
        using var vsd = new VideoStreamDecoder(url, HWDevice);

        Console.WriteLine($"codec name: {vsd.CodecName}");

        var info = vsd.GetContextInfo();
        info.ToList().ForEach(x => Console.WriteLine($"{x.Key} = {x.Value}"));

        var sourceSize = vsd.FrameSize;
        var sourcePixelFormat = HWDevice == AVHWDeviceType.AV_HWDEVICE_TYPE_NONE
            ? vsd.PixelFormat
            : GetHWPixelFormat(HWDevice);
        var destinationSize = sourceSize;
        var destinationPixelFormat = AVPixelFormat.AV_PIX_FMT_BGR24;
        using var vfc = new VideoFrameConverter(sourceSize, sourcePixelFormat, destinationSize, destinationPixelFormat);

        var frameNumber = 0;

        while (vsd.TryDecodeNextFrame(out var frame))
        {
            var convertedFrame = vfc.Convert(frame);

            // using (var bitmap = new Bitmap(convertedFrame.width,
            //            convertedFrame.height,
            //            convertedFrame.linesize[0],
            //            PixelFormat.Format24bppRgb,
            //            (IntPtr)convertedFrame.data[0]))
            //     bitmap.Save($"frames/frame.{frameNumber:D8}.jpg", ImageFormat.Jpeg);

            Debug.Log($"frame: {frameNumber}");
            frameNumber++;
        }
    }
    private static AVPixelFormat GetHWPixelFormat(AVHWDeviceType hWDevice)
    {
        return hWDevice switch
        {
            AVHWDeviceType.AV_HWDEVICE_TYPE_NONE => AVPixelFormat.AV_PIX_FMT_NONE,
            AVHWDeviceType.AV_HWDEVICE_TYPE_VDPAU => AVPixelFormat.AV_PIX_FMT_VDPAU,
            AVHWDeviceType.AV_HWDEVICE_TYPE_CUDA => AVPixelFormat.AV_PIX_FMT_CUDA,
            AVHWDeviceType.AV_HWDEVICE_TYPE_VAAPI => AVPixelFormat.AV_PIX_FMT_VAAPI,
            AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2 => AVPixelFormat.AV_PIX_FMT_NV12,
            AVHWDeviceType.AV_HWDEVICE_TYPE_QSV => AVPixelFormat.AV_PIX_FMT_QSV,
            AVHWDeviceType.AV_HWDEVICE_TYPE_VIDEOTOOLBOX => AVPixelFormat.AV_PIX_FMT_VIDEOTOOLBOX,
            AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA => AVPixelFormat.AV_PIX_FMT_NV12,
            AVHWDeviceType.AV_HWDEVICE_TYPE_DRM => AVPixelFormat.AV_PIX_FMT_DRM_PRIME,
            AVHWDeviceType.AV_HWDEVICE_TYPE_OPENCL => AVPixelFormat.AV_PIX_FMT_OPENCL,
            AVHWDeviceType.AV_HWDEVICE_TYPE_MEDIACODEC => AVPixelFormat.AV_PIX_FMT_MEDIACODEC,
            _ => AVPixelFormat.AV_PIX_FMT_NONE
        };
    }


}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

using Touch = UnityEngine.Touch;
using PLMpegSharp;
using PLMpegSharp.Container;


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

        DataBuffer buffer =
            DataBuffer.CreateWithFilename(@"C:\Users\XF\AppData\LocalLow\SNURhythm\iBMS\take003\bga_take.mpg");
        VideoDecoder decoder = new VideoDecoder(buffer);
        var current = 0;

      
            var startTime = DateTime.Now;
            while (decoder.HasEnded == false)
            {

                Frame frame = decoder.Decode();

                byte[] data = new byte[frame.Width * frame.Height * 4];
                frame.ToARGB(data, decoder.Width * 4);
                Texture2D texture = new Texture2D(frame.Width, frame.Height, TextureFormat.ARGB32, false);
                texture.LoadRawTextureData(data);
                texture.Apply();
                // save as png
                byte[] bytes = texture.EncodeToPNG();
                File.WriteAllBytes(@"C:\Users\XF\AppData\LocalLow\SNURhythm\iBMS\take003\frames\bga_take_" + current + "_"+frame.Time*1000+ ".png", bytes);
                current++;
                
                if (current == 300) break;

            }
            Debug.Log("Time: " + (DateTime.Now - startTime).TotalMilliseconds);




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
}

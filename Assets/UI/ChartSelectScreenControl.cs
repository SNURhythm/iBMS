using System;

using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using B83.Image.BMP;
using Unity.VisualScripting;

using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

public class ChartSelectScreenControl : MonoBehaviour
{
    struct ChartItemProp
    {
        public Chart Chart;
        public string RootPath;
        public string BmsPath;
    }
    public VisualTreeAsset chartItem;

    private VisualElement chartSelectScreen;
    private List<ChartItemProp> chartItemProps = new List<ChartItemProp>();
    private OrderedDictionary imageCache = new OrderedDictionary();
    private string selectedBmsPath;
    void OnEnable()
    {
        var persistDataPath = Application.persistentDataPath;
        // Task.Run(() =>
        // {
            
            Debug.Log("Start parsing");

    
            var accumParseTime = 0.0;
            //
            var info = new DirectoryInfo(persistDataPath);
            var fileInfo = info.GetDirectories();
            Debug.Log("Directory count: " + fileInfo.Length);
            var startTimeTotal = DateTime.Now;
            for (var i = 0; i < fileInfo.Length; i++)
            {

                var file = fileInfo[i];


                var chartInfo = new DirectoryInfo(file.FullName);
                Debug.Log("Parsing " + chartInfo.FullName);
                var chartFileInfo = chartInfo.GetFiles();
                foreach (var chartFile in chartFileInfo)
                {
                    if (!new[] { ".bms", ".bme", ".bml" }.Contains(chartFile.Extension)) continue;
                    var parser = new BMSParser();
                    try
                    {
                        var startTime = DateTime.Now;
                        parser.Parse(chartFile.FullName, metaOnly: true);
                        var endTime = DateTime.Now;
                        accumParseTime += (endTime - startTime).TotalMilliseconds;
                    }
                    catch (Exception e)
                    {
                        Debug.Log(e);
                        continue;
                    }

                    var chart = parser.GetChart();
                    var chartItemProp = new ChartItemProp
                    {
                        Chart = chart,
                        RootPath = file.FullName,
                        BmsPath = chartFile.FullName
                    };
                    chartItemProps.Add(chartItemProp);
                }
            }

            Debug.Log("Parse time: " + accumParseTime);
            var endTimeTotal = DateTime.Now;
            Debug.Log("Total time: " + (endTimeTotal - startTimeTotal).TotalMilliseconds);
        // });

        chartSelectScreen = GetComponent<UIDocument>().rootVisualElement;
        var chartListView = chartSelectScreen.Q<ListView>("ChartListView");
        //disable scrollbar
        chartListView.Q<ScrollView>().verticalScrollerVisibility = ScrollerVisibility.Hidden;

        chartListView.selectionType = SelectionType.None;

        chartListView.itemsSource = chartItemProps;
        chartListView.makeItem = () =>
        {
            
            var chartItemElement = chartItem.CloneTree();
            var button = chartItemElement.Q<Button>("Button");
            button.focusable = false;
            button.clicked += () =>
            {
                var data = (ChartItemProp)chartItemElement.userData;
                // GameManager.Instance.BmsPath = data.Path;
                // UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("LoadingScene");
                // set selection
                var prevSelected = chartListView.Q<Button>(className: "selected");
                if (prevSelected != null)
                    prevSelected.RemoveFromClassList("selected");
                button.AddToClassList("selected");
                selectedBmsPath = data.BmsPath;
                chartSelectScreen.Q<Label>("ChartTitle").text = data.Chart.Title + (data.Chart.SubTitle != null ? " " + data.Chart.SubTitle : "");
                chartSelectScreen.Q<Label>("ChartArtist").text = data.Chart.Artist;
                if (data.Chart.StageFile != null && data.Chart.StageFile.Trim().Length > 0)
                {
                    chartSelectScreen.Q<Image>("JacketImage").image =
                        LoadImage(Path.Combine(data.RootPath, data.Chart.StageFile));
                }
                else
                {
                    chartSelectScreen.Q<Image>("JacketImage").image = null;
                }

                Debug.Log(data.BmsPath);
            };
            return chartItemElement;
        };
        chartListView.bindItem = (element, i) =>
        {


            var chartItemProp = (ChartItemProp)chartListView.itemsSource[i];

            var chartItemElement = (VisualElement)element;
            if(selectedBmsPath == chartItemProp.BmsPath)
                chartItemElement.Q<Button>("Button").AddToClassList("selected");
            var chart = chartItemProp.Chart;
            var title = chart.Title + (chart.SubTitle != null ? " " + chart.SubTitle : "");
            chartItemElement.Q<Label>("Title").text = title;
            chartItemElement.Q<Label>("Artist").text = chart.Artist;
            var trials = new[]
            {
                chart.Banner,
                chart.StageFile,
                chart.BackBmp
            };
            foreach (var trial in trials)
            {
                if (trial == null || trial.Trim().Length == 0) continue;
                var texture = LoadImage(Path.Combine(chartItemProp.RootPath, trial));
                if (texture != null)
                {
                    chartItemElement.Q<Image>("BannerImage").image = texture;
                    break;
                }
            }


            chartItemElement.userData = chartItemProp;
        };
        chartListView.unbindItem = (element, i) =>
        {
            // remove image
            var chartItemElement = (VisualElement)element;
            chartItemElement.Q<Image>("BannerImage").image = null;
            chartItemElement.Q<Button>("Button").RemoveFromClassList("selected");
            
        };
        
        var startButton = chartSelectScreen.Q<Button>("StartButton");
        startButton.clicked += () =>
        {
            if (selectedBmsPath == null)
            {
                Debug.Log("No chart selected");
                return;
            }

            GameManager.Instance.BmsPath = selectedBmsPath;
            UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("LoadingScene");
        };


    }

    private Texture2D LoadImage(string path)
    {
        try
        {
            if (imageCache.Contains(path))
            {
                return (Texture2D)imageCache[path];
            }

            Texture2D texture;
            if (path.ToLower().EndsWith(".bmp"))
            {
                var bmpLoader = new BMPLoader();
                var bmp = bmpLoader.LoadBMP(path);
                texture = bmp.ToTexture2D();
            }
            else
            {
                texture = new Texture2D(1, 1);
                texture.LoadImage(File.ReadAllBytes(path));
            }

            imageCache[path] = texture;

            // if cache is too large, remove the oldest one
            if (imageCache.Count > 10)
            {
                imageCache.RemoveAt(0);
            }

            return texture;
        }
        catch (Exception e)
        {
            Debug.Log(e);
            return null;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

using System;

using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using B83.Image.BMP;
using FMOD;
using Unity.VisualScripting;

using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

public class ChartSelectScreenControl : MonoBehaviour
{
    struct ChartItemProp
    {
        public Chart Chart;
        public string RootPath;
        public string BmsPath;
    }
    
    struct PathProp
    {
        public string RootPath;
        public string BmsPath;
    }
    public VisualTreeAsset chartItem;

    private VisualElement chartSelectScreen;
    private OrderedDictionary imageCache = new OrderedDictionary();
    private Dictionary<PathProp, ChartItemProp> chartCache = new Dictionary<PathProp, ChartItemProp>();
    private string selectedBmsPath;
    private List<PathProp> paths = new();
    // cancellation tokens for parsing
    private Dictionary<PathProp, CancellationTokenSource> cts = new Dictionary<PathProp, CancellationTokenSource>();
    private Dictionary<string, string> previews = new Dictionary<string, string>();
    FMOD.Sound previewSound;
    ChannelGroup channelGroup;
    void FindRecursive(DirectoryInfo directory)
    {
        
        var fileInfo = directory.GetFiles();
        foreach (var file in fileInfo)
        {
            if (file.Name.StartsWith("preview") && new[] { ".mp3", ".ogg", ".flac", ".wav" }.Contains(file.Extension))
            {
                if(previews.ContainsKey(directory.FullName)) continue;
                previews.Add(directory.FullName, file.FullName);
            }
            if (!new[] { ".bms", ".bme", ".bml" }.Contains(file.Extension)) continue;
            paths.Add(new PathProp
            {
                RootPath = directory.FullName,
                BmsPath = file.FullName
            });
        }

        var dirInfo = directory.GetDirectories();
        foreach (var dir in dirInfo)
        {
            FindRecursive(dir);
        }
    }

    void Awake()
    {
        FMODUnity.RuntimeManager.CoreSystem.getMasterChannelGroup(out channelGroup);
    }
    void OnEnable()
    {
        var persistDataPath = Application.persistentDataPath;

        var info = new DirectoryInfo(persistDataPath);
        FindRecursive(info);


        chartSelectScreen = GetComponent<UIDocument>().rootVisualElement;
        var chartListView = chartSelectScreen.Q<ListView>("ChartListView");
        //disable scrollbar
        chartListView.Q<ScrollView>().verticalScrollerVisibility = ScrollerVisibility.Hidden;

        chartListView.selectionType = SelectionType.None;

        chartListView.itemsSource = paths;
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
                channelGroup.stop();
                string preview;
                if (data.Chart.Preview != null)
                {
                    preview = Path.Combine(data.RootPath, data.Chart.Preview);
                }
                else
                {
                    preview = previews.ContainsKey(data.RootPath) ? previews[data.RootPath] : null;
                }

                if (preview != null)
                {
                    FMODUnity.RuntimeManager.CoreSystem.createSound(preview, FMOD.MODE.DEFAULT,
                        out previewSound);
                    FMODUnity.RuntimeManager.CoreSystem.playSound(previewSound, channelGroup, false, out var channel);
                }

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
        
        chartListView.bindItem = async (element, i) =>
        {
            Debug.Log(chartListView.childCount);

            ChartItemProp chartItemProp;
            var parseError = false;
            var path = (PathProp)chartListView.itemsSource[i];
            var chartItemElement = (VisualElement)element;
            var titleLabel = chartItemElement.Q<Label>("Title");
            var artistLabel = chartItemElement.Q<Label>("Artist");
            titleLabel.text = "로딩중...";
            artistLabel.text = "";
            if(chartCache.ContainsKey(path))
            {
                chartItemProp = chartCache[path];
            }
            else
            {
                var parser = new BMSParser();

                try
                {
                    cts.Add(path, new CancellationTokenSource());

                    var task = Task.Run(() =>
                        {
                            try
                            {
                                parser.Parse(path.BmsPath, metaOnly: true, cancellationToken: cts[path].Token);
                            }
                            catch (Exception e)
                            {
                                Debug.Log(e);
                                parseError = true;
                            }
                        }
                        , cts[path].Token);
                    await task;
                }
                catch (AggregateException e)
                {
                    Debug.Log(e);
                    return;
                }

                if (parseError)
                {
                    chartItemProp = new ChartItemProp
                    {
                        Chart = null,
                        RootPath = path.RootPath,
                        BmsPath = path.BmsPath
                    };
                }
                else
                {

                    var chart_ = parser.GetChart();
                    chartItemProp = new ChartItemProp
                    {
                        Chart = chart_,
                        RootPath = path.RootPath,
                        BmsPath = path.BmsPath
                    };

                    chartCache.Add(path, chartItemProp);
                }
            }


            if (selectedBmsPath == chartItemProp.BmsPath)
                chartItemElement.Q<Button>("Button").AddToClassList("selected");
            var chart = chartItemProp.Chart;
            string title;
            string artist;
            if (chart == null)
            {
                title = "Parse Error";
                artist = "Parse Error";
            }
            else
            {
                title = chart.Title + (chart.SubTitle != null ? " " + chart.SubTitle : "");
                artist = chart.Artist;

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
            }
            titleLabel.text = title;
            artistLabel.text = artist;


            chartItemElement.userData = chartItemProp;
            
        };
        chartListView.unbindItem = (element, i) =>
        {
            // remove image
            var chartItemElement = (VisualElement)element;
            chartItemElement.Q<Image>("BannerImage").image = null;
            chartItemElement.Q<Button>("Button").RemoveFromClassList("selected");
            chartItemElement.Q<Label>("Title").text = "";
            chartItemElement.Q<Label>("Artist").text = "";
            // cancel parsing
            var path = (PathProp)chartListView.itemsSource[i];
            if(cts.ContainsKey(path))
            {
                cts[path].Cancel();
                cts.Remove(path);
            }
            
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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using B83.Image.BMP;
using FMOD;
using Unity.VisualScripting;

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

public class ChartSelectScreenControl : MonoBehaviour
{
    enum DiffType
    {
        Deleted,
        New,
    }

    struct Diff
    {
        public string path;
        public DiffType type;
    }

    public VisualTreeAsset chartItem;

    private VisualElement chartSelectScreen;
    private OrderedDictionary imageCache = new OrderedDictionary();

    private string selectedBmsPath;

    private List<ChartMeta> chartMetas = new();
    // cancellation tokens for parsing
    private Dictionary<string, CancellationTokenSource> cts = new Dictionary<string, CancellationTokenSource>();
    Sound previewSound;
    ChannelGroup channelGroup;

    void FindNew(List<Diff> diffs, HashSet<string> prevPathSet, DirectoryInfo directory)
    {
        var fileInfo = directory.GetFiles();
        foreach (var file in fileInfo)
        {
            if (!new[] { ".bms", ".bme", ".bml" }.Contains(file.Extension)) continue;
            if (!prevPathSet.Contains(file.FullName))
            {
                diffs.Add(new Diff { path = file.FullName, type = DiffType.New });
            }
        }

        var dirInfo = directory.GetDirectories();
        foreach (var dir in dirInfo)
        {
            FindNew(diffs, prevPathSet, dir);
        }
    }
    
    void Awake()
    {
        FMODUnity.RuntimeManager.CoreSystem.getMasterChannelGroup(out channelGroup);
    }
    void OnEnable()
    {
        ChartDBHelper.Instance.Open();
        var persistDataPath = Application.persistentDataPath;
        chartMetas = ChartDBHelper.Instance.SelectAll();
        var info = new DirectoryInfo(persistDataPath);
        var task = Task.Run(() =>
        {
            try
            {
                var pathSet = new HashSet<string>();
                foreach (var chart in chartMetas)
                {
                    pathSet.Add(chart.BmsPath);
                }
                var diffs = new List<Diff>();
                FindNew(diffs, pathSet, info);
                // check deleted
                var count = diffs.Count;
                foreach (var path in pathSet)
                {
                    if (!File.Exists(path))
                    {
                        diffs.Add(new Diff { path = path, type = DiffType.Deleted });
                    }
                }
                var deletedCount = diffs.Count - count;
                Debug.Log($"Found {count} new charts and {deletedCount} deleted charts");


                if (diffs.Count > 0)
                {
                    Debug.Log("Scanning...");
                    
                    var errorCount = 0;
                    foreach (var diff in diffs)
                    {
                        if (diff.type == DiffType.Deleted)
                        {
                            // remove from db
                            ChartDBHelper.Instance.Delete(diff.path);
                            chartMetas.RemoveAll(chart => chart.BmsPath == diff.path);
                        }
                        else
                        {
                            var parser = new BMSParser();
                            try
                            {
                                parser.Parse(diff.path, metaOnly: true);
                            }
                            catch (Exception e)
                            {
                                Debug.LogWarning("ParserError: "+e);
                                errorCount++;
                                continue;
                            }

                            var chartMeta = parser.GetChart().ChartMeta;
                            chartMetas.Add(chartMeta);
                            Debug.Log($"{chartMetas.Count} ({chartMeta.Title})");
                            // insert to db
                            ChartDBHelper.Instance.Insert(chartMeta);
                        }
                    }

                    Debug.Log("Scan complete, " + errorCount + " errors");
                }


            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
            
        });
        task.ContinueWith(t =>
        {
            Debug.Log("Loading complete");
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                // update list
                var chartListView = chartSelectScreen.Q<ListView>("ChartListView");
                chartListView.itemsSource = chartMetas;
                chartListView.Rebuild();
            });
        });



        chartSelectScreen = GetComponent<UIDocument>().rootVisualElement;
        var chartListView = chartSelectScreen.Q<ListView>("ChartListView");
        //disable scrollbar
        chartListView.Q<ScrollView>().verticalScrollerVisibility = ScrollerVisibility.Hidden;

        chartListView.selectionType = SelectionType.None;

        chartListView.itemsSource = chartMetas;
        chartListView.makeItem = () =>
        {
            
            var chartItemElement = chartItem.CloneTree();
            var button = chartItemElement.Q<Button>("Button");
            button.focusable = false;
            button.clicked += () =>
            {
                var data = (Chart)chartItemElement.userData;
                // GameManager.Instance.BmsPath = data.Path;
                // UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("LoadingScene");
                // set selection
                var prevSelected = chartListView.Q<Button>(className: "selected");
                if (prevSelected != null)
                    prevSelected.RemoveFromClassList("selected");
                button.AddToClassList("selected");
                selectedBmsPath = data.ChartMeta.BmsPath;
                channelGroup.stop();
                string preview;
                if (data.ChartMeta.Preview != null)
                {
                    preview = Path.Combine(data.ChartMeta.Folder, data.ChartMeta.Preview);
                }
                else
                {
                    // find a mp3/wav/ogg/flac file which starts with "preview"
                    var files = Directory.GetFiles(data.ChartMeta.Folder);
                    preview = files.FirstOrDefault(file =>
                    {
                        var ext = Path.GetExtension(file);
                        return new[] { ".mp3", ".wav", ".ogg", ".flac" }.Contains(ext) &&
                               Path.GetFileName(file).StartsWith("preview");
                    });
                }

                if (preview != null)
                {
                    FMODUnity.RuntimeManager.CoreSystem.createSound(preview, FMOD.MODE.DEFAULT,
                        out previewSound);
                    FMODUnity.RuntimeManager.CoreSystem.playSound(previewSound, channelGroup, false, out var channel);
                }

                chartSelectScreen.Q<Label>("ChartTitle").text = data.ChartMeta.Title + (data.ChartMeta.SubTitle != null ? " " + data.ChartMeta.SubTitle : "");
                chartSelectScreen.Q<Label>("ChartArtist").text = data.ChartMeta.Artist;
                if (data.ChartMeta.StageFile != null && data.ChartMeta.StageFile.Trim().Length > 0)
                {
                    chartSelectScreen.Q<Image>("JacketImage").image =
                        LoadImage(Path.Combine(data.ChartMeta.Folder, data.ChartMeta.StageFile));
                }
                else
                {
                    chartSelectScreen.Q<Image>("JacketImage").image = null;
                }

                Debug.Log(data.ChartMeta.BmsPath);
            };
            return chartItemElement;
        };
        
        chartListView.bindItem = async (element, i) =>
        {

            var parseError = false;
            Chart chart = (Chart)chartListView.itemsSource[i];
            var chartItemElement = (VisualElement)element;
            var titleLabel = chartItemElement.Q<Label>("Title");
            var artistLabel = chartItemElement.Q<Label>("Artist");
            titleLabel.text = "Loading...";
            artistLabel.text = "";


            if (selectedBmsPath == chart.ChartMeta.BmsPath)
                chartItemElement.Q<Button>("Button").AddToClassList("selected");

            var title = chart.ChartMeta.Title + (chart.ChartMeta.SubTitle != null ? " " + chart.ChartMeta.SubTitle : "");
            var artist = chart.ChartMeta.Artist;

            var trials = new[]
            {
                chart.ChartMeta.Banner,
                chart.ChartMeta.StageFile,
                chart.ChartMeta.BackBmp
            };
            foreach (var trial in trials)
            {
                if (trial == null || trial.Trim().Length == 0) continue;
                var texture = LoadImage(Path.Combine(chart.ChartMeta.Folder, trial));
                if (texture != null)
                {
                    chartItemElement.Q<Image>("BannerImage").image = texture;
                    break;
                }
            }
            
            titleLabel.text = title;
            artistLabel.text = artist;


            chartItemElement.userData = chart;
            
        };
        chartListView.unbindItem = (element, i) =>
        {
            // remove image
            var chartItemElement = (VisualElement)element;
            chartItemElement.Q<Image>("BannerImage").image = null;
            chartItemElement.Q<Button>("Button").RemoveFromClassList("selected");
            chartItemElement.Q<Label>("Title").text = "";
            chartItemElement.Q<Label>("Artist").text = "";
            
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

            StartCoroutine(LoadScene());

        };


    }
    
    IEnumerator LoadScene()
    {
        var asyncOperation = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("PlayScene");
        while (!asyncOperation.isDone)
        {
            Debug.Log(asyncOperation.progress);
            yield return null;
        }

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
        SceneManager.sceneUnloaded += (scene) =>
        {

            channelGroup.stop();
            channelGroup.release();
            previewSound.release();
            ChartDBHelper.Instance.Close();
        };
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

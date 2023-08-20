using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
using Thread = System.Threading.Thread;

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
    private ListView chartListView;
    private OrderedDictionary imageCache = new OrderedDictionary();

    private ChartMeta selectedChartMeta;


public GameManager gm = GameManager.Instance;

    Sound previewSound;
    string previewSoundPath;
    ChannelGroup channelGroup;
    Task parseTask;
    // cancellation token for parsing
    private CancellationTokenSource parseCancellationTokenSource = new();

    void FindNew(Dictionary<string, List<Diff>> diffs, HashSet<string> prevPathSet, DirectoryInfo directory, CancellationToken token)
    {
        var dirInfo = directory.GetDirectories();
        foreach (var dir in dirInfo)
        {
            if (token.IsCancellationRequested)
            {
                Logger.Log("Parsing cancelled");
                return;
            }
            FindNew(diffs, prevPathSet, dir, token);
        }

        var fileInfo = directory.GetFiles();
        foreach (var file in fileInfo)
        {
            if (token.IsCancellationRequested)
            {
                Logger.Log("Parsing cancelled");
                return;
            }
            // check extension
            if (!new[] { ".bms", ".bme", ".bml" }.Contains(file.Extension))
            {
                continue;
            }
            if (!prevPathSet.Contains(file.FullName))
            {
                if (!diffs.ContainsKey(directory.FullName))
                {
                    diffs.Add(directory.FullName, new List<Diff>());
                }
                diffs[directory.FullName].Add(new Diff { path = file.FullName, type = DiffType.New });
            }
        }
    }


    void UpdateChartCountLabel(Label label, int loadingLeft, int errorCount)
    {
        if(isScanning)
        {
            label.text = "Scanning...";
            return;
        }
        var sb = new StringBuilder();
        if (loadingLeft > 0)
        {
            sb.Append("loading: ");
            sb.Append(loadingLeft);
        }
        if (errorCount > 0)
        {
            sb.Append(" error: ");
            sb.Append(errorCount);
        }

        label.text = sb.ToString();
    }

    void Sort(List<ChartMeta> chartMetas)
    {
        chartMetas.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.Ordinal));
    }
    bool isScanning = false;
    int errorCount = 0;
    int loadedCount = 0;
    int newCount = 0;
    int initialTotal = 0;
    private Label chartCountLabel;

    void Start()
    {
        FMODUnity.RuntimeManager.CoreSystem.getMasterChannelGroup(out channelGroup);
        Resources.UnloadUnusedAssets();
        System.GC.Collect();
        chartSelectScreen = GetComponent<UIDocument>().rootVisualElement;
        chartListView = chartSelectScreen.Q<ListView>("ChartListView");
        var searchBox = chartSelectScreen.Q<TextField>("SearchBox");
        chartCountLabel = chartSelectScreen.Q<Label>("ChartCountLabel");
        chartCountLabel.text = "";


        #region Update DB
        var persistDataPath = Application.persistentDataPath;
        var chartMetas = ChartDBHelper.Instance.SelectAll();
        // sort by title
        Sort(chartMetas);
        initialTotal = chartMetas.Count;
        chartCountLabel.text = "Scanning";
        var info = new DirectoryInfo(persistDataPath);
        var token = parseCancellationTokenSource.Token;
        var thisGameObject = gameObject;
        parseTask = Task.Run(() =>
        {
            try
            {
                var pathSet = new HashSet<string>();
                foreach (var chart in chartMetas)
                {
                    if (token.IsCancellationRequested)
                    {
                        Logger.Log("Parsing cancelled");
                        break;
                    }
                    pathSet.Add(chart.BmsPath);
                }
                var diffs = new Dictionary<string, List<Diff>>();
                isScanning = true;
                FindNew(diffs, pathSet, info, token);
                isScanning = false;
                newCount = diffs.Count;
                foreach (var path in pathSet)
                {
                    if (token.IsCancellationRequested)
                    {
                        Logger.Log("Parsing cancelled");
                        break;
                    }
                    if (!File.Exists(path))
                    {
                        // diffs.Add(Path.GetDirectoryName(path), new Diff { path = path, type = DiffType.Deleted });
                        if (!diffs.ContainsKey(Path.GetDirectoryName(path)))
                        {
                            diffs.Add(Path.GetDirectoryName(path), new List<Diff>());
                        }
                        diffs[Path.GetDirectoryName(path)].Add(new Diff { path = path, type = DiffType.Deleted });
                    }
                    
                }
                var deletedCount = diffs.Count - newCount;
                Logger.Log($"Found {newCount} new charts and {deletedCount} deleted charts");


                if (diffs.Count == 0) return;
                Logger.Log("Scanning...");

                // var tx = ChartDBHelper.Instance.BeginTransaction();
                try
                {

                    Parallel.ForEach(diffs,
                        new ParallelOptions
                            { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = token },
                        diffList =>
                        {
                            Logger.Log("Current Processor Id: " + Thread.GetCurrentProcessorId());
// Logger.Log("base path: " + diffList.Key);
                            
                                //Logger.Log($"Parsing {diff.path}");
                                Interlocked.Increment(ref loadedCount);
                                foreach (var diff in diffList.Value)
                                {
                                    if (token.IsCancellationRequested)
                                    {
                                        //Logger.Log("Parsing cancelled");
                                        return;
                                    }

                                    if (diff.type == DiffType.Deleted)
                                    {
                                        // remove from db
                                        ChartDBHelper.Instance.Delete(diff.path);
                                    }
                                    else
                                    {
                                        var parser = new BMSParser();
                                        try
                                        {
                                            var stopWatch = new Stopwatch();
                                            stopWatch.Start();
                                            parser.Parse(diff.path, metaOnly: true, cancellationToken: token);
                                            stopWatch.Stop();
                                            // Logger.Log($"Parsed {diff.path} in {stopWatch.ElapsedMilliseconds} ms");
                                        }
                                        catch (Exception e)
                                        {
                                            Logger.LogWarning("Error while parsing " + diff.path + ": " + e +
                                                              e.StackTrace);
                                            Interlocked.Increment(ref errorCount);
                                            return;
                                        }

                                        

                                        var chartMeta = parser.GetChart().ChartMeta;


                                        // insert to db
                                        var stopWatch2 = new Stopwatch();
                                        ChartDBHelper.Instance.Insert(chartMeta);
                                        stopWatch2.Stop();
                                        // Logger.Log($"Inserted {diff.path} in {stopWatch2.ElapsedMilliseconds} ms");
                                    }
                                }
                        });
                } catch (OperationCanceledException)
                {
                    Logger.Log("Parsing cancelled");
                }

                // tx.Commit();

                Logger.Log("Scan complete, " + errorCount + " errors");


            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }

        }, token);
        parseTask.ContinueWith(t =>
        {
            Logger.Log("Loading complete");
            if (!thisGameObject.IsDestroyed())
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    var all = ChartDBHelper.Instance.SelectAll();
                    // update list if search text is empty
                    if (searchBox.value != "") return;
                    Sort(all);
                    chartListView.itemsSource = all;
                    chartListView.Rebuild();
                });
            }
        });
        #endregion





        #region ChartListView

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
                var data = (ChartMeta)chartItemElement.userData;
                // GameManager.Instance.BmsPath = data.Path;
                // UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("LoadingScene");
                // set selection
                var prevSelected = chartListView.Q<Button>(className: "selected");
                if (prevSelected != null)
                    prevSelected.RemoveFromClassList("selected");
                button.AddToClassList("selected");
                selectedChartMeta = data;

                string newPreviewSoundPath;
                if (data.Preview != null)
                {
                    newPreviewSoundPath = Path.Combine(data.Folder, data.Preview);
                }
                else
                {
                    // find a mp3/wav/ogg/flac file which starts with "preview"
                    var files = Directory.GetFiles(data.Folder);
                    newPreviewSoundPath = files.FirstOrDefault(file =>
                    {
                        var ext = Path.GetExtension(file);
                        return new[] { ".mp3", ".wav", ".ogg", ".flac" }.Contains(ext) &&
                               Path.GetFileName(file).StartsWith("preview");
                    });
                }

                if (previewSoundPath != newPreviewSoundPath)
                {
                    previewSound.release();
                    channelGroup.stop();
                    if (newPreviewSoundPath != null)
                    {
                        FMODUnity.RuntimeManager.CoreSystem.createSound(newPreviewSoundPath, FMOD.MODE.LOOP_NORMAL,
                            out previewSound);
                        FMODUnity.RuntimeManager.CoreSystem.playSound(previewSound, channelGroup, true,
                            out var channel);
                        channel.setLoopCount(-1); // loop forever
                        channel.setPaused(false);
                    }
                    previewSoundPath = newPreviewSoundPath;
                }

                var sb = new StringBuilder();
                sb.Append(data.Title);
                if (data.SubTitle != null)
                {
                    sb.Append(" ");
                    sb.Append(data.SubTitle);
                }

                chartSelectScreen.Q<Label>("ChartTitle").text = sb.ToString();
                chartSelectScreen.Q<Label>("ChartArtist").text = data.Artist;
                if (data.StageFile != null && data.StageFile.Trim().Length > 0)
                {
                    chartSelectScreen.Q<Image>("JacketImage").image =
                        LoadImage(Path.Combine(data.Folder, data.StageFile));
                }
                else
                {
                    chartSelectScreen.Q<Image>("JacketImage").image = null;
                }

                Logger.Log(data.BmsPath);
            };
            return chartItemElement;
        };

        chartListView.bindItem = (element, i) =>
        {

            var parseError = false;
            ChartMeta chartMeta = (ChartMeta)chartListView.itemsSource[i];
            var chartItemElement = (VisualElement)element;
            var titleLabel = chartItemElement.Q<Label>("Title");
            var artistLabel = chartItemElement.Q<Label>("Artist");
            var playLevelLabel = chartItemElement.Q<Label>("PlayLevel");
            var keysLabel = chartItemElement.Q<Label>("Keys");
            titleLabel.text = "Loading...";
            artistLabel.text = "";


            if (selectedChartMeta != null && selectedChartMeta.BmsPath == chartMeta.BmsPath)
                chartItemElement.Q<Button>("Button").AddToClassList("selected");

            var trials = new[]
            {
                chartMeta.Banner,
                chartMeta.StageFile,
                chartMeta.BackBmp
            };
            foreach (var trial in trials)
            {
                if (trial == null || trial.Trim().Length == 0) continue;
                var texture = LoadImage(Path.Combine(chartMeta.Folder, trial));
                if (texture != null)
                {
                    chartItemElement.Q<Image>("BannerImage").image = texture;
                    break;
                }
            }

            var titleSb = new StringBuilder();
            var keysSb = new StringBuilder();
            keysSb.Append(chartMeta.KeyMode);
            keysSb.Append("K");
            if (chartMeta.Player != 1)
            {
                titleSb.Append("[DP-Unsupported] ");
                keysSb.Append("DP");
                // make it gray
                chartItemElement.Q<Label>("Title").style.color = new StyleColor(Color.gray);
            }
            else
            {
                keysSb.Append("SP");
                chartItemElement.Q<Label>("Title").style.color = new StyleColor(Color.white);
            }
            titleSb.Append(chartMeta.Title);
            if (chartMeta.SubTitle != null)
            {
                titleSb.Append(" ");
                titleSb.Append(chartMeta.SubTitle);
            }

            titleLabel.text = titleSb.ToString();
            artistLabel.text = chartMeta.Artist;
            playLevelLabel.text = chartMeta.PlayLevel.ToString();
            keysLabel.text = keysSb.ToString();


            chartItemElement.userData = chartMeta;

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
        #endregion

        #region SearchBox

        searchBox.RegisterValueChangedCallback(evt =>
        {
            var keyword = evt.newValue.Trim();

            List<ChartMeta> charts;
            charts = keyword.Length == 0 ? ChartDBHelper.Instance.SelectAll() : ChartDBHelper.Instance.Search(keyword);
            Sort(charts);
            chartListView.itemsSource = charts;
            chartListView.Rebuild();
            if (chartListView.itemsSource.Count > 0)
                chartListView.ScrollToItem(0);
        });
        #endregion

        var startButton = chartSelectScreen.Q<Button>("StartButton");
        startButton.clicked += () =>
        {

            if (selectedChartMeta == null)
            {
                Logger.Log("No chart selected");
                return;
            }

            GameManager.Instance.BmsPath = selectedChartMeta.BmsPath;
            GameManager.Instance.KeyMode = selectedChartMeta.KeyMode;
            StartCoroutine(LoadScene());

        };

    }

    IEnumerator LoadScene()
    {
        var asyncOperation = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("PlayScene");
        while (!asyncOperation.isDone)
        {
            Logger.Log(asyncOperation.progress);
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
            Logger.Log(e);
            return null;
        }
    }



    // Update is called once per frame
    void Update()
    {
        UpdateChartCountLabel(chartCountLabel,
            newCount - loadedCount - errorCount, errorCount);
    }

    private void OnDestroy()
    {
        Logger.Log("OnDestroy");
        parseCancellationTokenSource.Cancel();

        channelGroup.stop();
        channelGroup.release();
        previewSound.release();
        chartListView.itemsSource = null;
        imageCache.Clear();
        chartListView.Clear();
        chartListView.viewController.ClearItems();
        chartListView = null;
        chartSelectScreen = null;

        parseTask.Wait();
        Resources.UnloadUnusedAssets();
        System.GC.Collect();
    }
}

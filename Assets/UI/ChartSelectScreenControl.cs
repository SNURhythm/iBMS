using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
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




    Sound previewSound;
    string previewSoundPath;
    ChannelGroup channelGroup;
    Task parseTask;
    // cancellation token for parsing
    private CancellationTokenSource parseCancellationTokenSource = new();

    void FindNew(List<Diff> diffs, HashSet<string> prevPathSet, DirectoryInfo directory, CancellationToken token)
    {
        var fileInfo = directory.GetFiles();
        foreach (var file in fileInfo)
        {
            if (token.IsCancellationRequested)
            {
                Debug.Log("Parsing cancelled");
                return;
            }
            if (!new[] { ".bms", ".bme", ".bml" }.Contains(file.Extension)) continue;
            if (!prevPathSet.Contains(file.FullName))
            {
                diffs.Add(new Diff { path = file.FullName, type = DiffType.New });
            }
        }

        var dirInfo = directory.GetDirectories();
        foreach (var dir in dirInfo)
        {
            if (token.IsCancellationRequested)
            {
                Debug.Log("Parsing cancelled");
                return;
            }
            FindNew(diffs, prevPathSet, dir, token);
        }
    }

    void Awake()
    {
        FMODUnity.RuntimeManager.CoreSystem.getMasterChannelGroup(out channelGroup);
    }
    void UpdateChartCountLabel(Label label, int total, int loadingLeft)
    {
        var sb = new StringBuilder();
        sb.Append(total);
        sb.Append(" chart(s)");
        if (loadingLeft > 0)
        {
            sb.Append(" (loading: ");
            sb.Append(loadingLeft);
            sb.Append(")");
        }

        label.text = sb.ToString();
    }

    void Sort(List<ChartMeta> chartMetas)
    {
        chartMetas.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.Ordinal));
    }
    void OnEnable()
    {
        Resources.UnloadUnusedAssets();
        System.GC.Collect();
        chartSelectScreen = GetComponent<UIDocument>().rootVisualElement;
        chartListView = chartSelectScreen.Q<ListView>("ChartListView");
        var searchBox = chartSelectScreen.Q<TextField>("SearchBox");
        var chartCountLabel = chartSelectScreen.Q<Label>("ChartCountLabel");
        chartCountLabel.text = "";


        #region Update DB
        var persistDataPath = Application.persistentDataPath;
        var connection = ChartDBHelper.Instance.Connect();
        connection.Open();
        var chartMetas = ChartDBHelper.Instance.SelectAll(connection);
        // sort by title
        Sort(chartMetas);
        var initialTotal = chartMetas.Count;
        UpdateChartCountLabel(chartCountLabel, initialTotal, 0);
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
                        Debug.Log("Parsing cancelled");
                        break;
                    }
                    pathSet.Add(chart.BmsPath);
                }
                var diffs = new List<Diff>();
                FindNew(diffs, pathSet, info, token);
                // check deleted
                var count = diffs.Count;
                foreach (var path in pathSet)
                {
                    if (token.IsCancellationRequested)
                    {
                        Debug.Log("Parsing cancelled");
                        break;
                    }
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
                    var loadedCount = 0;
                    var tx = connection.BeginTransaction();
                    foreach (var diff in diffs)
                    {
                        if (token.IsCancellationRequested)
                        {
                            Debug.Log("Parsing cancelled");
                            break;
                        }
                        if (diff.type == DiffType.Deleted)
                        {
                            // remove from db
                            ChartDBHelper.Instance.Delete(connection, diff.path);
                        }
                        else
                        {
                            var parser = new BMSParser();
                            try
                            {
                                var stopWatch = new System.Diagnostics.Stopwatch();
                                stopWatch.Start();
                                parser.Parse(diff.path, metaOnly: true, cancellationToken: token);
                                stopWatch.Stop();
                                // Debug.Log($"Parsed {diff.path} in {stopWatch.ElapsedMilliseconds} ms");
                            }
                            catch (Exception e)
                            {
                                Debug.LogWarning("Error while parsing " + diff.path + ": " + e + e.StackTrace);
                                errorCount++;
                                continue;
                            }
                            loadedCount++;

                            var chartMeta = parser.GetChart().Meta;
                            var count1 = loadedCount;
                            if (loadedCount % 10 == 0 || loadedCount <= 10)
                            {
                                if (!thisGameObject.IsDestroyed())
                                {
                                    UnityMainThreadDispatcher.Instance().Enqueue(() =>
                                    {
                                        UpdateChartCountLabel(chartCountLabel, count1 + initialTotal,
                                            count - count1);
                                    });
                                }
                            }

                            // insert to db
                            ChartDBHelper.Instance.Insert(connection, chartMeta);
                        }
                    }
                    tx.Commit();

                    Debug.Log("Scan complete, " + errorCount + " errors");
                }


            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }

        }, token);
        parseTask.ContinueWith(t =>
        {
            Debug.Log("Loading complete");
            if (!thisGameObject.IsDestroyed())
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    var all = ChartDBHelper.Instance.SelectAll(connection);
                    UpdateChartCountLabel(chartCountLabel, all.Count, 0);
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
                
                // GenreRow, BPMRow, TotalRow, ...
                var genreRow = chartSelectScreen.Q<VisualElement>("GenreRow");
                var bpmRow = chartSelectScreen.Q<VisualElement>("BPMRow");
                var totalRow = chartSelectScreen.Q<VisualElement>("TotalRow");
                var notesRow = chartSelectScreen.Q<VisualElement>("NotesRow");
                var judgementRow = chartSelectScreen.Q<VisualElement>("JudgementRow");
                
                genreRow.Q<Label>("Value").text = data.Genre;
                // min~max (mainbpm)
                var bpmSb = new StringBuilder();
                bpmSb.Append(data.MinBpm);
                if (Math.Abs(data.MinBpm - data.MaxBpm) > 0.0001)
                {
                    bpmSb.Append("~");
                    bpmSb.Append(data.MaxBpm);
                    bpmSb.Append(" (");
                    bpmSb.Append(data.Bpm);
                    bpmSb.Append(")");
                }
                bpmRow.Q<Label>("Value").text = bpmSb.ToString();
                totalRow.Q<Label>("Value").text = data.Total.ToString(CultureInfo.InvariantCulture);
                notesRow.Q<Label>("Value").text = data.TotalNotes.ToString();
                judgementRow.Q<Label>("Value").text = Judge.GetRankDescription(data.Rank);
                

                Debug.Log(data.BmsPath);
            };
            return chartItemElement;
        };

        chartListView.bindItem = async (element, i) =>
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
            charts = keyword.Length == 0 ? ChartDBHelper.Instance.SelectAll(connection) : ChartDBHelper.Instance.Search(connection, keyword);
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
                Debug.Log("No chart selected");
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


        };
    }

    // Update is called once per frame
    void Update()
    {

    }

    private void OnDestroy()
    {
        Debug.Log("OnDestroy");
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

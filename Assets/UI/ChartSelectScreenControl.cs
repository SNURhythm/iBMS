using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unity.VisualScripting;

using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

public class ChartSelectScreenControl : MonoBehaviour
{
    struct ChartItemProp
    {
        public string Title;
        public string Composer;
        public string Path;
    }
    public VisualTreeAsset chartItem;

    private VisualElement chartSelectScreen;
    private List<ChartItemProp> chartItemProps = new List<ChartItemProp>();
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
                    var title = chart.Title + (chart.SubTitle != null ? " " + chart.SubTitle : "");
                    var chartItemProp = new ChartItemProp
                    {
                        Title = title,
                        Composer = chart.Artist,
                        Path = chartFile.FullName
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
        //disable selection
        chartListView.selectionType = SelectionType.None;
        chartListView.itemsSource = chartItemProps;
        chartListView.makeItem = () =>
        {
            
            var chartItemElement = chartItem.CloneTree();
            var button = chartItemElement.Q<Button>("Button");
            button.clicked += () =>
            {
                var data = (ChartItemProp)chartItemElement.userData;
                // GameManager.Instance.BmsPath = data.Path;
                // UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("LoadingScene");

                Debug.Log(data.Path);
            };
            return chartItemElement;
        };
        chartListView.bindItem = (element, i) =>
        {
            var chartItemProp = (ChartItemProp)chartListView.itemsSource[i];
            var chartItemElement = (VisualElement)element;
            chartItemElement.Q<Label>("Title").text = chartItemProp.Title;
            chartItemElement.Q<Label>("Composer").text = chartItemProp.Composer;
            chartItemElement.userData = chartItemProp;
        };


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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Networking;


// .bms (7key) parser

public class BMSParser : IParser
{
    /* Headers
     *
     * ! Not supported
     *
     * #PLAYER int
     * #GENRE string
     * #TITLE string
     * #ARTIST string
     * #BPM float? int? -> 03: 16진수 / 08: 지정BPM
     * #MIDIFILE string
     * #VIDEOFILE string
     * #PLAYLEVEL int
     * #RANK int
     * #TOTAL int
     * #VOLWAV int
     * #STAGEFILE
     * #WAVxx string
     * #BMPxx string
     * #RANDOM int
     * #IF int
     * #ENDIF
     * #ExtChr string !
     * #xxx01-06 string
     * #xxx11-17 string
     * #xxx21-27 string
     * #xxx31-36 string
     * #xxx41-46 string

     선곡창
     -> 플레이 화면
     리절트

     1. 노래가 나오게 하자 = 파싱을 한다

     [마디, 마디, 마디]
     마디 => [[가로줄], [가로줄]]

     가로줄: {
        타이밍: int (마이크로초)
        
     }
    */
    private const int TempKey = 8;
    public const int NoWav = -1;


    public const int Scroll = 1020;


    public static readonly int[] NoteChannels =
    {
        Channel.P1KeyBase,
        Channel.P2KeyBase,
        Channel.P1InvisibleKeyBase,
        Channel.P2InvisibleKeyBase,
        Channel.P1LongKeyBase,
        Channel.P2LongKeyBase,
        Channel.P1MineKeyBase,
        Channel.P2MineKeyBase
    };

    private readonly double[] bpmTable = new double[36 * 36];

    private readonly Chart chart = new();
    private readonly string[] wavTable = new string[36 * 36];
    private readonly string[] bmpTable = new string[36 * 36];
    private int lnobj = -1;
    private int lntype = 1;

    private Dictionary<int, Dictionary<double, TimeLine>> sections;


    public void Parse(string path)
    {
        // <measure number, (channel, data)>
        Dictionary<int, List<(int channel, string data)>> measures = new();

        // read line by line
        StreamReader br;
        if (Application.platform == RuntimePlatform.Android)
        {
            Debug.Log("Android");
            //use UnityWebRequest to read file
            var www = UnityWebRequest.Get(path);
            www.SendWebRequest();
            while (!www.isDone)
            {
            }

            var bytes = www.downloadHandler.data;
            br = new StreamReader(new MemoryStream(bytes));
        }
        else
        {
            br = new StreamReader(path);
        }

        string line;
        while ((line = br.ReadLine()) != null)
        {
            if (!line.StartsWith("#")) continue;
            var measureRegex = new Regex(@"#(\d{3})(\d\d):(.+)");
            var match = measureRegex.Match(line);
            if (match.Success)
            {
                var measure = int.Parse(match.Groups[1].Value);
                var channel = DecodeBase36(match.Groups[2].Value);
                var value = match.Groups[3].Value;
                if (!measures.ContainsKey(measure)) measures.Add(measure, new List<(int channel, string data)>());
                measures[measure].Add((channel, value));
                // TODO: 마디별로 한 번에 처리하도록 수정
                // ParseMeasure(int.Parse(measure), decodeBase36(channel, value);
            }
            else
            {
                if (line.StartsWith("#WAV") || line.StartsWith("#BMP"))
                {
                    var xx = line.Substring(4, 2);
                    var value = line.Substring(7);
                    ParseHeader(line.Substring(1, 3), xx, value); // TODO: refactor this shit
                } else if (line.StartsWith("#BPM"))
                {
                    // #BPMxx value or #BPM value
                    if (line.Substring(4).StartsWith(' '))
                    {
                        var value = line.Substring(5);
                        ParseHeader("BPM", null, value);
                    }
                    else
                    {
                        var xx = line.Substring(4, 2);
                        var value = line.Substring(7);
                        ParseHeader("BPM", xx, value);
                    }
                    
                }
                else
                {
                    var regex = new Regex(@"#([A-Za-z]+)(\d\d)? +(.+)?");
                    match = regex.Match(line);
                    if (match.Success)
                    {
                        var cmd = match.Groups[1].Value;
                        var xx = match.Groups.Count > 3 ? match.Groups[2].Value : null;
                        var value = match.Groups.Count == 3 ? match.Groups[2].Value :
                            match.Groups.Count > 3 ? match.Groups[3].Value : null;

                        ParseHeader(cmd, xx, value);
                    }
                }
            }
        }

        var lastMeasure = measures.Keys.Max();

        long timePassed = 0;
        var bpm = chart.Bpm;
        var currentBpm = bpm;
        var lastNote = new Note[TempKey];
        var lnStart = new LongNote[TempKey];
        for (var i = 0; i <= lastMeasure; i++)
        {
            if (!measures.ContainsKey(i))
            {
                measures.Add(i, new List<(int channel, string data)>());
            }

            // gcd (int, int)
            Measure measure = new();
            SortedDictionary<double, TimeLine> timelines = new();

            foreach (var (channel, data) in measures[i])
            {
                var _channel = channel;
                if (channel == Channel.SectionRate)
                {
                    measure.Scale = double.Parse(data);
                    // Debug.Log($"measure.scale: {measure.scale}, on measure {i}");
                    continue;
                }

                var laneNumber = 0;
                if (channel >= Channel.P1KeyBase && channel < Channel.P1KeyBase + 9)
                {
                    laneNumber = KeyAssign.Beat7[channel - Channel.P1KeyBase];
                    _channel = Channel.P1KeyBase;
                }
                else if (channel >= Channel.P2KeyBase && channel < Channel.P2KeyBase + 9)
                {
                    laneNumber = KeyAssign.Beat7[channel - Channel.P2KeyBase + 9];
                    _channel = Channel.P1KeyBase;
                }
                else if (channel >= Channel.P1InvisibleKeyBase && channel < Channel.P1InvisibleKeyBase + 9)
                {
                    laneNumber = KeyAssign.Beat7[channel - Channel.P1InvisibleKeyBase];
                    _channel = Channel.P1InvisibleKeyBase;
                }
                else if (channel >= Channel.P2InvisibleKeyBase && channel < Channel.P2InvisibleKeyBase + 9)
                {
                    laneNumber = KeyAssign.Beat7[channel - Channel.P2InvisibleKeyBase + 9];
                    _channel = Channel.P1InvisibleKeyBase;
                }
                else if (channel >= Channel.P1LongKeyBase && channel < Channel.P1LongKeyBase + 9)
                {
                    laneNumber = KeyAssign.Beat7[channel - Channel.P1LongKeyBase];
                    _channel = Channel.P1LongKeyBase;
                }
                else if (channel >= Channel.P2LongKeyBase && channel < Channel.P2LongKeyBase + 9)
                {
                    laneNumber = KeyAssign.Beat7[channel - Channel.P2LongKeyBase + 9];
                    _channel = Channel.P1LongKeyBase;
                }
                else if (channel >= Channel.P1MineKeyBase && channel < Channel.P1MineKeyBase + 9)
                {
                    laneNumber = KeyAssign.Beat7[channel - Channel.P1MineKeyBase];
                    _channel = Channel.P1MineKeyBase;
                }
                else if (channel >= Channel.P2MineKeyBase && channel < Channel.P2MineKeyBase + 9)
                {
                    laneNumber = KeyAssign.Beat7[channel - Channel.P2MineKeyBase + 9];
                    _channel = Channel.P1MineKeyBase;
                }

                if (laneNumber == -1) continue;

                for (var j = 0; j < data.Length / 2; j++)
                {
                    var g = Gcd(j, data.Length / 2);
                    // ReSharper disable PossibleLossOfFraction
                    var position = (double)(j / g) / (data.Length / 2 / g);
                    var val = data.Substring(j * 2, 2);

                    if (!timelines.ContainsKey(position)) timelines.Add(position, new TimeLine(TempKey));

                    var timeline = timelines[position];
                    switch (_channel)
                    {
                        case Channel.LaneAutoplay:
                            if (DecodeBase36(val) != 0)
                            {
                                var bgNote = new Note(DecodeBase36(val));
                                bgNote.Bpm = currentBpm;
                                timeline.AddBackgroundNote(bgNote);
                            }

                            break;
                        case Channel.BpmChange:
                            if (val == "00") break;

                            timeline.Bpm = Convert.ToInt32(val, 16);
                            Debug.Log($"BPM_CHANGE: {timeline.Bpm}, on measure {i}");
                            timeline.BpmChange = true;
                            currentBpm = timeline.Bpm;


                            break;
                        case Channel.BgaPlay:
                            if (val == "00") break;
                            timeline.BgaBase = DecodeBase36(val);
                            break;
                        case Channel.PoorPlay:
                            if (val == "00") break;
                            timeline.BgaPoor = DecodeBase36(val);
                            break;
                        case Channel.LayerPlay:
                            if (val == "00") break;
                            timeline.BgaLayer = DecodeBase36(val);
                            break;
                        case Channel.BpmChangeExtend:
                            if (val == "00") break;

                            timeline.Bpm = bpmTable[DecodeBase36(val)];
                            Debug.Log($"BPM_CHANGE_EXTEND: {timeline.Bpm}, on measure {i}, {val}");
                            timeline.BpmChange = true;
                            currentBpm = timeline.Bpm;


                            break;
                        case Channel.Stop:
                            break;
                        case Channel.P1KeyBase:
                            var ch = DecodeBase36(val);
                            if (ch == lnobj)
                            {
                                if (lastNote[laneNumber] != null)
                                {
                                    var lastTimeline = lastNote[laneNumber].Timeline;
                                    var ln = new LongNote(lastNote[laneNumber].Wav);
                                    ln.Bpm = currentBpm;
                                    ln.Tail = new LongNote(NoWav);
                                    ln.Tail.Bpm = currentBpm;
                                    ln.Tail.Head = ln;
                                    lastTimeline.SetNote(
                                        laneNumber, ln
                                    );
                                    timeline.SetNote(
                                        laneNumber, ln.Tail
                                    );
                                }
                            }
                            else if (ch != 0)
                            {
                                var note = new Note(ch);
                                note.Bpm = currentBpm;
                                timeline.SetNote(laneNumber, note);
                                lastNote[laneNumber] = note;
                            }

                            break;
                        case Channel.P1InvisibleKeyBase:
                            var invNote = new Note(DecodeBase36(val));
                            invNote.Bpm = currentBpm;
                            timeline.SetInvisibleNote(laneNumber, invNote);

                            break;
                        case Channel.P1LongKeyBase:
                            if (val == "00") break;
                            if (lntype == 1)
                            {
                                if (lnStart[laneNumber] == null)
                                {
                                    var ln = new LongNote(DecodeBase36(val));
                                    ln.Bpm = currentBpm;
                                    timeline.SetNote(
                                        laneNumber, ln
                                    );
                                    lnStart[laneNumber] = ln;
                                }
                                else
                                {
                                    var tail = new LongNote(NoWav);
                                    tail.Bpm = currentBpm;
                                    tail.Head = lnStart[laneNumber];
                                    lnStart[laneNumber].Tail = tail;
                                    timeline.SetNote(
                                        laneNumber, tail
                                    );
                                }
                            }

                            break;
                        case Channel.P1MineKeyBase:
                            break;
                    }
                }
            }


            var lastPosition = 0.0;
            measure.Timing = timePassed;
            chart.Measures.Add(measure);
            foreach (var (position, timeline) in timelines)
            {

                // Debug.Log($"measure: {i}, position: {position}, lastPosition: {lastPosition} bpm: {bpm} scale: {measure.scale} interval: {240 * 1000 * 1000 * (position - lastPosition) * measure.scale / bpm}");
                var interval = 240 * 1000 * 1000 * (position - lastPosition) * measure.Scale / bpm;
                if (timeline.BpmChange) bpm = timeline.Bpm;
                timePassed += (long)interval;
                timeline.Timing = timePassed;
                measure.Timelines.Add(timeline);

                lastPosition = position;
            }

            if (measure.Timelines.Count == 0)
            {
                var timeline = new TimeLine(TempKey);
                timeline.Timing = timePassed;
                measure.Timelines.Add(timeline);
            }

            timePassed += (long)(240 * 1000 * 1000 * (1 - lastPosition) * measure.Scale / bpm);
            
        }
    }

    private static class Channel
    {
        public const int LaneAutoplay = 1;
        public const int SectionRate = 2;
        public const int BpmChange = 3;
        public const int BgaPlay = 4;
        public const int PoorPlay = 6;
        public const int LayerPlay = 7;
        public const int BpmChangeExtend = 8;
        public const int Stop = 9;

        public const int P1KeyBase = 1 * 36 + 1;
        public const int P2KeyBase = 2 * 36 + 1;
        public const int P1InvisibleKeyBase = 3 * 36 + 1;
        public const int P2InvisibleKeyBase = 4 * 36 + 1;
        public const int P1LongKeyBase = 5 * 36 + 1;
        public const int P2LongKeyBase = 6 * 36 + 1;
        public const int P1MineKeyBase = 13 * 36 + 1;
        public const int P2MineKeyBase = 14 * 36 + 1;
    }

    private static class KeyAssign
    {
        public static readonly int[] Beat5 =
            { 0, 1, 2, 3, 4, 5, -1, -1, -1, 6, 7, 8, 9, 10, 11, -1, -1, -1 };

        public static readonly int[] Beat7 =
            { 0, 1, 2, 3, 4, 7, -1, 5, 6, 8, 9, 10, 11, 12, 15, -1, 13, 14 };

        public static readonly int[] PopN =
            { 0, 1, 2, 3, 4, -1, -1, -1, -1, -1, 5, 6, 7, 8, -1, -1, -1, -1 };
    }

    public Chart GetChart()
    {
        return chart;
    }

    public string GetWavFileName(int id)
    {
        return wavTable[id];
    }
    
    public string GetBmpFileName(int id)
    {
        return bmpTable[id];
    }

    private int DecodeBase36(string str)
    {
        var result = 0;
        foreach (var c in str)
        {
            result *= 36;
            if (c >= '0' && c <= '9')
                result += c - '0';
            else if (c >= 'A' && c <= 'Z')
                result += c - 'A' + 10;
            else if (c >= 'a' && c <= 'z')
                result += c - 'a' + 10;
            else
                return -1; // invalid character
        }

        return result;
    }

    private void ParseHeader(string cmd, string xx, string value)
    {
        Debug.Log($"cmd: {cmd}, xx: {xx} isXXNull: {xx == null}, value: {value}");
        switch (cmd)
        {
            case "PLAYER":
                break;
            case "GENRE":
                chart.Genre = value;
                break;
            case "TITLE":
                chart.Title = value;
                break;
            case "ARTIST":
                chart.Artist = value;
                break;
            case "BPM":
                if (value == null) throw new Exception("invalid BPM value");
                if (string.IsNullOrEmpty(xx))
                    // chart initial bpm
                    chart.Bpm = double.Parse(value);
                else
                {
                    Debug.Log($"BPM: {DecodeBase36(xx)} = {double.Parse(value)}");
                    bpmTable[DecodeBase36(xx)] = double.Parse(value);
                    
                }

                break;
            case "MIDIFILE":
                break;
            case "VIDEOFILE":
                break;
            case "PLAYLEVEL":
                break;
            case "RANK":
                chart.Rank = int.Parse(value);
                break;
            case "TOTAL":
                break;
            case "VOLWAV":
                break;
            case "STAGEFILE":
                break;
            case "WAV":
                if (xx == null || value == null)
                {
                    Debug.LogWarning("WAV command requires two arguments");
                    break;
                }

                wavTable[DecodeBase36(xx)] = value;
                break;
            case "BMP":
                if (xx == null || value == null)
                {
                    Debug.LogWarning("WAV command requires two arguments");
                    break;
                }
                
                bmpTable[DecodeBase36(xx)] = value;
                if (xx == "00")
                {
                    chart.BgaPoorDefault = true;
                }
                break;
            case "RANDOM":
                break;
            case "IF":
                break;
            case "ENDIF":
                break;
            case "LNOBJ":
                lnobj = DecodeBase36(value);
                break;
            case "LNTYPE":
                lntype = int.Parse(value);
                break;

            // case "ExtChr":
            // break;
            default:
                Debug.LogWarning("Unknown command: " + cmd);
                break;
        }
    }

    private int Gcd(int a, int b)
    {
        if (b == 0) return a;
        return Gcd(b, a % b);
    }
}
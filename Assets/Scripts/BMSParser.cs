using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Networking;
using UtfUnknown;


// .bms (7key) parser

// ReSharper disable once InconsistentNaming
public class BMSParser
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
    public const int MetronomeWav = -2;


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
    private readonly int[] StopLengthTable = new int[36 * 36];
    private readonly Chart chart = new();
    private readonly string[] wavTable = new string[36 * 36];
    private readonly string[] bmpTable = new string[36 * 36];
    private int lnobj = -1;
    // ReSharper disable once IdentifierTypo
    private int lntype = 1;

    private static readonly Regex headerRegex = new(@"#([A-Za-z]+)(\d\d)? +(.+)?");
    private static readonly Regex measureRegex = new (@"#(\d{3})(\d\d):(.+)");

    public BMSParser(bool metaOnly = false)
    {
        
    }
    public void Parse(string path, bool addReadyMeasure = false, bool metaOnly = false, CancellationToken cancellationToken = default)
    {
        
        // <measure number, (channel, data)>
        Dictionary<int, List<(int channel, string data)>> measures = new();
        var bytes = File.ReadAllBytes(path);
        var result = CharsetDetector.DetectFromBytes(bytes);

        var encoding = Encoding.GetEncoding(932); // 932: Shift-JIS
        if (result?.Detected?.Encoding != null)
        {
            if (result.Detected.Confidence >= 0.875)
            {
                encoding = result.Detected.Encoding;
            }
        }
        
        var md5 = MD5.Create();
        var md5Hash = md5.ComputeHash(bytes);
        var md5Hex = BitConverter.ToString(md5Hash).Replace("-", "").ToLowerInvariant();
        var sha256 = SHA256.Create();
        var sha256Hash = sha256.ComputeHash(bytes);
        var sha256Hex = BitConverter.ToString(sha256Hash).Replace("-", "").ToLowerInvariant();
        chart.ChartMeta.MD5 = md5Hex;
        chart.ChartMeta.SHA256 = sha256Hex;
        chart.ChartMeta.BmsPath = path;
        chart.ChartMeta.Folder = Path.GetDirectoryName(path);
        
        // read bytes line by line
        using var br = new StreamReader(new MemoryStream(bytes), encoding);

        while (br.ReadLine() is { } line)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                br.Close();
                return;
            }
            if (!line.StartsWith("#")) continue;

            if (line.StartsWith("#WAV") || line.StartsWith("#BMP"))
            {
                var xx = line.Substring(4, 2);
                var value = line.Substring(7);
                ParseHeader(line.Substring(1, 3), xx, value); // TODO: refactor this shit
            }
            else if (line.StartsWith("#BPM"))
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
                var match = headerRegex.Match(line);
                if (match.Success)
                {
                    var cmd = match.Groups[1].Value;
                    var xx = match.Groups.Count > 3 ? match.Groups[2].Value : null;
                    var value = match.Groups.Count == 3 ? match.Groups[2].Value :
                        match.Groups.Count > 3 ? match.Groups[3].Value : null;

                    ParseHeader(cmd, xx, value);
                }
                else
                {
                    match = measureRegex.Match(line);
                    if (match.Success)
                    {
                        var measure = int.Parse(match.Groups[1].Value) + (addReadyMeasure ? 1 : 0);
                        var channel = DecodeBase36(match.Groups[2].Value);
                        var value = match.Groups[3].Value;
                        if (!measures.ContainsKey(measure))
                            measures.Add(measure, new List<(int channel, string data)>());
                        measures[measure].Add((channel, value));
                    }
                }
            }
        }
        br.Close();
        if (addReadyMeasure)
        {
            measures.Add(0, new List<(int channel, string data)>());
            measures[0].Add((Channel.LaneAutoplay, "********"));
        }
        var lastMeasure = measures.Keys.Max();
        

        double timePassed = 0;
        int totalNotes = 0;
        var currentBpm = chart.ChartMeta.Bpm;
        var lastNote = new Note[TempKey];
        var lnStart = new LongNote[TempKey];
        for (var i = 0; i <= lastMeasure; i++)
        {
            if(cancellationToken.IsCancellationRequested) return;
            if (!measures.ContainsKey(i))
            {
                measures.Add(i, new List<(int channel, string data)>());
            }

            // gcd (int, int)
            Measure measure = new();
            SortedDictionary<double, TimeLine> timelines = new();

            foreach (var (channel, data) in measures[i])
            {
                if(cancellationToken.IsCancellationRequested) return;
                var _channel = channel;
                if (channel == Channel.SectionRate)
                {
                    measure.Scale = double.Parse(data);
                    // Debug.Log($"measure.scale: {measure.scale}, on measure {i}");
                    continue;
                }

                var laneNumber = 0;
                if (channel is >= Channel.P1KeyBase and < Channel.P1KeyBase + 9)
                {
                    laneNumber = KeyAssign.Beat7[channel - Channel.P1KeyBase];
                    _channel = Channel.P1KeyBase;
                }
                else if (channel is >= Channel.P2KeyBase and < Channel.P2KeyBase + 9)
                {
                    laneNumber = KeyAssign.Beat7[channel - Channel.P2KeyBase + 9];
                    _channel = Channel.P1KeyBase;
                }
                else if (channel is >= Channel.P1InvisibleKeyBase and < Channel.P1InvisibleKeyBase + 9)
                {
                    laneNumber = KeyAssign.Beat7[channel - Channel.P1InvisibleKeyBase];
                    _channel = Channel.P1InvisibleKeyBase;
                }
                else if (channel is >= Channel.P2InvisibleKeyBase and < Channel.P2InvisibleKeyBase + 9)
                {
                    laneNumber = KeyAssign.Beat7[channel - Channel.P2InvisibleKeyBase + 9];
                    _channel = Channel.P1InvisibleKeyBase;
                }
                else if (channel is >= Channel.P1LongKeyBase and < Channel.P1LongKeyBase + 9)
                {
                    laneNumber = KeyAssign.Beat7[channel - Channel.P1LongKeyBase];
                    _channel = Channel.P1LongKeyBase;
                }
                else if (channel is >= Channel.P2LongKeyBase and < Channel.P2LongKeyBase + 9)
                {
                    laneNumber = KeyAssign.Beat7[channel - Channel.P2LongKeyBase + 9];
                    _channel = Channel.P1LongKeyBase;
                }
                else if (channel is >= Channel.P1MineKeyBase and < Channel.P1MineKeyBase + 9)
                {
                    laneNumber = KeyAssign.Beat7[channel - Channel.P1MineKeyBase];
                    _channel = Channel.P1MineKeyBase;
                }
                else if (channel is >= Channel.P2MineKeyBase and < Channel.P2MineKeyBase + 9)
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
                    if (new[] { Channel.LaneAutoplay, Channel.P1InvisibleKeyBase, Channel.P1KeyBase, Channel.P1LongKeyBase, Channel.P1MineKeyBase }.Contains(_channel))
                    {
                        if(metaOnly) break;
                    }
                    switch (_channel)
                    {
                        case Channel.LaneAutoplay:
                            if (val == "**")
                            {
                                timeline.AddBackgroundNote(new Note(MetronomeWav));
                                break;
                            }
                            if (DecodeBase36(val) != 0)
                            {
                                var bgNote = new Note(ToWaveId(val));
                                timeline.AddBackgroundNote(bgNote);
                            }

                            break;
                        case Channel.BpmChange:
                            if (val == "00") break;

                            timeline.Bpm = Convert.ToInt32(val, 16);
                            // Debug.Log($"BPM_CHANGE: {timeline.Bpm}, on measure {i}");
                            timeline.BpmChange = true;


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
                            // Debug.Log($"BPM_CHANGE_EXTEND: {timeline.Bpm}, on measure {i}, {val}");
                            timeline.BpmChange = true;


                            break;
                        case Channel.Stop:
                            timeline.StopLength = StopLengthTable[DecodeBase36(val)];
                            // Debug.Log($"STOP: {timeline.StopLength}, on measure {i}");
                            break;
                        case Channel.P1KeyBase:
                            var ch = DecodeBase36(val);
                            if (ch == 0) break;
                            if (ch == lnobj)
                            {
                                if (lastNote[laneNumber] != null)
                                {
                                    var lastTimeline = lastNote[laneNumber].Timeline;
                                    var ln = new LongNote(lastNote[laneNumber].Wav);

                                    ln.Tail = new LongNote(NoWav)
                                    {
                                        Head = ln
                                    };
                                    lastTimeline.SetNote(
                                        laneNumber, ln
                                    );
                                    timeline.SetNote(
                                        laneNumber, ln.Tail
                                    );
                                }
                            }
                            else
                            {
                                totalNotes++;
                                var note = new Note(ToWaveId(val));
                                timeline.SetNote(laneNumber, note);
                                lastNote[laneNumber] = note;
                            }

                            break;
                        case Channel.P1InvisibleKeyBase:
                            var invNote = new Note(ToWaveId(val));
                            timeline.SetInvisibleNote(laneNumber, invNote);

                            break;
                        case Channel.P1LongKeyBase:
                            if (val == "00") break;
                            if (lntype == 1)
                            {
                                if (lnStart[laneNumber] == null)
                                {
                                    totalNotes++;
                                    var ln = new LongNote(ToWaveId(val));
                                    timeline.SetNote(
                                        laneNumber, ln
                                    );
                                    lnStart[laneNumber] = ln;
                                }
                                else
                                {
                                    var tail = new LongNote(NoWav)
                                    {
                                        Head = lnStart[laneNumber]
                                    };
                                    lnStart[laneNumber].Tail = tail;
                                    timeline.SetNote(
                                        laneNumber, tail
                                    );
                                    lnStart[laneNumber] = null;
                                }
                            }

                            break;
                        case Channel.P1MineKeyBase:
                            break;
                    }
                }
            }
            chart.TotalNotes = totalNotes;


            var lastPosition = 0.0;
            var minBpm = chart.ChartMeta.Bpm;
            var maxBpm = chart.ChartMeta.Bpm;
            measure.Timing = (long)timePassed;
            if(!metaOnly) chart.Measures.Add(measure);
            foreach (var (position, timeline) in timelines)
            {
                if(cancellationToken.IsCancellationRequested) return;

                // Debug.Log($"measure: {i}, position: {position}, lastPosition: {lastPosition} bpm: {bpm} scale: {measure.scale} interval: {240 * 1000 * 1000 * (position - lastPosition) * measure.scale / bpm}");
                double interval = 240 * 1000 * 1000 * (position - lastPosition) * measure.Scale / currentBpm;
                timePassed += interval;
                timeline.Timing = (long)timePassed;
                if (timeline.BpmChange)
                {
                    currentBpm = timeline.Bpm;
                    minBpm = Math.Min(minBpm, timeline.Bpm);
                    maxBpm = Math.Max(maxBpm, timeline.Bpm);
                }
                else timeline.Bpm = currentBpm;

                // Debug.Log($"measure: {i}, position: {position}, lastPosition: {lastPosition}, bpm: {currentBpm} scale: {measure.Scale} interval: {interval} stop: {timeline.GetStopDuration()}");

                if(!metaOnly) measure.Timelines.Add(timeline);
                timePassed += timeline.GetStopDuration();
                
                if (timeline.Notes.Count > 0) chart.ChartMeta.PlayLength = (long)timePassed;

                lastPosition = position;
            }

            if (!metaOnly && measure.Timelines.Count == 0)
            {
                var timeline = new TimeLine(TempKey)
                {
                    Timing = (long)timePassed,
                    Bpm = currentBpm
                };
                measure.Timelines.Add(timeline);
            }

            chart.ChartMeta.TotalLength = (long)timePassed;
            chart.ChartMeta.MinBpm = minBpm;
            chart.ChartMeta.MaxBpm = maxBpm;

            timePassed += (long)(240 * 1000 * 1000 * (1 - lastPosition) * measure.Scale / currentBpm);

        }
        
        
        

    }
    
    private int ToWaveId(string wav)
    {
        var decoded = DecodeBase36(wav);
        // check range
        if(decoded is < 0 or > 36 * 36 - 1)
        {
            return NoWav;
        }
        
        return wavTable[decoded] == null ? NoWav : decoded;
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
            switch (c)
            {
                case >= '0' and <= '9':
                    result += c - '0';
                    break;
                case >= 'A' and <= 'Z':
                    result += c - 'A' + 10;
                    break;
                case >= 'a' and <= 'z':
                    result += c - 'a' + 10;
                    break;
                default:
                    return -1; // invalid character
            }
        }

        return result;
    }

    private void ParseHeader(string cmd, string xx, string value)
    {
        // Debug.Log($"cmd: {cmd}, xx: {xx} isXXNull: {xx == null}, value: {value}");
        switch (cmd.ToUpper())
        {
            case "PLAYER":
                break;
            case "GENRE":
                chart.ChartMeta.Genre = value;
                break;
            case "TITLE":
                chart.ChartMeta.Title = value;
                break;
            case "SUBTITLE":
                chart.ChartMeta.SubTitle = value;
                break;
            case "ARTIST":
                chart.ChartMeta.Artist = value;
                break;
            case "SUBARTIST":
                chart.ChartMeta.SubArtist = value;
                break;
            case "DIFFICULTY":
                chart.ChartMeta.Difficulty = int.Parse(value);
                break;
            case "BPM":
                if (value == null) throw new Exception("invalid BPM value");
                if (string.IsNullOrEmpty(xx))
                    // chart initial bpm
                    chart.ChartMeta.Bpm = double.Parse(value);
                else
                {
                    // Debug.Log($"BPM: {DecodeBase36(xx)} = {double.Parse(value)}");
                    bpmTable[DecodeBase36(xx)] = double.Parse(value);

                }

                break;
            case "STOP":
                if (value == null || xx == null || xx.Length == 0) throw new Exception("invalid arguments in #STOP");
                StopLengthTable[DecodeBase36(xx)] = int.Parse(value);
                break;
            case "MIDIFILE":
                break;
            case "VIDEOFILE":
                break;
            case "PLAYLEVEL":
                chart.ChartMeta.PlayLevel = int.Parse(value);
                break;
            case "RANK":
                chart.ChartMeta.Rank = int.Parse(value);
                break;
            case "TOTAL":
                break;
            case "VOLWAV":
                break;
            case "STAGEFILE":
                chart.ChartMeta.StageFile = value;
                break;
            case "BANNER":
                chart.ChartMeta.Banner = value;
                break;
            case "BACKBMP":
                chart.ChartMeta.BackBmp = value;
                break;
            case "PREVIEW":
                chart.ChartMeta.Preview = value;
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
                    chart.ChartMeta.BgaPoorDefault = true;
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

    private static int Gcd(int a, int b)
    {
        while (true)
        {
            if (b == 0) return a;
            var a1 = a;
            a = b;
            b = a1 % b;
        }
    }
}
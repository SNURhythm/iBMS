using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Timing = System.UInt64;
using System.Text.RegularExpressions;


public class Note
{
    public int wav;
    public TimeLine timeline;
    public int lane;
    // 레인
    // private Note nextNote;
    public Note(int wav)
    {
        this.wav = wav;
    }
}
public class LongNote : Note
{
    public LongNote end;

    public bool isEnd()
    {
        return end == null;
    }
    public LongNote(int wav) : base(wav)
    {

    }
}


public class TimeLine
{

    public Timing timing;
    public List<Note> backgroundNotes;
    public List<Note> notes;
    public List<Note> invisibleNotes;
    public bool bpmChange = false;
    public double bpm = 0.0;
    public double scroll = 1.0;
    public double pauseLength = 0;
    public TimeLine(int lanes)
    {
        notes = Enumerable.Repeat<Note>(null, lanes).ToList();
        invisibleNotes = Enumerable.Repeat<Note>(null, lanes).ToList();
        backgroundNotes = new List<Note>();
    }
    public TimeLine SetNote(int lane, Note note)
    {
        notes[lane] = note;
        note.lane = lane;
        note.timeline = this;
        return this;
    }
    public TimeLine SetInvisibleNote(int lane, Note note)
    {
        invisibleNotes[lane] = note;
        note.lane = lane;
        note.timeline = this;
        return this;
    }
    public TimeLine AddBackgroundNote(Note note)
    {
        backgroundNotes.Add(note);
        return this;
    }
}
// .bms (7key) parser

public class Measure
{
    public double scale = 1; // 0 ~ 1
    public List<TimeLine> timelines;


}

public class Chart
{
    public string title;
    public string genre;
    public string artist;

    public double bpm;

    public List<TimeLine> timelines = new List<TimeLine>();
}

public class BMSParser : Parser
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
    const int TEMP_KEY = 8;
    public const int NO_WAV = -1;
    public const int LANE_AUTOPLAY = 1;
    public const int SECTION_RATE = 2;
    public const int BPM_CHANGE = 3;
    public const int BGA_PLAY = 4;
    public const int POOR_PLAY = 6;
    public const int LAYER_PLAY = 7;
    public const int BPM_CHANGE_EXTEND = 8;
    public const int STOP = 9;

    public const int P1_KEY_BASE = 1 * 36 + 1;
    public const int P2_KEY_BASE = 2 * 36 + 1;
    public const int P1_INVISIBLE_KEY_BASE = 3 * 36 + 1;
    public const int P2_INVISIBLE_KEY_BASE = 4 * 36 + 1;
    public const int P1_LONG_KEY_BASE = 5 * 36 + 1;
    public const int P2_LONG_KEY_BASE = 6 * 36 + 1;
    public const int P1_MINE_KEY_BASE = 13 * 36 + 1;
    public const int P2_MINE_KEY_BASE = 14 * 36 + 1;
    private static readonly int[] CHANNELASSIGN_BEAT5 = { 0, 1, 2, 3, 4, 5, -1, -1, -1, 6, 7, 8, 9, 10, 11, -1, -1, -1 };
    private static readonly int[] CHANNELASSIGN_BEAT7 = { 0, 1, 2, 3, 4, 7, -1, 5, 6, 8, 9, 10, 11, 12, 15, -1, 13, 14 };
    private static readonly int[] CHANNELASSIGN_POPN = { 0, 1, 2, 3, 4, -1, -1, -1, -1, -1, 5, 6, 7, 8, -1, -1, -1, -1 };

    public const int SCROLL = 1020;
    private int lnobj;
    public static readonly int[] NOTE_CHANNELS = {
        P1_KEY_BASE,
        P2_KEY_BASE,
        P1_INVISIBLE_KEY_BASE,
        P2_INVISIBLE_KEY_BASE,
        P1_LONG_KEY_BASE,
        P2_LONG_KEY_BASE,
        P1_MINE_KEY_BASE,
        P2_MINE_KEY_BASE
    };
    public string[] wavTable = new string[36 * 36];
    private double[] bpmTable = new double[36 * 36];

    private Dictionary<int, Dictionary<double, TimeLine>> sections;

    public Chart chart = new Chart();

    private int decodeBase36(string str)
    {
        int result = 0;
        foreach (char c in str)
        {
            result *= 36;
            if (c >= '0' && c <= '9')
            {
                result += c - '0';
            }
            else if (c >= 'A' && c <= 'Z')
            {
                result += c - 'A' + 10;
            }
            else if (c >= 'a' && c <= 'z')
            {
                result += c - 'a' + 10;
            }
            else
            {
                return -1; // invalid character
            }
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
                chart.genre = value;
                break;
            case "TITLE":
                chart.title = value;
                break;
            case "ARTIST":
                chart.artist = value;
                break;
            case "BPM":
                if (value == null)
                {
                    throw new System.Exception("invalid BPM value");
                }
                if (xx == null || xx.Length == 0)
                { // chart initial bpm
                    chart.bpm = double.Parse(value);
                }
                else
                {
                    bpmTable[decodeBase36(xx)] = double.Parse(value);
                }

                break;
            case "MIDIFILE":
                break;
            case "VIDEOFILE":
                break;
            case "PLAYLEVEL":
                break;
            case "RANK":
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
                wavTable[decodeBase36(xx)] = value;

                break;
            case "BMP":
                break;
            case "RANDOM":
                break;
            case "IF":
                break;
            case "ENDIF":
                break;
            case "LNOBJ":
                lnobj = decodeBase36(value);
                break;
            // case "ExtChr":
            // break;
            default:
                Debug.LogWarning("Unknown command: " + cmd);
                break;
        }
    }

    public void Parse(string path)
    {

        // <measure number, (channel, data)>
        Dictionary<int, List<(int channel, string data)>> measures = new();

        // read line by line
        var br = new System.IO.StreamReader(path);
        string line;
        while ((line = br.ReadLine()) != null)
        {
            if (!line.StartsWith("#")) continue;
            var measureRegex = new Regex(@"#(\d{3})(\d\d):(.+)");
            var match = measureRegex.Match(line);
            if (match.Success)
            {
                int measure = int.Parse(match.Groups[1].Value);
                int channel = decodeBase36(match.Groups[2].Value);
                var value = match.Groups[3].Value;
                if (!measures.ContainsKey(measure))
                {
                    measures.Add(measure, new List<(int channel, string data)>());
                }
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
                }
                else
                {
                    var regex = new Regex(@"#([A-Za-z]+)(\d\d)? +(.+)?");
                    match = regex.Match(line);
                    if (match.Success)
                    {
                        var cmd = match.Groups[1].Value;
                        var xx = match.Groups.Count > 3 ? match.Groups[2].Value : null;
                        var value = match.Groups.Count == 3 ? match.Groups[2].Value : (match.Groups.Count > 3 ? match.Groups[3].Value : null);

                        ParseHeader(cmd, xx, value);
                    }
                }
            }
        }

        int lastMeasure = measures.Keys.Max();

        Timing timePassed = 0;
        double totalMeasureScale = 0;
        double bpm = chart.bpm;
        Note[] lastNote = new Note[10];

        for (int i = 0; i <= lastMeasure; i++)
        {
            if (measures.ContainsKey(i))
            {
                // gcd (int, int)

                Measure measure = new();
                SortedDictionary<double, TimeLine> timelines = new();

                foreach ((int channel, string data) in measures[i])
                {
                    int _channel = channel;
                    if (channel == SECTION_RATE)
                    {
                        measure.scale = double.Parse(data);
                        Debug.Log($"measure.scale: {measure.scale}, on measure {i}");
                        continue;
                    }

                    int laneNumber = 0;
                    if (channel >= P1_KEY_BASE && channel < P1_KEY_BASE + 9)
                    {
                        laneNumber = CHANNELASSIGN_BEAT7[channel - P1_KEY_BASE];
                        _channel = P1_KEY_BASE;
                    }
                    else if (channel >= P2_KEY_BASE && channel < P2_KEY_BASE + 9)
                    {
                        laneNumber = CHANNELASSIGN_BEAT7[channel - P2_KEY_BASE + 9];
                        _channel = P1_KEY_BASE;
                    }
                    else if (channel >= P1_INVISIBLE_KEY_BASE && channel < P1_INVISIBLE_KEY_BASE + 9)
                    {
                        laneNumber = CHANNELASSIGN_BEAT7[channel - P1_INVISIBLE_KEY_BASE];
                        _channel = P1_INVISIBLE_KEY_BASE;
                    }
                    else if (channel >= P2_INVISIBLE_KEY_BASE && channel < P2_INVISIBLE_KEY_BASE + 9)
                    {
                        laneNumber = CHANNELASSIGN_BEAT7[channel - P2_INVISIBLE_KEY_BASE + 9];
                        _channel = P1_INVISIBLE_KEY_BASE;
                    }
                    else if (channel >= P1_LONG_KEY_BASE && channel < P1_LONG_KEY_BASE + 9)
                    {
                        laneNumber = CHANNELASSIGN_BEAT7[channel - P1_LONG_KEY_BASE];
                        _channel = P1_LONG_KEY_BASE;
                    }
                    else if (channel >= P2_LONG_KEY_BASE && channel < P2_LONG_KEY_BASE + 9)
                    {
                        laneNumber = CHANNELASSIGN_BEAT7[channel - P2_LONG_KEY_BASE + 9];
                        _channel = P1_LONG_KEY_BASE;
                    }
                    else if (channel >= P1_MINE_KEY_BASE && channel < P1_MINE_KEY_BASE + 9)
                    {
                        laneNumber = CHANNELASSIGN_BEAT7[channel - P1_MINE_KEY_BASE];
                        _channel = P1_MINE_KEY_BASE;
                    }
                    else if (channel >= P2_MINE_KEY_BASE && channel < P2_MINE_KEY_BASE + 9)
                    {
                        laneNumber = CHANNELASSIGN_BEAT7[channel - P2_MINE_KEY_BASE + 9];
                        _channel = P1_MINE_KEY_BASE;
                    }

                    if (laneNumber == -1) continue;

                    for (int j = 0; j < data.Length / 2; j++)
                    {
                        int g = gcd(j, data.Length / 2);
                        double position = ((double)(j / g)) / (data.Length / 2 / g);
                        string val = data.Substring(j * 2, 2);

                        if (!timelines.ContainsKey(position))
                        {
                            timelines.Add(position, new TimeLine(TEMP_KEY));
                        }

                        TimeLine timeline = timelines[position];
                        switch (_channel)
                        {
                            case LANE_AUTOPLAY:
                                if (decodeBase36(val) != 0)
                                    timeline.AddBackgroundNote(new Note(decodeBase36(val)));
                                break;
                            case BPM_CHANGE:
                                timeline.bpm = System.Convert.ToInt32(val, 16);
                                Debug.Log($"BPM_CHANGE: {timeline.bpm}, on measure {i}");
                                timeline.bpmChange = true;
                                break;
                            case BGA_PLAY:
                                break;
                            case POOR_PLAY:
                                break;
                            case LAYER_PLAY:
                                break;
                            case BPM_CHANGE_EXTEND:
                                timeline.bpm = bpmTable[decodeBase36(val)];
                                Debug.Log($"BPM_CHANGE_EXTEND: {timeline.bpm}, on measure {i}");
                                timeline.bpmChange = true;
                                break;
                            case STOP:
                                break;
                            case P1_KEY_BASE:
                                int ch = decodeBase36(val);
                                if (ch == lnobj)
                                {
                                    if (lastNote[laneNumber] != null)
                                    {
                                        TimeLine lastTimeline = lastNote[laneNumber].timeline;
                                        LongNote ln = new LongNote(lastNote[laneNumber].wav);
                                        ln.end = new LongNote(NO_WAV);
                                        lastTimeline.SetNote(
                                            laneNumber, ln
                                        );
                                        timeline.SetNote(
                                            laneNumber, ln.end
                                        );
                                    }

                                }
                                else if (ch != 0)
                                {
                                    Note note = new Note(ch);
                                    timeline.SetNote(laneNumber, note);
                                    lastNote[laneNumber] = note;
                                }
                                break;
                            case P1_INVISIBLE_KEY_BASE:

                                timeline.SetInvisibleNote(laneNumber, new Note(decodeBase36(val)));

                                break;
                            case P1_LONG_KEY_BASE:
                                break;
                            case P1_MINE_KEY_BASE:
                                break;



                        }

                    }



                }


                double lastPosition = 0.0;
                foreach ((double position, TimeLine timeline) in timelines)
                {
                    if (timeline.bpmChange) bpm = timeline.bpm;
                    Debug.Log($"measure: {i}, position: {position}, lastPosition: {lastPosition} bpm: {bpm} scale: {measure.scale} interval: {240 * 1000 * 1000 * (position - lastPosition) * measure.scale / bpm}");
                    double interval = 240 * 1000 * 1000 * (position - lastPosition) * measure.scale / bpm;


                    timePassed += (Timing)interval;
                    timeline.timing = timePassed;
                    lastPosition = position;
                    chart.timelines.Add(timeline);
                }

                timePassed += (Timing)(240 * 1000 * 1000 * (1 - lastPosition) * measure.scale / bpm);

                totalMeasureScale += measure.scale;

            }

        }


    }

    private int gcd(int a, int b)
    {
        if (b == 0) return a;
        return gcd(b, a % b);
    }


    private bool MatchKeyword(string line, string keyword)
    {
        if (line.Length <= keyword.Length) return false; // '=' is intended to exclude '#'
        return line.ToLower().Substring(0, keyword.Length) == keyword.ToLower();
    }
}

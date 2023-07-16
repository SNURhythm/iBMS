using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Timing = System.UInt64;
using System.Text.RegularExpressions;

public abstract class ChartObject
{
    public Timing timing;
}

public class Note : ChartObject
{
    private int wav;
    // 레인
    // private Note nextNote;
}
public class LongNote : Note
{
    private LongNote end;

    public bool isEnd()
    {
        return end == null;
    }
}


public class BpmChange : ChartObject
{

}

public class TimeLine
{
    private Timing timing;
    private List<Note> notes;
    private double bpm;
    private double scroll = 1.0;
    private double pause_length;

    /*
    
    #03901:00
    #03901:4N000000003S4Q52
    #03901:58
    #03901:006Z006Y006Z006Y
    #03901:00
    #03901:007G007H
    #03901:00
    #03901:9N00009O00009P009O00009N00009L00
    #03903:CA
    #03911:7V000025
    #03912:000000000000002T
    #03913:2O
    #03914:00350000002T0000
    #03915:0032
    #03916:71
    #03918:0000003A00000000
    #03919:003B0000
    
    */

    /*
        변속
        일시정지

        변박 -> 마디에
    */

}
// .bms (5key) parser

public class Measure
{
    private double length;
    private List<TimeLine> timelines;


}

public class Chart
{
    public string title;
    public string genre;
    public string artist;

    public double bpm;

    public List<Measure> measures;
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
    private string[] wavTable = new string[36 * 36];
    private double[] bpmTable = new double[36 * 36];

    private Dictionary<int, Dictionary<double, TimeLine>> sections;

    private Chart chart = new Chart();

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
                if (xx == null)
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
            // case "ExtChr":
            // break;
            default:
                Debug.LogWarning("Unknown command: " + cmd);
                break;
        }
    }

    private void ParseMeasure(string measure, string channel, string value)
    {

        /*
        
        */

        if (value.Length % 2 != 0)
        {
            throw new System.Exception("invalid value length");
        }
        int measureNumber = int.Parse(measure);
        var section = sections[measureNumber];
        int beatLength = value.Length / 2;
        int channelNumber = decodeBase36(channel);
        switch (channelNumber)
        {
            case -1:
                break;
            case LANE_AUTOPLAY:
                break;
            case SECTION_RATE:
                break;
            case BPM_CHANGE:
                break;
            case BGA_PLAY:
                break;
            case POOR_PLAY:
                break;
            case LAYER_PLAY:
                break;
            case BPM_CHANGE_EXTEND:
                break;
            case STOP:
                break;

        }
        int laneNumber;
        if (channelNumber >= P1_KEY_BASE && channelNumber < P1_KEY_BASE + 9)
        {

        }


    }
    public void Parse(string path)
    {
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
                var measure = match.Groups[1].Value;
                var channel = match.Groups[2].Value;
                var value = match.Groups[3].Value;

                // TODO: 마디별로 한 번에 처리하도록 수정
                ParseMeasure(measure, channel, value);
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



    private bool MatchKeyword(string line, string keyword)
    {
        if (line.Length <= keyword.Length) return false; // '=' is intended to exclude '#'
        return line.ToLower().Substring(0, keyword.Length) == keyword.ToLower();


    }
}

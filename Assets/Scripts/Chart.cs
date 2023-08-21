using System.Collections.Generic;

public class ChartMeta
{
    public string SHA256;
    public string MD5;
    public string BmsPath;
    public string Folder;
    public string Artist = "";
    public string SubArtist = "";
    public double Bpm;
    public string Genre = "";
    public string Title = "";
    public string SubTitle = "";
    public int Rank = 3;
    public double Total = 100;
    public long PlayLength = 0; // Timing of the last playable note, in microseconds
    public long TotalLength = 0; // Timing of the last timeline(including background note, bga change note, invisible note, ...), in microseconds
    public string Banner;
    public string StageFile;
    public string BackBmp;
    public string Preview;
    public bool BgaPoorDefault = false;
    public int Difficulty;
    public double PlayLevel = 3;
    public double MinBpm;
    public double MaxBpm;
    public int Player = 1;
    public int KeyMode = 5;
    public int TotalNotes;
    public int TotalLongNotes;
    public int TotalScratchNotes;
    public int TotalBackSpinNotes;
    public int LnMode = 0; // 0: user decides, 1: LN, 2: CN, 3: HCN
}

public class Chart
{
    public readonly List<Measure> Measures = new();


    private readonly ChartMeta chartMeta = new ChartMeta();

    public ChartMeta Meta
    {
        get { return chartMeta; }
    }
}
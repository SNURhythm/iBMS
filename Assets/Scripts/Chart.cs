using System.Collections.Generic;

public class Chart
{
    public string SHA256;
    public string MD5;
    public string BmsPath;
    public string Folder;
    public string Artist;
    public string SubArtist;

    public double Bpm;
    public string Genre;
    
    public readonly List<Measure> Measures = new();
    public string Title;
    public string SubTitle;
    public int Rank = 3;
    public long PlayLength = 0; // Timing of the last playable note, in microseconds
    public long TotalLength = 0; // Timing of the last timeline(including background note, bga change note, invisible note, ...), in microseconds
    public string Banner;
    public string StageFile;
    public string BackBmp;
    public string Preview;
    public bool BgaPoorDefault = false;

    public int TotalNotes;
    public int Difficulty;
    public int PlayLevel;
    public double MinBpm;
    public double MaxBpm;
}
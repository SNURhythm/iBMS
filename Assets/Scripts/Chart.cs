using System.Collections.Generic;

public class Chart
{
    public string Artist;

    public double Bpm;
    public string Genre;

    public readonly List<TimeLine> Timelines = new();
    public string Title;
}
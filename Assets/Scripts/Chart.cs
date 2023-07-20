using System.Collections.Generic;

public class Chart
{
    public string Artist;

    public double Bpm;
    public string Genre;
    
    public readonly List<Measure> Measures = new();
    public string Title;
    public int Rank = 3;
}
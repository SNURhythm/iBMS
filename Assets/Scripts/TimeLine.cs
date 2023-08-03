using System.Collections.Generic;
using System.Linq;

public class TimeLine
{
    public readonly List<Note> BackgroundNotes;
    public double Bpm = 0.0;
    public bool BpmChange = false;
    public bool BpmChangeApplied = false;
    public readonly List<Note> InvisibleNotes;
    public readonly List<Note> Notes;
    public int BgaBase = -1;
    public int BgaLayer = -1;
    public int BgaPoor = -1;
    public readonly List<Note> LandmineNotes;
    public double StopLength = 0.0;
    public double Scroll = 1.0;

    public long Timing;
    public double Pos;

    public TimeLine(int lanes)
    {
        Notes = Enumerable.Repeat<Note>(null, lanes).ToList();
        InvisibleNotes = Enumerable.Repeat<Note>(null, lanes).ToList();
        LandmineNotes = Enumerable.Repeat<Note>(null, lanes).ToList();
        BackgroundNotes = new List<Note>();
    }

    public TimeLine SetNote(int lane, Note note)
    {
        Notes[lane] = note;
        note.Lane = lane;
        note.Timeline = this;
        return this;
    }

    public TimeLine SetInvisibleNote(int lane, Note note)
    {
        InvisibleNotes[lane] = note;
        note.Lane = lane;
        note.Timeline = this;
        return this;
    }
    
    public TimeLine SetLandmineNote(int lane, Note note)
    {
        LandmineNotes[lane] = note;
        note.Lane = lane;
        note.Timeline = this;
        return this;
    }

    public TimeLine AddBackgroundNote(Note note)
    {
        BackgroundNotes.Add(note);
        note.Timeline = this;
        return this;
    }
    
    
    public double GetStopDuration()
    {
        return 240 * 1000 * 1000 / 192 * StopLength / Bpm;
    }
}
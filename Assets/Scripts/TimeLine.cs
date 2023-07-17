using System.Collections.Generic;
using System.Linq;

public class TimeLine
{
    public List<Note> backgroundNotes;
    public double bpm = 0.0;
    public bool bpmChange = false;
    public List<Note> invisibleNotes;
    public List<Note> notes;
    public double pauseLength = 0;
    public double scroll = 1.0;

    public ulong timing;

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
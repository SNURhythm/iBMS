
using System;
using System.Collections.Generic;
using UnityEngine;

public class BMSRenderer: MonoBehaviour
{
    private Chart chart;
    private readonly float laneWidth = 3.0f;
    private readonly float laneMargin = 0.1f;
    private readonly float noteHeight = 1.0f;
    private readonly float judgeLinePosition = 0.0f;
    private readonly float judgeLineHeight = 1.0f;
    private readonly Dictionary<Note, GameObject> noteObjects = new Dictionary<Note, GameObject>();
    private GameObject notePrefab;
    void Init(Chart chart)
    {
        this.chart = chart;
    }
    private Note testnote = new Note(0);
    private void Start()
    {
        notePrefab = new GameObject();
        notePrefab.SetActive(false);
        notePrefab.transform.position = new Vector3(0, 0, 0);
        notePrefab.transform.localScale = new Vector3(laneWidth, noteHeight, 0);
        notePrefab.AddComponent<SpriteRenderer>();
        notePrefab.GetComponent<SpriteRenderer>().color = Color.red;
        notePrefab.GetComponent<SpriteRenderer>().sprite = Resources.Load<Sprite>("Sprites/Square");
        notePrefab.name = "Note";

        testnote.Lane = 0;
        
        DrawNote(testnote, 1);
        var testnote2 = new Note(0);
        testnote2.Lane = 1;
        
        DrawNote(testnote2, 2);
        // after 1 sec, move testnote
        Invoke(nameof(MoveTestNote), 1);
    }
    void MoveTestNote()
    {

        DrawNote(testnote, 2);
    }
    

    void Draw(ulong currentTime)
    {
        var measures = chart.Measures;
        foreach (var measure in measures)
        {
            DrawMeasureLine(measure.Timing - currentTime);
            foreach (var timeLine in measure.Timelines)
            {
                var offset = timeLine.Timing - currentTime;

                if (IsOverUpperBound(offset)) break;
                foreach (var note in timeLine.Notes)
                {
                    if (note == null) continue;
                    if (isUnderLowerBound(offset))
                    {
                        DestroyNote(note);
                        return;
                    }
                    if (note is LongNote longNote)
                    {
                        if (!longNote.IsTail()) DrawLongNote(longNote, offset, longNote.Tail.Timeline.Timing - currentTime);
                    }
                    else
                    {
                        DrawNote(note, offset);
                    }
                }
                // is there a case we should draw background notes?
                // foreach (var bgNote in timeLine.BackgroundNotes)
                // {
                //     if (bgNote == null) continue;
                // }
                //
                // foreach (var invNote in timeLine.InvisibleNotes)
                // {   
                //     if (invNote == null) continue;
                // }

            }
        }
    }

    void DestroyNote(Note note)
    {
        Destroy(noteObjects[note]);
        noteObjects.Remove(note);

    }
    void DrawMeasureLine(ulong offset)
    {
        var top = OffsetToTop(offset);
        
    }
    void DrawNote(Note note, ulong offset)
    {
        if(note.IsPlayed) return;
        var left = LaneToLeft(note.Lane);
        var top = OffsetToTop(offset);

        // draw note
        if(noteObjects.ContainsKey(note))
        {
            var noteObject = noteObjects[note];
            noteObject.transform.position = new Vector3(left, top, 0);
            noteObject.SetActive(true);
        }
        else
        {
            var noteObject = Instantiate(notePrefab);
            noteObject.transform.position = new Vector3(left, top, 0);
            noteObjects.Add(note, noteObject);
            noteObject.SetActive(true);
        }

        
        

        
    }

    void DrawLongNote(LongNote head, ulong startOffset, ulong endOffset)
    {
        if(head.Tail.IsPlayed) return;
        var left = LaneToLeft(head.Lane);
    }

    float LaneToLeft(int lane)
    {
        return lane * (laneWidth + laneMargin);
    }

    float OffsetToTop(ulong offset)
    {
        // TODO: Implement
        return judgeLinePosition + offset;
    }
    bool IsOverUpperBound(ulong offset)
    {
        // TODO: Implement
        return true;
    }

    bool isUnderLowerBound(ulong offset)
    {
        // TODO: Implement
        return true;
    }
}

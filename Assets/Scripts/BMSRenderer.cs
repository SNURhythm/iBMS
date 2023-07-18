
using System;
using System.Collections.Generic;
using UnityEngine;

public class BMSRenderer: MonoBehaviour
{
    private Chart chart;
    private readonly float laneWidth = 3.0f;
    private readonly float laneMargin = 0f;
    private readonly float noteHeight = 1.0f;
    private readonly float judgeLinePosition = 0.0f;
    private readonly float judgeLineHeight = 1.0f;
    private readonly Dictionary<Note, GameObject> noteObjects = new Dictionary<Note, GameObject>();
    private GameObject notePrefab;
    
    private int passedTimelineCount = 0;
    private int passedMeasureCount = 0;
    private List<LongNote> orphanLongNotes = new List<LongNote>();
    private double currentBpm = 0;
    private double startBpm = 0;
    private long lastDrawTime = 0;
    private long deltaTime = 0;
    private long greenNumber = 300;
    private long whiteNumber = 0;
    private float hiSpeed = 1;
    private float spawnPosition = 500;
    public void Init(Chart chart)
    {
        this.chart = chart;
        this.currentBpm = chart.Bpm;
        this.startBpm = chart.Bpm;
    }

    private void Start()
    {
        notePrefab = new GameObject();
        notePrefab.SetActive(false);
        notePrefab.transform.position = new Vector3(0, 0, 0);
        notePrefab.transform.localScale = new Vector3(laneWidth, noteHeight, 0);
        notePrefab.AddComponent<SpriteRenderer>();
        notePrefab.GetComponent<SpriteRenderer>().color = Color.red;
        notePrefab.GetComponent<SpriteRenderer>().sprite = Resources.Load<Sprite>("Sprites/Square");
        notePrefab.GetComponent<SpriteRenderer>().sortingLayerName = "Note";
        notePrefab.name = "Note";

    }


    public void Draw(long currentTime)
    {
        deltaTime = currentTime - lastDrawTime;
        foreach (var orphanLongNote in orphanLongNotes)
        {
            DrawLongNote(orphanLongNote, orphanLongNote.Timeline.Timing - currentTime, orphanLongNote.Tail.Timeline.Timing - currentTime, true);
        }
        var measures = chart.Measures;
        for (int i=passedMeasureCount; i<measures.Count; i++)
        {
            var isFirstMeasure = i == passedMeasureCount;
            var measure = measures[i];
            DrawMeasureLine(measure.Timing - currentTime);
            for (int j=isFirstMeasure?passedTimelineCount:0; j<measure.Timelines.Count; j++)
            {
                var timeLine = measure.Timelines[j];
                var offset = timeLine.Timing - currentTime;
                if (offset <= 0)
                {
                    if (timeLine.BpmChange)
                    {
                        currentBpm = timeLine.Bpm;
                    }
                }
                if (IsOverUpperBound(offset)) break;
                var shouldDestroy = IsUnderLowerBound(offset);
                if (shouldDestroy && isFirstMeasure)
                {
                    passedTimelineCount++;
                    Debug.Log($"Destroying timeline, passedTimelineCount: {passedTimelineCount}, total timeline count: {measure.Timelines.Count}");
                }

                foreach (var note in timeLine.Notes)
                {
                    if (note == null) continue;
                    if (shouldDestroy)
                    {
                        if (note is LongNote ln)
                        {
                            if (ln.IsTail())
                            {
                                if (orphanLongNotes.Contains(ln.Head)) orphanLongNotes.Remove(ln.Head);
                            }
                            else
                            {
                                // add orphan tail
                                orphanLongNotes.Add(ln);
                            }
                        }
                        DestroyNote(note);
                        continue;
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
            if(passedTimelineCount == measure.Timelines.Count && isFirstMeasure)
            {
                passedTimelineCount = 0;
                passedMeasureCount++;
                Debug.Log($"Skipping measure since all {measure.Timelines.Count} timelines are passed, passedMeasureCount: {passedMeasureCount}");
            }
        }
        lastDrawTime = currentTime;
    }

    void DestroyNote(Note note)
    {
        Destroy(noteObjects[note]);
        noteObjects.Remove(note);

    }
    void DrawMeasureLine(long offset)
    {
        var top = OffsetToTop(offset);
        
    }
    void DrawNote(Note note, long offset)
    {
        if(note.IsPlayed) return;
        var left = LaneToLeft(note.Lane);

        // draw note
        if(noteObjects.ContainsKey(note))
        {
            var noteObject = noteObjects[note];
            noteObject.transform.position = new Vector3(left, OffsetToTop(offset), 0);
            noteObject.SetActive(true);
        }
        else
        {
            var noteObject = Instantiate(notePrefab);
            noteObject.transform.position = new Vector3(left, OffsetToTop(offset), 0);
            noteObjects.Add(note, noteObject);
            noteObject.SetActive(true);
        }
    }

    void DrawLongNote(LongNote head, long startOffset, long endOffset, bool tailOnly = false)
    {
        if(head.Tail.IsPlayed) return;
        var tail = head.Tail;
        var left = LaneToLeft(head.Lane);
        var startTop = OffsetToTop(startOffset);
        var endTop = OffsetToTop(endOffset);
        var height = endTop - startTop;
        // draw start note, end note, and draw a bar between them
        // we should make tail note have a height, not head, since head will be disappeared before tail is played
        if (!tailOnly)
        {
            if (noteObjects.ContainsKey(head))
            {
                var noteObject = noteObjects[head];
                noteObject.transform.position = new Vector3(left, startTop, 0);
                noteObject.transform.localScale = new Vector3(laneWidth, noteHeight, 0);
                noteObject.SetActive(true);
            }
            else
            {
                var noteObject = Instantiate(notePrefab);
                noteObject.transform.position = new Vector3(left, startTop, 0);
                noteObject.transform.localScale = new Vector3(laneWidth, noteHeight, 0);
                noteObjects.Add(head, noteObject);
                noteObject.SetActive(true);
            }
        }

        if(noteObjects.ContainsKey(tail))
        {
            var noteObject = noteObjects[tail];
            noteObject.transform.position = new Vector3(left, (startTop + endTop) / 2, 0);
            noteObject.SetActive(true);
        }
        else
        {
            var noteObject = Instantiate(notePrefab);
            noteObject.transform.position = new Vector3(left, (startTop + endTop) / 2, 0);
            noteObject.transform.localScale = new Vector3(laneWidth, height, 0);
            noteObjects.Add(tail, noteObject);
            noteObject.SetActive(true);
        }
    }

    float LaneToLeft(int lane)
    {
        return lane * (laneWidth + laneMargin);
    }

    float OffsetToTop(long offset)
    {
        // TODO: Implement
        return (float)(judgeLinePosition + offset*0.00007f);
    }
    
    
    bool IsOverUpperBound(long offset)
    {
        return OffsetToTop(offset) > spawnPosition;
    }

    bool IsUnderLowerBound(long offset)
    {
        return offset < -300000; // TODO: account for late-miss window
    }
}

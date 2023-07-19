
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
    private readonly Dictionary<Measure, GameObject> measureLineObjects = new Dictionary<Measure, GameObject>();
    private GameObject squarePrefab;
    
    private int passedTimelineCount = 0;
    private int passedMeasureCount = 0;
    private List<LongNote> orphanLongNotes = new List<LongNote>();
    private double currentBpm = 0;
    private double startBpm = 0;
    private long lastDrawTime = 0;
    private long deltaTime = 0;
    private (double timing, double pos) lastBpmChange = (0.0, 0.0);
    private long greenNumber = 300;
    private long whiteNumber = 0;
    private float hiSpeed = 1;
    private float spawnPosition = 500;
    private float despawnPosition = -30;

    private Color[] noteColors =
        { Color.white, Color.cyan, Color.white, Color.cyan, Color.white, Color.cyan, Color.white, Color.red }; // TODO: make customizable

    public void Init(Chart chart)
    {
        this.chart = chart;
        this.currentBpm = chart.Bpm;
        this.startBpm = chart.Bpm;
        this.lastBpmChange = (0.0, 0.0);

        double bpm = chart.Bpm;
        (double timing, double pos) lastBpmChange = (0.0, 0.0);

        foreach (Measure measure in chart.Measures)
        {
            measure.Pos = lastBpmChange.pos + (measure.Timing - lastBpmChange.timing) * bpm / chart.Bpm;
            foreach (TimeLine timeline in measure.Timelines)
            {
                timeline.Pos = lastBpmChange.pos + (timeline.Timing - lastBpmChange.timing) * bpm / chart.Bpm;
                if (timeline.BpmChange)
                {
                    bpm = timeline.Bpm;
                    lastBpmChange.timing = timeline.Timing;
                    lastBpmChange.pos = timeline.Pos;
                }
            }
        }
    }

    private void Start()
    {
        squarePrefab = new GameObject();
        squarePrefab.SetActive(false);
        squarePrefab.transform.position = new Vector3(0, 0, 0);
        squarePrefab.transform.localScale = new Vector3(laneWidth, noteHeight, 0);
        squarePrefab.AddComponent<SpriteRenderer>();
        squarePrefab.GetComponent<SpriteRenderer>().color = Color.red;
        squarePrefab.GetComponent<SpriteRenderer>().sprite = Resources.Load<Sprite>("Sprites/Square");
        squarePrefab.GetComponent<SpriteRenderer>().sortingLayerName = "Note";
        squarePrefab.name = "Note";

    }


    public void Draw(long currentTime)
    {
        deltaTime = currentTime - lastDrawTime;
        double currentPos = lastBpmChange.pos + (currentTime - lastBpmChange.timing) * currentBpm / chart.Bpm;

        var measures = chart.Measures;
        for (int i = passedMeasureCount; i < measures.Count; i++)
        {
            var isFirstMeasure = i == passedMeasureCount;
            var measure = measures[i];
            

            for (int j = isFirstMeasure ? passedTimelineCount : 0; j < measure.Timelines.Count; j++)
            {
                var timeline = measure.Timelines[j];
                var offset = timeline.Pos - currentPos;
                if (offset <= 0 && timeline.BpmChange && !timeline.BpmChangeApplied)
                {
                    currentBpm = timeline.Bpm;
                    lastBpmChange = (timeline.Timing, timeline.Pos);
                    currentPos = lastBpmChange.pos + (currentTime - lastBpmChange.timing) * currentBpm / chart.Bpm;
                    offset = timeline.Pos - currentPos;
                    timeline.BpmChangeApplied = true;
                }
                if(j==0)DrawMeasureLine(measure, measure.Pos - currentPos);

                if (IsOverUpperBound(offset)) break;
                var shouldDestroy = IsUnderLowerBound(offset);
                if (shouldDestroy && isFirstMeasure)
                {
                    passedTimelineCount++;
                    Debug.Log($"Destroying timeline, passedTimelineCount: {passedTimelineCount}, total timeline count: {measure.Timelines.Count}");
                }

                foreach (var note in timeline.Notes)
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
                        if (!longNote.IsTail()) DrawLongNote(longNote, offset, longNote.Tail.Timeline.Pos - currentPos);
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
                DestroyMeasureLine(measure);
                Debug.Log($"Skipping measure since all {measure.Timelines.Count} timelines are passed, passedMeasureCount: {passedMeasureCount}");
            }
        }

        foreach (var orphanLongNote in orphanLongNotes)
        {
            DrawLongNote(orphanLongNote, orphanLongNote.Timeline.Pos - currentPos, orphanLongNote.Tail.Timeline.Pos - currentPos, true);
        }
        
        lastDrawTime = currentTime;
    }

    void DestroyNote(Note note)
    {
        if (noteObjects.ContainsKey(note))
        {
            Destroy(noteObjects[note]);
            noteObjects.Remove(note);
        }

    }
    
    void DestroyMeasureLine(Measure measure)
    {
        if (measureLineObjects.ContainsKey(measure))
        {
            Destroy(measureLineObjects[measure]);
            measureLineObjects.Remove(measure);
        }
    }
    void DrawMeasureLine(Measure measure, double offset)
    {
        var top = OffsetToTop(offset);

        if (measureLineObjects.ContainsKey(measure))
        {
            measureLineObjects[measure].transform.position = new Vector3(laneWidth * 7/2, top-noteHeight/2, 0);
            measureLineObjects[measure].SetActive(true);
        }
        else
        {
            var measureLineObject = Instantiate(squarePrefab);
            measureLineObject.transform.position = new Vector3(laneWidth * 7/2, top-noteHeight/2, 0);
            measureLineObject.transform.localScale = new Vector3(laneWidth * 8, 0.2f, 0);
            var spriteRenderer = measureLineObject.GetComponent<SpriteRenderer>();
            spriteRenderer.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);

            spriteRenderer.sortingLayerName = "MeasureLine";
            measureLineObject.name = "MeasureLine";
            measureLineObjects.Add(measure, measureLineObject);
            measureLineObject.SetActive(true);
        }
        
    }
    void DrawNote(Note note, double offset)
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
            var noteObject = Instantiate(squarePrefab);
            noteObject.transform.position = new Vector3(left, OffsetToTop(offset), 0);
            noteObject.GetComponent<SpriteRenderer>().color = noteColors[note.Lane];
            noteObjects.Add(note, noteObject);
            noteObject.SetActive(true);
        }
    }

    void DrawLongNote(LongNote head, double startOffset, double endOffset, bool tailOnly = false)
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
            }
            else
            {
                var noteObject = Instantiate(squarePrefab);
                noteObject.transform.position = new Vector3(left, startTop, 0);
                noteObject.transform.localScale = new Vector3(laneWidth, noteHeight, 0);
                noteObject.GetComponent<SpriteRenderer>().color = noteColors[head.Lane];
                noteObjects.Add(head, noteObject);
                noteObject.SetActive(true);
            }
        }

        if(noteObjects.ContainsKey(tail))
        {
            var noteObject = noteObjects[tail];
            noteObject.transform.position = new Vector3(left, (startTop + endTop) / 2, 0);
        }
        else
        {
            var noteObject = Instantiate(squarePrefab);
            noteObject.transform.position = new Vector3(left, (startTop + endTop) / 2, 0);
            noteObject.transform.localScale = new Vector3(laneWidth, height, 0);
            noteObject.GetComponent<SpriteRenderer>().color = noteColors[tail.Lane];
            noteObjects.Add(tail, noteObject);
            noteObject.SetActive(true);
        }
    }

    float LaneToLeft(int lane)
    {
        return lane * (laneWidth + laneMargin);
    }

    float OffsetToTop(double offset)
    {
        // TODO: Implement
        return (float)(judgeLinePosition + offset*0.00007f);
    }
    
    
    bool IsOverUpperBound(double offset)
    {
        return OffsetToTop(offset) > spawnPosition;
    }

    bool IsUnderLowerBound(double offset)
    {
        return OffsetToTop(offset) < despawnPosition;
    }
}

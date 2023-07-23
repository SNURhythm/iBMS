
using System;
using System.Collections.Generic;
using UnityEngine;

public class BMSRenderer : MonoBehaviour
{
    public GameObject LaneArea;
    public GameObject NoteArea;
    private Chart chart;
    private readonly float laneWidth = 3.0f;
    private readonly float laneMargin = 0f;
    private readonly float noteHeight = 1.0f;
    private readonly float judgeLinePosition = 0.0f;
    private readonly float judgeLineHeight = 1.0f;

    private float judgeLineBottom => judgeLinePosition - judgeLineHeight / 2;

    private readonly Dictionary<Note, GameObject> noteObjects = new();
    private readonly Dictionary<Measure, GameObject> measureLineObjects = new();
    private GameObject squarePrefab;
    private Dictionary<GameObject, Queue<GameObject>> instancePool = new();
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
        { Color.white, Color.blue, Color.white, Color.blue, Color.white, Color.blue, Color.white, Color.red }; // TODO: make customizable

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
        squarePrefab.name = "Square";
        spawnPosition = NoteArea.transform.localScale.y;
        var lane = new GameObject();
        lane.SetActive(false);

        lane.transform.localScale = new Vector3(0.1f, NoteArea.transform.localScale.y, 0);
        lane.AddComponent<SpriteRenderer>();
        lane.GetComponent<SpriteRenderer>().color = new Color(0.5f, 0.5f, 0.5f, 0.2f);
        lane.GetComponent<SpriteRenderer>().sprite = Resources.Load<Sprite>("Sprites/Square");
        for (int i = 0; i < 8; i++)
        {
            if (i == 6) continue;
            lane.name = "Lane";
            var newLane = Instantiate(lane, LaneArea.transform);
            newLane.SetActive(true);
            newLane.transform.localPosition = new Vector3(LaneToLeft(i) + laneWidth / 2 - 0.05f, NoteArea.transform.localScale.y / 2 - judgeLineHeight / 2, 0);
        }

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

                if (IsOverUpperBound(offset)) break;

                if (j == 0) DrawMeasureLine(measure, measure.Pos - currentPos);
                var shouldDestroyTimeline = IsUnderLowerBound(offset);
                if (shouldDestroyTimeline && isFirstMeasure)
                {
                    passedTimelineCount++;
                    // Debug.Log($"Destroying timeline, passedTimelineCount: {passedTimelineCount}, total timeline count: {measure.Timelines.Count}");
                }

                foreach (var note in timeline.Notes)
                {
                    if (note == null) continue;
                    if (note.IsDead) continue;
                    if (shouldDestroyTimeline || note.IsPlayed)
                    {
                        if (note is LongNote ln)
                        {
                            if (ln.IsTail)
                            {
                                orphanLongNotes.Remove(ln.Head);
                            }
                            else
                            {
                                // add orphan long note's head
                                orphanLongNotes.Add(ln);
                            }
                        }
                        note.IsDead = true;
                        DestroyNote(note);
                        continue;
                    }

                    if (note is LongNote longNote)
                    {
                        if (!longNote.IsTail) DrawLongNote(longNote, offset, longNote.Tail.Timeline.Pos - currentPos);
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
            if (passedTimelineCount == measure.Timelines.Count && isFirstMeasure)
            {
                passedTimelineCount = 0;
                passedMeasureCount++;
                DestroyMeasureLine(measure);
                // Debug.Log($"Skipping measure since all {measure.Timelines.Count} timelines are passed, passedMeasureCount: {passedMeasureCount}");
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
        if (!noteObjects.ContainsKey(note)) return; // there is a case that note is not even instantiated (e.g. extremely fast bpm change, fps drop)
        RecycleInstance(squarePrefab, noteObjects[note]);
        noteObjects.Remove(note);
    }

    void RecycleInstance(GameObject prefab, GameObject instance)
    {
        instance.SetActive(false);
        if (!instancePool.ContainsKey(squarePrefab))
        {
            instancePool.Add(prefab, new Queue<GameObject>());
        }
        instancePool[squarePrefab].Enqueue(instance);
    }
    GameObject GetInstance(GameObject prefab)
    {
        if (instancePool.ContainsKey(prefab) && instancePool[prefab].Count > 0)
        {
            var instance = instancePool[prefab].Dequeue();
            instance.SetActive(true);
            return instance;
        }
        else
        {
            var instance = Instantiate(prefab, LaneArea.transform);
            instance.SetActive(true);
            return instance;
        }
    }
    void DestroyMeasureLine(Measure measure)
    {
        if (!measureLineObjects.ContainsKey(measure)) return;
        RecycleInstance(squarePrefab, measureLineObjects[measure]);
        measureLineObjects.Remove(measure);

    }
    void DrawMeasureLine(Measure measure, double offset)
    {
        var top = OffsetToTop(offset);

        if (measureLineObjects.ContainsKey(measure))
        {
            measureLineObjects[measure].transform.localPosition = new Vector3(laneWidth * 7 / 2, top - noteHeight / 2, 0);
            measureLineObjects[measure].SetActive(true);
        }
        else
        {
            var measureLineObject = GetInstance(squarePrefab);
            measureLineObject.transform.localPosition = new Vector3(laneWidth * 7 / 2, top - noteHeight / 2, 0);
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
        if (note.IsPlayed) return;
        var left = LaneToLeft(note.Lane);

        // draw note
        if (noteObjects.ContainsKey(note))
        {
            var noteObject = noteObjects[note];
            noteObject.transform.localPosition = new Vector3(left, OffsetToTop(offset), 0);
            noteObject.SetActive(true);
        }
        else
        {
            var noteObject = GetInstance(squarePrefab);
            noteObject.transform.localPosition = new Vector3(left, OffsetToTop(offset), 0);
            noteObject.transform.localScale = new Vector3(laneWidth, noteHeight, 0);
            var spriteRenderer = noteObject.GetComponent<SpriteRenderer>();
            spriteRenderer.color = noteColors[note.Lane];
            spriteRenderer.sortingLayerName = "Note";
            noteObject.name = "Note";
            noteObjects.Add(note, noteObject);
            noteObject.SetActive(true);
        }
    }

    void DrawLongNote(LongNote head, double startOffset, double endOffset, bool tailOnly = false)
    {
        if (head.Tail.IsPlayed) return;
        var tail = head.Tail;
        var left = LaneToLeft(head.Lane);
        var startTop = OffsetToTop(startOffset);

        var endTop = OffsetToTop(endOffset);
        if (head.IsHolding)
        {
            // TODO: Good start, early release -> tail should keep its height when it's released

            startTop = Math.Min(judgeLineBottom, endTop - noteHeight);
        }
        var height = endTop - startTop;
        // draw start note, end note, and draw a bar between them
        // we should make tail note have a height, not head, since head will be disappeared before tail is played
        if (!tailOnly)
        {
            if (noteObjects.ContainsKey(head))
            {
                var noteObject = noteObjects[head];
                noteObject.transform.localPosition = new Vector3(left, startTop, 0);
            }
            else
            {
                var noteObject = GetInstance(squarePrefab);
                noteObject.transform.localPosition = new Vector3(left, startTop, 0);
                noteObject.transform.localScale = new Vector3(laneWidth, noteHeight, 0);
                var spriteRenderer = noteObject.GetComponent<SpriteRenderer>();
                spriteRenderer.color = noteColors[head.Lane];
                spriteRenderer.sortingLayerName = "Note";
                noteObject.name = "LongNoteHead";

                noteObjects.Add(head, noteObject);
                noteObject.SetActive(true);
            }
        }

        if (noteObjects.ContainsKey(tail))
        {
            var noteObject = noteObjects[tail];
            noteObject.transform.localPosition = new Vector3(left, (startTop + endTop) / 2, 0);
            noteObject.transform.localScale = new Vector3(laneWidth, height, 0);
        }
        else
        {
            var noteObject = GetInstance(squarePrefab);
            noteObject.transform.localPosition = new Vector3(left, (startTop + endTop) / 2, 0);
            noteObject.transform.localScale = new Vector3(laneWidth, height, 0);
            var spriteRenderer = noteObject.GetComponent<SpriteRenderer>();
            spriteRenderer.color = noteColors[tail.Lane];
            spriteRenderer.sortingLayerName = "Note";
            noteObject.name = "LongNoteTail";
            noteObjects.Add(tail, noteObject);
            noteObject.SetActive(true);
        }
    }

    float LaneToLeft(int lane)
    {
        return (lane + 1) % 8 * (laneWidth + laneMargin);
    }

    float OffsetToTop(double offset)
    {
        // TODO: Implement
        return (float)(judgeLinePosition + offset * 0.00007f);
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

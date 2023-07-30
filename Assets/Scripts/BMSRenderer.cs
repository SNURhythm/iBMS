
using System;
using System.Collections.Generic;
using UnityEngine;

public class BMSRenderer : MonoBehaviour
{
    public GameObject LaneArea;
    public GameObject NoteArea;
    public GameObject ParticlePrefab;
    private LaneBeamEffect[] laneBeamEffects;
    private GameObject[] lineBeams;
    private GameObject[] keyBombs;
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

    private double startBpm = 0;
    private long lastDrawTime = 0;
    private long deltaTime = 0;

    private TimeLine lastTimeline;

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
        this.startBpm = chart.Bpm;
        this.lastTimeline = chart.Measures[0].Timelines[0];

        TimeLine lastTimeline = chart.Measures[0].Timelines[0];
        lastTimeline.Pos = 0.0;

        foreach (Measure measure in chart.Measures)
        {
            measure.Pos = lastTimeline.Pos + (measure.Timing - (lastTimeline.Timing + lastTimeline.GetStopDuration())) * lastTimeline.Bpm / chart.Bpm;
            foreach (TimeLine timeline in measure.Timelines)
            {
                timeline.Pos = lastTimeline.Pos + (timeline.Timing - (lastTimeline.Timing + lastTimeline.GetStopDuration())) * lastTimeline.Bpm / chart.Bpm;
                lastTimeline = timeline;
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
        var laneDivider = new GameObject();
        laneDivider.SetActive(false);

        laneDivider.transform.localScale = new Vector3(0.1f, NoteArea.transform.localScale.y, 0);
        var laneDividerSr = laneDivider.AddComponent<SpriteRenderer>();
        laneDividerSr.color = new Color(0.5f, 0.5f, 0.5f, 0.2f);
        laneDividerSr.sprite = Resources.Load<Sprite>("Sprites/Square");
        
        var laneBeam = new GameObject();

        laneBeam.SetActive(false);
        var laneBeamSr = laneBeam.AddComponent<SpriteRenderer>();
        laneBeamSr.sprite = Resources.Load<Sprite>("Sprites/GradientLine");
        laneBeamSr.sortingLayerName = "LaneBeam";
        laneBeamSr.drawMode = SpriteDrawMode.Sliced;
        laneBeamSr.size = new Vector2(laneWidth, NoteArea.transform.localScale.y);


        keyBombs = new GameObject[8];
        laneBeamEffects = new LaneBeamEffect[8];
        for (int i = 0; i < 8; i++)
        {
            if (i != 6)
            {
                laneDivider.name = "LaneDivider";
                var newLaneDivider = Instantiate(laneDivider, LaneArea.transform);
                newLaneDivider.SetActive(true);
                newLaneDivider.transform.localPosition = new Vector3(LaneToLeft(i) + laneWidth / 2 - 0.05f,
                    NoteArea.transform.localScale.y / 2 - judgeLineHeight / 2, 0);
            }

            // keyBomb particle
            var keyBomb = Instantiate(ParticlePrefab, LaneArea.transform);
            keyBomb.transform.localPosition = new Vector3(LaneToLeft(i), judgeLinePosition, 0);
            keyBomb.transform.localScale = new Vector3(laneWidth, laneWidth, 1);
            keyBomb.SetActive(false);
            keyBomb.GetComponent<ParticleSystemRenderer>().sortingLayerName = "KeyBomb";
            var ps = keyBomb.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.1f;
            main.simulationSpeed = 2;
            keyBombs[i] = keyBomb;
            
            var newLaneBeam = Instantiate(laneBeam, LaneArea.transform);
            newLaneBeam.SetActive(true);
            newLaneBeam.transform.localPosition = new Vector3(LaneToLeft(i), NoteArea.transform.localScale.y / 2 - judgeLineHeight / 2, 0);
            newLaneBeam.GetComponent<SpriteRenderer>().color = noteColors[i];
            laneBeamEffects[i] = new LaneBeamEffect(newLaneBeam, 0.2f);



        }

    }

    public void PlayKeyBomb(int laneNumber, Judgement judgement)
    {
        var keyBomb = keyBombs[laneNumber];
        keyBomb.SetActive(true);
        var ps = keyBomb.GetComponent<ParticleSystem>();

        ps.Play();

    }
    
    public void StartLaneBeamEffect(int laneNumber)
    {
        laneBeamEffects[laneNumber].StartEffect(0, true);
    }
    
    public void ResumeLaneBeamEffect(int laneNumber)
    {
        laneBeamEffects[laneNumber].ResumeEffect();
    }
    
    

    public void Draw(long currentTime)
    {
        for (int i = 0; i < 8; i++)
        {
            laneBeamEffects[i].Tick();
        }
        deltaTime = currentTime - lastDrawTime;

        var measures = chart.Measures;

        // update lastTimeline
        for (int i = passedMeasureCount; i < measures.Count; i++)
        {
            var isFirstMeasure = i == passedMeasureCount;
            var measure = measures[i];
            if (measure.Timing > currentTime) break;


            for (int j = isFirstMeasure ? passedTimelineCount : 0; j < measure.Timelines.Count; j++)
            {
                var timeline = measure.Timelines[j];
                if (timeline.Timing > currentTime) break;
                lastTimeline = timeline;
            }
        }

        // draw notes
        double currentPos = (currentTime < lastTimeline.Timing + lastTimeline.GetStopDuration()) ? lastTimeline.Pos
                              : lastTimeline.Pos + (currentTime - (lastTimeline.Timing + lastTimeline.GetStopDuration())) * lastTimeline.Bpm / chart.Bpm;

        // Debug.Log($"lastTimeline.Timing: {lastTimeline.Timing}, lastTimtline.Pos: {lastTimeline.Pos}, currentTime: {currentTime}, currentPos: {currentPos}");

        for (int i = passedMeasureCount; i < measures.Count; i++)
        {
            var isFirstMeasure = i == passedMeasureCount;
            var measure = measures[i];

            for (int j = isFirstMeasure ? passedTimelineCount : 0; j < measure.Timelines.Count; j++)
            {
                var timeline = measure.Timelines[j];

                var offset = timeline.Pos - currentPos;

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
                        var dontDestroy = false;
                        if (note is LongNote ln)
                        {
                            if (ln.IsTail)
                            {
                                if (shouldDestroyTimeline)
                                {
                                    orphanLongNotes.Remove(ln.Head);
                                }
                                else
                                {
                                    dontDestroy = true;
                                }
                            }
                            else
                            {
                                // add orphan long note's head
                                orphanLongNotes.Add(ln);
                            }
                        }

                        if (!dontDestroy)
                        {
                            note.IsDead = true;
                            DestroyNote(note);
                        }

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
        //if (head.Tail.IsPlayed) return;
        var tail = head.Tail;
        var left = LaneToLeft(head.Lane);
        var startTop = OffsetToTop(startOffset);

        var endTop = OffsetToTop(endOffset);
        
        if (head.IsPlayed)
        {
            if (endTop < judgeLineBottom)
            {
                DestroyNote(tail);
                return;
            }

            startTop = Math.Max(judgeLineBottom, startTop);
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
        var alpha = 1.0f;
        if (head.IsHolding)
        {
            alpha = 1.0f;
        }
        else
        {
            alpha = head.IsPlayed ? 0.2f : 0.5f;
        }
        if (noteObjects.ContainsKey(tail))
        {
            var noteObject = noteObjects[tail];
            noteObject.transform.localPosition = new Vector3(left, (startTop + endTop) / 2, 0);
            noteObject.transform.localScale = new Vector3(laneWidth, height, 0);
            var color = noteColors[tail.Lane];
            noteObject.GetComponent<SpriteRenderer>().color = new Color(color.r, color.g, color.b, alpha);
        }
        else
        {
            var noteObject = GetInstance(squarePrefab);
            noteObject.transform.localPosition = new Vector3(left, (startTop + endTop) / 2, 0);
            noteObject.transform.localScale = new Vector3(laneWidth, height, 0);
            var spriteRenderer = noteObject.GetComponent<SpriteRenderer>();
            var color = noteColors[tail.Lane];

            spriteRenderer.color = new Color(color.r, color.g, color.b, alpha);
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using FMOD;
using FMODUnity;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

public class RhythmControl : MonoBehaviour
{
    private const int MaxChannels = 1024;
    private const long TimeMargin = 5000000; // 5 seconds
    private Queue<(ulong dspclock, int wav)> soundQueue;


    private Sound[] wavSounds;


    private ChannelGroup channelGroup;

    private bool isPlaying;
#if UNITY_EDITOR
    private PauseState lastPauseState = PauseState.Unpaused;

#endif
    private Sound music;
    private BMSParser parser;
    private BMSRenderer renderer;
    private BGAPlayer bgaPlayer;
    private Judge judge;

    private ulong startDSPClock;
    private FMOD.System system;
    private int sameDspClockCount = 0;
    private long lastDspTime = 0;
    private long maxCompensatedDspTime = 0;
    private void Awake()
    {
        Application.targetFrameRate = 120;
        parser = new BMSParser();
    }

    // Start is called before the first frame update
    private void Start()
    {
#if UNITY_EDITOR
        EditorApplication.pauseStateChanged += OnPauseStateChanged;
        lastPauseState = EditorApplication.isPaused ? PauseState.Paused : PauseState.Unpaused;
#endif
        RuntimeManager.StudioSystem.release();
        RuntimeManager.CoreSystem.release();
        Factory.System_Create(out system); // TODO: make system singleton
        system.setSoftwareChannels(256);
        // set buffer size
        // var result = _system.setDSPBufferSize(256, 2);
        // if (result != FMOD.RESULT.OK) Debug.Log($"setDSPBufferSize failed. {result}");
        system.init(MaxChannels, INITFLAGS.NORMAL, IntPtr.Zero);
        soundQueue = new();
        wavSounds = new Sound[36 * 36];
        bgaPlayer = new();
        renderer = GetComponent<BMSRenderer>();
        LoadGame();
        Debug.Log("Load Complete");
        channelGroup.setPaused(true);
    }

    private long GetCurrentDspTimeMicro()
    {
        channelGroup.getDSPClock(out var dspClock, out var parentClock);
        system.getSoftwareFormat(out var sampleRate, out _, out _);
        return (long)((double)dspClock / sampleRate * 1000000 - (double)startDSPClock / sampleRate * 1000000);
    }

    private long GetCompensatedDspTimeMicro()
    {

        return GetCurrentDspTimeMicro() + sameDspClockCount * (long)(Time.fixedDeltaTime * 1000000);
    }
    // Update is called once per frame
    private void Update()
    {
        if (!isPlaying) return;
        var currentDspTime = GetCurrentDspTimeMicro();
        maxCompensatedDspTime = Math.Max(maxCompensatedDspTime, GetCompensatedDspTimeMicro());
        var time = Math.Max(currentDspTime, maxCompensatedDspTime);
        renderer.Draw(time);

        if (time >= parser.GetChart().PlayLength + TimeMargin)
        {
            isPlaying = false;
            UnloadGame();
            // go back to chart select
            Debug.Log("Game Over");
            // load scene
            SceneManager.LoadScene("ChartSelectScene");
        }
    }

    private void FixedUpdate()
    {
        // channelGroup.getDSPClock(out var dspclock, out var parentclock);
        system.update();
        // system.getSoftwareFormat(out var samplerate, out _, out _);
        // WriteTxt(Application.streamingAssetsPath + "/log.log", "FixedUpdate: " + (double)dspclock / samplerate * 1000 + ", " + Time.time);

        // Debug.Log("dspclock: " + (double)dspclock / samplerate * 1000);
        system.getChannelsPlaying(out var playingChannels, out var realChannels);
        // Debug.Log("playing channels: " + playingChannels + ", real channels: " + realChannels);
        var availableChannels = MaxChannels - playingChannels;
        if (availableChannels > 0 && soundQueue.Count > 0)
        {
            for (var i = 0; i < availableChannels; i++)
            {
                if (soundQueue.Count == 0) break;
                var (startDSP, wav) = soundQueue.Dequeue();
                // Debug.Log("Playing queued sound: " + wav);
                system.playSound(wavSounds[wav], channelGroup, true, out var channel);
                channel.setDelay(startDSP, 0);
                channel.setPaused(false);
            }
        }

        if (isPlaying)
        {
            var currentDspTime = GetCurrentDspTimeMicro();
            if (lastDspTime == currentDspTime)
            {
                sameDspClockCount++;
            }
            else
            {
                sameDspClockCount = 0;
                lastDspTime = currentDspTime;
            }

            CheckPassedTimeline(currentDspTime);
            // AutoPlay(GetCompensatedDspTimeMicro());
            // bgaPlayer.Update(GetCompensatedDspTimeMicro());
        }


    }
    int passedMeasureCount = 0;
    int passedTimelineCount = 0;
    private void CheckPassedTimeline(long time)
    {
        var measures = parser.GetChart().Measures;
        for (int i = passedMeasureCount; i < measures.Count; i++)
        {
            var measure = measures[i];
            for (int j = passedTimelineCount; j < measure.Timelines.Count; j++)
            {
                var timeline = measure.Timelines[j];
                if (timeline.Timing < time - 200000)
                {
                    passedTimelineCount++;
                    // make remaining notes POOR
                    foreach (var note in timeline.Notes)
                    {
                        if (note == null) continue;
                        if (!note.IsPlayed)
                        {

                            if (note is LongNote { IsTail: false } ln)
                            {
                                ln.MissPress(time);
                            }
                            else
                            {
                                combo = 0;
                                latestJudgement = Judgement.KPOOR;
                            }
                        }
                    }
                }
                else break;
            }
            if (passedTimelineCount == measure.Timelines.Count)
            {
                passedTimelineCount = 0;
                passedMeasureCount++;
            }
            else break;
        }
    }

    private int autoplayedTimelines = 0;
    private int autoplayedMeasures = 0;
    private long testRandomOffsetRange = 0;
    private float randomOffset = -1;
    private int combo = 0;
    private Judgement latestJudgement;
    private void AutoPlay(long currentTime)
    {
        if (randomOffset < 0)
        {
            randomOffset = UnityEngine.Random.Range(0, testRandomOffsetRange);
        }
        var measures = parser.GetChart().Measures;
        for (int i = autoplayedMeasures; i < measures.Count; i++)
        {
            var measure = measures[i];
            for (int j = autoplayedTimelines; j < measure.Timelines.Count; j++)
            {

                var timeline = measure.Timelines[j];
                // if (i==autoplayedMeasures && j==autoplayedTimelines) Debug.Log($"offset: {randomOffset}, timing: {timeline.Timing}, current: {currentTime}");
                if (Math.Abs(timeline.Timing - currentTime) < randomOffset || timeline.Timing - currentTime < -testRandomOffsetRange)
                {
                    randomOffset = UnityEngine.Random.Range(0, testRandomOffsetRange);
                    // mimic press
                    foreach (var note in timeline.Notes)
                    {
                        if (note == null) continue;

                        var judgeResult = judge.JudgeNow(note, currentTime);
                        if (judgeResult.IsNotePlayed)
                        {
                            if (note is LongNote longNote)
                            {
                                if (longNote.IsTail)
                                {
                                    longNote.Release(currentTime);
                                }
                                else
                                {
                                    longNote.Press(currentTime);
                                }
                            }
                            else
                            {
                                note.Press(currentTime);
                            }
                        }
                        else
                        {
                            if (note is LongNote { IsTail: true } longNote)
                            {
                                longNote.MissRelease(currentTime);
                            }
                        }
                        // Debug.Log($"Judgement: {judgeResult.Judgement}, Diff: {judgeResult.Diff}");
                        latestJudgement = judgeResult.Judgement;
                        if (judgeResult.ShouldComboBreak)
                        {
                            combo = 0;
                        }
                        else
                        {
                            if (note is not LongNote or LongNote { IsTail: true })
                                combo++;
                        }
                        // Debug.Log($"Combo: {combo}");
                    }

                    autoplayedTimelines = j + 1;
                    // Debug.Log("Autoplayed: " + autoplayedTimelines);
                    if (autoplayedTimelines == measure.Timelines.Count)
                    {
                        autoplayedTimelines = 0;
                        autoplayedMeasures = i + 1;
                    }
                    i = measures.Count;
                    break;
                }
            }
        }
    }

    private void OnPressLane(int lane)
    {
        Debug.Log("Press: " + lane);
        var measures = parser.GetChart().Measures;
        for (int i = 0; i < measures.Count; i++)
        {
            var measure = measures[i];
            for (int j = 0; j < measure.Timelines.Count; j++)
            {
                var timeline = measure.Timelines[j];
                if (timeline.Timing < GetCompensatedDspTimeMicro() - 200000) continue;
                var note = timeline.Notes[lane];
                if (note == null) continue;
                var judgeResult = judge.JudgeNow(note, GetCompensatedDspTimeMicro());
                if (judgeResult.Judgement != Judgement.NONE)
                {
                    latestJudgement = judgeResult.Judgement;
                    if (note is LongNote longNote)
                    {
                        if (!longNote.IsTail)
                        {
                            longNote.Press(GetCompensatedDspTimeMicro());
                        }
                    }
                    else
                    {
                        note.Press(GetCompensatedDspTimeMicro());
                    }

                    if (judgeResult.ShouldComboBreak) combo = 0;
                    else if (judgeResult.Judgement != Judgement.KPOOR) combo++;
                }
                return;
            }
        }
    }

    private void OnReleaseLane(int lane)
    {
        Debug.Log("Release: " + lane);
        var measures = parser.GetChart().Measures;
        for (int i = 0; i < measures.Count; i++)
        {
            var measure = measures[i];
            for (int j = 0; j < measure.Timelines.Count; j++)
            {
                var timeline = measure.Timelines[j];
                if (timeline.Timing < GetCompensatedDspTimeMicro() - 200000) continue;
                var note = timeline.Notes[lane];
                if (note == null) continue;
                var judgeResult = judge.JudgeNow(note, GetCompensatedDspTimeMicro());
                if (judgeResult.Judgement != Judgement.NONE)
                {
                    latestJudgement = judgeResult.Judgement;
                    if (note is LongNote longNote)
                    {
                        if (longNote.IsTail)
                        {
                            longNote.Release(GetCompensatedDspTimeMicro());
                            if (judgeResult.ShouldComboBreak) combo = 0;
                            else if (judgeResult.Judgement != Judgement.KPOOR) combo++;
                        }
                    }


                }
                else
                {
                    if (note is LongNote longNote)
                    {
                        if (longNote.IsTail)
                        {
                            longNote.MissRelease(GetCompensatedDspTimeMicro());
                        }
                    }
                }
                return;
            }
        }
    }

    private void OnDisable()
    {
        system.release();
    }

    private void ScheduleSound(double timing, int wav)
    {
        system.getChannelsPlaying(out var playingChannels, out var realChannels);
        var startDSP = startDSPClock + MsToDSP(timing / 1000);
        if (playingChannels >= MaxChannels)
        {
            soundQueue.Enqueue((startDSP, wav)); // Too many channels playing, queue the sound
            return;
        }

        system.playSound(wavSounds[wav], channelGroup, true, out var channel);
        // this.wav[wav].getLength(out uint length, FMOD.TIMEUNIT.MS);
        // var lengthDSP = MsToDSP((double)length);

        // _channel.setMode(FMOD.MODE.VIRTUAL_PLAYFROMSTART);
        if (startDSP == 0) startDSP = 1;
        channel.setDelay(startDSP, 0);
        channel.setPaused(false);
    }

    private void StartMusic()
    {
        if (isPlaying) return;
        parser.GetChart().Measures.ForEach(measure => measure.Timelines.ForEach(timeline =>
        {
            if (timeline.BgaBase != -1)
            {
                bgaPlayer.Schedule(timeline.BgaBase, timeline.Timing);
            }
        }));
        channelGroup.getDSPClock(out startDSPClock, out _);
        channelGroup.setPaused(false);
        Debug.Log("Play");
        parser.GetChart().Measures.ForEach(measure => measure.Timelines.ForEach(timeline =>
        {
            timeline.Notes.ForEach(note =>
            {
                if (note == null || note.Wav == BMSParser.NoWav) return;
                // Debug.Log(note.wav + "wav");
                // Debug.Log("NoteTiming: " + timeline.timing / 1000);
                ScheduleSound(timeline.Timing, note.Wav);
            });
            timeline.BackgroundNotes.ForEach(note =>
            {
                if (note == null || note.Wav == BMSParser.NoWav) return;
                // Debug.Log("BgnWav: " + note.Wav+" Timing: "+timeline.Timing);
                ScheduleSound(timeline.Timing, note.Wav);
            });
            timeline.InvisibleNotes.ForEach(note =>
            {
                if (note == null || note.Wav == BMSParser.NoWav) return;
                // Debug.Log("InvNoteTiming: " + timeline.timing / 1000);

                ScheduleSound(timeline.Timing, note.Wav);
            });
        }));
        isPlaying = true;
    }

    private async void LoadGame()
    {


        // set defaultDecodeBufferSize
        var advancedSettings = new ADVANCEDSETTINGS
        {
            defaultDecodeBufferSize = 32
        };
        var result = system.setAdvancedSettings(ref advancedSettings);
        if (result != RESULT.OK) Debug.Log($"setAdvancedSettings failed. {result}");

        result = system.getDSPBufferSize(out var blockSize, out var numBlocks);
        result = system.getSoftwareFormat(out var frequency, out _, out _);
        system.getMasterChannelGroup(out channelGroup);
        var bgas = new List<(int id, string path)>();
        await Task.Run(() =>
        {

            var basePath = Path.GetDirectoryName(GameManager.Instance.BmsPath);
            parser.Parse(GameManager.Instance.BmsPath);
            for (var i = 0; i < 36 * 36; i++)
            {
                var wavFileName = parser.GetWavFileName(i);
                var bmpFileName = parser.GetBmpFileName(i);
                if (wavFileName != null)
                {
                    byte[] wavBytes = GetWavBytes(basePath + "/" + wavFileName);
                    if (wavBytes == null)
                    {
                        Debug.LogWarning("wavBytes is null:" + parser.GetWavFileName(i));
                    }
                    else
                    {
                        var createSoundExInfo = new CREATESOUNDEXINFO
                        {
                            length = (uint)wavBytes.Length,
                            cbsize = Marshal.SizeOf(typeof(CREATESOUNDEXINFO))
                        };
                        result = system.createSound(wavBytes, MODE.OPENMEMORY | MODE.CREATESAMPLE | MODE.ACCURATETIME,
                            ref createSoundExInfo, out wavSounds[i]);
                        wavSounds[i].setLoopCount(0);
                        // _system.playSound(wav[i], _channelGroup, true, out channel);

                        if (result != RESULT.OK) Debug.Log($"createSound failed wav{i}. {result}");
                    }
                }

                if (bmpFileName != null)
                {
                    bgas.Add((i, Application.streamingAssetsPath + basePath + bmpFileName));
                }
            }
            var ms = blockSize * 1000.0f / frequency;

            Debug.Log($"Mixer blockSize        = {ms} ms");
            Debug.Log($"Mixer Total bufferSize = {ms * numBlocks} ms");
            Debug.Log($"Mixer Average Latency  = {ms * (numBlocks - 1.5f)} ms");
        });
        foreach (var (id, path) in bgas)
        {
            // We should load bga in main thread because it adds VideoPlayer on main camera.
            // And it is OK to call this method synchronously because VideoPlayer loads video asynchronously.
            bgaPlayer.Load(id, path);
        }
        renderer.Init(parser.GetChart());
        judge = new Judge(parser.GetChart().Rank);
        Debug.Log($"PlayLength: {parser.GetChart().PlayLength}, TotalLength: {parser.GetChart().TotalLength}");
        if (bgaPlayer.TotalPlayers != bgaPlayer.LoadedPlayers)
        {
            bgaPlayer.OnAllPlayersLoaded += (sender, args) => Invoke(nameof(StartMusic), 0.0f);
        }
        else
        {
            Invoke(nameof(StartMusic), 0.0f);
        }
    }

    private void UnloadGame()
    {
        // release all sounds
        foreach (var sound in wavSounds)
        {
            sound.release();
        }

        // release system
        system.release();
        Resources.UnloadUnusedAssets();
        System.GC.Collect();
    }

    private byte[] AndroidTryGetWav(string path)
    {

        var www = UnityWebRequest.Get(path);
        www.SendWebRequest();
        while (!www.isDone)
        {
        }

        if (www.isNetworkError || www.isHttpError) return null;
        return www.downloadHandler.data;

    }
    private byte[] GetWavBytes(string path)
    {
        // we can't trust given extension, so we should try all supported extensions (mp3, ogg, wav, flac)
        var splitIndex = path.LastIndexOf('.');
        var pathWithoutExtension = path.Substring(0, splitIndex);
        var extensions = new[] { ".mp3", ".ogg", ".wav", ".flac" };

        if (Application.platform == RuntimePlatform.Android)
        {
            var temp = AndroidTryGetWav(path);
            if (temp != null) return temp;
            foreach (var extension in extensions)
            {
                var newPath = pathWithoutExtension + extension;
                temp = AndroidTryGetWav(newPath);
                if (temp != null) return temp;
            }
        }

        if (File.Exists(path))
        {
            return File.ReadAllBytes(path);
        }

        foreach (var extension in extensions)
        {
            var newPath = pathWithoutExtension + extension;
            if (File.Exists(newPath))
            {
                return File.ReadAllBytes(newPath);
            }
        }

        return null;
    }
#if UNITY_EDITOR
    private void OnPauseStateChanged(PauseState state)
    {
        Debug.Log($"OnApplicationPause: {state}");
        lastPauseState = state;
        if (state == PauseState.Paused)
        {
            channelGroup.setPaused(true);
            bgaPlayer.PauseAll(); ;
        }
        else
        {
            channelGroup.setPaused(false);
            bgaPlayer.ResumeAll(GetCompensatedDspTimeMicro());
        }
    }
#endif
    private ulong DSPToMs(ulong dspClock)
    {
        system.getSoftwareFormat(out var sampleRate, out _, out _);
        return (ulong)((double)dspClock / sampleRate * 1000);
    }

    private ulong MsToDSP(double ms)
    {
        system.getSoftwareFormat(out var sampleRate, out _, out _);
        return (ulong)(ms * sampleRate / 1000);
    }
    int[] touchingFingers = new int[8] { -1, -1, -1, -1, -1, -1, -1, -1 };
    public void FingerMove(Finger finger, int laneNumber)
    {
        for (int i = 0; i < 8; i++)
        {
            if (touchingFingers[i] == finger.index)
            {
                if (laneNumber != i)
                {
                    FingerUp(finger, i);
                    FingerDown(finger, laneNumber);
                }
                break;
            }
        }
    }

    public void FingerDown(Finger finger, int laneNumber)
    {
        if (touchingFingers[laneNumber] == -1)
        {
            OnPressLane(laneNumber);
        }
        touchingFingers[laneNumber] = finger.index;
    }
    public void FingerUp(Finger finger, int laneNumber)
    {
        if (touchingFingers[laneNumber] == finger.index)
        {
            touchingFingers[laneNumber] = -1;
            OnReleaseLane(laneNumber);
        }
    }

    private void OnGUI()
    {
        var style = new GUIStyle
        {
            fontSize = 120,
            normal =
            {
                textColor = Color.white
            }
        };
        GUILayout.BeginArea(new Rect(0, 0, Screen.width, Screen.height));
        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (combo > 0 || latestJudgement == Judgement.BAD || latestJudgement == Judgement.KPOOR)
            GUILayout.Label($"{combo} {latestJudgement}", style);

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        if (parser.GetChart() != null)
            GUILayout.Label($"{GetCompensatedDspTimeMicro() / 1000000}/{(parser.GetChart().TotalLength + TimeMargin) / 1000000}", style);
        GUILayout.EndHorizontal();
        GUILayout.EndArea();



    }
}
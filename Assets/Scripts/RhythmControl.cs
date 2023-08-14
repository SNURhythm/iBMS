using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using FMOD;
using FMODUnity;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

class GameState
{
    public readonly Queue<(ulong dspclock, int wav)> SoundQueue = new();
    public bool IsPlaying;
    public Judge Judge;
    public ulong StartDSPClock;
    public int SameDspClockCount = 0;
    public long LastDspTime = 0;
    public long MaxCompensatedDspTime = 0;
    
    public int PassedMeasureCount = 0;
    public int PassedTimelineCount = 0;
    
    public int Combo = 0;
    public Judgement LatestJudgement;
    
    public int AutoPlayedTimelines = 0;
    public int AutoPlayedMeasures = 0;
    private long firstTiming = 0;
    public GameState(Chart chart, bool addReadyMeasure)
    {
        Judge = new Judge(chart.Rank);
        if(addReadyMeasure)
            if(chart.Measures.Count > 1)
                firstTiming = chart.Measures[1].Timelines[0].Timing;
        
    }
    
    public long GetCurrentDspTimeMicro(FMOD.System system, ChannelGroup channelGroup)
    {
        channelGroup.getDSPClock(out var dspClock, out var parentClock);
        system.getSoftwareFormat(out var sampleRate, out _, out _);
        var micro = (long)((double)dspClock / sampleRate * 1000000 - (double)StartDSPClock / sampleRate * 1000000);
        if(micro>firstTiming)
        { 
            return micro;
        }
        else
        {
            SameDspClockCount = 0;
            return firstTiming;
        }
        
    }

    public long GetCompensatedDspTimeMicro(FMOD.System system, ChannelGroup channelGroup)
    {

        return GetCurrentDspTimeMicro(system, channelGroup) + SameDspClockCount * (long)(Time.fixedDeltaTime * 1000000);
    }
}
public class RhythmControl : MonoBehaviour
{
    public GameObject PausePanel;
    
    private const int MaxRealChannels = 512;
    private const int MaxBgRealChannels = MaxRealChannels - 50;
    private const long TimeMargin = 5000000; // 5 seconds
    private byte[] metronomeBytes;


    private Dictionary<int, Sound> wavSounds = new();


    private ChannelGroup channelGroup;


#if UNITY_EDITOR
    private PauseState lastPauseState = PauseState.Unpaused;

#endif
    private BMSParser parser;
    private BMSRenderer bmsRenderer;
    private BGAPlayer bgaPlayer;
    private GameState gameState = null;
    private bool IsPaused = false;


    private FMOD.System system;
    private readonly CancellationTokenSource loadGameTokenSource = new();
    private Task loadGameTask;
    private bool addReadyMeasure = true; // TODO: add to config
    
    private bool isLoaded = false;
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
        system.setSoftwareChannels(MaxRealChannels);
        system.setSoftwareFormat(48000, SPEAKERMODE.DEFAULT, 0);
        // set buffer size
        var result = system.setDSPBufferSize(256, 4);
        // if (result != FMOD.RESULT.OK) Debug.Log($"setDSPBufferSize failed. {result}");
        system.init(MaxRealChannels, INITFLAGS.NORMAL, IntPtr.Zero);
        bgaPlayer = new();
        bmsRenderer = GetComponent<BMSRenderer>();
        metronomeBytes = Resources.Load<TextAsset>("Sfx/metronome").bytes;
        LoadGame();

    }


    // Update is called once per frame
    private void Update()
    {
        bmsRenderer.UpdateLaneBeam();
        if (gameState == null) return;
        if (!gameState.IsPlaying) return;
        var currentDspTime = gameState.GetCurrentDspTimeMicro(system, channelGroup);
        gameState.MaxCompensatedDspTime = Math.Max(gameState.MaxCompensatedDspTime, gameState.GetCompensatedDspTimeMicro(system, channelGroup));
        var time = Math.Max(currentDspTime, gameState.MaxCompensatedDspTime);
        bmsRenderer.Draw(time);

        if (time >= parser.GetChart().PlayLength + TimeMargin)
        {
            gameState.IsPlaying = false;
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
        if (gameState == null) return;
        var availableChannels = MaxBgRealChannels - realChannels;
        if (availableChannels > 0 && gameState.SoundQueue.Count > 0)
        {
            for (var i = 0; i < availableChannels; i++)
            {
                if (gameState.SoundQueue.Count == 0) break;
                var (startDSP, wav) = gameState.SoundQueue.Dequeue();
                // Debug.Log("Playing queued sound: " + wav);
                system.playSound(wavSounds[wav], channelGroup, true, out var channel);
                channel.setDelay(startDSP, 0);
                channel.setPaused(false);
            }
        }

        if (gameState.IsPlaying)
        {
            var currentDspTime = gameState.GetCurrentDspTimeMicro(system, channelGroup);
            if (gameState.LastDspTime == currentDspTime)
            {
                gameState.SameDspClockCount++;
            }
            else
            {
                gameState.SameDspClockCount = 0;
                gameState.LastDspTime = currentDspTime;
            }

            CheckPassedTimeline(currentDspTime);
            if(GameManager.Instance.AutoPlay) AutoPlay(gameState.GetCompensatedDspTimeMicro(system, channelGroup));
            bgaPlayer.Update(gameState.GetCompensatedDspTimeMicro(system, channelGroup));
        }


    }

    private void CheckPassedTimeline(long time)
    {
        var measures = parser.GetChart().Measures;
        for (int i = gameState.PassedMeasureCount; i < measures.Count; i++)
        {
            var isFirstMeasure = i == gameState.PassedMeasureCount;
            var measure = measures[i];
            for (int j = isFirstMeasure ? gameState.PassedTimelineCount : 0; j < measure.Timelines.Count; j++)
            {
                var timeline = measure.Timelines[j];
                if (timeline.Timing < time - 200000)
                {
                    gameState.PassedTimelineCount++;
                    // make remaining notes POOR
                    foreach (var note in timeline.Notes)
                    {
                        if (note == null) continue;
                        if (note.IsPlayed) continue;


                        if (note is LongNote { IsTail: false } ln)
                        {
                            ln.MissPress(time);
                        }

                        gameState.Combo = 0;
                        gameState.LatestJudgement = Judgement.KPOOR;
                    }
                }
                else if (timeline.Timing <= time)
                {
                    // auto-release long notes
                    foreach (var note in timeline.Notes)
                    {
                        if (note == null) continue;
                        if (note.IsPlayed) continue;
                        if (note is LongNote { IsTail: true } longNote)
                        {
                            if (!longNote.IsHolding) continue;
                            longNote.Release(time);
                            var headJudgeResult = gameState.Judge.JudgeNow(longNote.Head, longNote.Head.PlayedTime);
                            gameState.LatestJudgement = headJudgeResult.Judgement;
                            if (headJudgeResult.ShouldComboBreak) gameState.Combo = 0;
                            else if (headJudgeResult.Judgement != Judgement.KPOOR)
                                gameState.Combo++;
                        }
                    }
                }
            }
            if (gameState.PassedTimelineCount == measure.Timelines.Count && isFirstMeasure)
            {
                gameState.PassedTimelineCount = 0;
                gameState.PassedMeasureCount++;
            }
            else break;
        }
    }


    private readonly long testRandomOffsetRange = 0;
    private float randomOffset = -1;

    private void AutoPlay(long currentTime)
    {
        if (randomOffset < 0)
        {
            randomOffset = UnityEngine.Random.Range(0, testRandomOffsetRange);
        }
        var measures = parser.GetChart().Measures;
        for (int i = gameState.AutoPlayedMeasures; i < measures.Count; i++)
        {
            var measure = measures[i];
            for (int j = gameState.AutoPlayedTimelines; j < measure.Timelines.Count; j++)
            {

                var timeline = measure.Timelines[j];

                if (!(Math.Abs(timeline.Timing - currentTime) < randomOffset) &&
                    timeline.Timing - currentTime >= -testRandomOffsetRange) continue;
                randomOffset = UnityEngine.Random.Range(0, testRandomOffsetRange);
                // mimic press
                foreach (var note in timeline.Notes)
                {
                    if (note == null) continue;
                    if (note is LongNote { IsTail: true })
                    {
                        ReleaseLane(note.Lane);
                    }
                    else
                    {
                        PressLane(note.Lane);
                        if(note is not LongNote)
                            ReleaseLane(note.Lane);

                    }
                    // Debug.Log($"Combo: {combo}");
                }

                gameState.AutoPlayedTimelines = j + 1;
                // Debug.Log("Autoplayed: " + autoplayedTimelines);
                if (gameState.AutoPlayedTimelines == measure.Timelines.Count)
                {
                    gameState.AutoPlayedTimelines = 0;
                    gameState.AutoPlayedMeasures = i + 1;
                }
                i = measures.Count;
                break;
            }
        }
    }

    public void PressLane(int lane, double inputDelay = 0)
    {
        bmsRenderer.StartLaneBeamEffect(lane);
        if (gameState == null) return;
        if (!gameState.IsPlaying) return;


        // Debug.Log("Press: " + lane + ", " + inputDelay);
        var measures = parser.GetChart().Measures;
        var pressedTime = gameState.GetCompensatedDspTimeMicro(system, channelGroup) - (long)(inputDelay * 1000000);

        for (int i = gameState.PassedMeasureCount; i < measures.Count; i++)
        {
            var isFirstMeasure = i == gameState.PassedMeasureCount;
            var measure = measures[i];

            for (int j = isFirstMeasure ? gameState.PassedTimelineCount : 0; j < measure.Timelines.Count; j++)
            {
                var timeline = measure.Timelines[j];
                if (timeline.Timing < pressedTime - 200000) continue;
                var note = timeline.Notes[lane];
                if (note == null) continue;
                if (note.IsPlayed) continue;
                if (GameManager.Instance.KeySound && note.Wav != BMSParser.NoWav)
                {
                    var thread = new System.Threading.Thread(() => system.playSound(wavSounds[note.Wav], channelGroup, false, out var channel));
                    thread.Start();

                }
                var judgeResult = gameState.Judge.JudgeNow(note, pressedTime);
                if (judgeResult.Judgement != Judgement.NONE)
                {

                    if (judgeResult.IsNotePlayed)
                    {
                        bmsRenderer.PlayKeyBomb(lane, judgeResult.Judgement);
                        if (note is LongNote longNote)
                        {
                            if (!longNote.IsTail)
                            {
                                longNote.Press(pressedTime);
                            }
                            // LongNote Head's judgement is determined on release
                            return;
                        }

                        note.Press(pressedTime);
                        
                    }
                    gameState.LatestJudgement = judgeResult.Judgement;

                    if (judgeResult.ShouldComboBreak) gameState.Combo = 0;
                    else if (judgeResult.Judgement != Judgement.KPOOR)
                        gameState.Combo++;

                }
                return;

            }
        }
    }

    public void ReleaseLane(int lane, double inputDelay = 0)
    {
        bmsRenderer.ResumeLaneBeamEffect(lane);
        if (gameState == null) return;
        if (!gameState.IsPlaying) return;

        // Debug.Log("Release: " + lane);
        var releasedTime = gameState.GetCompensatedDspTimeMicro(system, channelGroup) - (long)(inputDelay * 1000000);
        var measures = parser.GetChart().Measures;
        for (int i = gameState.PassedMeasureCount; i < measures.Count; i++)
        {
            var isFirstMeasure = i == gameState.PassedMeasureCount;
            var measure = measures[i];
            for (int j = isFirstMeasure ? gameState.PassedTimelineCount : 0; j < measure.Timelines.Count; j++)
            {
                var timeline = measure.Timelines[j];
                if (timeline.Timing < releasedTime - 200000) continue;
                var note = timeline.Notes[lane];
                if (note == null) continue;
                if (note.IsPlayed) continue;
                var judgeResult = gameState.Judge.JudgeNow(note, releasedTime);
                if (note is LongNote { IsTail: true } longNote)
                {
                    if (!longNote.Head.IsHolding) return;
                    // if judgement is not good/great/pgreat, it will be judged as bad
                    if (judgeResult.Judgement is Judgement.NONE or Judgement.KPOOR or Judgement.BAD)
                    {
                        longNote.Release(releasedTime);
                        gameState.LatestJudgement = Judgement.BAD;
                        gameState.Combo = 0;
                        return;
                    }
                    longNote.Release(releasedTime);
                    var headJudgeResult = gameState.Judge.JudgeNow(longNote.Head, longNote.Head.PlayedTime);
                    gameState.LatestJudgement = headJudgeResult.Judgement;
                    if (headJudgeResult.ShouldComboBreak) gameState.Combo = 0;
                    else if (headJudgeResult.Judgement != Judgement.KPOOR)
                        gameState.Combo++;
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
        var startDSP = gameState.StartDSPClock + MsToDSP(timing / 1000);
        if (!wavSounds.ContainsKey(wav)) return;
        
        if (realChannels >= MaxBgRealChannels)
        {
            gameState.SoundQueue.Enqueue((startDSP, wav)); // Too many channels playing, queue the sound
            return;
        }

        system.playSound(wavSounds[wav], channelGroup, true, out var channel);
        // this.wav[wav].getLength(out uint length, FMOD.TIMEUNIT.MS);
        // var lengthDSP = MsToDSP((double)length);

        // _channel.setMode(FMOD.MODE.VIRTUAL_PLAYFROMSTART);
        channel.setDelay(startDSP, 0);
        channel.setPaused(false);
    }

    private void StartGame()
    {
        if (gameState.IsPlaying) return;
        parser.GetChart().Measures.ForEach(measure => measure.Timelines.ForEach(timeline =>
        {
            if (timeline.BgaBase != -1)
            {
                bgaPlayer.Schedule(timeline.BgaBase, timeline.Timing);
            }
        }));
        channelGroup.getDSPClock(out gameState.StartDSPClock, out _);
        channelGroup.setPaused(true);
        Debug.Log("Play");
        parser.GetChart().Measures.ForEach(measure => measure.Timelines.ForEach(timeline =>
        {
            if (!GameManager.Instance.KeySound)
            {
                timeline.Notes.ForEach(note =>
                {
                    if (note == null || note.Wav == BMSParser.NoWav) return;
                    // Debug.Log(note.wav + "wav");
                    // Debug.Log("NoteTiming: " + timeline.timing / 1000);
                    ScheduleSound(timeline.Timing, note.Wav);
                });
            }

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
        channelGroup.setPaused(IsPaused);

        
        gameState.IsPlaying = !IsPaused;
        
    }

    private Sound GetMetronomeSound()
    {

        var createSoundExInfo = new CREATESOUNDEXINFO
        {
            length = (uint)metronomeBytes.Length,
            cbsize = Marshal.SizeOf(typeof(CREATESOUNDEXINFO))
        };
        var result = system.createSound(metronomeBytes, MODE.OPENMEMORY | MODE.CREATESAMPLE | MODE.ACCURATETIME,
            ref createSoundExInfo, out var sound);
        if (result != RESULT.OK) Debug.Log($"createSound failed. {result}");
        return sound;
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

        system.getDSPBufferSize(out var blockSize, out var numBlocks);
        system.getSoftwareFormat(out var frequency, out _, out _);
        system.getMasterChannelGroup(out channelGroup);
        var bgas = new List<(int id, string path)>();
        
        CancellationToken ct = loadGameTokenSource.Token;
        loadGameTask = Task.Run(() =>
        {
            var basePath = Path.GetDirectoryName(GameManager.Instance.BmsPath);
            ct.ThrowIfCancellationRequested();
            parser.Parse(GameManager.Instance.BmsPath, addReadyMeasure);
            ct.ThrowIfCancellationRequested();
            wavSounds[BMSParser.MetronomeWav] = GetMetronomeSound();

            for (var i = 0; i < 36 * 36; i++)
            {
                ct.ThrowIfCancellationRequested();
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
                            ref createSoundExInfo, out var sound);
                        if (result != RESULT.OK)
                        {
                            Debug.LogWarning($"createSound failed wav{i}. {result}");
                            continue;
                        }
                        wavSounds[i] = sound;
                        wavSounds[i].setLoopCount(0);
                        // _system.playSound(wav[i], _channelGroup, true, out channel);

                        
                    }
                }

                if (bmpFileName != null)
                {
                    bgas.Add((i, basePath + "/" + bmpFileName));
                }
            }
            var ms = blockSize * 1000.0f / frequency;

            Debug.Log($"Mixer blockSize        = {ms} ms");
            Debug.Log($"Mixer Total bufferSize = {ms * numBlocks} ms");
            Debug.Log($"Mixer Average Latency  = {ms * (numBlocks - 1.5f)} ms");
        }, ct);
        try
        {
            await loadGameTask;

            foreach (var (id, path) in bgas)
            {
                // We should load bga in main thread because it adds VideoPlayer on main camera.
                // And it is OK to call this method synchronously because VideoPlayer loads video asynchronously.
                bgaPlayer.Load(id, path);
            }

            bmsRenderer.Init(parser.GetChart());
            gameState = new(parser.GetChart(), addReadyMeasure);
            
            Debug.Log("Load Complete");
            Debug.Log($"PlayLength: {parser.GetChart().PlayLength}, TotalLength: {parser.GetChart().TotalLength}");
            if (bgaPlayer.TotalPlayers != bgaPlayer.LoadedPlayers)
            {
                bgaPlayer.OnAllPlayersLoaded += (sender, args) =>
                {
                    isLoaded = true;
                    StartGame();
                };
            }
            else
            {
                isLoaded = true;
                StartGame();
            }
        } catch (OperationCanceledException)
        {
            Debug.Log("Load game canceled");
        }
    }

    private void UnloadGame()
    {
        isLoaded = false;
        loadGameTokenSource.Cancel();
        try
        {
            loadGameTask?.Wait();
        }
        catch (AggregateException)
        {
            // ignored
        }

        // release all sounds
        foreach (var (i, sound) in wavSounds)
        {
            if (sound.hasHandle())
            {
                sound.release();
            }
        }

        // release system
        system.release();
        Resources.UnloadUnusedAssets();
        System.GC.Collect();
    }


    private byte[] GetWavBytes(string path)
    {
        // we can't trust given extension, so we should try all supported extensions (mp3, ogg, wav, flac)
        var splitIndex = path.LastIndexOf('.');
        var pathWithoutExtension = path.Substring(0, splitIndex);
        var extensions = new[] { ".mp3", ".ogg", ".wav", ".flac" };

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
            bgaPlayer.ResumeAll(gameState.GetCompensatedDspTimeMicro(system, channelGroup));
        }
    }
#endif

    private ulong MsToDSP(double ms)
    {
        system.getSoftwareFormat(out var sampleRate, out _, out _);
        return (ulong)(ms * sampleRate / 1000);
    }

    
    public void ExitGame()
    {
        UnloadGame();
        SceneManager.LoadScene("ChartSelectScene");
    }
    
    public void RetryGame()
    {
        IsPaused = false;
        PausePanel.SetActive(false);
        channelGroup.stop();
        gameState.IsPlaying = false;
        gameState = null;
        bmsRenderer.Reset();
        bgaPlayer.Reset();
        System.GC.Collect();

        parser.GetChart().Measures.ForEach(measure => measure.Timelines.ForEach(timeline =>
        {
            timeline.Notes.ForEach(note => note?.Reset());
        }));

        gameState = new(parser.GetChart(), addReadyMeasure);

        StartGame();
    }
    

    public void ResumeGame()
    {
        IsPaused = false;
        channelGroup.setPaused(false);
        gameState.IsPlaying = true;
        bgaPlayer.ResumeAll(gameState.GetCompensatedDspTimeMicro(system, channelGroup));
        PausePanel.SetActive(false);
    }

    public void PauseGame()
    {
        IsPaused = true;
        channelGroup.setPaused(true);
        gameState.IsPlaying = false;
        bgaPlayer.PauseAll();
        PausePanel.SetActive(true);
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            PauseGame();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            PauseGame();
    }


    private void OnGUI()
    {
        if(gameState == null)
            return;
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
        if (gameState.Combo > 0 || gameState.LatestJudgement == Judgement.BAD || gameState.LatestJudgement == Judgement.KPOOR)
            GUILayout.Label($"{gameState.Combo} {gameState.LatestJudgement}", style);

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        if (parser.GetChart() != null && isLoaded)
            GUILayout.Label($"{gameState.GetCompensatedDspTimeMicro(system, channelGroup) / 1000000}/{(parser.GetChart().TotalLength + TimeMargin) / 1000000}", style);
        GUILayout.EndHorizontal();
        GUILayout.EndArea();



    }
}
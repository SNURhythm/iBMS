using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FMOD;
using FMODUnity;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using Debug = UnityEngine.Debug;
using Thread = System.Threading.Thread;

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
    public Dictionary<Judgement, int> Record = new();
    
    private long firstTiming = 0;
    public GameState(Chart chart, bool addReadyMeasure)
    {
        Judge = new Judge(chart.Meta.Rank);
        // if(addReadyMeasure)
        //     if(chart.Measures.Count > 1)
        //         firstTiming = chart.Measures[1].Timelines[0].Timing;

    }

    public long GetCurrentDspTimeMicro(FMOD.System system, ChannelGroup channelGroup)
    {
        channelGroup.getDSPClock(out var dspClock, out var parentClock);
        system.getSoftwareFormat(out var sampleRate, out _, out _);
        var micro = (long)((double)dspClock / sampleRate * 1000000 - (double)StartDSPClock / sampleRate * 1000000);
        if (micro > firstTiming)
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

    private const int MaxRealChannels = 4092;
    private const int MaxBgRealChannels = MaxRealChannels - 1024;
    private const long TimeMargin = 5000000; // 5 seconds
    private byte[] metronomeBytes;
    private byte[] landmineBytes;


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
    private bool[] isLanePressed = new bool[8];


    private FMOD.System system;
    private readonly CancellationTokenSource loadGameTokenSource = new();
    private readonly CancellationTokenSource mainLoopTokenSource = new();
    private Task loadGameTask;
    private bool addReadyMeasure = true; // TODO: add to config


    private Task mainLoopTask;

    private bool isLoaded = false;
    private void Awake()
    {
        Application.targetFrameRate = 120;
        parser = new BMSParser();
    }

    // Start is called before the first frame update
    private void Start()
    {
        // make garbage collection manual
#if !UNITY_EDITOR
        GarbageCollector.GCMode = GarbageCollector.Mode.Manual;
#endif
#if UNITY_EDITOR
        EditorApplication.pauseStateChanged += OnPauseStateChanged;
        lastPauseState = EditorApplication.isPaused ? PauseState.Paused : PauseState.Unpaused;
#endif
        // RuntimeManager.StudioSystem.release();
        // RuntimeManager.CoreSystem.release();
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
        landmineBytes = Resources.Load<TextAsset>("Sfx/explosion").bytes;
        LoadGame();
        var token = mainLoopTokenSource.Token;
        mainLoopTask = Task.Run(() =>
        {
            while (true)
            {
                if (token.IsCancellationRequested) break;
                if (gameState == null) continue;
                if (!gameState.IsPlaying) continue;
                try
                {
                    var currentDspTime = gameState.GetCurrentDspTimeMicro(system, channelGroup);
                    CheckPassedTimeline(currentDspTime, token);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                    break;
                }
            }
            Debug.Log("MainLoopTask is canceled");
        }, token);

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

        if (time >= parser.GetChart().Meta.PlayLength + TimeMargin)
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
        if (gameState == null) return;
        // channelGroup.getDSPClock(out var dspclock, out var parentclock);
        system.update();
        // system.getSoftwareFormat(out var samplerate, out _, out _);
        // WriteTxt(Application.streamingAssetsPath + "/log.log", "FixedUpdate: " + (double)dspclock / samplerate * 1000 + ", " + Time.time);

        // Debug.Log("dspclock: " + (double)dspclock / samplerate * 1000);
        system.getChannelsPlaying(out var playingChannels, out var realChannels);
        // Debug.Log("playing channels: " + playingChannels + ", real channels: " + realChannels);

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
            
            bgaPlayer.Update(gameState.GetCompensatedDspTimeMicro(system, channelGroup));
        }


    }

    private void CheckPassedTimeline(long time, CancellationToken token)
    {
        var measures = parser.GetChart().Measures;
        if (token.IsCancellationRequested || gameState == null) return;
        for (int i = gameState.PassedMeasureCount; i < measures.Count; i++)
        {
            if (token.IsCancellationRequested) return;
            var isFirstMeasure = i == gameState.PassedMeasureCount;
            var measure = measures[i];
            for (int j = isFirstMeasure ? gameState.PassedTimelineCount : 0; j < measure.Timelines.Count; j++)
            {
                if (token.IsCancellationRequested) return;
                var timeline = measure.Timelines[j];
                if (timeline.Timing < time - 200000)
                {
                    if(isFirstMeasure)
                        gameState.PassedTimelineCount++;
                    // make remaining notes POOR
                    foreach (var note in timeline.Notes)
                    {
                        if (note == null) continue;
                        if (note.IsPlayed) continue;
                        if (note is LandmineNote) continue;


                        if (note is LongNote { IsTail: false } ln)
                        {
                            ln.MissPress(time);
                        }
                        var judgeResult = new JudgeResult(Judgement.POOR, time - timeline.Timing);
                        OnJudge(judgeResult);
                    }

                    
                } else if (timeline.Timing <= time)
                {
                    // auto-release long notes
                    foreach (var note in timeline.Notes)
                    {
                        if (token.IsCancellationRequested) return;
                        if (note == null) continue;
                        if (note.IsPlayed) continue;
                        if (note is LandmineNote)
                        {
                            note.Press(time);
                            if (isLanePressed[note.Lane])
                            {
                                PlayNoteSound(note);
                                Debug.Log("Landmine");
                                
                            }
                            continue;
                        }
                        if (note is LongNote { IsTail: true } longNote)
                        {
                            if (!longNote.IsHolding) continue;
                            longNote.Release(time);
                            var headJudgeResult = gameState.Judge.JudgeNow(longNote.Head, longNote.Head.PlayedTime);
                            OnJudge(headJudgeResult);
                            if (GameManager.Instance.AutoPlay)
                            {
                                bmsRenderer.ResumeLaneBeamEffect(note.Lane);
                            }
                        }
                        else
                        {
                            if(GameManager.Instance.AutoPlay)
                            {
                                PressNote(note, time);
                                bmsRenderer.StartLaneBeamEffect(note.Lane);
                                if (note is not LongNote { IsTail: false })
                                {
                                    bmsRenderer.ResumeLaneBeamEffect(note.Lane);
                                }
                            }
                        }
                    }
                }
                else
                {
                    i = measures.Count;
                    break;
                }
                
            }
            if (gameState.PassedTimelineCount == measure.Timelines.Count && isFirstMeasure)
            {
                gameState.PassedTimelineCount = 0;
                gameState.PassedMeasureCount++;
            }
        }
    }


    private readonly long testRandomOffsetRange = 0;
    private float randomOffset = 0;

    public void PressLane(int lane, double inputDelay = 0)
    {
        isLanePressed[lane] = true;
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
                if (note is LandmineNote) continue;
                PressNote(note, pressedTime);
                return;

            }
        }
    }

    public void ReleaseLane(int lane, double inputDelay = 0)
    {
        isLanePressed[lane] = false;
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
                ReleaseNote(note, releasedTime);
                return;
            }
        }
    }
    
    private void PlayNoteSound(Note note)
    {
        if ((GameManager.Instance.KeySound || note is LandmineNote) && note.Wav != BMSParser.NoWav)
        {
            var thread = new System.Threading.Thread(() => system.playSound(wavSounds[note.Wav], channelGroup, false, out var channel));
            thread.Start();

        }
    }

    private void PressNote(Note note, long pressedTime)
    {
        PlayNoteSound(note);
        var judgeResult = gameState.Judge.JudgeNow(note, pressedTime);
        if (judgeResult.Judgement != Judgement.NONE)
        {

            if (judgeResult.IsNotePlayed)
            {
                bmsRenderer.PlayKeyBomb(note.Lane, judgeResult.Judgement);
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
            OnJudge(judgeResult);
            

        }
    }

    private void ReleaseNote(Note note, long releasedTime)
    {
        var judgeResult = gameState.Judge.JudgeNow(note, releasedTime);
        if (note is LongNote { IsTail: true } longNote)
        {
            if (!longNote.Head.IsHolding) return;
            longNote.Release(releasedTime);
            // if judgement is not good/great/pgreat, it will be judged as bad
            if (judgeResult.Judgement is Judgement.NONE or Judgement.KPOOR or Judgement.BAD)
            {
                judgeResult.Judgement = Judgement.BAD;
                OnJudge(judgeResult);
                return;
            }
            var headJudgeResult = gameState.Judge.JudgeNow(longNote.Head, longNote.Head.PlayedTime);
            OnJudge(headJudgeResult);
        }
    }

    private void OnJudge(JudgeResult judgeResult)
    {
        gameState.Record[judgeResult.Judgement] = gameState.Record.GetValueOrDefault(judgeResult.Judgement) + 1;
        gameState.LatestJudgement = judgeResult.Judgement;
        if (judgeResult.ShouldComboBreak) gameState.Combo = 0;
        else if (judgeResult.Judgement != Judgement.KPOOR)
            gameState.Combo++;
    }

    private int CalculateScore()
    {
        return gameState.Record.GetValueOrDefault(Judgement.PGREAT) * 2 + gameState.Record.GetValueOrDefault(Judgement.GREAT);
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
            // timeline.InvisibleNotes.ForEach(note =>
            // {
            //     if (note == null || note.Wav == BMSParser.NoWav) return;
            //     // Debug.Log("InvNoteTiming: " + timeline.timing / 1000);
            //
            //     ScheduleSound(timeline.Timing, note.Wav);
            // });
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

    private void CreateSound(byte[] bytes, out Sound sound)
    {
        var createSoundExInfo = new CREATESOUNDEXINFO
        {
            length = (uint)bytes.Length,
            cbsize = Marshal.SizeOf(typeof(CREATESOUNDEXINFO))
        };
        var result = system.createSound(bytes, MODE.OPENMEMORY | MODE.CREATESAMPLE | MODE.ACCURATETIME,
            ref createSoundExInfo, out sound);
        if (result != RESULT.OK) Debug.Log($"createSound failed. {result}");
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
            CreateSound(metronomeBytes, out var metronomeSound);
            wavSounds[BMSParser.MetronomeWav] = metronomeSound;

            var tasks = new Task<(Sound, (int, string))>[36 * 36];
            for (var i = 0; i < 36 * 36; i++)
            {
                var id = i;
                tasks[i] = Task.Run(() =>
                {
                    ct.ThrowIfCancellationRequested();
                    var wavFileName = parser.GetWavFileName(id);
                    var bmpFileName = parser.GetBmpFileName(id);
                    Sound sound = default;
                    if (wavFileName != null)
                    {
                        byte[] wavBytes = GetWavBytes(Path.Combine(basePath, wavFileName));
                        ct.ThrowIfCancellationRequested();
                        if (wavBytes == null)
                        {
                            Debug.LogWarning("wavBytes is null:" + parser.GetWavFileName(id));
                        }
                        else
                        {
                            CreateSound(wavBytes, out sound);

                            ct.ThrowIfCancellationRequested();

                            // _system.playSound(wav[i], _channelGroup, true, out channel);
                        }
                    } else if (id == 0)
                    {
                        // default landmine sound
                        CreateSound(landmineBytes, out sound);
                    }

                    (int id, string path) bgainfo = (-1, null);
                    if (bmpFileName != null)
                    {

                        bgainfo = (id, Path.Combine(basePath, bmpFileName));
                    }

                    return (sound, bgainfo);
                }, ct);
            }
            try
            {
                Task.WhenAll(tasks).Wait(ct);
                for (var i = 0; i < 36 * 36; i++)
                {
                    var (sound, bgainfo) = ((Sound sound, (int id, string path) bgainfo))tasks[i].Result;

                    wavSounds[i] = sound; // To prevent concurrent modification, we should wait for all tasks to complete before assigning wavSounds.
                    wavSounds[i].setLoopCount(0);
                    if (bgainfo.id != -1)
                    {
                        bgas.Add(bgainfo); // Same as above.
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log("LoadGameTask is canceled");
                return;
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
            Debug.Log($"PlayLength: {parser.GetChart().Meta.PlayLength}, TotalLength: {parser.GetChart().Meta.TotalLength}");
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
        }
        catch (OperationCanceledException)
        {
            Debug.Log("Load game canceled");
        }
    }

    private void UnloadGame()
    {
        gameState = null;
        isLoaded = false;
        loadGameTokenSource.Cancel();
        mainLoopTokenSource.Cancel();
        try
        {
            loadGameTask?.Wait();
        }
        catch (AggregateException)
        {
            // ignored
        }
        try
        {
            mainLoopTask?.Wait();
        }
        catch (AggregateException)
        {
            // ignored
        }
        bgaPlayer.Dispose();

        // release all sounds
        foreach (var (i, sound) in wavSounds)
        {
            sound.release();
        }

        bmsRenderer.Dispose();
        // release system
        system.release();
        Resources.UnloadUnusedAssets();
        System.GC.Collect();
        // make garbage collector automatically collect

#if !UNITY_EDITOR
        GarbageCollector.GCMode = GarbageCollector.Mode.Enabled;
#endif
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

        if (gameState != null)
        {
            gameState.IsPlaying = true;
        }

        bgaPlayer.ResumeAll(gameState.GetCompensatedDspTimeMicro(system, channelGroup));
        PausePanel.SetActive(false);
    }

    public void PauseGame()
    {
        IsPaused = true;
        channelGroup.setPaused(true);

        if (gameState != null)
        {
            gameState.IsPlaying = false;
        }

        bgaPlayer.PauseAll();
        PausePanel.SetActive(true);
        System.GC.Collect();
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

    private void OnApplicationQuit()
    {
        UnloadGame();
    }

    private int lastRenderedCombo = 0;
    private Judgement lastRenderedJudgement;
    private string lastComboString = "";
    private void OnGUI()
    {
        if (gameState == null)
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
        if (gameState.Combo > 0 || gameState.LatestJudgement == Judgement.BAD ||
            gameState.LatestJudgement == Judgement.KPOOR || gameState.LatestJudgement == Judgement.POOR)
        {
            if (gameState.Combo != lastRenderedCombo || gameState.LatestJudgement != lastRenderedJudgement)
            {
                lastRenderedCombo = gameState.Combo;
                lastRenderedJudgement = gameState.LatestJudgement;
                var sb = new StringBuilder();
                sb.Append(gameState.Combo);
                sb.Append(' ');
                sb.Append(gameState.LatestJudgement);
                lastComboString = sb.ToString();
            }
            GUILayout.Label(lastComboString, style);
        }

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        if (parser.GetChart() != null && isLoaded)
        {
            var sb = new StringBuilder();
            sb.Append(gameState.GetCompensatedDspTimeMicro(system, channelGroup) / 1000000);
            sb.Append('/');
            sb.Append((parser.GetChart().Meta.TotalLength + TimeMargin) / 1000000);

            GUILayout.Label(sb.ToString(), style);

        }
        GUILayout.FlexibleSpace();
        GUILayout.Label(CalculateScore().ToString(), style);
        GUILayout.EndHorizontal();
        GUILayout.EndArea();



    }
}
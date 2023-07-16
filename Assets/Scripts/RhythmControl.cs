using System.Collections;
using System.Collections.Generic;
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using System.IO;
public class RhythmControl : MonoBehaviour
{
#if UNITY_EDITOR
    private PauseState _lastPauseState = PauseState.Unpaused;

#endif 
    private FMOD.Sound _music;
    private FMOD.Sound[] wav = new FMOD.Sound[36 * 36];
    private FMOD.Channel[] channels = new FMOD.Channel[36 * 36];
    private FMOD.Channel _channel;
    private FMOD.ChannelGroup _channelGroup;
    private FMOD.System _system;
    private Queue<(ulong dspclock, int wav)> _soundQueue = new();
    private const int MAX_CHANNELS = 1024;

    private bool isPlaying = false;
    private int timelineCursor = 0;

    private ulong startDSPClock = 0;
    private BMSParser _parser;
    void Awake()
    {
        Application.targetFrameRate = 120;
        _parser = new BMSParser();
    }

    // Start is called before the first frame update
    void Start()
    {

#if UNITY_EDITOR
        EditorApplication.pauseStateChanged += OnPauseStateChanged;
        _lastPauseState = EditorApplication.isPaused ? PauseState.Paused : PauseState.Unpaused;
#endif
        FMODUnity.RuntimeManager.StudioSystem.release();
        FMODUnity.RuntimeManager.CoreSystem.release();
        FMOD.Factory.System_Create(out _system); // TODO: make system singleton
        _system.setSoftwareChannels(256);
        // set buffer size
        var result = _system.setDSPBufferSize(256, 2);
        if (result != FMOD.RESULT.OK) Debug.Log($"setDSPBufferSize failed. {result}");
        _system.init(MAX_CHANNELS, FMOD.INITFLAGS.NORMAL, IntPtr.Zero);
        LoadGame();
        Debug.Log("Load Complete");
        _channelGroup.setPaused(true);
        Invoke("StartMusic", 3.0f);
    }

    void OnDisable()
    {
        _system.release();
    }
    void ScheduleSound(double timing, int wav)
    {
        _system.getChannelsPlaying(out int playingChannels, out int realChannels);
        var startDSP = startDSPClock + MsToDSP(timing / 1000);
        if (playingChannels >= MAX_CHANNELS)
        {
            _soundQueue.Enqueue((startDSP, wav)); // Too many channels playing, queue the sound
            return;
        }
        _system.playSound(this.wav[wav], _channelGroup, true, out channels[wav]);
        // this.wav[wav].getLength(out uint length, FMOD.TIMEUNIT.MS);
        // var lengthDSP = MsToDSP((double)length);

        // channels[wav].setMode(FMOD.MODE.VIRTUAL_PLAYFROMSTART);
        channels[wav].setDelay(startDSP, 0, true);
        channels[wav].setPaused(false);
    }
    void StartMusic()
    {
        if (isPlaying) return;
        _channelGroup.getDSPClock(out startDSPClock, out _);
        _channelGroup.setPaused(false);
        Debug.Log("Play");
        _parser.chart.timelines.ForEach((timeline) =>
        {
            timeline.notes.ForEach((note) =>
            {
                if (note == null || note.wav == BMSParser.NO_WAV) return;
                // Debug.Log(note.wav + "wav");
                // Debug.Log("NoteTiming: " + timeline.timing / 1000);
                ScheduleSound((double)timeline.timing, note.wav);
            });
            timeline.backgroundNotes.ForEach((note) =>
            {
                if (note == null || note.wav == BMSParser.NO_WAV) return;
                // Debug.Log("InvNoteTiming: " + timeline.timing / 1000);

                ScheduleSound((double)timeline.timing, note.wav);
            });
            timeline.invisibleNotes.ForEach((note) =>
            {
                if (note == null || note.wav == BMSParser.NO_WAV) return;
                // Debug.Log("BGNoteTiming: " + timeline.timing / 1000);

                ScheduleSound((double)timeline.timing, note.wav);
            });
        });
        isPlaying = true;
    }
    // Update is called once per frame
    void Update()
    {


    }

    private void FixedUpdate()
    {
        _channelGroup.getDSPClock(out ulong dspclock, out ulong parentclock);
        _system.update();
        _system.getSoftwareFormat(out var samplerate, out _, out _);
        // WriteTxt(Application.streamingAssetsPath + "/log.log", "FixedUpdate: " + (double)dspclock / samplerate * 1000 + ", " + Time.time);

        Debug.Log("dspclock: " + (double)dspclock / samplerate * 1000);
        _system.getChannelsPlaying(out int playingChannels, out int realChannels);
        Debug.Log("playing channels: " + playingChannels + ", real channels: " + realChannels);
        var availableChannels = MAX_CHANNELS - playingChannels;
        if (availableChannels > 0 && _soundQueue.Count > 0)
        {
            for (int i = 0; i < availableChannels; i++)
            {
                if (_soundQueue.Count == 0) break;
                var (startDSP, wav) = _soundQueue.Dequeue();
                _system.playSound(this.wav[wav], _channelGroup, true, out channels[wav]);
                channels[wav].setDelay(startDSP, 0, true);
                channels[wav].setPaused(false);
            }
        }

    }

    private void LoadGame()
    {
        _parser.Parse(Application.streamingAssetsPath + "/testbms/PUPA[SPH].bml");
        FMOD.RESULT result;

        // set defaultDecodeBufferSize
        var advancedSettings = new FMOD.ADVANCEDSETTINGS
        {
            defaultDecodeBufferSize = 32
        };
        result = _system.setAdvancedSettings(ref advancedSettings);
        if (result != FMOD.RESULT.OK) Debug.Log($"setAdvancedSettings failed. {result}");

        uint blocksize;
        int numblocks;
        float ms;
        int frequency;
        result = _system.getDSPBufferSize(out blocksize, out numblocks);
        result = _system.getSoftwareFormat(out frequency, out _, out _);
        _system.getMasterChannelGroup(out _channelGroup);

        for (int i = 0; i < 36 * 36; i++)
        {
            if (_parser.wavTable[i] == null) continue;
            var channel = new FMOD.Channel();
            channel.setChannelGroup(_channelGroup);
            channel.setLoopCount(0);

            result = _system.createSound(Application.streamingAssetsPath + "/testbms/" + _parser.wavTable[i],
            FMOD.MODE.CREATESAMPLE | FMOD.MODE.ACCURATETIME, out wav[i]);
            wav[i].setLoopCount(0);
            // _system.playSound(wav[i], _channelGroup, true, out channel);
            channels[i] = channel;
            if (result != FMOD.RESULT.OK) Debug.Log($"createSound failed wav{i}. {result}");
        }





        ms = (float)blocksize * 1000.0f / (float)frequency;

        Debug.Log($"Mixer blocksize        = {ms} ms");
        Debug.Log($"Mixer Total buffersize = {ms * numblocks} ms");
        Debug.Log($"Mixer Average Latency  = {ms * ((float)numblocks - 1.5f)} ms");
    }
#if UNITY_EDITOR
    private void OnPauseStateChanged(PauseState state)
    {
        Debug.Log($"OnApplicationPause: {state}");
        _lastPauseState = state;
        if (state == PauseState.Paused)
        {
            _channelGroup.setPaused(true);
        }
        else
        {
            _channelGroup.setPaused(false);
        }
    }
#endif
    private ulong DSPToMs(ulong dspclock)
    {
        _system.getSoftwareFormat(out var samplerate, out _, out _);
        return (ulong)((double)dspclock / samplerate * 1000);
    }
    private ulong MsToDSP(double ms)
    {
        _system.getSoftwareFormat(out var samplerate, out _, out _);
        return (ulong)(ms * samplerate / 1000);
    }
    public void FingerMove(Finger obj)
    {
        _channelGroup.getDSPClock(out ulong dspclock, out ulong parentclock);
        _system.getSoftwareFormat(out var samplerate, out _, out _);
        // WriteTxt(Application.streamingAssetsPath + "/log.log", "Finger Move on clock: " + (double)parentclock / samplerate * 1000 + ", " + Time.time);
    }

    void WriteTxt(string filePath, string message)
    {
        DirectoryInfo directoryInfo = new DirectoryInfo(Path.GetDirectoryName(filePath));

        if (!directoryInfo.Exists)
        {
            directoryInfo.Create();
        }

        // This text is added only once to the file.
        if (!File.Exists(filePath))
        {
            // Create a file to write to.
            using (StreamWriter sw = File.CreateText(filePath))
            {
                sw.WriteLine(message);
            }
        }
        else
        {
            // This text is always added, making the file longer over time
            // if it is not deleted.
            using (StreamWriter sw = File.AppendText(filePath))
            {
                sw.WriteLine(message);
            }
        }

    }
}

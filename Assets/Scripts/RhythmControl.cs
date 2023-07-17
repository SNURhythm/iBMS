using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using FMOD;
using FMODUnity;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

public class RhythmControl : MonoBehaviour
{
    private const int MAX_CHANNELS = 1024;
    private Channel _channel;
    private ChannelGroup _channelGroup;
#if UNITY_EDITOR
    private PauseState _lastPauseState = PauseState.Unpaused;

#endif
    private Sound _music;
    private BMSParser _parser;
    private readonly Queue<(ulong dspclock, int wav)> _soundQueue = new();
    private FMOD.System _system;
    private readonly Channel[] channels = new Channel[36 * 36];

    private bool isPlaying;

    private ulong startDSPClock;
    private int timelineCursor = 0;
    private readonly Sound[] wav = new Sound[36 * 36];

    private void Awake()
    {
        Application.targetFrameRate = 120;
        _parser = new BMSParser();
    }

    // Start is called before the first frame update
    private void Start()
    {
#if UNITY_EDITOR
        EditorApplication.pauseStateChanged += OnPauseStateChanged;
        _lastPauseState = EditorApplication.isPaused ? PauseState.Paused : PauseState.Unpaused;
#endif
        RuntimeManager.StudioSystem.release();
        RuntimeManager.CoreSystem.release();
        Factory.System_Create(out _system); // TODO: make system singleton
        _system.setSoftwareChannels(256);
        // set buffer size
        // var result = _system.setDSPBufferSize(256, 2);
        // if (result != FMOD.RESULT.OK) Debug.Log($"setDSPBufferSize failed. {result}");
        _system.init(MAX_CHANNELS, INITFLAGS.NORMAL, IntPtr.Zero);
        LoadGame();
        Debug.Log("Load Complete");
        _channelGroup.setPaused(true);
        Invoke("StartMusic", 3.0f);
    }

    // Update is called once per frame
    private void Update()
    {
    }

    private void FixedUpdate()
    {
        _channelGroup.getDSPClock(out var dspclock, out var parentclock);
        _system.update();
        _system.getSoftwareFormat(out var samplerate, out _, out _);
        // WriteTxt(Application.streamingAssetsPath + "/log.log", "FixedUpdate: " + (double)dspclock / samplerate * 1000 + ", " + Time.time);

        // Debug.Log("dspclock: " + (double)dspclock / samplerate * 1000);
        _system.getChannelsPlaying(out var playingChannels, out var realChannels);
        // Debug.Log("playing channels: " + playingChannels + ", real channels: " + realChannels);
        var availableChannels = MAX_CHANNELS - playingChannels;
        if (availableChannels > 0 && _soundQueue.Count > 0)
            for (var i = 0; i < availableChannels; i++)
            {
                if (_soundQueue.Count == 0) break;
                var (startDSP, wav) = _soundQueue.Dequeue();
                _system.playSound(this.wav[wav], _channelGroup, true, out channels[wav]);
                channels[wav].setDelay(startDSP, 0);
                channels[wav].setPaused(false);
            }
    }

    private void OnDisable()
    {
        _system.release();
    }

    private void ScheduleSound(double timing, int wav)
    {
        _system.getChannelsPlaying(out var playingChannels, out var realChannels);
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
        channels[wav].setDelay(startDSP, 0);
        channels[wav].setPaused(false);
    }

    private void StartMusic()
    {
        if (isPlaying) return;
        _channelGroup.getDSPClock(out startDSPClock, out _);
        _channelGroup.setPaused(false);
        Debug.Log("Play");
        _parser.chart.timelines.ForEach(timeline =>
        {
            timeline.notes.ForEach(note =>
            {
                if (note == null || note.wav == BMSParser.NO_WAV) return;
                // Debug.Log(note.wav + "wav");
                // Debug.Log("NoteTiming: " + timeline.timing / 1000);
                ScheduleSound(timeline.timing, note.wav);
            });
            timeline.backgroundNotes.ForEach(note =>
            {
                if (note == null || note.wav == BMSParser.NO_WAV) return;
                // Debug.Log("InvNoteTiming: " + timeline.timing / 1000);

                ScheduleSound(timeline.timing, note.wav);
            });
            timeline.invisibleNotes.ForEach(note =>
            {
                if (note == null || note.wav == BMSParser.NO_WAV) return;
                // Debug.Log("BGNoteTiming: " + timeline.timing / 1000);

                ScheduleSound(timeline.timing, note.wav);
            });
        });
        isPlaying = true;
    }

    private void LoadGame()
    {
        _parser.Parse(Application.streamingAssetsPath + "/testbms/PUPA[SPH].bml");
        RESULT result;

        // set defaultDecodeBufferSize
        var advancedSettings = new ADVANCEDSETTINGS
        {
            defaultDecodeBufferSize = 32
        };
        result = _system.setAdvancedSettings(ref advancedSettings);
        if (result != RESULT.OK) Debug.Log($"setAdvancedSettings failed. {result}");

        uint blocksize;
        int numblocks;
        float ms;
        int frequency;
        result = _system.getDSPBufferSize(out blocksize, out numblocks);
        result = _system.getSoftwareFormat(out frequency, out _, out _);
        _system.getMasterChannelGroup(out _channelGroup);

        for (var i = 0; i < 36 * 36; i++)
        {
            if (_parser.wavTable[i] == null) continue;
            var channel = new Channel();
            channel.setChannelGroup(_channelGroup);
            channel.setLoopCount(0);
            byte[] wavBytes;
            if (Application.platform == RuntimePlatform.Android)
            {
                var www = UnityWebRequest.Get(Application.streamingAssetsPath + "/testbms/" + _parser.wavTable[i]);
                www.SendWebRequest();
                while (!www.isDone)
                {
                }

                wavBytes = www.downloadHandler.data;
            }
            else
            {
                wavBytes = File.ReadAllBytes(Application.streamingAssetsPath + "/testbms/" + _parser.wavTable[i]);
            }

            var createSoundExInfo = new CREATESOUNDEXINFO
            {
                length = (uint)wavBytes.Length,
                cbsize = Marshal.SizeOf(typeof(CREATESOUNDEXINFO))
            };
            result = _system.createSound(wavBytes, MODE.OPENMEMORY | MODE.CREATESAMPLE | MODE.ACCURATETIME,
                ref createSoundExInfo, out wav[i]);
            wav[i].setLoopCount(0);
            // _system.playSound(wav[i], _channelGroup, true, out channel);
            channels[i] = channel;
            if (result != RESULT.OK) Debug.Log($"createSound failed wav{i}. {result}");
        }


        ms = blocksize * 1000.0f / frequency;

        Debug.Log($"Mixer blocksize        = {ms} ms");
        Debug.Log($"Mixer Total buffersize = {ms * numblocks} ms");
        Debug.Log($"Mixer Average Latency  = {ms * (numblocks - 1.5f)} ms");
    }
#if UNITY_EDITOR
    private void OnPauseStateChanged(PauseState state)
    {
        Debug.Log($"OnApplicationPause: {state}");
        _lastPauseState = state;
        if (state == PauseState.Paused)
            _channelGroup.setPaused(true);
        else
            _channelGroup.setPaused(false);
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
        _channelGroup.getDSPClock(out var dspclock, out var parentclock);
        _system.getSoftwareFormat(out var samplerate, out _, out _);
        // WriteTxt(Application.streamingAssetsPath + "/log.log", "Finger Move on clock: " + (double)parentclock / samplerate * 1000 + ", " + Time.time);
    }

    private void WriteTxt(string filePath, string message)
    {
        var directoryInfo = new DirectoryInfo(Path.GetDirectoryName(filePath));

        if (!directoryInfo.Exists) directoryInfo.Create();

        // This text is added only once to the file.
        if (!File.Exists(filePath))
            // Create a file to write to.
            using (var sw = File.CreateText(filePath))
            {
                sw.WriteLine(message);
            }
        else
            // This text is always added, making the file longer over time
            // if it is not deleted.
            using (var sw = File.AppendText(filePath))
            {
                sw.WriteLine(message);
            }
    }
}
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
    private const int MaxChannels = 1024;

    private readonly Queue<(ulong dspclock, int wav)> soundQueue = new();

    private readonly Sound[] wavSounds = new Sound[36 * 36];

    private ChannelGroup channelGroup;

    private bool isPlaying;
#if UNITY_EDITOR
    private PauseState lastPauseState = PauseState.Unpaused;

#endif
    private Sound music;
    private BMSParser parser;

    private ulong startDSPClock;
    private FMOD.System system;

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
        LoadGame();
        Debug.Log("Load Complete");
        channelGroup.setPaused(true);
        Invoke(nameof(StartMusic), 3.0f);
    }

    // Update is called once per frame
    private void Update()
    {
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
            for (var i = 0; i < availableChannels; i++)
            {
                if (soundQueue.Count == 0) break;
                var (startDSP, wav) = soundQueue.Dequeue();
                system.playSound(wavSounds[wav], channelGroup, true, out var channel);
                channel.setDelay(startDSP, 0);
                channel.setPaused(false);
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
        channel.setDelay(startDSP, 0);
        channel.setPaused(false);
    }

    private void StartMusic()
    {
        if (isPlaying) return;
        channelGroup.getDSPClock(out startDSPClock, out _);
        channelGroup.setPaused(false);
        Debug.Log("Play");
        parser.GetChart().Timelines.ForEach(timeline =>
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
                // Debug.Log("InvNoteTiming: " + timeline.timing / 1000);

                ScheduleSound(timeline.Timing, note.Wav);
            });
            timeline.InvisibleNotes.ForEach(note =>
            {
                if (note == null || note.Wav == BMSParser.NoWav) return;
                // Debug.Log("BGNoteTiming: " + timeline.timing / 1000);

                ScheduleSound(timeline.Timing, note.Wav);
            });
        });
        isPlaying = true;
    }

    private void LoadGame()
    {
        parser.Parse(Application.streamingAssetsPath + "/testbms/PUPA[SPH].bml");

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

        for (var i = 0; i < 36 * 36; i++)
        {
            if (parser.GetWavFileName(i) == null) continue;
            byte[] wavBytes;
            if (Application.platform == RuntimePlatform.Android)
            {
                var www = UnityWebRequest.Get(Application.streamingAssetsPath + "/testbms/" + parser.GetWavFileName(i));
                www.SendWebRequest();
                while (!www.isDone)
                {
                }

                wavBytes = www.downloadHandler.data;
            }
            else
            {
                wavBytes = File.ReadAllBytes(Application.streamingAssetsPath + "/testbms/" + parser.GetWavFileName(i));
            }

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


        var ms = blockSize * 1000.0f / frequency;

        Debug.Log($"Mixer blockSize        = {ms} ms");
        Debug.Log($"Mixer Total bufferSize = {ms * numBlocks} ms");
        Debug.Log($"Mixer Average Latency  = {ms * (numBlocks - 1.5f)} ms");
    }
#if UNITY_EDITOR
    private void OnPauseStateChanged(PauseState state)
    {
        Debug.Log($"OnApplicationPause: {state}");
        lastPauseState = state;
        if (state == PauseState.Paused)
            channelGroup.setPaused(true);
        else
            channelGroup.setPaused(false);
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

    public void FingerMove(Finger obj)
    {
        channelGroup.getDSPClock(out var dspClock, out var parentClock);
        system.getSoftwareFormat(out var sampleRate, out _, out _);
        // WriteTxt(Application.streamingAssetsPath + "/log.log", "Finger Move on clock: " + (double)parentclock / samplerate * 1000 + ", " + Time.time);
    }

    private void WriteTxt(string filePath, string message)
    {
        var directoryInfo = new DirectoryInfo(Path.GetDirectoryName(filePath));

        if (!directoryInfo.Exists) directoryInfo.Create();

        // This text is added only once to the file.
        if (!File.Exists(filePath))
            // Create a file to write to.
        {
            using var sw = File.CreateText(filePath);
            sw.WriteLine(message);
        }
        else
            // This text is always added, making the file longer over time
            // if it is not deleted.
        {
            using var sw = File.AppendText(filePath);
            sw.WriteLine(message);
        }
    }
}
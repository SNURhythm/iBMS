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
    private FMOD.Sound _music;
    private FMOD.Channel _channel;
    private FMOD.ChannelGroup _channelGroup;
#if UNITY_EDITOR
    private PauseState _lastPauseState = PauseState.Unpaused;
#endif

    void Awake()
    {
        Application.targetFrameRate = 120;
    }

    // Start is called before the first frame update
    void Start()
    {

#if UNITY_EDITOR
        EditorApplication.pauseStateChanged += OnPauseStateChanged;
        _lastPauseState = EditorApplication.isPaused ? PauseState.Paused : PauseState.Unpaused;
#endif
        LoadGame();
    }

    // Update is called once per frame
    void Update()
    {


    }

    private void FixedUpdate()
    {
        Debug.Log("FixedUpdate "+Time.time);
        _channel.getDSPClock(out ulong dspclock, out ulong parentclock);
        FMODUnity.RuntimeManager.CoreSystem.getSoftwareFormat(out var samplerate, out _, out _);
        WriteTxt(Application.streamingAssetsPath+"/log.log","FixedUpdate: " + (double)dspclock/samplerate * 1000 +", "+Time.time);

    }

    private void LoadGame()
    {
        FMOD.RESULT result;

        // set buffer size
        result = FMODUnity.RuntimeManager.CoreSystem.setDSPBufferSize(256, 1);
        if (result != FMOD.RESULT.OK) Debug.Log($"setDSPBufferSize failed. {result}");

        FMODUnity.RuntimeManager.CoreSystem.init(256, FMOD.INITFLAGS.NORMAL, IntPtr.Zero);
        // set defaultDecodeBufferSize
        var advancedSettings = new FMOD.ADVANCEDSETTINGS
        {
            defaultDecodeBufferSize = 32
        };
        result = FMODUnity.RuntimeManager.CoreSystem.setAdvancedSettings(ref advancedSettings);
        if (result != FMOD.RESULT.OK) Debug.Log($"setAdvancedSettings failed. {result}");

        uint blocksize;
        int numblocks;
        float ms;
        int frequency;
        result = FMODUnity.RuntimeManager.CoreSystem.getDSPBufferSize(out blocksize, out numblocks);
        result = FMODUnity.RuntimeManager.CoreSystem.getSoftwareFormat(out frequency, out _, out _);
        FMODUnity.RuntimeManager.CoreSystem.getMasterChannelGroup(out _channelGroup);
        _channel = new FMOD.Channel();
        _channel.setChannelGroup(_channelGroup);
        result = FMODUnity.RuntimeManager.CoreSystem.createSound(Application.streamingAssetsPath + "/testbms/bgm_1.wav",
            FMOD.MODE.CREATESAMPLE | FMOD.MODE.ACCURATETIME, out _music);
        if (result != FMOD.RESULT.OK) Debug.Log($"createSound failed. {result}");
        FMODUnity.RuntimeManager.CoreSystem.playSound(_music, _channelGroup, false, out _channel);
#if UNITY_EDITOR
        if (_lastPauseState == PauseState.Paused) _channel.setPaused(true);
#endif
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
            _channel.setPaused(true);
        }
        else
        {
            _channel.setPaused(false);
        }
    }
#endif
    
    public void FingerMove(Finger obj)
    {
        _channel.getDSPClock(out ulong dspclock, out ulong parentclock);
        FMODUnity.RuntimeManager.CoreSystem.getSoftwareFormat(out var samplerate, out _, out _);
        WriteTxt(Application.streamingAssetsPath+"/log.log","Finger Move on clock: " + (double)parentclock/samplerate * 1000 +", "+Time.time);
    }
    
    void WriteTxt(string filePath, string message)
    {
        DirectoryInfo directoryInfo = new DirectoryInfo(Path.GetDirectoryName(filePath));

        if (!directoryInfo.Exists)
        {
            directoryInfo.Create();
        }

       // This text is added only once to the file.
        if (!File.Exists(filePath)) {
            // Create a file to write to.
            using (StreamWriter sw = File.CreateText(filePath))
            {
                sw.WriteLine(message);
            }   
        } else {
            // This text is always added, making the file longer over time
            // if it is not deleted.
            using (StreamWriter sw = File.AppendText(filePath)) {
                sw.WriteLine(message);
            }   
        }
        
    }
}

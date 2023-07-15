using System.Collections;
using System.Collections.Generic;
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
public class RhythmControl : MonoBehaviour
{
    private FMOD.Sound _music;
    private FMOD.Channel _channel;
    private FMOD.ChannelGroup _channelGroup;
#if UNITY_EDITOR
    private PauseState _lastPauseState = PauseState.Unpaused;
#endif
    // Start is called before the first frame update
    void Start()
    {
        LoadGame();
#if UNITY_EDITOR
        EditorApplication.pauseStateChanged += OnPauseStateChanged;
#endif
    }

    // Update is called once per frame
    void Update()
    {
        
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
        result = FMODUnity.RuntimeManager.CoreSystem.createSound(Application.streamingAssetsPath+"/testbms/bgm_1.wav",
            FMOD.MODE.CREATESAMPLE | FMOD.MODE.ACCURATETIME, out _music);
        if (result != FMOD.RESULT.OK) Debug.Log($"createSound failed. {result}");
        FMODUnity.RuntimeManager.CoreSystem.playSound(_music, _channelGroup, false, out _channel);
#if UNITY_EDITOR
        if(_lastPauseState == PauseState.Paused) _channel.setPaused(true);
#endif
        ms = (float) blocksize * 1000.0f / (float) frequency;

        Debug.Log($"Mixer blocksize        = {ms} ms");
        Debug.Log($"Mixer Total buffersize = {ms * numblocks} ms");
        Debug.Log($"Mixer Average Latency  = {ms * ((float) numblocks - 1.5f)} ms");
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
}

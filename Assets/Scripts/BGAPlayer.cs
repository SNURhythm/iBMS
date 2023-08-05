using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Video;


class BGAPlayerState
{
    // schedules

    public SortedDictionary<long, List<int>> Schedules = new();

    public void Dispose()
    {
        Schedules.Clear();
    }
}
public class BGAPlayer
{
    private static readonly string[] extensions = { "mp4", "wmv", "m4v", "webm", "mpg", "mpeg", "m1v", "m2v", "avi" };
    private readonly Dictionary<int, VideoPlayer> players = new();
    private BGAPlayerState state = new();

    public int TotalPlayers { get; private set; }
    public int LoadedPlayers { get; private set; }

    // event on all players loaded
    public event EventHandler OnAllPlayersLoaded;

    public void Load(int id, string path)
    {
        // check extension
        var ext = path.Substring(path.LastIndexOf('.') + 1);
        if (Array.IndexOf(extensions, ext) == -1)
        {
            Debug.Log("Unsupported BGA file extension: " + ext);
            return;
        }
        var player = Camera.main.gameObject.AddComponent<VideoPlayer>();

        Debug.Log("Loading BGA player " + id + " from " + path);
        Debug.Log(player);
        try
        {
            player.playOnAwake = false;
            player.source = VideoSource.Url;
            player.url = path;
            player.timeReference = VideoTimeReference.ExternalTime;
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.renderMode = VideoRenderMode.CameraFarPlane;
            player.targetCameraAlpha = 0.3f;
            player.targetCamera = Camera.main;
            player.isLooping = false;
            player.prepareCompleted += OnPrepareCompleted;
            player.errorReceived += (source, message) =>
            {
                Debug.Log("BGA player " + id + " error: " + message);
                OnPrepareCompleted(player);
            };
            player.Prepare();
            players.Add(id, player);
            TotalPlayers++;
        } catch (Exception e)
        {
            Debug.Log("Failed to load BGA player " + id + " from " + path);
            Debug.Log(e);
            OnPrepareCompleted(player);
        }
    }

    private void OnPrepareCompleted(VideoPlayer source)
    {
        LoadedPlayers++;
        if (LoadedPlayers == TotalPlayers)
        {
            foreach (var player in players.Values)
            {
                player.prepareCompleted -= OnPrepareCompleted;
                player.skipOnDrop = true;
            }

            Debug.Log("All BGA players are prepared");
            OnAllPlayersLoaded?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Update(long timeMicro)
    {
        foreach (var (time, ids) in state.Schedules)
        {
            foreach (var id in ids)
            {
                if (!players.ContainsKey(id)) continue;
                // Debug.Log("updating " + id + " at " + timeMicro);
                if (timeMicro >= time)
                {
                    players[id].skipOnDrop = true;
                    players[id].externalReferenceTime = (timeMicro - time) / 1000000d;
                    if (!players[id].isPlaying) players[id].Play();

                }
            }
        }
    }
    
    public void ResumeAll(long timeMicro)
    {
        foreach (var (time, ids) in state.Schedules)
        foreach (var id in ids)
        {
            if (!players.ContainsKey(id)) continue;
            if (timeMicro >= time)
            {

                
                players[id].skipOnDrop = true;
                players[id].externalReferenceTime = (timeMicro - time) / 1000000d;
                
                players[id].time = (timeMicro - time) / 1000000d;
                players[id].Play();
            }
        }
    }
    
    
    public void PauseAll()
    {
        foreach (var player in players.Values)
            player.Pause();
    }

    public void Schedule(int id, long time)
    {
        if(!players.ContainsKey(id)) return;
        Debug.Log("Scheduling " + id + " at " + time);
        if (!state.Schedules.ContainsKey(time))
            state.Schedules.Add(time, new List<int>());
        
        state.Schedules[time].Add(id);
    }

    public void Reset()
    {
        state?.Dispose();
        state = new BGAPlayerState();
        foreach (var player in players.Values)
            player.Stop();
    }
}
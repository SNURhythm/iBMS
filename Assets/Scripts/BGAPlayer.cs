using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class BGAPlayer
{
    private static readonly string[] extensions = { "mp4", "wmv", "m4v", "webm", "mpg", "mpeg", "m1v", "m2v", "avi" };
    private readonly Dictionary<int, VideoPlayer> players = new();

    // schedules
    public SortedDictionary<long, List<int>> Schedules = new();
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
        Debug.Log("What");
        
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
        foreach (var (time, ids) in Schedules)
        {
            foreach (var id in ids)
            {
                if (!players.ContainsKey(id)) continue;
                Debug.Log("updating " + id + " at " + timeMicro);
                if (timeMicro >= time)
                {
                    players[id].skipOnDrop = true;
                    players[id].externalReferenceTime = (timeMicro - time) / 1000000d;
                    if (!players[id].isPlaying) SafePlay(id);

                }
            }
        }
    }
    
    public void ResumeAll(long timeMicro)
    {
        foreach (var (time, ids) in Schedules)
        foreach (var id in ids)
        {
            if (!players.ContainsKey(id)) continue;
            if (timeMicro >= time)
            {
                players[id].skipOnDrop = true;
                players[id].externalReferenceTime = (timeMicro - time) / 1000000d;
                SafePlay(id);
                players[id].time = (timeMicro - time) / 1000000d;
            }
        }
    }

    private void SafePlay(int id)
    {
        try
        {
            players[id].Play();
        }
        catch (Exception e)
        {
            
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
        if (!Schedules.ContainsKey(time))
            Schedules.Add(time, new List<int>());
        
        Schedules[time].Add(id);
    }
}
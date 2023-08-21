using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using transcoding;
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

    public BGAPlayer()
    {
        if (Application.platform == RuntimePlatform.IPhonePlayer)
        {
            ffmpeg.RootPath = Application.dataPath + "/Binaries/iOS/ffmpeg";
        }
        else
        {
            ffmpeg.RootPath = Application.dataPath + "/Binaries/Windows/ffmpeg/bin/x64";
        }
    }

    public unsafe void Transcode(string input, string output)
    {
        FFmpegTranscoder transcoder = new FFmpegTranscoder();
        transcoder.Transcode(input, output);
    }
    public void Dispose()
    {
        state?.Dispose();
        foreach (var player in players.Values)
        {
            player.Stop();
            player.targetCamera = null;
        }
    }

    private static AVPixelFormat GetHWPixelFormat(AVHWDeviceType hWDevice)
    {
        return hWDevice switch
        {
            AVHWDeviceType.AV_HWDEVICE_TYPE_NONE => AVPixelFormat.AV_PIX_FMT_NONE,
            AVHWDeviceType.AV_HWDEVICE_TYPE_VDPAU => AVPixelFormat.AV_PIX_FMT_VDPAU,
            AVHWDeviceType.AV_HWDEVICE_TYPE_CUDA => AVPixelFormat.AV_PIX_FMT_CUDA,
            AVHWDeviceType.AV_HWDEVICE_TYPE_VAAPI => AVPixelFormat.AV_PIX_FMT_VAAPI,
            AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2 => AVPixelFormat.AV_PIX_FMT_NV12,
            AVHWDeviceType.AV_HWDEVICE_TYPE_QSV => AVPixelFormat.AV_PIX_FMT_QSV,
            AVHWDeviceType.AV_HWDEVICE_TYPE_VIDEOTOOLBOX => AVPixelFormat.AV_PIX_FMT_VIDEOTOOLBOX,
            AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA => AVPixelFormat.AV_PIX_FMT_NV12,
            AVHWDeviceType.AV_HWDEVICE_TYPE_DRM => AVPixelFormat.AV_PIX_FMT_DRM_PRIME,
            AVHWDeviceType.AV_HWDEVICE_TYPE_OPENCL => AVPixelFormat.AV_PIX_FMT_OPENCL,
            AVHWDeviceType.AV_HWDEVICE_TYPE_MEDIACODEC => AVPixelFormat.AV_PIX_FMT_MEDIACODEC,
            _ => AVPixelFormat.AV_PIX_FMT_NONE
        };
    }

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
        // first, convert to mp4 via Remux
        var outPath = Path.Combine(Application.temporaryCachePath, "bga" + id + ".mp4");
        if (File.Exists(outPath))
        {
            File.Delete(outPath);
        }
        
        Transcode(path, outPath);

        try
        {
            player.playOnAwake = false;
            player.source = VideoSource.Url;
            player.url = outPath;
            player.timeReference = VideoTimeReference.ExternalTime;
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.renderMode = VideoRenderMode.CameraFarPlane;
            player.targetCameraAlpha = 0.3f;
            player.targetCamera = Camera.main;
            player.isLooping = false;
            players.Add(id, player);
            player.prepareCompleted += OnPrepareCompleted;
            player.errorReceived += (source, message) =>
            {
                player.Stop();
                Debug.Log("BGA player " + id + " error: " + message);
                players.Remove(id);
                OnPrepareCompleted(player);
            };
            player.Prepare();

            TotalPlayers++;
        }
        catch (Exception e)
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
        if (!players.ContainsKey(id)) return;
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
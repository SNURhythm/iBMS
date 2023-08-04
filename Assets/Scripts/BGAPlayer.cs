using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;


public class BGAPlayer: MonoBehaviour
{
    private static readonly string[] extensions = { "mp4", "wmv", "m4v", "webm", "mpg", "mpeg", "m1v", "m2v", "avi" };
    private readonly Dictionary<int, object> players = new();
    int currentPlayingId = -1;
    long currentTimeMicro;

    private Texture2D texture;
    // schedules
    public SortedDictionary<long, List<int>> Schedules = new();
    public int TotalPlayers { get; private set; }
    public int LoadedPlayers { get; private set; }


    private int lastRenderedFrame = -1;
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

        try
        {
            // if ext is mpg/mpeg/m1v, use MPGDecoder
            if (ext is "mpg" or "mpeg" or "m1v")
            {
                var player = new MPGDecoder();
                player.OnReadyToPlay += (sender, args) =>
                {
                    
                };
                player.InitDecoder(path);
                texture = new Texture2D(player.Width, player.Height, TextureFormat.RGB24, false);

                MarkAsLoaded();
                players.Add(id, player);
            }
            else
            {
                var player = gameObject.AddComponent<VideoPlayer>();
                Debug.Log("Loading BGA player " + id + " from " + path);
                Debug.Log(player);
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
            }

            TotalPlayers++;
        } catch (Exception e)
        {
            Debug.Log("Failed to load BGA player " + id + " from " + path);
            Debug.Log(e);
            MarkAsLoaded();
        }
    }

    private void MarkAsLoaded()
    {
        LoadedPlayers++;
        if (LoadedPlayers == TotalPlayers)
        {
            OnAllPlayersLoaded?.Invoke(this, EventArgs.Empty);
        }

    }

    private void OnPrepareCompleted(VideoPlayer source)
    {
        MarkAsLoaded();
        source.prepareCompleted -= OnPrepareCompleted;
        source.skipOnDrop = true;
    }

    public void Draw(long timeMicro)
    {
        currentTimeMicro = timeMicro;


        foreach (var (time, ids) in Schedules)
        {
            foreach (var id in ids)
            {
                if (!players.ContainsKey(id)) continue;
                // Debug.Log("updating " + id + " at " + timeMicro);
                currentPlayingId = id;
                if (timeMicro >= time)
                {
                    if (players[id] is VideoPlayer videoPlayer)
                    {

                        videoPlayer.skipOnDrop = true;
                        videoPlayer.externalReferenceTime = (timeMicro - time) / 1000000d;
                        if (!videoPlayer.isPlaying) SafePlay(id);
                    }

                    if (players[id] is MPGDecoder mpgDecoder)
                    {
                        var currentFrame = (int) ((timeMicro - time) / 1000000d * mpgDecoder.Framerate);
                        if (currentFrame != lastRenderedFrame)
                        {
                            lastRenderedFrame = currentFrame;
                            mpgDecoder.ReadFrame();
                        }
                    }
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
            if (players[id] is VideoPlayer videoPlayer)
            {
                if (timeMicro >= time)
                {
                    videoPlayer.skipOnDrop = true;
                    videoPlayer.externalReferenceTime = (timeMicro - time) / 1000000d;
                    SafePlay(id);
                    videoPlayer.time = (timeMicro - time) / 1000000d;
                }
            }
        }
    }

    private void SafePlay(int id)
    {
        try
        {
            if (players[id] is VideoPlayer videoPlayer)
                videoPlayer.Play();
            
        }
        catch (Exception e)
        {
            
        }
    }
    
    public void PauseAll()
    {
        foreach (var player in players.Values)
            if (player is VideoPlayer videoPlayer)
                videoPlayer.Pause();
    }

    public void Schedule(int id, long time)
    {
        if(!players.ContainsKey(id)) return;
        Debug.Log("Scheduling " + id + " at " + time);
        if (!Schedules.ContainsKey(time))
            Schedules.Add(time, new List<int>());
        
        Schedules[time].Add(id);
    }
    
    private static void VerticallyFlipRenderTexture(RenderTexture target)
    {
        var temp = RenderTexture.GetTemporary(target.descriptor);
        Graphics.Blit(target, temp, new Vector2(1, -1), new Vector2(0, 1));
        Graphics.Blit(temp, target);
        RenderTexture.ReleaseTemporary(temp);
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (currentPlayingId == -1) return;
        if (!players.ContainsKey(currentPlayingId)) return;
        if (players[currentPlayingId] is not MPGDecoder player) return;
        var width = player.Width;
        var height = player.Height;


        if (player.data!=null)
        {


            texture.LoadRawTextureData(player.data);
            texture.Apply();
            Graphics.Blit(texture, destination);
            VerticallyFlipRenderTexture(destination);
        }
        else
        {
            Graphics.Blit(source, destination);
        }
    }
}
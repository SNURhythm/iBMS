using System;
using System.Collections;
using System.Collections.Generic;
using PLMpegSharp;
using PLMpegSharp.Container;
using UnityEngine;

public class BGACamera : MonoBehaviour
{
    private List<byte[]> frames = new List<byte[]>();

    private int width;
    private int height;

    private Texture2D texture;
    // Start is called before the first frame update
    void Start()
    {
        Application.targetFrameRate = 30;
        DataBuffer buffer =
            DataBuffer.CreateWithFilename(Application.persistentDataPath + "/take003/bga_take.mpg");
        VideoDecoder decoder = new VideoDecoder(buffer);

        // Task.Run(() =>
        // {

        var current = 0;


        var startTime = DateTime.Now;
        Debug.Log("gogogogo");
        while (decoder.HasEnded == false)
        {

            Frame frame = decoder.Decode();
            try
            {
                byte[] data = new byte[frame.Width * frame.Height * 3];
                width = frame.Width;
                height = frame.Height;
                frame.ToRGB(data, decoder.Width * 3);
                frames.Add(data);
                // Texture2D texture = new Texture2D(frame.Width, frame.Height, TextureFormat.RGB24, false);
                // texture.LoadRawTextureData(data);
                // texture.Apply();
                //
                // RenderTexture renderTexture = new RenderTexture(frame.Width, frame.Height, 0);
                // Graphics.Blit(texture, renderTexture);
                // Camera.main.targetTexture = renderTexture;

            }
            catch (Exception e)
            {
                Debug.Log(e);
            }

            current++;
            if (current == 600) break;


        }
        

        Debug.Log("MPGDecodeTime: " + (DateTime.Now - startTime).TotalMilliseconds);

        // });


    }

    // Update is called once per frame
    void Update()
    {
        currentFrame++;
    }
    int currentFrame = 0;
    private static void VerticallyFlipRenderTexture(RenderTexture target)
    {
        var temp = RenderTexture.GetTemporary(target.descriptor);
        Graphics.Blit(target, temp, new Vector2(1, -1), new Vector2(0, 1));
        Graphics.Blit(temp, target);
        RenderTexture.ReleaseTemporary(temp);
    }
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (currentFrame < frames.Count)
        {

            var frame = frames[currentFrame];
            texture.LoadRawTextureData(frame);
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

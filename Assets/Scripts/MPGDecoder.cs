using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PLMpegSharp;
using PLMpegSharp.Container;
using UnityEngine;

public class MPGDecoder: IDisposable
{
    private CancellationTokenSource cancellationTokenSource;
    public event EventHandler<byte[]> OnFrame;
    public event EventHandler OnEnd;
    public event EventHandler OnReadyToPlay;
    public int Width;
    public int Height;
    public double Framerate;
    private int lastFrame = 0;
    private VideoDecoder decoder;
    public byte[] data;
    public void InitDecoder(string path)
    {
        DataBuffer buffer =
            DataBuffer.CreateWithFilename(path);
        decoder = new VideoDecoder(buffer);
        Framerate = decoder.Framerate;
        Width = decoder.Width;
        Height = decoder.Height;
        data = new byte[Width * Height * 3];
    }
    
    public void ReadFrame()
    {
        if(decoder.HasEnded) return;

        cancellationTokenSource = new CancellationTokenSource();

        
        // var task = Task.Run(() =>
        // {

            var startTime = DateTime.Now;
            Frame frame = decoder.Decode();
            try
            {

                frame.ToRGB(data, decoder.Width * 3);

  

            }
            catch (Exception e)
            {
                Debug.Log(e);
            }



        // }, cancellationTokenSource.Token);
    }

    public  void GetLastAvailableFrame(long timeMicro, byte[] frame)
    {
        // var currentFrame = (int) (timeMicro / (1000000 / Framerate));
        //
        // stream.Seek(currentFrame * Width * Height * 3, SeekOrigin.Begin);
        // stream.Read(frame);

    }
    
    public void FreeUntil(long timeMicro)
    {
        // var currentFrame = (int) (timeMicro / (1000000 / Framerate));
        // stream.Seek(currentFrame * Width * Height * 3, SeekOrigin.Begin);
        
    }

    public void Dispose()
    {
        cancellationTokenSource?.Cancel();
        Debug.Log("MPGDecoder disposed");
    }
}

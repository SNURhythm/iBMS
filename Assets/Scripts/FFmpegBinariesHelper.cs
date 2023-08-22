using System;
using System.IO;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen.Bindings.DynamicallyLoaded;
using FFmpeg.AutoGen.Bindings.StaticallyLinked;
using UnityEngine;
using Application = UnityEngine.Device.Application;

public class FFmpegBinariesHelper
{
    internal static void RegisterFFmpegBinaries()
    {
        if (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer)
        {
            var ffmpegBinaryPath = Path.Combine(Application.streamingAssetsPath, "Binaries","Windows", "ffmpeg","bin", Environment.Is64BitProcess ? "x64" : "x86");
FFmpeg.AutoGen.DynamicallyLoadedBindings.Initialize();
            DynamicallyLoadedBindings.LibrariesPath = ffmpegBinaryPath;
        }
        else if (Application.platform == RuntimePlatform.IPhonePlayer)
        {
            var ffmpegBinaryPath = Path.Combine(Application.streamingAssetsPath, "Binaries", "iOS", "ffmpeg", "lib");
            StaticallyLinkedBindings.Initialize();
        }
    }
}
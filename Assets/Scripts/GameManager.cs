
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Microsoft.IO;
using UnityEngine;

public sealed class GameManager
{
    readonly RecyclableMemoryStreamManager _rmsm = new RecyclableMemoryStreamManager();
    // singleton
    public static readonly GameManager Instance = new GameManager();

    GameManager()
    {
        _rmsm.AggressiveBufferReturn = true;
        _rmsm.MaximumFreeSmallPoolBytes = _rmsm.LargeBufferMultiple * 4;
        _rmsm.MaximumFreeLargePoolBytes = 100 * _rmsm.BlockSize;
        Task.Run(() =>
        {
            var testArray = new byte[1024];
            for (var i = 0; i < 1024; i++)
            {
                testArray[i] = (byte) i;
            }
            var rangePartitioner = Partitioner.Create(0, 100000);
            Parallel.ForEach(rangePartitioner, (range, loopState) =>
            {
                Logger.Log("Current processor Id on GameManager: " + System.Threading.Thread.GetCurrentProcessorId());
                for (var i = range.Item1; i < range.Item2; i++)
                {
 
                }
            });
        });
    }

    public string BmsPath;
    public bool AutoPlay = false;
    public bool KeySound = true;
    public int KeyMode = 5;
    
    public MemoryStream GetMemoryStream(byte[] data)
    {
        return _rmsm.GetStream(data);
    }
}

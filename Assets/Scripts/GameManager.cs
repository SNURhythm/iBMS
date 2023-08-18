
using System.IO;
using Microsoft.IO;

public class GameManager
{
    readonly RecyclableMemoryStreamManager _rmsm = new RecyclableMemoryStreamManager();
    // singleton
    public static GameManager Instance = new GameManager();

    GameManager()
    {
        _rmsm.AggressiveBufferReturn = true;
        _rmsm.MaximumFreeSmallPoolBytes = _rmsm.LargeBufferMultiple * 4;
        _rmsm.MaximumFreeLargePoolBytes = 100 * _rmsm.BlockSize;
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

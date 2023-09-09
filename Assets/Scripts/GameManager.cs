﻿
public class GameManager
{
    // singleton
    private static GameManager instance;
    public static GameManager Instance => instance ??= new GameManager();
    
    public string BmsPath;
    public bool AutoPlay = true;
    public bool KeySound = true;
    public int KeyMode = 5;
}

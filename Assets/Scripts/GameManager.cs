
public class GameManager
{
    // singleton
    private static GameManager _instance;
    public static GameManager Instance => _instance ?? (_instance = new GameManager());
    
    public string bmsPath;
}

public class Note
{
    public int Lane;
    public TimeLine Timeline;
    public double Bpm;
    public readonly int Wav;
    
    public bool IsPlayed = false;
    public ulong PlayedTime = 0;

    // 레인
    // private Note nextNote;
    public Note(int wav)
    {
        Wav = wav;
    }
}
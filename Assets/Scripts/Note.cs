public class Note
{
    public int Lane;
    public TimeLine Timeline;

    public readonly int Wav;

    // 레인
    // private Note nextNote;
    public Note(int wav)
    {
        Wav = wav;
    }
}
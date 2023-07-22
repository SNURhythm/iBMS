public class Note
{
    public int Lane;
    public TimeLine Timeline;
    public double Bpm;
    public readonly int Wav;

    public bool IsPlayed { get; private set; } = false;
    public bool IsDead = false;
    public long PlayedTime { get; private set; } = 0;

    // 레인
    // private Note nextNote;
    public Note(int wav)
    {
        Wav = wav;
    }

    protected void Play(long time)
    {
        IsPlayed = true;
        PlayedTime = time;
    }

    public void Press(long time)
    {
        Play(time);
    }
}
public class LongNote : Note
{
    public LongNote End;

    public LongNote(int wav) : base(wav)
    {
    }

    public bool IsEnd()
    {
        return End == null;
    }
}
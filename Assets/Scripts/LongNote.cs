public class LongNote : Note
{
    public LongNote end;

    public LongNote(int wav) : base(wav)
    {
    }

    public bool isEnd()
    {
        return end == null;
    }
}
public class LongNote : Note
{
    public LongNote Tail;
    public LongNote Head;

    public LongNote(int wav) : base(wav)
    {
    }

    public bool IsTail()
    {
        return Tail == null;
    }
}
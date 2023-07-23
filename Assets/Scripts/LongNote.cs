using UnityEngine;

public class LongNote : Note
{
    public LongNote Tail;
    public LongNote Head;
    public bool IsHolding { get; private set; }
    public bool IsTail => Tail == null;
    public long ReleaseTime { get; private set; }
    public LongNote(int wav) : base(wav)
    {
    }


    public new void Press(long time)
    {
        Play(time);
        IsHolding = true;
        Tail.IsHolding = true;
    }

    public void Release(long time)
    {
        Play(time);
        IsHolding = false;
        Head.IsHolding = false;
        ReleaseTime = time;
    }

    public void MissRelease(long time)
    {
        IsHolding = false;
        Head.IsHolding = false;
        ReleaseTime = time;
    }

    public void MissPress(long time)
    {

    }

}
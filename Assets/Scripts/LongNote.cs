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


    public override void Press(long time)
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

    public void MissPress(long time)
    {

    }
    
    public override void Reset()
    {
        base.Reset();
        IsHolding = false;
        ReleaseTime = 0;
    }

}
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

[SuppressMessage("ReSharper", "IdentifierTypo")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public enum Judgement
{
    PGREAT,
    GREAT,
    GOOD,
    BAD,
    KPOOR,
    NONE
}
public struct JudgeResult
{

    public JudgeResult(Judgement judgement, long diff)
    {
        Judgement = judgement;
        Diff = diff;
    }

    public Judgement Judgement { get; }
    public long Diff { get; }
    public bool ShouldComboBreak
    {
        get => Judgement == Judgement.BAD;
    }
    public bool IsNotePlayed
    {
        get => Judgement != Judgement.KPOOR && Judgement != Judgement.NONE;
    }
}
public class Judge
{


    private static readonly Dictionary<Judgement, (long early, long late)>[] judgeWindowTableByRank = new[]
    {
        new Dictionary<Judgement, (long, long)>{
            [Judgement.PGREAT] = (-5000, 5000),
            [Judgement.GREAT] = (-15000, 15000),
            [Judgement.GOOD] = (-37500, 37500),
            [Judgement.BAD] = (-385000, 490000),
            [Judgement.KPOOR] = (-500000, 150000)
        },
        new Dictionary<Judgement, (long, long)>{
            [Judgement.PGREAT] = (-10000, 10000),
            [Judgement.GREAT] = (-30000, 30000),
            [Judgement.GOOD] = (-75000, 75000),
            [Judgement.BAD] = (-330000, 420000),
            [Judgement.KPOOR] = (-500000, 150000)
        },
        new Dictionary<Judgement, (long, long)>{
            [Judgement.PGREAT] = (-15000, 15000),
            [Judgement.GREAT] = (-45000, 45000),
            [Judgement.GOOD] = (-112500, 112500),
            [Judgement.BAD] = (-275000, 350000),
            [Judgement.KPOOR] = (-500000, 150000)
        },
        new Dictionary<Judgement, (long, long)>{
            [Judgement.PGREAT] = (-20000, 20000),
            [Judgement.GREAT] = (-60000, 60000),
            [Judgement.GOOD] = (-150000, 150000),
            [Judgement.BAD] = (-220000, 280000),
            [Judgement.KPOOR] = (-500000, 150000)
        }
    };
    private Dictionary<Judgement, (long early, long late)> judgeWindowTable;


    public Judge(int rank)
    {
        judgeWindowTable = judgeWindowTableByRank[rank];
    }

    private static bool CheckRange(long value, long early, long late)
    {
        return early <= value && value <= late;
    }

    public JudgeResult JudgeNow(Note note, long inputTime)
    {
        var timeline = note.Timeline;
        var diff = inputTime - timeline.Timing;
        foreach (var judgement in new[] { Judgement.PGREAT, Judgement.GREAT, Judgement.GOOD, Judgement.BAD, Judgement.KPOOR })
        {
            if (CheckRange(diff, judgeWindowTable[judgement].early, judgeWindowTable[judgement].late))
            {
                return new JudgeResult(judgement, diff);
            }
        }
        return new JudgeResult(Judgement.NONE, diff);
    }
    
    public static string GetRankDescription(int rank)
    {
        switch (rank)
        {
            case 0:
                return "VERY HARD";
            case 1:
                return "HARD";
            case 2:
                return "NORMAL";
            case 3:
                return "EASY";
            default:
                return "EASY";
        }
    }
}

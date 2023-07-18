﻿using System.Collections.Generic;

public class Measure
{
    public double Scale = 1; // 0 ~ 1
    public long Timing;
    public readonly List<TimeLine> Timelines = new();
}
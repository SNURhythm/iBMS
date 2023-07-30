using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LaneBeamEffect : Effect
{
    private readonly SpriteRenderer sprite;
    private Color color;
    private readonly float duration;
    public LaneBeamEffect(GameObject lineBeam, float duration)
    {
        sprite = lineBeam.GetComponent<SpriteRenderer>();
        color = sprite.color;
        color.a = 0;
        sprite.color = color;
        this.duration = duration;
    }
    // Update is called once per frame

    public override void Tick()
    {
        base.Tick();
        var time = GetCurrentTime();
        if (!IsStarted) return;
        if (time > duration)
        {
            time = duration;
            StopEffect();
        }
        color.a = (1 - (time / duration)) * 0.3f;
        sprite.color = color;
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Effect
{
    protected bool IsStarted = false;
    private bool isPaused = false;
    private float currentTime = 0f;

    // Update is called once per frame
    public virtual void Tick()
    {
        if (IsStarted && !isPaused)
        {
            currentTime += Time.deltaTime;
        }
    }

    public virtual void StartEffect(float timeOffset, bool pause = false)
    {
        currentTime = timeOffset;
        IsStarted = true;
        isPaused = pause;
    }
    public virtual void PauseEffect()
    {
        isPaused = true;
    }
    public virtual void ResumeEffect()
    {
        isPaused = false;
    }
    public virtual void StopEffect()
    {
        IsStarted = false;
        currentTime = 0f;
    }
    public float GetCurrentTime()
    {
        return currentTime;
    }
}

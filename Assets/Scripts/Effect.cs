using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Effect
{
    protected bool isStarted = false;
    protected bool isPaused = false;
    private float currentTime = 0f;

    // Update is called once per frame
    public virtual void Tick()
    {
        if (isStarted && !isPaused)
        {
            currentTime += Time.deltaTime;
        }
    }

    public void StartEffect(float timeOffset, bool pause = false)
    {
        currentTime = timeOffset;
        isStarted = true;
        isPaused = pause;
    }
    public void PauseEffect()
    {
        isPaused = true;
    }
    public void ResumeEffect()
    {
        isPaused = false;
    }
    public void StopEffect()
    {
        isStarted = false;
        currentTime = 0f;
    }
    public float GetCurrentTime()
    {
        return currentTime;
    }
}

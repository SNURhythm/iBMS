using UnityEngine;

public class KeyBombEffect : Effect
{
    private bool isFirstTick = true;
    private readonly ParticleSystem particleSystem;
    private readonly float speed;
    public KeyBombEffect(ParticleSystem particleSystem, float speed = 1f)
    {
        this.particleSystem = particleSystem;
        this.speed = speed;
    }
    public override void Tick()
    {
        base.Tick();
        var time = GetCurrentTime();
        if (!IsStarted) return;
        if (time > particleSystem.main.duration/speed)
        {
            StopEffect();
        }
        particleSystem.Simulate(Time.deltaTime*speed, true, isFirstTick);
        isFirstTick = false;
        
    }
    
    public override void StartEffect(float timeOffset, bool pause = false)
    {
        base.StartEffect(timeOffset, pause);
        isFirstTick = true;
    }

}
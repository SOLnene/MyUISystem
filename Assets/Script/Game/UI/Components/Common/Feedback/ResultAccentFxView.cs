using UnityEngine;

public class ResultAccentFxView : MonoBehaviour
{
    [SerializeField]
    ParticleSystem[] particles;

    void Awake()
    {
        if (particles == null || particles.Length == 0)
            particles = GetComponentsInChildren<ParticleSystem>(true);

        Stop();
    }

    public void Play()
    {
        if (particles == null)
            return;

        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i] == null)
                continue;

            particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles[i].Play(true);
        }
    }

    public void Stop()
    {
        if (particles == null)
            return;

        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i] == null)
                continue;

            particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}

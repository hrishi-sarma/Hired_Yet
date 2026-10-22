using UnityEngine;

public class EventAudio : MonoBehaviour
{
    AudioSource src;
    float volume = 1f;

    public void Init(float vol)
    {
        volume = vol;
        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 3f;
        src.maxDistance = 30f;
    }

    /// <summary>Intro voice: the assigned slot wins, otherwise the catalog event/alien clip.</summary>
    public void PlayBefore(string id, AudioClip slot)
    {
        Play(slot != null ? slot : NpcSounds.Event(id), false);
    }

    public void Play(AudioClip clip, bool loop)
    {
        if (src == null) return;
        src.Stop();
        if (clip == null) return;
        src.clip = clip;
        src.loop = loop;
        src.volume = Mathf.Clamp01(volume * AudioDirector.Volume("npcs"));
        src.Play();
    }

    public void Stop()
    {
        if (src != null) src.Stop();
    }
}

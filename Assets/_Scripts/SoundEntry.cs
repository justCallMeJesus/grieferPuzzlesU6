using UnityEngine;

[System.Serializable]
public class SoundEntry
{
    [Tooltip("Unique name used to play this sound via code")]
    public string id;

    public AudioClip clip;

    [Range(0f, 1f)]
    public float volume = 1f;

    [Range(0.1f, 3f)]
    public float pitch = 1f;

    [Tooltip("Randomly offsets pitch by ±this value each play, for variation")]
    [Range(0f, 0.5f)]
    public float pitchVariance = 0f;

    public bool loop = false;

    [HideInInspector]
    public AudioSource source; // assigned at runtime
}
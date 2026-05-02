using UnityEngine;
using System.Collections.Generic;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Sounds")]
    [SerializeField] private SoundEntry[] sounds;

    [Header("Global Settings")]
    [SerializeField][Range(0f, 1f)] private float masterVolume = 1f;

    private Dictionary<string, SoundEntry> soundMap;

    void Awake()
    {
        // Singleton setup — persists across scenes
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeSounds();
    }

    private void InitializeSounds()
    {
        soundMap = new Dictionary<string, SoundEntry>();

        foreach (var sound in sounds)
        {
            if (string.IsNullOrEmpty(sound.id) || sound.clip == null)
            {
                Debug.LogWarning($"[SoundManager] Skipping entry with missing id or clip.");
                continue;
            }

            // Each sound gets its own AudioSource on this GameObject
            AudioSource src = gameObject.AddComponent<AudioSource>();
            src.clip = sound.clip;
            src.volume = sound.volume * masterVolume;
            src.pitch = sound.pitch;
            src.loop = sound.loop;
            src.playOnAwake = false;
            sound.source = src;

            soundMap[sound.id] = sound;
        }
    }

    // ── Public API ──────────────────────────────────────────

    /// <summary>Play a sound by its id.</summary>
    public void Play(string id)
    {
        if (!TryGetSound(id, out var sound)) return;

        sound.source.volume = sound.volume * masterVolume;
        sound.source.pitch = sound.pitch + Random.Range(-sound.pitchVariance, sound.pitchVariance);
        sound.source.Play();
    }

    /// <summary>Play a sound with a one-shot (supports overlapping).</summary>
    public void PlayOneShot(string id)
    {
        if (!TryGetSound(id, out var sound)) return;

        float pitch = sound.pitch + Random.Range(-sound.pitchVariance, sound.pitchVariance);
        sound.source.pitch = pitch;
        sound.source.PlayOneShot(sound.clip, sound.volume * masterVolume);
    }

    /// <summary>Stop a looping or playing sound.</summary>
    public void Stop(string id)
    {
        if (!TryGetSound(id, out var sound)) return;
        sound.source.Stop();
    }

    /// <summary>Check if a sound is currently playing.</summary>
    public bool IsPlaying(string id)
    {
        if (!TryGetSound(id, out var sound)) return false;
        return sound.source.isPlaying;
    }

    /// <summary>Change master volume at runtime (0–1).</summary>
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        foreach (var sound in sounds)
        {
            if (sound.source != null)
                sound.source.volume = sound.volume * masterVolume;
        }
    }

    // ── Helpers ─────────────────────────────────────────────

    private bool TryGetSound(string id, out SoundEntry sound)
    {
        if (soundMap.TryGetValue(id, out sound)) return true;
        Debug.LogWarning($"[SoundManager] Sound '{id}' not found.");
        return false;
    }
}
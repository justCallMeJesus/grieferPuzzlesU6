using UnityEngine;

[DefaultExecutionOrder(-100)]
public class RemoveAudioListner : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        AudioListener listener = GetComponent<AudioListener>();
        if (listener != null)
            Destroy(listener);
    }
}

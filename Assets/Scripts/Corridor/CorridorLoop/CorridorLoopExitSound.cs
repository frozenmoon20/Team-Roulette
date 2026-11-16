
using UnityEngine;

public class CorridorLoopExitSound : MonoBehaviour
{
    [Header("반복 복도 매니저")]
    public CorridorLoopManager manager;

    [Header("출구 문 효과음")]
    public AudioClip doorOpenSound;

    [Range(0f, 1f)]
    public float volume = 0.8f;

    private bool soundPlayed;

    private void Awake()
    {
        if (manager == null)
            manager = GetComponent<CorridorLoopManager>();
    }

    private void Update()
    {
        if (soundPlayed || manager == null)
            return;

        if (!manager.IsCleared)
            return;

        if (manager.exitDoor == null)
            return;

        soundPlayed = true;

        AudioManager.Play(
            doorOpenSound,
            volume
        );
    }
}

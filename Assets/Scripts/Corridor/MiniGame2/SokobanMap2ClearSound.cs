
using UnityEngine;

public class SokobanMap2ClearSound : MonoBehaviour
{
    [Header("소코반 게임 매니저")]
    public SokobanGameManager gameManager;

    [Header("맵2 클리어 효과음")]
    public AudioClip clearSound;

    [Range(0f, 1f)]
    public float soundVolume = 0.8f;

    private bool soundPlayed;

    private void Awake()
    {
        if (gameManager == null)
            gameManager = GetComponent<SokobanGameManager>();
    }

    private void Update()
    {
        if (soundPlayed || gameManager == null)
            return;

        if (!gameManager.IsCleared)
            return;

        // 맵2 클리어 후 한 번만 재생
        soundPlayed = true;

        AudioManager.Play(
            clearSound,
            soundVolume
        );
    }
}

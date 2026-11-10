
using UnityEngine;
using UnityEngine.Events;

public class DrawerPuzzleClicker : MonoBehaviour,
    IInteractable, IHasName
{
    [Header("아이템 이름")]
    public string displayName = "겨울이의 클릭커";

    [Header("획득 효과음")]
    public AudioSource audioSource;
    public AudioClip pickupSound;

    [Header("추가 획득 이벤트")]
    public UnityEvent onCollected;

    DrawerPuzzleManager manager;

    bool canCollect;
    bool collected;

    public string DisplayName => displayName;

    public void Initialize(DrawerPuzzleManager puzzleManager)
    {
        manager = puzzleManager;
        canCollect = false;
        enabled = false;
    }

    public void EnableCollection()
    {
        canCollect = true;
        enabled = true;
    }

    public void Interact()
    {
        if (!canCollect || collected)
            return;

        collected = true;

        // 클릭커 획득 효과음
        if (audioSource != null && pickupSound != null)
            audioSource.PlayOneShot(pickupSound);

        if (manager != null)
            manager.CollectClicker();

        onCollected?.Invoke();

        // 실제 클릭커 모델은 획득 후 숨김
        gameObject.SetActive(false);
    }
}

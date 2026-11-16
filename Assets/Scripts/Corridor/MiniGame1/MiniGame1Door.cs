
using UnityEngine;
using TeamRoulette.UI;

public class MiniGame1Door : MonoBehaviour, IInteractable, IHasName
{
    public string displayName = "";
    public Transform door;

    public float openAngle = 90f;
    public float openSpeed = 3f;

    [Header("문 효과음")]
    public AudioClip lockedSound;
    public AudioClip unlockSound;
    public AudioClip openSound;

    [Range(0f, 1f)]
    public float soundVolume = 0.8f;

    [Min(0f)]
    public float lockedSoundCooldown = 0.3f;

    bool opened;
    float lastLockedSoundTime = -999f;

    Quaternion closedRotation;
    Quaternion openRotation;

    public string DisplayName => displayName;

    void Start()
    {
        if (door == null)
            door = transform;

        closedRotation = door.localRotation;

        openRotation =
            closedRotation *
            Quaternion.Euler(0f, openAngle, 0f);
    }

    void Update()
    {
        if (!opened)
            return;

        door.localRotation =
            Quaternion.Slerp(
                door.localRotation,
                openRotation,
                openSpeed * Time.deltaTime
            );
    }

    public void Interact()
    {
        if (opened)
            return;

        if (!MiniGame1State.HasKey)
        {
            PlayerInteractor.ShowHint("잠겨 있다.");

            if (Time.time - lastLockedSoundTime >=
                lockedSoundCooldown)
            {
                AudioManager.Play(
                    lockedSound,
                    soundVolume
                );

                lastLockedSoundTime = Time.time;
            }

            return;
        }

        opened = true;

        // 잠금 해제 효과음
        AudioManager.Play(
            unlockSound,
            soundVolume
        );

        // 기존 문 열림 동작 시작
        AudioManager.Play(
            openSound,
            soundVolume
        );

        SceneUIContent ui =
            FindFirstObjectByType<SceneUIContent>();

        if (ui != null)
        {
            ui.CompleteObjective("minigame1");
        }

        PlayerInteractor.ShowHint(
            "문이 열렸다."
        );
    }
}

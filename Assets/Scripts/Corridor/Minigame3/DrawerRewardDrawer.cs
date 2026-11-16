
using System.Collections;
using UnityEngine;

// 보상 서랍을 Memory_Safe 금고로 교체한 버전.
// 퍼즐 클리어 -> 잠금 해제
// 플레이어가 금고 클릭 -> 문 열기
// 문이 완전히 열린 후 클릭커 획득 가능

public class DrawerRewardDrawer : MonoBehaviour, IInteractable, IHasName
{
    [Header("Memory_Safe 모델")]
    [Tooltip("Safe_Door 오브젝트")]
    public Transform safeDoor;

    [Tooltip("Safe_Door 아래 Safe_Hinge 오브젝트")]
    public Transform safeHinge;

    [Header("문 열림 설정")]
    [Tooltip("반대쪽으로 열리면 부호를 변경하세요.")]
    public float openAngle = -100f;

    public float openDuration = 1.2f;
    public float unlockDelay = 0.4f;

    [Header("금속 효과음")]
    public AudioSource audioSource;

    public AudioClip lockedSound;
    public AudioClip unlockSound;
    public AudioClip openSound;

    [Range(0f, 1f)]
    public float soundVolume = 0.8f;

    [Header("잠김 효과음 중복 방지")]
    public float lockedSoundCooldown = 0.3f;

    [Header("금고 안의 보상")]
    public GrabbableItem clicker;

    private bool prepared;
    private bool unlocking;
    private bool unlocked;
    private bool opening;
    private bool opened;

    private float lastLockedSoundTime = -999f;

    private Vector3 closedDoorPosition;
    private Quaternion closedDoorRotation;
    private Vector3 hingeWorldPosition;
    private Vector3 hingeWorldAxis;

    public string DisplayName
    {
        get
        {
            if (!unlocked)
                return "잠긴 금고";

            if (opened)
                return "열린 금고";

            return "금고 문 열기";
        }
    }

    // DrawerPuzzleManager.Start()에서 호출
    public void Prepare(DrawerPuzzleManager manager)
    {
        Transform door = safeDoor != null
            ? safeDoor
            : transform;

        prepared = true;
        unlocking = false;
        unlocked = false;
        opening = false;
        opened = false;

        lastLockedSoundTime = -999f;

        closedDoorPosition = door.position;
        closedDoorRotation = door.rotation;

        // 경첩의 회전 전 월드 좌표 저장
        if (safeHinge != null)
        {
            hingeWorldPosition = safeHinge.position;
        }
        else
        {
            Debug.LogError(
                "Memory_Safe: Safe Hinge를 연결해 주세요.",
                this
            );
        }

        // 기존 회전축 계산 방식 유지
        hingeWorldAxis = door.parent != null
            ? door.parent.up
            : Vector3.up;

        if (hingeWorldAxis.sqrMagnitude < 0.0001f)
            hingeWorldAxis = Vector3.up;

        hingeWorldAxis.Normalize();

        // 금고가 열리기 전에는 클릭커 숨김
        if (clicker != null)
        {
            clicker.enabled = false;
            clicker.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning(
                "Memory_Safe: Clicker가 연결되지 않았습니다.",
                this
            );
        }
    }

    // 퍼즐 클리어 시 호출
    // 잠금만 해제하며 문은 열지 않음
    public void OpenDrawer()
    {
        if (!prepared || unlocking || unlocked)
            return;

        StartCoroutine(UnlockRoutine());
    }

    private IEnumerator UnlockRoutine()
    {
        unlocking = true;

        yield return new WaitForSeconds(
            Mathf.Max(0f, unlockDelay)
        );

        unlocked = true;
        unlocking = false;

        PlaySound(unlockSound);

        Debug.Log(
            "Memory_Safe: 철컥! 잠금 해제 완료 (문은 아직 닫힘)"
        );

        PlayerInteractor.ShowHint(
            "철컥! 금고 잠금이 풀렸다. 문을 눌러 열자."
        );
    }

    // 금고 문 좌클릭 상호작용
    public void Interact()
    {
        // 금고가 잠겨 있거나 해제 중인 경우
        if (!prepared || unlocking || !unlocked)
        {
            PlayLockedSound();

            PlayerInteractor.ShowHint(
                "금고가 잠겨 있다."
            );

            return;
        }

        if (opening)
            return;

        if (opened)
        {
            PlayerInteractor.ShowHint(
                "금고 안의 물건을 확인하자."
            );

            return;
        }

        if (safeHinge == null)
        {
            Debug.LogError(
                "Memory_Safe: Safe Hinge가 없어 문을 열 수 없습니다.",
                this
            );

            return;
        }

        StartCoroutine(OpenRoutine());
    }

    // 잠긴 금고 효과음
    private void PlayLockedSound()
    {
        if (Time.time - lastLockedSoundTime <
            lockedSoundCooldown)
        {
            return;
        }

        lastLockedSoundTime = Time.time;

        PlaySound(lockedSound);
    }

    // 금고 효과음 재생
    private void PlaySound(AudioClip clip)
    {
        if (clip == null)
            return;

        if (audioSource != null)
        {
            audioSource.PlayOneShot(
                clip,
                soundVolume
            );
        }
        else
        {
            AudioManager.Play(
                clip,
                soundVolume
            );
        }
    }

    private IEnumerator OpenRoutine()
    {
        opening = true;

        // 문이 열리기 시작하면 클릭커 표시
        // 완전히 열리기 전에는 획득 불가
        if (clicker != null)
        {
            clicker.gameObject.SetActive(true);
            clicker.enabled = false;
        }

        // 금고 문 열림 효과음
        PlaySound(openSound);

        Transform door = safeDoor != null
            ? safeDoor
            : transform;

        float duration = Mathf.Max(
            0.01f,
            openDuration
        );

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(elapsed / duration)
            );

            // 문의 중심점이 아닌 경첩 위치 기준 회전
            Quaternion rotation =
                Quaternion.AngleAxis(
                    openAngle * t,
                    hingeWorldAxis
                );

            door.SetPositionAndRotation(
                hingeWorldPosition +
                rotation *
                (closedDoorPosition - hingeWorldPosition),

                rotation * closedDoorRotation
            );

            yield return null;
        }

        opened = true;
        opening = false;

        // 완전히 열린 후 클릭커 획득 가능
        if (clicker != null)
            clicker.enabled = true;

        PlayerInteractor.ShowHint(
            "금고 안에 겨울이의 클릭커가 있다."
        );

        Debug.Log(
            "Memory_Safe: 금고 문 열림 완료"
        );
    }
}

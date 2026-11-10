using System.Collections;
using UnityEngine;

// 보상 서랍을 Memory_Safe 금고로 교체한 버전.
// DrawerPuzzleManager는 수정하지 않아도 됩니다.
// 퍼즐 완료 -> OpenDrawer() 호출 = '잠금 해제만' 진행.
// 플레이어가 금고 문을 좌클릭 -> Interact() = 실제 문 열기.
public class DrawerRewardDrawer : MonoBehaviour, IInteractable, IHasName
{
    [Header("Memory_Safe 모델")]
    [Tooltip("Safe_Door 오브젝트 (이 컴포넌트를 Safe_Door에 달았다면 비워도 됩니다)")]
    public Transform safeDoor;

    [Tooltip("Safe_Door 아래에 있는 Safe_Hinge 오브젝트")]
    public Transform safeHinge;

    [Header("문 열림 설정")]
    [Tooltip("반대쪽으로 열리면 부호를 바꿔 주세요.")]
    public float openAngle = -100f;
    public float openDuration = 1.2f;
    public float unlockDelay = 0.4f;

    [Header("금속 효과음")]
    public AudioSource audioSource;
    public AudioClip unlockSound;
    public AudioClip openSound;

    [Header("금고 안의 보상")]
    public GrabbableItem clicker;

    private bool prepared;
    private bool unlocking;
    private bool unlocked;
    private bool opening;
    private bool opened;

    private Vector3 closedDoorPosition;
    private Quaternion closedDoorRotation;
    private Vector3 hingeWorldPosition;
    private Vector3 hingeWorldAxis;

    public string DisplayName
    {
        get
        {
            if (!unlocked) return "잠긴 금고";
            if (opened) return "열린 금고";
            return "금고 문 열기";
        }
    }

    // 기존 DrawerPuzzleManager.Start()에서 호출됨.
    public void Prepare(DrawerPuzzleManager manager)
    {
        Transform door = safeDoor != null ? safeDoor : transform;

        prepared = true;
        unlocking = false;
        unlocked = false;
        opening = false;
        opened = false;

        closedDoorPosition = door.position;
        closedDoorRotation = door.rotation;

        // 힌지는 문의 자식이므로, 회전 전의 월드 좌표를 저장해 둡니다.
        if (safeHinge != null)
            hingeWorldPosition = safeHinge.position;
        else
            Debug.LogError("Memory_Safe: Safe Hinge를 연결해 주세요.", this);

        // 회전축은 금고 모델의 '위쪽'입니다.
        hingeWorldAxis = door.parent != null ? door.parent.up : Vector3.up;
        if (hingeWorldAxis.sqrMagnitude < 0.0001f)
            hingeWorldAxis = Vector3.up;
        hingeWorldAxis.Normalize();

        // 금고가 열리기 전에는 클릭커를 볼 수도, 집을 수도 없습니다.
        if (clicker != null)
        {
            clicker.enabled = false;
            clicker.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning("Memory_Safe: Clicker가 연결되지 않았습니다.", this);
        }
    }

    // 기존 DrawerPuzzleManager.CompletePuzzle()가 호출하는 메서드.
    // 이름은 호환성을 위해 OpenDrawer로 유지하지만 여기서는 문을 열지 않습니다.
    public void OpenDrawer()
    {
        if (!prepared || unlocking || unlocked)
            return;

        StartCoroutine(UnlockRoutine());
    }

    private IEnumerator UnlockRoutine()
    {
        unlocking = true;
        yield return new WaitForSeconds(Mathf.Max(0f, unlockDelay));

        unlocked = true;
        unlocking = false;

        if (audioSource != null && unlockSound != null)
            audioSource.PlayOneShot(unlockSound);

        Debug.Log("Memory_Safe: 철컥! 잠금 해제 완료 (문은 아직 닫힘)");
        PlayerInteractor.ShowHint("철컥! 금고 잠금이 풀렸다. 문을 눌러 열자.");
    }

    // Safe_Door를 바라보고 좌클릭하면 PlayerInteractor가 호출합니다.
    public void Interact()
    {
        if (!prepared || unlocking || !unlocked)
        {
            PlayerInteractor.ShowHint("금고가 잠겨 있다.");
            return;
        }

        if (opening)
            return;

        if (opened)
        {
            PlayerInteractor.ShowHint("금고 안의 물건을 확인하자.");
            return;
        }

        if (safeHinge == null)
        {
            Debug.LogError("Memory_Safe: Safe Hinge가 없어 문을 열 수 없습니다.", this);
            return;
        }

        StartCoroutine(OpenRoutine());
    }

    private IEnumerator OpenRoutine()
    {
        opening = true;

        // 문이 열리기 시작할 때 내부의 클릭커가 보이도록 합니다.
        // 문이 완전히 열리기 전까지 집기는 막아 둡니다.
        if (clicker != null)
        {
            clicker.gameObject.SetActive(true);
            clicker.enabled = false;
        }

        if (audioSource != null && openSound != null)
            audioSource.PlayOneShot(openSound);

        Transform door = safeDoor != null ? safeDoor : transform;
        float duration = Mathf.Max(0.01f, openDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

            // 문의 중심점이 아닌, 경첩의 고정 위치를 기준으로 회전합니다.
            Quaternion rotation = Quaternion.AngleAxis(openAngle * t, hingeWorldAxis);
            door.SetPositionAndRotation(
                hingeWorldPosition + rotation * (closedDoorPosition - hingeWorldPosition),
                rotation * closedDoorRotation
            );

            yield return null;
        }

        opened = true;
        opening = false;

        if (clicker != null)
            clicker.enabled = true;

        PlayerInteractor.ShowHint("금고 안에 겨울이의 클릭커가 있다.");
        Debug.Log("Memory_Safe: 금고 문 열림 완료");
    }
}

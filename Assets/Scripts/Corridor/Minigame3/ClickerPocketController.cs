
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[DefaultExecutionOrder(1100)]
public class ClickerPocketController : MonoBehaviour
{
    [Header("클릭커")]
    public GrabbableItem clicker;
    public DrawerPuzzleManager puzzleManager;

    [Header("손 연출")]
    public Transform handBone;
    public Vector3 pocketOffset =
        new Vector3(-0.12f, -0.25f, -0.08f);
    public Vector3 pocketRotation =
        new Vector3(20f, 0f, -15f);
    public float animationDuration = 0.6f;

    [Header("입력")]
    public KeyCode pocketKey = KeyCode.E;
    public KeyCode clickKey = KeyCode.Mouse0;

    [Header("클릭커 효과음")]
    public AudioSource audioSource;
    public AudioClip clickSound;

    [Header("클릭커를 눌렀을 때 실행")]
    public UnityEvent onClickerPressed;

    bool owned;
    bool inPocket;
    bool animating;

    float pocketBlend;

    public bool HasClicker => owned;
    public bool IsInPocket => owned && inPocket;

    public bool IsHoldingClicker =>
        owned &&
        !inPocket &&
        PlayerHand.Instance != null &&
        PlayerHand.Instance.Held == clicker;

    void Update()
    {
        PlayerHand hand = PlayerHand.Instance;

        if (clicker == null || hand == null)
            return;

        // 보상 서랍에서 처음 클릭커를 집었는지 확인
        if (!owned && hand.Held == clicker)
        {
            owned = true;
            inPocket = false;

            if (puzzleManager != null)
                puzzleManager.CollectClicker();

            PlayerInteractor.ShowHint(
                "E키로 클릭커를 주머니에 넣을 수 있다."
            );
        }

        if (!owned || animating)
            return;

        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        // E키: 넣기 / 꺼내기
        if (Input.GetKeyDown(pocketKey))
        {
            if (inPocket)
            {
                if (hand.Held == null && hand.IsVisible)
                    StartCoroutine(TakeOutRoutine());
                else
                    PlayerInteractor.ShowHint(
                        "먼저 손을 비워야 한다."
                    );
            }
            else if (hand.Held == clicker)
            {
                StartCoroutine(PutAwayRoutine());
            }

            return;
        }

        // 클릭커를 손에 든 상태에서 왼쪽 클릭
        if (IsHoldingClicker &&
            Input.GetKeyDown(clickKey))
        {
            PressClicker();
        }
    }

    IEnumerator PutAwayRoutine()
    {
        animating = true;

        // 손을 주머니 쪽으로 내림
        yield return MoveHand(0f, 1f);

        PlayerHand hand = PlayerHand.Instance;

        inPocket = true;

        if (hand != null && hand.Held == clicker)
        {
            // 기존 손 시스템에서 안전하게 분리
            hand.PlaceHeld(null, true);
        }
        else
        {
            clicker.enabled = false;
            clicker.gameObject.SetActive(false);
        }

        // 빈손을 원래 위치로
        yield return MoveHand(1f, 0f);

        animating = false;
    }

    IEnumerator TakeOutRoutine()
    {
        animating = true;

        // 빈손을 주머니 쪽으로 내림
        yield return MoveHand(0f, 1f);

        PlayerHand hand = PlayerHand.Instance;

        if (hand != null &&
            hand.Held == null &&
            hand.IsVisible)
        {
            clicker.gameObject.SetActive(true);
            clicker.enabled = true;

            if (hand.Hold(clicker))
            {
                // 꺼내는 순간 손바닥에 배치
                Transform point = hand.palmPoint != null
                    ? hand.palmPoint
                    : hand.transform;

                clicker.AttachTo(point);
                inPocket = false;
            }
            else
            {
                clicker.enabled = false;
                clicker.gameObject.SetActive(false);
            }
        }

        // 클릭커를 든 손을 다시 올림
        yield return MoveHand(1f, 0f);

        animating = false;
    }

    IEnumerator MoveHand(float from, float to)
    {
        float duration =
            Mathf.Max(0.05f, animationDuration * 0.5f);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            t = Mathf.SmoothStep(0f, 1f, t);

            pocketBlend = Mathf.Lerp(from, to, t);

            yield return null;
        }

        pocketBlend = to;
    }

    void LateUpdate()
    {
        // 기존 PlayerHand 손 움직임에
        // 주머니 방향 이동을 덧붙임
        if (handBone == null || pocketBlend <= 0f)
            return;

        handBone.localPosition +=
            pocketOffset * pocketBlend;

        handBone.localRotation *=
            Quaternion.Euler(
                pocketRotation * pocketBlend
            );
    }

    void PressClicker()
    {
        if (audioSource != null && clickSound != null)
            audioSource.PlayOneShot(clickSound);

        onClickerPressed?.Invoke();

        Debug.Log("클릭커 딸깍!");
    }
}

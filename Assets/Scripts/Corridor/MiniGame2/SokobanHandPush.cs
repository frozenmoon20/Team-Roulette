
using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(1000)]
public class SokobanHandPush : MonoBehaviour
{
    public static SokobanHandPush Instance { get; private set; }

    [Header("기존 손 연결")]
    public PlayerHand playerHand;
    public HandPose handPose;

    [Header("손 밀기 위치와 회전")]
    public Vector3 pushOffset = new Vector3(0f, 0f, 0.08f);
    public Vector3 pushRotation = new Vector3(-15f, 0f, 0f);

    [Header("손가락 끝마디 굽힘")]
    [Range(0f, 1f)]
    public float tipCurl = 0.22f;

    [Header("애니메이션 시간")]
    public float forwardTime = 0.12f;
    public float holdTime = 0.08f;
    public float returnTime = 0.20f;

    Transform[,] tipBones = new Transform[5, 1];

    float blend;
    bool playing;
    Coroutine pushRoutine;

    void Awake()
    {
        Instance = this;

        if (playerHand == null)
            playerHand = GetComponent<PlayerHand>();

        if (handPose == null)
            handPose = GetComponent<HandPose>();
    }

    void Start()
    {
        FindFingerTips();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void FindFingerTips()
    {
        if (handPose == null || handPose.rig == null)
        {
            Debug.LogWarning(
                "SokobanHandPush: HandPose 또는 Rig 연결 확인 필요"
            );
            return;
        }

        Transform[] bones =
            handPose.rig.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < 5; i++)
        {
            string targetName =
                handPose.bonePrefix +
                (HandPose.Finger)i +
                "3";

            foreach (Transform bone in bones)
            {
                if (bone.name == targetName)
                {
                    tipBones[i, 0] = bone;
                    break;
                }
            }

            if (tipBones[i, 0] == null)
                Debug.LogWarning(
                    "SokobanHandPush: 손가락 끝마디를 찾지 못함 - "
                    + targetName
                );
        }
    }

    public void PlayPush()
    {
        if (playing || playerHand == null)
            return;

        if (!playerHand.IsVisible ||
            playerHand.Held != null ||
            playerHand.handBone == null ||
            playerHand.palmUpPose == null)
            return;

        pushRoutine = StartCoroutine(PushRoutine());
    }

    IEnumerator PushRoutine()
    {
        playing = true;

        yield return AnimateBlend(
            0f, 1f, forwardTime
        );

        yield return new WaitForSeconds(
            Mathf.Max(0f, holdTime)
        );

        yield return AnimateBlend(
            1f, 0f, returnTime
        );

        blend = 0f;
        playing = false;
        pushRoutine = null;
    }

    IEnumerator AnimateBlend(
        float from, float to, float duration
    )
    {
        float elapsed = 0f;
        duration = Mathf.Max(0.001f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(elapsed / duration)
            );

            blend = Mathf.Lerp(from, to, t);

            yield return null;
        }

        blend = to;
    }

    void LateUpdate()
    {
        if (blend <= 0f ||
            playerHand == null ||
            playerHand.handBone == null)
            return;

        if (!playerHand.IsVisible ||
            playerHand.Held != null)
            return;

        Transform handBone = playerHand.handBone;

        // 기존 PlayerHand 동작 뒤에 밀기 위치 추가
        handBone.localPosition += pushOffset * blend;

        // 손바닥을 앞으로 향하게 회전
        handBone.localRotation *=
            Quaternion.Euler(pushRotation * blend);

        if (handPose == null)
            return;

        // 손가락 끝마디에만 굽힘 추가
        for (int i = 0; i < 5; i++)
        {
            Transform tip = tipBones[i, 0];

            if (tip == null)
                continue;

            bool isThumb = i == 0;

            Vector3 axis = isThumb
                ? handPose.thumbAxis
                : handPose.fingerAxis;

            Vector3 angles = isThumb
                ? handPose.thumbAngles
                : handPose.fingerAngles;

            float angle = angles.z * tipCurl * blend;

            tip.localRotation *=
                Quaternion.AngleAxis(angle, axis);
        }
    }
}

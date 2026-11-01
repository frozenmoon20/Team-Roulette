using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// 미니게임 2 - 꿈속에서 소리로 개 찾기
// 1) 눈을 감고 있을 때만 고개를 돌릴 수 있다. 개 짖는 소리를 듣고 방향을 찾는다.
//    - 3D 소리라 좌우는 귀로 구분된다.
//    - 3D 소리만으로는 "정면"과 "등 뒤"가 똑같이 들리므로, 정면일수록 또렷하고 크게 / 등 뒤일수록 먹먹하고 작게 만든다.
// 2) 클릭하면 눈을 잠깐 뜬다 (EyeOpen.Peek). 뜬 동안에는 고개가 고정되고 소리가 확 옅어진다 (보려고 하면 들리지 않는다).
// 3) 눈을 다 뜬 순간 개가 정면(lookAngle 안)에 있으면 성공 → onFound.
//    눈이 다시 감기면 개는 다른 자리로 옮겨 가서 짖는다. 실패하면 아무 일 없이 다시 감기고 다시 찾으면 된다.
// 4) needed번 성공하면 눈을 뜬 채로 유지 → onLooked (깨어나는 연출).
// 꿈 공간의 DreamTest 오브젝트에 붙인다.
public class DreamTurn : MonoBehaviour
{
    [Header("연결")]
    public EyeOpen eye;                      // 눈 (원형 시야)
    public Transform body;                   // 돌릴 대상 = Player (좌우 회전은 몸 전체)
    public Transform cameraRoot;             // Player의 PlayerCameraRoot (고개 숙이기용)
    public Transform dog;                    // 개 (임시 도형이어도 됨)
    public Transform[] spots;                // 개가 차례로 나타날 자리들 (성공할 때마다 다음 자리로)
    public AudioSource bark;                 // 개에 붙은 짖는 소리 (3D 소리)
    public AudioLowPassFilter barkFilter;    // 개와 같은 오브젝트에 붙인 Audio Low Pass Filter (먹먹함 조절)

    [Header("고개 돌리기 (눈을 감고 있을 때만)")]
    public float turnSensitivity = 1.5f;     // 눈 감았을 때 돌아가는 빠르기
    public float lookDown = 15f;             // 꿈속에서 고개를 숙이는 각도 (도). 바닥의 작은 개가 시야 가운데 오도록

    [Header("소리: 정면일수록 또렷하고 크게")]
    public float barkInterval = 2f;          // 짖는 간격 (초). 짧을수록 방향 찾기 쉬움
    public float behindCutoff = 500f;        // 개가 등 뒤에 있을 때 먹먹함 (Hz, 낮을수록 먹먹)
    public float frontCutoff = 22000f;       // 개가 정면에 있을 때 (22000 = 필터 없음과 같음)
    [Range(0f, 1f)] public float behindVolume = 0.35f;   // 등 뒤에 있을 때 소리 크기
    [Range(0f, 1f)] public float openVolume = 0.15f;     // 눈을 다 떴을 때 소리 크기 배율

    [Header("판정")]
    public float lookAngle = 20f;            // 눈을 다 뜬 순간 개가 이 각도 안에 있으면 성공
    public int needed = 3;                   // 몇 번 찾으면 끝나는지

    [Header("찾을 때마다 할 일 (목표 완료 등)")]
    public UnityEvent onFound;

    [Header("마지막으로 찾았을 때 할 일 (깨어나는 연출)")]
    public UnityEvent onLooked;

    [Header("테스트용: 에디터에서 이 키를 누르면 바로 깨어남 (빌드에선 무시)")]
    public KeyCode skipKey = KeyCode.F9;

    [Header("확인용 (Play 중 값 보기)")]
    public bool locked;                      // true면 조작을 안 받음 (Minigame2가 시작 연출 동안 잠금)
    public float facingAngle;                // 개가 정면에서 몇 도 떨어져 있는지 (0 = 정면, 180 = 등 뒤)
    public int found;                        // 지금까지 찾은 횟수

    int spotIndex;
    float barkTimer;
    bool judged;                             // 이번에 눈을 뜬 것에 대해 판정했는지
    bool moving;                             // 성공 후 개가 자리를 옮기는 중 (그동안 클릭 무시)
    bool done;

    void OnEnable()
    {
        spotIndex = 0;
        found = 0;
        judged = true;                       // 처음 연출(천천히 눈 뜨기) 중에는 판정하지 않음. 첫 클릭부터 판정
        moving = false;
        done = false;
        MoveDogTo(0);
    }

    void Update()
    {
        if (done || body == null || dog == null || eye == null) return;

        // 0) 꿈속에서는 고개를 살짝 숙인 채로
        if (cameraRoot != null) cameraRoot.localRotation = Quaternion.Euler(lookDown, 0f, 0f);

        // 1) 개가 정면에서 몇 도 떨어져 있는지 (좌우만 봄)
        Vector3 toDog = dog.position - body.position;
        toDog.y = 0f;
        facingAngle = Vector3.Angle(body.forward, toDog);

        // 2) 소리 (대사 중에도 계속 짖음)
        UpdateSound(eye.Open);
        barkTimer -= Time.deltaTime;
        if (barkTimer <= 0f) Bark();

        // 대사·설정창이 떠 있으면 공용 UI가 눈(EyeOpen)을 꺼 둔다 → 그동안은 조작을 안 받음
        if (!eye.enabled || locked) return;

        // [테스트용] 에디터에서 skipKey를 누르면 마지막으로 찾은 것으로 처리 → 바로 깨어나는 연출로
        if (Application.isEditor && Input.GetKeyDown(skipKey))
        {
            found = needed - 1;
            Found();
            return;
        }

        // 3) 고개 돌리기: 눈을 감고 있을 때만 (뜨는 중·보는 중·감는 중에는 고정)
        if (!eye.IsBusy)
        {
            float mx = Input.GetAxis("Mouse X") * turnSensitivity;
            body.Rotate(0f, mx, 0f);
        }

        // 4) 클릭하면 눈을 잠깐 뜸
        if (!eye.IsBusy && !moving && Input.GetMouseButtonDown(0))
        {
            eye.Peek();
            judged = false;
        }

        // 5) 눈을 다 뜬 순간 한 번만 판정
        if (eye.IsBusy && !judged && eye.Open >= 0.99f)
        {
            judged = true;
            if (facingAngle < lookAngle) Found();
        }
    }

    void Found()
    {
        found++;
        Debug.Log("미니게임 2: 개를 찾음 " + found + " / " + needed);
        onFound.Invoke();

        if (found >= needed)
        {
            // 마지막: 눈을 뜬 채로 유지하고 깨어나는 연출로
            done = true;
            eye.ForceOpen();
            Bark();
            onLooked.Invoke();
            return;
        }

        StartCoroutine(MoveAfterClose());
    }

    // 눈이 다시 감기고 나서 개를 다음 자리로 (옮기는 순간이 보이지 않게)
    IEnumerator MoveAfterClose()
    {
        moving = true;
        while (eye.IsBusy) yield return null;
        MoveDogTo(spotIndex + 1);
        moving = false;
    }

    // 정면일수록 또렷하고 크게, 눈을 뜰수록 작게
    void UpdateSound(float open)
    {
        float front = 1f - facingAngle / 180f;   // 1 = 정면, 0 = 등 뒤
        front *= front;                           // 제곱: 정면 가까이에서 확 또렷해지게

        // MG1과 같은 방식: 귀가 느끼는 대로 고르게 바뀌도록 비율로 계산
        if (barkFilter != null)
            barkFilter.cutoffFrequency = behindCutoff * Mathf.Pow(frontCutoff / behindCutoff, front);

        if (bark != null)
            bark.volume = Mathf.Lerp(behindVolume, 1f, front) * Mathf.Lerp(1f, openVolume, open);
    }

    void MoveDogTo(int index)
    {
        if (spots == null || spots.Length == 0) return;
        spotIndex = Mathf.Clamp(index, 0, spots.Length - 1);
        dog.SetPositionAndRotation(spots[spotIndex].position, spots[spotIndex].rotation);
        Bark();
    }

    void Bark()
    {
        barkTimer = barkInterval;
        if (bark != null) bark.Play();
    }
}
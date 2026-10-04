using UnityEngine;

// 미니게임 2-1 눈 뜨기 (뼈대)
// 왼쪽 버튼을 꾹 누른 채 마우스를 위로 끌면 눈꺼풀이 열린다.
// 끌지 않으면 천천히, 버튼을 놓으면 빠르게 다시 감긴다.
// EyeOpen 오브젝트에 붙인다. EyeOpen이 켜지는 순간 감긴 상태에서 시작한다.
public class EyeOpen : MonoBehaviour
{
    public RectTransform lidTop;
    public RectTransform lidBottom;

    [Header("조작 느낌")]
    public float dragPower = 0.08f;          // 위로 끈 만큼 얼마나 열릴지
    public float holdCloseSpeed = 0.15f;     // 누르고만 있을 때 감기는 빠르기 (1초에)
    public float releaseCloseSpeed = 0.6f;   // 버튼을 놓았을 때 감기는 빠르기 (1초에)

    [Header("확인용 (Play 중 값 보기)")]
    [Range(0f, 1f)] public float open;       // 0 = 감김, 1 = 다 뜸

    void OnEnable()
    {
        open = 0f;
        Apply();
    }

    void Update()
    {
        bool holding = Input.GetMouseButton(0);

        if (holding)
        {
            float up = Mathf.Max(0f, Input.GetAxis("Mouse Y"));   // 위로 끈 양만 (아래로 끌기는 무시)
            open += up * dragPower;
            open -= holdCloseSpeed * Time.deltaTime;
        }
        else
        {
            open -= releaseCloseSpeed * Time.deltaTime;
        }

        open = Mathf.Clamp01(open);
        Apply();
    }

    // open 값만큼 위 눈꺼풀은 위로, 아래 눈꺼풀은 아래로 밀어낸다
    void Apply()
    {
        float shift = open * 0.5f;   // 다 뜨면 각각 화면 높이의 절반만큼 밀려나 화면 밖으로

        lidTop.anchorMin = new Vector2(0f, 0.5f + shift);
        lidTop.anchorMax = new Vector2(1f, 1f + shift);

        lidBottom.anchorMin = new Vector2(0f, 0f - shift);
        lidBottom.anchorMax = new Vector2(1f, 0.5f - shift);
    }
}
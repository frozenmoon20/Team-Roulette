using UnityEngine;

// 꿈 공간에 거대한 기둥들을 둥글게 흩뿌려 세운다 (고개를 돌리고 있다는 기준점 역할).
// 에디터에서 컴포넌트 이름을 우클릭 → "기둥 만들기"를 누르면 자식으로 기둥이 생긴다.
// 마음에 안 들면 Seed 값을 바꾸고 다시 누르면 된다 (기존 기둥은 지우고 새로 만듦).
// 생긴 기둥은 일반 오브젝트라 손으로 옮기거나 지워도 된다.
// DreamSpace 아래 빈 오브젝트 "Pillars"에 붙인다.
public class DreamPillars : MonoBehaviour
{
    public Material material;                // 기둥 색 (M_DreamPillar)
    public int count = 20;                   // 기둥 개수
    public int seed = 7;                     // 배치 모양 번호. 바꾸면 다른 배치

    [Header("거리 (중심에서, m)")]
    public float minDistance = 8f;           // 개(4m)보다 멀리 둬서 시야를 가리지 않게
    public float maxDistance = 35f;          // 하늘 구(반지름 40)보다 안쪽

    [Header("크기 (m)")]
    public Vector2 heightRange = new Vector2(20f, 45f);
    public Vector2 radiusRange = new Vector2(0.4f, 1.8f);
    public float maxTilt = 4f;               // 살짝 기울기 (도)

    [ContextMenu("기둥 만들기")]
    void Generate()
    {
        Clear();
        Random.State saved = Random.state;
        Random.InitState(seed);

        for (int i = 0; i < count; i++)
        {
            // 원을 count등분한 방향 + 약간의 흔들림 → 사방에 고르게 퍼짐
            float angle = (i + Random.Range(-0.4f, 0.4f)) / count * 360f;
            float dist = Random.Range(minDistance, maxDistance);
            Vector3 pos = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * dist;

            float h = Random.Range(heightRange.x, heightRange.y);
            float r = Random.Range(radiusRange.x, radiusRange.y);

            GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            p.name = "Pillar_" + i;
            p.transform.SetParent(transform, false);
            // 원기둥은 기본 높이가 2라서 h/2로 늘리고, 바닥에 박히게 중심을 h/2 높이에
            p.transform.localPosition = pos + Vector3.up * (h * 0.5f);
            p.transform.localRotation = Quaternion.Euler(Random.Range(-maxTilt, maxTilt), Random.Range(0f, 360f), Random.Range(-maxTilt, maxTilt));
            p.transform.localScale = new Vector3(r * 2f, h * 0.5f, r * 2f);

            DestroyImmediate(p.GetComponent<Collider>());   // 부딪힐 일 없음
            if (material != null) p.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        Random.state = saved;
    }

    [ContextMenu("기둥 모두 지우기")]
    void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);
    }
}
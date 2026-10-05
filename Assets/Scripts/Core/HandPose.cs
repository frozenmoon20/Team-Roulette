using UnityEngine;

public class HandPose : MonoBehaviour
{
    public enum Finger { Thumb, Index, Middle, Ring, Pinky }

    public Transform rig;

    public string bonePrefix = "J_Right_Hand";

    // 각 손가락의 굽힘 정도
    [Range(0f, 1f)] public float thumb = 0.15f;
    [Range(0f, 1f)] public float index = 0.15f;
    [Range(0f, 1f)] public float middle = 0.15f;
    [Range(0f, 1f)] public float ring = 0.15f;
    [Range(0f, 1f)] public float pinky = 0.15f;

    public Vector3 fingerAxis = Vector3.right;                  // 검지 확인 결과: 로컬 X축 +
    public Vector3 thumbAxis = Vector3.right;                   // 엄지는 테스트 후 수정
    public Vector3 fingerAngles = new Vector3(70f, 90f, 70f);   // 관절 1, 2, 3의 최대 각도
    public Vector3 thumbAngles = new Vector3(30f, 40f, 50f);
    // 굽히는 빠르기
    public float speed = 4f;                                    // 4 = 0.25초에 완전히 굽힘

    [Header("가만히 있을 때 (손가락 꼼지락)")]
    public float idleFinger = 0.04f;      // 손가락이 저절로 움직이는 폭 (0이면 끔)
    public float idleSpeed = 0.4f;        // 꼼지락 빠르기

    // 관절별 값 내부 저장 공간
    Transform[,] bones = new Transform[5, 3];
    Quaternion[,] rest = new Quaternion[5, 3];
    float[] current = new float[5];
    bool ready;

    void Awake()
    {
        if (rig == null) return;

        // 뼈를 이름으로 찾고, 지금 자세(펴진 손)를 기준으로 기억
        Transform[] all = rig.GetComponentsInChildren<Transform>(true);
        for (int f = 0; f < 5; f++)
        {
            for (int j = 0; j < 3; j++)
            {
                string boneName = bonePrefix + (Finger)f + (j + 1);
                foreach (Transform t in all)
                {
                    if (t.name == boneName) { bones[f, j] = t; rest[f, j] = t.localRotation; break; }
                }
                if (bones[f, j] == null) Debug.LogWarning("HandPose: 뼈를 못 찾음 " + boneName);
            }
            current[f] = GetFinger((Finger)f);     // 시작 자세는 바로 적용
        }
        ready = true;
    }

    void LateUpdate()
    {
        if (!ready) return;

        for (int f = 0; f < 5; f++)
        {
            current[f] = Mathf.MoveTowards(current[f], GetFinger((Finger)f), speed * Time.deltaTime);

            Vector3 axis = f == 0 ? thumbAxis : fingerAxis;
            Vector3 angles = f == 0 ? thumbAngles : fingerAngles;
            // 손가락마다 다른 박자로 아주 살짝 꼼지락 (f * 10f로 손가락별 박자를 다르게)
            float idle = (Mathf.PerlinNoise(Time.time * idleSpeed, f * 10f) - 0.5f) * 2f * idleFinger;
            float curl = Mathf.Clamp01(current[f] + idle);

            for (int j = 0; j < 3; j++)
            {
                if (bones[f, j] == null) continue;
                bones[f, j].localRotation = rest[f, j] * Quaternion.AngleAxis(curl * angles[j], axis);
            }
        }
    }

    // ===== 다른 스크립트에서 쓰는 함수 =====

    // 손가락 하나 굽히기 (미니게임 3: 키마다 손가락 하나씩)
    public void SetFinger(Finger finger, float curl)
    {
        curl = Mathf.Clamp01(curl);
        switch (finger)
        {
            case Finger.Thumb: thumb = curl; break;
            case Finger.Index: index = curl; break;
            case Finger.Middle: middle = curl; break;
            case Finger.Ring: ring = curl; break;
            case Finger.Pinky: pinky = curl; break;
        }
    }

    // 다섯 손가락 한꺼번에 (0 = 편 손, 1 = 주먹)
    public void SetAll(float curl)
    {
        curl = Mathf.Clamp01(curl);
        thumb = index = middle = ring = pinky = curl;
    }

    public float GetFinger(Finger finger)
    {
        switch (finger)
        {
            case Finger.Thumb: return thumb;
            case Finger.Index: return index;
            case Finger.Middle: return middle;
            case Finger.Ring: return ring;
            default: return pinky;
        }
    }


}

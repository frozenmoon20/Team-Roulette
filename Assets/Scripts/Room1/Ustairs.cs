using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
#endif

// (임시 박스 계단) U자로 꺾어 내려가는 계단을 큐브로 자동으로 만든다.
//   첫 번째 계단을 앞으로 내려감 → 넓은 참(평평한 곳)에서 U자로 몸을 돌림 → 두 번째 계단을 반대 방향으로 내려감
//   두 번째 계단 끝 무렵에 트리거(Zone_StairsEnd)를 만들어, 밟으면 하얗게 페이드되며 거실 씬으로 넘어가게 한다.
// 원리: 이 오브젝트의 위치 = 첫 계단이 시작되는 "위층 바닥 가장자리", 파란 화살표(앞) = 내려가는 방향.
//       계단 한 칸 높이는 (층 높이 ÷ 전체 칸 수)로 자동 계산 → 마지막 칸이 정확히 아래층 바닥 높이에 닿는다.
//       칸마다 아래층 바닥까지 꽉 찬 블록이라 밑이 뚫려 보이지 않는다. 블록마다 Box Collider가 있어 걸어 내려갈 수 있다.
//       (한 칸 높이가 플레이어 CharacterController의 Step Offset(보통 0.3)보다 낮아야 걸어서 오르내림)
// 사용: 빈 오브젝트 "Stairs"를 만들어 위층 바닥 가장자리(계단이 시작할 곳)에 놓고, 파란 화살표를 내려갈 쪽으로 돌린 뒤
//       이 스크립트를 붙이고 Inspector 오른쪽 위 ⋮ → "계단 만들기"를 누른다. 값을 바꾸고 다시 누르면 새로 만든다.
//       게임 실행 중에는 아무 일도 안 한다 (빌드에도 영향 없음).
public class UStairs : MonoBehaviour
{
    [Header("크기 (m)")]
    public float floorHeight = 3f;           // 위층 바닥 ~ 아래층 바닥 높이
    public int stepsPerFlight = 8;           // 계단 한 줄의 칸 수 (두 줄이므로 전체 칸 = 2배)
    public float stepDepth = 0.28f;          // 한 칸 깊이
    public float width = 1f;                 // 계단 한 줄의 폭
    public float gap = 0.1f;                 // 두 계단 줄 사이 틈
    public float landingDepth = 1.1f;        // 중간 참(U자로 도는 곳)의 깊이
    public float bottomDepth = 1.5f;         // 아래층에 내려와서 서는 바닥 깊이
    public bool turnRight = true;            // 체크: 오른쪽으로 돌아 내려감 / 해제: 왼쪽

    [Header("벽 (선택)")]
    public bool buildWalls = true;           // 계단실 양옆·끝 벽
    public float wallThickness = 0.1f;

    [Header("모양")]
    public Material material;                // 계단·벽 머티리얼 (비우면 기본)

    [Header("거실로 넘어가는 트리거")]
    public bool buildEndZone = true;
    public string nextScene = "Room3_LivingRoom";
    public int zoneFromStep = 5;             // 두 번째 계단의 몇 번째 칸부터 트리거 (0부터 셈, 클수록 더 내려가서 넘어감)
    public float fadeOutTime = 1.5f;         // 하얗게 되는 시간
    public float fadeInTime = 1.5f;          // 거실에서 하얀 화면이 걷히는 시간

    float StepHeight => floorHeight / (stepsPerFlight * 2);
    float Side => turnRight ? 1f : -1f;

#if UNITY_EDITOR
    [ContextMenu("계단 만들기")]
    void Build()
    {
        if (Application.isPlaying) return;
        Clear();

        int n = stepsPerFlight;
        float h = StepHeight, d = stepDepth, w = width;
        float bottom = -floorHeight;                         // 아래층 바닥 높이 (이 오브젝트 기준)
        float x2 = (w + gap) * Side;                         // 두 번째 계단 줄의 좌우 위치
        float flightLen = n * d;

        // 1) 첫 번째 계단: 앞(+Z)으로 내려감. i번째 칸 윗면 = -(i+1)*h
        for (int i = 0; i < n; i++)
        {
            float top = -(i + 1) * h;
            Box("Step1_" + (i + 1), new Vector3(0f, (top + bottom) / 2f, i * d + d / 2f), new Vector3(w, top - bottom, d));
        }

        // 2) 중간 참: 첫 줄 마지막 칸 높이로, 두 줄을 다 덮는 넓이
        float landTop = -n * h;
        float landWidth = w * 2f + gap;
        Box("Landing", new Vector3(x2 / 2f, (landTop + bottom) / 2f, flightLen + landingDepth / 2f), new Vector3(landWidth, landTop - bottom, landingDepth));

        // 3) 두 번째 계단: 뒤(-Z)로 내려감. j번째 칸 윗면 = -(n+j+1)*h
        for (int j = 0; j < n; j++)
        {
            float top = -(n + j + 1) * h;
            float height = Mathf.Max(top - bottom, 0.02f);   // 마지막 칸은 아래층 바닥과 같은 높이 → 얇은 판
            Box("Step2_" + (j + 1), new Vector3(x2, top - height / 2f, flightLen - j * d - d / 2f), new Vector3(w, height, d));
        }

        // 4) 아래층 바닥 (두 번째 계단 끝에서 더 뒤로)
        Box("BottomFloor", new Vector3(x2, bottom - 0.05f, -bottomDepth / 2f), new Vector3(w, 0.1f, bottomDepth));

        // 5) 벽: 바깥 양옆 + 끝 (위층 바닥 높이 + 3m까지)
        if (buildWalls)
        {
            float t = wallThickness;
            float wallTop = 3f;
            float wallH = wallTop - bottom;
            float wallY = (wallTop + bottom) / 2f;
            float zStart = -bottomDepth;
            float zEnd = flightLen + landingDepth;
            float zLen = zEnd - zStart;
            float zMid = (zStart + zEnd) / 2f;
            float outer1 = -Side * (w / 2f + t / 2f);                    // 첫 줄 바깥쪽
            float outer2 = x2 + Side * (w / 2f + t / 2f);                // 둘째 줄 바깥쪽
            Box("Wall_Side1", new Vector3(outer1, wallY, (zEnd) / 2f), new Vector3(t, wallH, zEnd));          // 첫 줄 옆은 위층 바닥부터
            Box("Wall_Side2", new Vector3(outer2, wallY, zMid), new Vector3(t, wallH, zLen));
            Box("Wall_End", new Vector3(x2 / 2f, wallY, zEnd + t / 2f), new Vector3(landWidth + t * 2f, wallH, t));
            // 두 줄 사이 막기 (아래층 바닥 ~ 위층 바닥 위 1m 난간 높이)
            Box("Wall_Middle", new Vector3(x2 / 2f, (1f + bottom) / 2f, flightLen / 2f), new Vector3(gap, 1f - bottom, flightLen));
            // 위층에서 두 번째 계단 쪽 구멍으로 떨어지지 않게 막는 벽 (위층 바닥 높이부터 위로)
            Box("Wall_Guard", new Vector3(Side * (w + gap / 2f), wallTop / 2f, t / 2f), new Vector3(w + gap, wallTop, t));
        }

        // 6) 거실로 넘어가는 트리거 (두 번째 계단 zoneFromStep번째 칸부터 끝까지)
        if (buildEndZone)
        {
            int k = Mathf.Clamp(zoneFromStep, 0, n - 1);
            float zFront = flightLen - k * d;                 // 그 칸의 앞쪽 끝
            float zBack = -bottomDepth;
            float zoneTop = -(n + k) * h + 2f;
            GameObject zone = GameObject.CreatePrimitive(PrimitiveType.Cube);
            zone.name = "Zone_StairsEnd";
            Undo.RegisterCreatedObjectUndo(zone, "계단 만들기");
            zone.transform.SetParent(transform, false);
            zone.transform.localPosition = new Vector3(x2, (zoneTop + bottom) / 2f, (zFront + zBack) / 2f);
            zone.transform.localScale = new Vector3(w, zoneTop - bottom, zFront - zBack);
            Object.DestroyImmediate(zone.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(zone.GetComponent<MeshFilter>());
            zone.GetComponent<BoxCollider>().isTrigger = true;
            TriggerZone tz = zone.AddComponent<TriggerZone>();
            SceneGate gate = zone.AddComponent<SceneGate>();
            gate.sceneName = nextScene;
            gate.fadeColor = Color.white;
            gate.fadeOutTime = fadeOutTime;
            gate.fadeInTime = fadeInTime;
            if (tz.onEnter == null) tz.onEnter = new UnityEngine.Events.UnityEvent();   // 방금 붙인 컴포넌트는 이벤트 칸이 아직 비어(null) 있음
            UnityEventTools.AddPersistentListener(tz.onEnter, gate.Go);
            EditorUtility.SetDirty(tz);
        }

        EditorUtility.SetDirty(gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log("U자 계단 만들기 완료: 한 칸 높이 " + h.ToString("0.###") + "m (CharacterController Step Offset보다 낮아야 걸어서 내려감)", this);
    }

    [ContextMenu("계단 지우기")]
    void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(transform.GetChild(i).gameObject);
    }

    // 큐브 하나 만들기 (이 오브젝트 기준 위치·크기)
    void Box(string name, Vector3 localPos, Vector3 size)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        Undo.RegisterCreatedObjectUndo(g, "계단 만들기");
        g.transform.SetParent(transform, false);
        g.transform.localPosition = localPos;
        g.transform.localRotation = Quaternion.identity;
        g.transform.localScale = size;
        if (material != null) g.GetComponent<MeshRenderer>().sharedMaterial = material;
    }
#endif
}
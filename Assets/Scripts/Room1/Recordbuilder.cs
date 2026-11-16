using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// (모양 전용) LP판(LP_Record)을 진짜 레코드판 모양으로 자동으로 만든다: 검은 판 + 홈(동심원 띠) + 빨간 라벨 + 가운데 구멍
// 원리: 얇은 원기둥(Cylinder)을 크기를 줄여 가며 아주 조금씩 높게 겹쳐 쌓는다.
//       위에 올라간 작은 원이 아래 원의 가운데를 가려서, 바깥쪽 테두리만 띠처럼 보인다 → 검정/짙은 회색을 번갈아 쌓으면 홈처럼 보임.
//       광택(Smoothness)이 높은 머티리얼을 쓰면 빛이 닿는 쪽만 번쩍여서 사진 같은 반사가 생긴다.
//   - 이 오브젝트(LP_Record)는 그대로 쓴다 (GrabbableItem, LP_Spot·LP 플레이어 연결 유지).
//     원래 원기둥 모양(Mesh Renderer)과 Mesh Collider는 끄고, Scale을 1로 바꾸고, 판 크기에 맞는 Box Collider를 붙인다.
//   - 판의 기준점 = 밑면 가운데 (LP 플레이어가 Place Point 한가운데에 맞출 때 딱 놓이게)
// 사용: LP_Record에 붙이고, 머티리얼 칸을 채운 뒤 Inspector 오른쪽 위 ⋮ → "LP판 만들기".
//       값을 바꾸고 다시 누르면 새로 만든다. 게임 실행 중에는 아무 일도 안 한다 (빌드에도 영향 없음).
public class RecordBuilder : MonoBehaviour
{
    [Header("머티리얼")]
    public Material black;                   // 판 (검정, Smoothness 높게)
    public Material groove;                  // 홈 띠 (아주 짙은 회색, Smoothness 높게)
    public Material label;                   // 가운데 라벨 (빨강)
    public Material hole;                    // 가운데 구멍 (흰색 등, 비우면 안 만듦)

    [Header("크기 (m)")]
    public float diameter = 0.3f;            // 판 지름 (턴테이블 원판에 맞추기)
    public float thickness = 0.003f;         // 판 두께
    public float labelDiameter = 0.1f;       // 라벨 지름
    public float holeDiameter = 0.008f;      // 구멍 지름
    public int grooveBands = 4;              // 홈 띠 개수

    const string ModelName = "Vinyl";
    const float Layer = 0.0002f;             // 겹쳐 쌓을 때 한 겹 높이 (겹침 깜빡임 방지)

#if UNITY_EDITOR
    [ContextMenu("LP판 만들기")]
    void Build()
    {
        if (Application.isPlaying) return;
        Clear();

        // 이 오브젝트: 크기 1로, 원래 원기둥 모양·콜라이더는 끔, 판 크기 Box Collider
        Undo.RecordObject(transform, "LP판");
        transform.localScale = Vector3.one;
        if (TryGetComponent(out MeshRenderer mr)) { Undo.RecordObject(mr, "LP판"); mr.enabled = false; }
        if (TryGetComponent(out MeshCollider mc)) { Undo.RecordObject(mc, "LP판"); mc.enabled = false; }
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) box = Undo.AddComponent<BoxCollider>(gameObject);
        Undo.RecordObject(box, "LP판");
        float clickThick = Mathf.Max(thickness, 0.01f);                // 너무 얇으면 클릭이 어려워서 최소 1cm
        box.center = new Vector3(0f, clickThick / 2f, 0f);
        box.size = new Vector3(diameter, clickThick, diameter);

        GameObject root = new GameObject(ModelName);
        Undo.RegisterCreatedObjectUndo(root, "LP판");
        root.transform.SetParent(transform, false);

        // 1) 판
        float y = 0f;
        Disc(root, "Disc", diameter, thickness, y, black);
        y += thickness;

        // 2) 홈 띠: 바깥 → 안쪽으로 작아지며 검정/짙은 회색을 번갈아 쌓음
        float outer = diameter * 0.97f;
        float inner = labelDiameter * 1.2f;
        int n = Mathf.Max(0, grooveBands) * 2;
        for (int i = 0; i < n; i++)
        {
            float d = Mathf.Lerp(outer, inner, (float)i / Mathf.Max(n, 1));
            Disc(root, "Groove_" + i, d, Layer, y, i % 2 == 0 ? groove : black);
            y += Layer;
        }

        // 3) 라벨, 4) 구멍
        Disc(root, "Label", labelDiameter, Layer, y, label);
        y += Layer;
        if (hole != null) Disc(root, "Hole", holeDiameter, Layer, y, hole);

        EditorUtility.SetDirty(gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log("LP판 만들기 완료", this);
    }

    [ContextMenu("LP판 지우기")]
    void Clear()
    {
        Transform old = transform.Find(ModelName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
    }

    // 원판 하나 (지름 d, 두께 h, 밑면 높이 y). Cylinder는 기본 지름 1, 높이 2라서 Y 크기를 h/2로
    void Disc(GameObject root, string name, float d, float h, float y, Material mat)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.name = name;
        Undo.RegisterCreatedObjectUndo(g, "LP판");
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(root.transform, false);
        g.transform.localPosition = new Vector3(0f, y + h / 2f, 0f);
        g.transform.localRotation = Quaternion.identity;
        g.transform.localScale = new Vector3(d, h / 2f, d);
        if (mat != null) g.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }
#endif
}
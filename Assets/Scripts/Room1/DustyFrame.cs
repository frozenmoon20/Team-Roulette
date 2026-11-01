using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 먼지 낀 겨울이 액자 → 직접 문질러 먼지를 닦고 → 사진을 보고 → 회상(미니게임5)으로
//   - 손(Hand)이 없을 때: dustyDialogue만 ("…먼지가 뿌옇게 앉아서 안 보여.")
//   - 손이 생기면:
//       1) 액자 앞으로 다가감 (ViewFocus)
//       2) 손이 사진 앞에 나타나고 조작 안내 자막
//       3) 좌클릭을 누른 채 마우스를 움직이면 손이 따라 움직이며, 손가락이 지나간 자리의 먼지가 실시간으로 지워짐
//          (손은 화면 밖으로만 못 나가게 막음)
//       4) 먼지가 finishRatio(기본 85%)만큼 지워지면 끝 (남은 먼지는 그대로 둠) → 손을 천천히 거둠
//       5) 사진 쪽으로 조금 더 다가감
//       6) photoDialogue가 완전히 끝나고 holdAfterDialogue만큼 사진을 본 뒤 → 사진을 바라보는 채로 onMemory (MemoryTrip.Enter: 하얗게 → 회상)
//          화면이 거의 다 하얘지면 그 뒤에서 몰래 원래 자리로 되돌려 둠 (ViewFocus.ReturnInstant)
//          → 회상에서 돌아오면 액자를 보기 전 서 있던 자리에서 깨어남
//   - 회상을 다녀온 뒤: doneDialogue만 (비우면 photoDialogue)
// 먼지 지우기 원리: 먼지 판(Dust)의 머티리얼에 작은 "투명도 그림"(텍스처)을 만들어 씌우고,
//   손이 지나간 자리의 픽셀만 투명하게 바꾼다. → 문지른 자국대로 사진이 드러난다.
//   Dust의 머티리얼은 URP/Unlit, Surface Type = Transparent 여야 한다 (Base Map은 비워 둬도 됨, 스크립트가 넣음).
// 손 움직임 원리: 미니게임3의 HandAnchor를 그대로 쓴다. HandAnchor가 손을 붙잡아 둘 자리(Grip Point)를
//   이 스크립트가 만든 "손 목표점"으로 바꿔 두고, 마우스에 따라 그 목표점을 사진 면을 따라 옮긴다.
//   손 각도는 Wipe Point의 회전 그대로 (맞춰 둔 값 사용). Wipe Point = 손이 사진 가운데를 문지를 때의 손 자리.
// 대사는 전부 대화창(DialogueStep)으로 나온다. 화면 아래 짧은 글자(ShowHint)는 쓰지 않는다.
// 팀 공용 ConditionalInteractable을 상속만 한다 (원본은 건드리지 않음). Required Options는 비워 둔다.
// 사용: 액자(Collider 있어야 함)에 이 스크립트 + InteractName을 붙이고 칸을 채운다.
public class DustyFrame : ConditionalInteractable
{
    [Header("연결")]
    public ViewFocus focus;                  // 액자 앞으로 다가가기 (View Point = 액자 정면 눈 자리)
    public HandAnchor wipeHand;              // 닦기용 HandAnchor (Grip Point는 비워 둬도 됨, 이 스크립트가 정함)
    public Transform wipePoint;              // 손이 사진 가운데를 문지를 때의 손 자리·방향 (맞춰 둔 값)
    public Renderer dust;                    // 사진 위 먼지 판 (Quad, URP/Unlit Transparent 머티리얼)
    public ControlHint hint;                 // 화면 위 조작 안내 (MG3_Hint)
    public Graphic whiteScreen;              // MemoryTrip과 같은 하얀 화면 Image. 이게 다 하얘진 뒤에 포커싱을 풂
    [TextArea] public string guideText = "좌클릭을 누른 채 마우스를 움직여 먼지를 닦자";

    [Header("문지르기")]
    public float reachTime = 0.4f;           // 손이 액자 앞까지 가는 시간 (초)
    public float mouseSpeed = 0.02f;         // 마우스 움직임 1당 손이 움직이는 거리 (m). 손이 잘 안 움직이면 키우기
    public float brushSize = 0.25f;          // 한 번에 지워지는 크기 (사진 너비 대비, 0.25 = 25%)
    public Transform brushBone;              // 먼지를 지우는 기준이 되는 손가락 뼈 (비우면 자동: 가운뎃손가락 두 번째 마디)
    public Vector2 brushOffset = Vector2.zero;   // 지워지는 자리를 손가락 기준에서 조금 옮김 (사진 크기 대비, 미세 조정용)
    [Range(0f, 0.3f)] public float screenMargin = 0.08f;   // 손이 화면 가장자리에서 이만큼 안쪽까지만 (0.08 = 8%)
    public float wipeTwist = 10f;            // 빠르게 문지를 때 손목이 기우는 최대 각도 (도)
    [Range(0.5f, 1f)] public float finishRatio = 0.85f;   // 먼지가 이만큼 지워지면 끝 (남은 먼지는 그대로)
    public int maskWidth = 128;              // 먼지 그림 해상도 (가로). 세로는 먼지 판 비율대로
    public float returnTime = 0.5f;          // 다 닦고 손이 원래 자리로 돌아가는 시간 (초)

    [Header("다 닦은 뒤")]
    public float closeDistance = 0.12f;      // 사진 쪽으로 더 다가가는 거리 (m)
    public float closeTime = 0.8f;           // 다가가는 시간 (초)
    public float holdAfterDialogue = 1f;     // 사진 대사가 다 끝나고 회상으로 넘어가기 전 잠깐 사진을 바라보는 시간 (초)

    [Header("대사")]
    public DialogueStep dustyDialogue;       // 손이 없을 때
    public DialogueStep photoDialogue;       // 닦고 나서 사진을 보며 (회상 직전)
    public DialogueStep doneDialogue;        // 회상을 다녀온 뒤 (비우면 photoDialogue)

    [Header("이벤트")]
    public UnityEvent onWiped;               // 다 닦았을 때 (소리 등, 비워도 됨)
    public UnityEvent onMemory;              // 사진 대사가 끝난 뒤, 사진을 바라보는 채로 (MemoryTrip.Enter)

    [Header("확인용")]
    [Range(0f, 1f)] public float cleared;    // 지금까지 지운 비율
    public Vector2 mouseInput;               // 이번 프레임 마우스 움직임 (좌클릭 중일 때). 0이면 입력이 안 들어오는 것
    public bool handOnScreen;                // 손 기준점이 화면 안에 있는지. false면 화면 밖 막기를 끄고 움직임

    bool busy, used, ready;
    Transform handTarget;                    // HandAnchor가 손을 붙잡아 둘 목표점
    Material dustMat;
    Texture2D mask;                          // 먼지 투명도 그림
    Color32[] pixels;
    int maskH, clearedCount;
    HandSway sway;
    Camera cam;
    Vector3 lastBrush;
    bool hasLastBrush;
    bool dirty;                              // 아직 화면에 반영 안 된 지운 자국이 있는지

    // 원본 구조상 첫 클릭은 OnUnlocked, 그 뒤는 OnRepeatInteract로 온다 → 둘 다 같은 처리
    protected override void OnUnlocked() { Use(); }
    protected override void OnRepeatInteract() { Use(); }

    // 처음 쓸 때 한 번 준비 (Awake/Start를 쓰지 않는 이유: 부모 ConditionalInteractable의 Awake/Start를 가리지 않기 위해)
    void Init()
    {
        if (ready) return;
        ready = true;
        handTarget = new GameObject("DustyFrame_HandTarget").transform;
        if (wipeHand != null) wipeHand.gripPoint = handTarget;
        cam = Camera.main;
        if (dust != null) MakeMask();
    }

    // 먼지 판에 "전부 불투명한" 투명도 그림을 새로 만들어 씌움
    void MakeMask()
    {
        dustMat = dust.material;
        Vector3 s = dust.transform.lossyScale;
        float aspect = Mathf.Abs(s.x) > 0.0001f ? Mathf.Abs(s.y / s.x) : 1f;
        maskH = Mathf.Max(8, Mathf.RoundToInt(maskWidth * aspect));
        mask = new Texture2D(maskWidth, maskH, TextureFormat.RGBA32, false);
        mask.wrapMode = TextureWrapMode.Clamp;
        pixels = new Color32[maskWidth * maskH];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
        mask.SetPixels32(pixels);
        mask.Apply();
        if (dustMat.HasProperty("_BaseMap")) dustMat.SetTexture("_BaseMap", mask);   // URP
        if (dustMat.HasProperty("_MainTex")) dustMat.SetTexture("_MainTex", mask);   // Built-in 대비
        clearedCount = 0;
        cleared = 0f;
    }

    void Use()
    {
        if (busy) return;
        Init();
        if (used) { StartCoroutine(PlayOnly(doneDialogue != null ? doneDialogue : photoDialogue)); return; }

        bool hand = OptionState.Instance != null && OptionState.Instance.IsUnlocked(OptionType.Hand);
        if (!hand) { StartCoroutine(PlayOnly(dustyDialogue)); return; }

        StartCoroutine(WipeRoutine());
    }

    IEnumerator WipeRoutine()
    {
        busy = true;
        used = true;

        // 1) 액자 앞으로 다가감
        if (focus != null)
        {
            focus.Focus();
            // 한 프레임 뒤에 지움: 이 클릭은 PlayerInteractor 안에서 불려서, 같은 프레임에 PlayerInteractor가
            // 이름·조준점을 한 번 더 그리고 끝난다. 그 다음 프레임부터는 꺼져 있으니 지운 상태가 유지된다.
            yield return null;
            ClearAimLabel();
            yield return new WaitForSeconds(focus.moveTime + 0.05f);
        }

        Transform hand = PlayerHand.Instance != null ? PlayerHand.Instance.transform : null;
        if (hand == null || wipeHand == null || wipePoint == null || dust == null)
        {
            Debug.LogWarning("DustyFrame: 손 / Wipe Hand / Wipe Point / Dust 연결을 확인하세요.", this);
        }
        else
        {
            Vector3 homeLocal = hand.localPosition;
            Quaternion homeLocalRot = hand.localRotation;
            sway = hand.GetComponent<HandSway>();
            if (sway != null) sway.enabled = false;
            if (brushBone == null) brushBone = FindFingerBone(hand);

            // 2) 손이 사진 앞에 나타남 + 조작 안내
            //    cursor = 사진 면 위의 "문지르는 점". 처음엔 사진 가운데.
            //    손 목표점 = cursor + (Wipe Point - 사진 가운데) → cursor가 가운데일 때 손은 맞춰 둔 Wipe Point 자리
            Transform surf = dust.transform;
            Vector3 normal = surf.forward;
            Vector3 cursor = surf.position;
            Vector3 handOffset = wipePoint.position - surf.position;
            handTarget.SetPositionAndRotation(cursor + handOffset, wipePoint.rotation);
            wipeHand.moveTime = reachTime;
            wipeHand.Begin();
            while (!wipeHand.IsHolding) yield return null;
            if (hint != null) hint.Show(guideText);

            // 3) 좌클릭을 누른 채 마우스로 문지르기
            //    손 기준점(손목 쪽)이 처음부터 화면 밖이면 "화면 밖 막기"가 계속 움직임을 막으므로, 그때는 막기를 끔
            bool clampToScreen = OnScreen(cursor + handOffset);
            handOnScreen = clampToScreen;
            if (!clampToScreen) Debug.LogWarning("DustyFrame: 손 기준점이 처음부터 화면 밖이라 '화면 밖 막기'를 껐어요. Screen Margin을 줄이거나 Wipe Point를 화면 안쪽으로 옮기세요.", this);
            float tilt = 0f;
            while (cleared < finishRatio)
            {
                float dt = Time.deltaTime;
                Vector3 right = Vector3.ProjectOnPlane(cam != null ? cam.transform.right : surf.right, normal).normalized;
                Vector3 up = Vector3.ProjectOnPlane(cam != null ? cam.transform.up : surf.up, normal).normalized;
                Vector3 moveX = Vector3.zero, moveY = Vector3.zero;

                mouseInput = Vector2.zero;
                if (Input.GetMouseButton(0))
                {
                    // 화면의 오른쪽·위 방향을 사진 면 위로 옮겨서, 마우스를 움직인 쪽으로 손이 움직이게
                    mouseInput = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
                    moveX = right * (mouseInput.x * mouseSpeed);
                    moveY = up * (mouseInput.y * mouseSpeed);

                    // 화면 밖으로 나가면 그 방향으로는 안 움직임 (가로·세로 따로 확인)
                    if (!clampToScreen || OnScreen(cursor + moveX + handOffset)) cursor += moveX; else moveX = Vector3.zero;
                    if (!clampToScreen || OnScreen(cursor + moveY + handOffset)) cursor += moveY; else moveY = Vector3.zero;
                }

                // 빠르게 좌우로 문지를수록 손목이 살짝 기움
                float speedX = dt > 0f ? Vector3.Dot(moveX, right) / dt : 0f;
                tilt = Mathf.Lerp(tilt, Mathf.Clamp(-speedX * 20f, -wipeTwist, wipeTwist), 1f - Mathf.Exp(-10f * dt));
                Quaternion twist = Quaternion.AngleAxis(tilt, normal);
                handTarget.SetPositionAndRotation(cursor + handOffset, twist * wipePoint.rotation);

                // 지우기: 손목이 아니라 손가락이 닿는 자리 기준. 빠르게 문질러도 끊기지 않게 지난 자리부터 이어서 지움
                Vector3 brush = brushBone != null ? brushBone.position : cursor;
                if (Input.GetMouseButton(0)) EraseLine(hasLastBrush ? lastBrush : brush, brush);
                lastBrush = brush;
                hasLastBrush = Input.GetMouseButton(0);
                yield return null;
            }

            // 4) 다 닦음 → 안내 숨김, 손을 원래 자리로 천천히 (남은 먼지는 그대로)
            if (hint != null) hint.Hide();
            onWiped.Invoke();
            Vector3 fromPos = handTarget.position;
            Quaternion fromRot = handTarget.rotation;
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(returnTime, 0.01f))
            {
                float k = Mathf.SmoothStep(0f, 1f, t);
                Vector3 homePos = hand.parent != null ? hand.parent.TransformPoint(homeLocal) : homeLocal;
                Quaternion homeRot = hand.parent != null ? hand.parent.rotation * homeLocalRot : homeLocalRot;
                handTarget.SetPositionAndRotation(Vector3.Lerp(fromPos, homePos, k), Quaternion.Slerp(fromRot, homeRot, k));
                yield return null;
            }
            wipeHand.Release();
            if (sway != null) sway.enabled = true;
        }

        // 5) 사진 쪽으로 조금 더 다가감 (바라보는 방향으로 몸을 이동, 고개 각도는 그대로)
        if (focus != null && cam != null && closeDistance > 0f)
        {
            FirstPersonPlayer fpp = FindFirstObjectByType<FirstPersonPlayer>();
            if (fpp != null)
            {
                Transform body = fpp.transform;
                Vector3 from = body.position;
                Vector3 to = from + cam.transform.forward * closeDistance;
                for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(closeTime, 0.01f))
                {
                    body.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
                    yield return null;
                }
                body.position = to;
            }
        }

        // 6) 사진을 보며 대사 → (대사창이 완전히 닫히고) 잠깐 바라본 뒤 → 물러서면서 하얗게 → 회상
        if (photoDialogue == null) Debug.LogWarning("DustyFrame: Photo Dialogue가 비어 있어서 대사 없이 바로 회상으로 가요.", this);
        yield return Wait(photoDialogue);
        yield return new WaitForSeconds(holdAfterDialogue);
        onMemory.Invoke();                   // 사진을 바라보는 채로 하얗게 덮이기 시작
        if (focus != null) yield return ReleaseFocusBehindWhite();
        busy = false;
    }

    // 하얀 화면이 거의 다 덮이면, 그 뒤에서 포커싱을 즉시 풀고 원래 자리로 되돌려 둠
    //   MemoryTrip은 완전히 하얘진 순간의 자리를 "돌아올 자리"로 저장하므로, 그 전에 원래 자리로 돌려놔야
    //   회상에서 돌아왔을 때 액자 코앞이 아니라 원래 서 있던 자리에서 깨어난다.
    IEnumerator ReleaseFocusBehindWhite()
    {
        if (whiteScreen == null)
        {
            Debug.LogWarning("DustyFrame: White Screen이 비어 있어서 물러서는 모습이 보여요. MemoryTrip의 하얀 Image를 넣어 주세요.", this);
            focus.Return();
            yield break;
        }

        // 최대 5초 기다림 (회상이 이미 진행 중이라 Enter가 무시된 경우 등 대비)
        for (float t = 0f; t < 5f && whiteScreen.color.a < 0.99f; t += Time.deltaTime) yield return null;

        if (whiteScreen.color.a >= 0.99f)
        {
            focus.ReturnInstant();
            // ReturnInstant가 조작을 다시 켜므로, 하얀 화면 동안은 다시 꺼 둠 (회상 쪽에서 알아서 다시 켬)
            FirstPersonPlayer fpp = FindFirstObjectByType<FirstPersonPlayer>();
            if (fpp != null) fpp.enabled = false;
        }
        else
        {
            focus.Return();                  // 하얘지지 않았으면 평소처럼 물러섬
        }
    }

    // 화면 아래 조준 이름("오래된 액자")과 손 아이콘 지우기
    //   ViewFocus가 PlayerInteractor를 끄면, 꺼지기 직전 이름이 화면에 그대로 남는다.
    //   공용 PlayerInteractor는 건드리지 않고, 이쪽에서 글자만 비워 둔다.
    //   (돌아오면 PlayerInteractor가 다시 켜지면서 알아서 이름을 새로 씀)
    void ClearAimLabel()
    {
        PlayerInteractor pi = FindFirstObjectByType<PlayerInteractor>();
        if (pi == null) return;
        if (pi.nameText != null) pi.nameText.text = "";
        if (pi.handIcon != null) pi.handIcon.SetActive(false);
        if (pi.crosshairDot != null) pi.crosshairDot.SetActive(false);   // 돌아오면 ViewFocus·PlayerInteractor가 다시 켬
    }

    // 손 목표점이 화면 안(가장자리 여백 제외)에 있는지
    bool OnScreen(Vector3 worldPos)
    {
        if (cam == null) return true;
        Vector3 v = cam.WorldToViewportPoint(worldPos);
        return v.z > 0f && v.x > screenMargin && v.x < 1f - screenMargin && v.y > screenMargin && v.y < 1f - screenMargin;
    }

    // 가운뎃손가락 두 번째 마디 뼈를 찾음 (HandPose의 뼈 이름 규칙: bonePrefix + Middle + 2)
    Transform FindFingerBone(Transform hand)
    {
        HandPose pose = hand.GetComponentInChildren<HandPose>();
        Transform root = pose != null && pose.rig != null ? pose.rig : hand;
        string want = (pose != null ? pose.bonePrefix : "J_Right_Hand") + "Middle2";
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == want) return t;
        Debug.LogWarning("DustyFrame: 손가락 뼈(" + want + ")를 못 찾아서 손 기준점으로 지워요. Brush Bone 칸에 직접 넣어 주세요.", this);
        return null;
    }

    // a → b 사이를 촘촘히 지움 (빠르게 문질러도 자국이 끊기지 않게)
    void EraseLine(Vector3 a, Vector3 b)
    {
        float width = Mathf.Abs(dust.transform.lossyScale.x);
        float step = Mathf.Max(0.002f, brushSize * width * 0.25f);   // 붓 크기의 1/4 간격
        int n = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(a, b) / step), 1, 30);
        for (int i = 1; i <= n; i++) Erase(Vector3.Lerp(a, b, (float)i / n), i == n);
    }

    // cursor 자리(+ Brush Offset)의 먼지를 둥글게 지움 (가장자리는 부드럽게)
    void Erase(Vector3 worldPoint, bool apply = true)
    {
        if (mask == null) return;
        Vector3 local = dust.transform.InverseTransformPoint(worldPoint);   // Quad 기준 -0.5 ~ 0.5
        float u = local.x + 0.5f + brushOffset.x;
        float v = local.y + 0.5f + brushOffset.y;

        int cx = Mathf.RoundToInt(u * (maskWidth - 1));
        int cy = Mathf.RoundToInt(v * (maskH - 1));
        int r = Mathf.Max(1, Mathf.RoundToInt(brushSize * maskWidth * 0.5f));
        bool changed = false;

        for (int y = cy - r; y <= cy + r; y++)
        {
            if (y < 0 || y >= maskH) continue;
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (x < 0 || x >= maskWidth) continue;
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / r;
                if (d > 1f) continue;
                byte target = (byte)Mathf.RoundToInt(255f * Mathf.SmoothStep(0f, 1f, d));   // 가운데일수록 투명
                int i = y * maskWidth + x;
                byte before = pixels[i].a;
                if (target < before)
                {
                    pixels[i].a = target;
                    if (before >= 128 && target < 128) clearedCount++;   // 반 이상 투명해진 픽셀 = 지워진 것으로 셈
                    changed = true;
                }
            }
        }

        if (changed) dirty = true;
        if (apply && dirty)
        {
            mask.SetPixels32(pixels);
            mask.Apply();
            cleared = (float)clearedCount / pixels.Length;
            dirty = false;
        }
    }

    IEnumerator PlayOnly(DialogueStep step)
    {
        busy = true;
        yield return Wait(step);
        busy = false;
    }

    // 대사가 "실제로 화면에 뜬 뒤" → "완전히 닫힐 때"까지 기다림
    //   DialogueStep은 Play() 직후 한 프레임 쉬고 나서 대사창을 띄우기 때문에,
    //   Play() 바로 다음에 "끝났나?"를 물으면 아직 시작도 안 한 상태라 끝난 걸로 착각할 수 있다. → 먼저 "떴는지"를 기다림
    IEnumerator Wait(DialogueStep step)
    {
        if (step == null) yield break;
        TeamRoulette.UI.GameUI ui = FindFirstObjectByType<TeamRoulette.UI.GameUI>();
        step.Play();

        // 1) 대사창이 뜰 때까지 (최대 2초. 대사 파일이 비어 있으면 안 뜨므로 그냥 넘어감)
        for (float t = 0f; t < 2f; t += Time.unscaledDeltaTime)
        {
            if (step.IsPlaying || (ui != null && ui.IsDialogueOpen)) break;
            yield return null;
        }

        // 2) 대사창이 닫히고 DialogueStep도 끝날 때까지
        while (step.IsPlaying || (ui != null && (ui.IsDialogueOpen || ui.IsPaused))) yield return null;
    }
}
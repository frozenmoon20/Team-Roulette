using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// 미니게임 2 전체 진행 담당
// Begin(): 꿈 공간(하얀 공간)을 켜고, 플레이어를 그곳으로 옮기고, 눈 뜨기(EyeOpen)와 고개 돌리기(DreamTurn)를 켠다.
//          꿈 공간에서만 하얀 안개를 켠다 (침실 설정은 기억해 뒀다가 깨어날 때 되돌림).
//          꿈속에서는 하프톤을 깨끗하게(dreamHalftone) 고정해서, 깨어날 때 짙어지는 대비를 만든다.
//          꿈속 내내 카메라가 미세하게 흔들린다 (심리적 불안정감). 하프톤 전환 때 점점 크고 빠르게 흔들리다가,
//          침실로 넘어오는 순간 확 줄어들어(깨어남) 조금만 남았다가 가라앉는다.
//   꿈에 들어가면: 캄캄한 채 개 소리 → 천천히 눈을 떠 공간을 보여 줌 → 혼잣말(introDialogue) → 눈이 감기며 게임 시작(onIntroFinished)
// OnDogLooked(): 개를 다 찾으면 DreamTurn의 onLooked에서 부른다 → 깨어남 연출 시작
//   개가 서서히 투명해지며 사라짐 → 하프톤이 짙어짐 → 침실 침대로 이동 → 하프톤 원래대로
//   → 대사(공용 UI, DialogueStep) → RotateCamera 해금 → 창밖 개 짖는 소리가 서서히 사라짐
// 미니게임 1(SoundSearch)의 onCleared에 Begin()을 연결한다.
//   꿈에 들어가면: 미니게임 1의 짖는 소리를 멈추고, 검은 화면(Blackout)을 끄고, 커서를 잠근다
//   깨어나면: 짖는 소리를 "창밖 소리"로 다시 틀고, 미니게임 1이 꺼 뒀던 것(PlayerInteractor, 조준점 등)을 다시 켠다
// 빈 오브젝트 "Minigame2"에 붙인다.
public class Minigame2 : MonoBehaviour
{
    [Header("연결")]
    public Transform player;                 // Player (맨 위 부모)
    public Transform cameraRoot;             // Player의 PlayerCameraRoot (깨어날 때 고개를 정면으로)
    public Transform shakeCamera;            // Player의 Camera (PlayerCameraRoot 아래). 흔들림은 여기에만 준다
    public GameObject dreamSpace;            // DreamSpace (평소엔 꺼 둠. 미니게임 2 동안만 켜짐)
    public Transform dreamStart;             // 꿈 공간에서 플레이어가 설 자리 (= DreamTest)
    public GameObject eyeOpen;               // MinigameCanvas/EyeOpen
    public GameObject dreamTurn;             // DreamTest (DreamTurn이 붙은 오브젝트)
    public Transform dog;                    // Test_Dog (사라지는 연출용)
    public Transform bedPoint;               // 깨어났을 때 설 자리 (침대 옆 빈 오브젝트, 파란 화살표가 바라볼 방향)

    [Header("꿈 시작 연출 (천천히 눈 뜨기 → 혼잣말 → 눈 감기)")]
    public DialogueStep introDialogue;       // 눈을 뜬 채로 나오는 혼잣말 (Dialogue_DreamStart)
    public float introDelay = 1.5f;          // 꿈에 들어와서 캄캄한 채 소리만 듣는 시간 (초)
    public float introOpenTime = 3f;         // 천천히 눈을 뜨는 시간 (초)
    public float introLookTime = 1.5f;       // 다 뜬 뒤 대사 전까지 둘러보는 시간 (초)
    public UnityEvent onIntroFinished;       // 눈이 감기고 게임이 시작될 때 (목표 표시 등)

    [Header("깨어남 이벤트 (목표창 숨기기/다시 보이기 등)")]
    public UnityEvent onDarkenStart;         // 개가 사라지고 꿈이 어두워지기 시작할 때
    public UnityEvent onWakeFinished;        // 침실에서 깨고 카메라 회전이 해금됐을 때

    [Header("미니게임 1 연결")]
    public SoundSearchMinigame soundSearch;  // MinigameCanvas/SoundSearch
    public GameObject blackout;              // SoundSearch/Blackout (미니게임 1의 검은 화면)
    public float outsideBarkCutoff = 1500f;  // 깨어난 뒤 창밖 짖는 소리의 먹먹함 (Hz, 낮을수록 먹먹)
    public float outsideBarkFadeTime = 10f;  // 해금 후 짖는 소리가 사라지는 데 걸리는 시간 (초)

    [Header("꿈 공간 안개")]
    public Color fogColor = Color.white;
    public float fogStart = 5f;              // 이 거리부터 흐려지기 시작 (m)
    public float fogEnd = 40f;               // 이 거리에서 완전히 안개 색 (m)

    [Header("하프톤 (이 프로젝트는 0 = 가장 짙음, 1 = 깨끗함)")]
    [Range(0f, 1f)] public float dreamHalftone = 1f;  // 꿈속: 해금 상태와 상관없이 깨끗한 하얀 공간으로
    [Range(0f, 1f)] public float darkHalftone = 0f;   // 깨어나는 순간: 가장 짙게
    public Color darkSpaceColor = new Color(0.08f, 0.08f, 0.08f);   // 하프톤이 짙어질 때 하얀 공간(하늘·바닥·기둥·안개)이 어두워질 색

    [Header("카메라 흔들림 (불안정감)")]
    public float shakeAngle = 0.8f;          // 꿈속 흔들림 폭 (도)
    public float shakeSpeed = 0.6f;          // 흔들림 빠르기 (낮을수록 느릿하게 출렁임)
    public float wakeShakeMultiplier = 3f;   // 깨어나는 순간 흔들림 폭이 몇 배까지 커질지
    public float wakeShakeSpeedMultiplier = 4f;   // 깨어나는 순간 흔들림 빠르기가 몇 배까지 빨라질지
    public float bedShake = 0.3f;            // 침실로 넘어온 직후 남는 흔들림 (꿈속 기본 대비 배율, 0이면 바로 멈춤)

    [Header("깨어남 연출 시간 (초)")]
    public float dogStayTime = 2f;           // 개를 바라본 뒤 머무는 시간
    public float dogFadeTime = 4f;           // 개가 투명해지며 사라지는 시간
    public float afterDogTime = 1.5f;        // 개가 사라진 뒤 빈 공간을 바라보는 시간
    public float halftoneInTime = 3f;        // 하프톤이 짙어지는 시간
    public float darkHoldTime = 1.5f;        // 가장 짙은 상태로 머무는 시간 (이때 침실로 이동)
    public float halftoneOutTime = 3f;       // 침실에서 하프톤이 원래대로 돌아오는 시간

    [Header("깨어난 뒤 대사 (DialogueStep. 비워 두면 대사 없이 바로 해금)")]
    public DialogueStep wakeDialogue;        // 대사가 끝날 때까지 기다렸다가 RotateCamera 해금

    [Header("테스트용")]
    public bool beginOnStart = false;        // 체크하면 Play 하자마자 미니게임 2 시작 (미니게임 1 건너뜀)
    [Tooltip("에디터 테스트용: 꿈 시작·깨어남 연출을 거의 0초로, 혼잣말·깨어남 대사 생략, 개 찾기도 자동으로 끝냄. 빌드에서는 무시됨")]
    public bool fastTest = false;

    float shakeStrength;                     // 지금 흔들림 폭 배율 (0 = 없음, 1 = 꿈속 기본)
    float shakeSpeedScale = 1f;              // 지금 흔들림 빠르기 배율 (1 = 꿈속 기본)
    float shakePhase;                        // 흔들림 노이즈를 읽는 위치 (매 프레임 빠르기만큼 앞으로 감)

    // 빠른 테스트 중인지 (에디터에서만)
    bool Fast => fastTest && Application.isEditor;

    // 연출 시간: 빠른 테스트면 거의 0초
    float T(float seconds) => Fast ? 0.01f : seconds;

    // 침실 안개 설정 (깨어날 때 되돌리기용)
    bool savedFog;
    Color savedFogColor;
    FogMode savedFogMode;
    float savedFogStart, savedFogEnd;

    void Start()
    {
        if (beginOnStart) Begin();
    }

    // 미니게임 2 시작 (나중에 미니게임 1이 끝날 때 이걸 부른다)
    public void Begin()
    {
        // 미니게임 1 정리: 짖는 소리 멈춤(꿈속 개 소리와 섞이지 않게), 검은 화면 끔, 커서 잠금
        if (soundSearch != null) soundSearch.StopBarking();
        if (blackout != null) blackout.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        dreamSpace.SetActive(true);
        Teleport(dreamStart);
        SetDreamFog(true);
        HalftoneController.SetOverride(dreamHalftone);
        shakeStrength = 1f;
        shakeSpeedScale = 1f;
        dreamTurn.SetActive(true);
        eyeOpen.SetActive(true);
        EyeOpen eye = eyeOpen.GetComponent<EyeOpen>();
        DreamTurn turn = dreamTurn.GetComponent<DreamTurn>();
        if (eye != null)
        {
            eye.ResetEye();                  // 캄캄한 상태로 처음부터 (EyeOpen은 켜질 때 스스로 초기화하지 않음)
            if (turn != null) turn.locked = true;   // 시작 연출 동안은 고개 돌리기·클릭 잠금
            StartCoroutine(Intro(eye, turn));
        }
    }

    // 꿈 시작 연출: 캄캄한 채 소리 → 천천히 눈 뜨기 → 혼잣말 → 눈 감기 → 게임 시작
    IEnumerator Intro(EyeOpen eye, DreamTurn turn)
    {
        yield return new WaitForSeconds(T(introDelay));

        eye.OpenSlow(T(introOpenTime));
        yield return new WaitForSeconds(T(introOpenTime + introLookTime));

        if (introDialogue != null && !Fast)
        {
            introDialogue.Play();
            yield return new WaitUntil(() => !introDialogue.IsPlaying);   // 대사가 다 끝나고 입력이 돌아올 때까지
        }

        eye.CloseEye();
        yield return new WaitUntil(() => !eye.IsBusy);                    // 눈이 다 감길 때까지
        if (turn != null) turn.locked = false;                            // 이제부터 조작 가능
        onIntroFinished.Invoke();

        // 빠른 테스트: 개 찾기 없이 바로 깨어남 연출로
        if (Fast)
        {
            if (turn != null) turn.locked = true;
            OnDogLooked();
        }
    }

    // 카메라 흔들림: 불규칙하게 천천히 출렁이는 회전 (위아래, 좌우, 기울기)
    void LateUpdate()
    {
        if (shakeCamera == null) return;

        // 빠르기를 Time.time * 속도로 계산하면 속도를 바꾸는 순간 위치가 확 튄다.
        // 그래서 "이번 프레임에 얼마나 나아갈지"만 더해 간다 → 속도가 바뀌어도 부드럽게 빨라짐
        shakePhase += Time.deltaTime * shakeSpeed * shakeSpeedScale;
        float t = shakePhase;
        float a = shakeAngle * shakeStrength;
        float x = (Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f * a;
        float y = (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f * a;
        float z = (Mathf.PerlinNoise(t, t) - 0.5f) * 2f * a * 0.6f;
        shakeCamera.localRotation = Quaternion.Euler(x, y, z);
    }

    // DreamTurn의 onLooked에 연결: 개를 다 바라봤을 때
    public void OnDogLooked()
    {
        StartCoroutine(WakeUp());
    }

    IEnumerator WakeUp()
    {
        // 1) 개를 바라보며 잠시 머문 뒤, 서서히 투명해지며 사라짐
        yield return new WaitForSeconds(T(dogStayTime));
        Renderer[] renderers = dog.GetComponentsInChildren<Renderer>();
        for (float t = 0f; t < 1f; t += Time.deltaTime / T(dogFadeTime))
        {
            SetAlpha(renderers, 1f - Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        dog.gameObject.SetActive(false);
        yield return new WaitForSeconds(T(afterDogTime));

        // 2) 하프톤이 짙어지고, 하얀 공간 전체가 어두워지고, 흔들림이 점점 크고 빨라짐 (꿈이 무너지는 느낌)
        onDarkenStart.Invoke();
        Renderer[] space = dreamSpace.GetComponentsInChildren<Renderer>();   // 하늘, 바닥, 기둥 (개는 이미 꺼짐)
        Material[] mats = CollectMaterials(space);
        Color[] startColors = new Color[mats.Length];
        for (int i = 0; i < mats.Length; i++) startColors[i] = mats[i].GetColor(BaseColorId);

        for (float t = 0f; t < 1f; t += Time.deltaTime / T(halftoneInTime))
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            HalftoneController.SetOverride(Mathf.Lerp(dreamHalftone, darkHalftone, k));
            for (int i = 0; i < mats.Length; i++) mats[i].SetColor(BaseColorId, Color.Lerp(startColors[i], darkSpaceColor, k));
            RenderSettings.fogColor = Color.Lerp(fogColor, darkSpaceColor, k);   // 안개도 같이 (안 하면 먼 기둥은 하얗게 남음)
            shakeStrength = Mathf.Lerp(1f, wakeShakeMultiplier, k);
            shakeSpeedScale = Mathf.Lerp(1f, wakeShakeSpeedMultiplier, k * k);   // k*k: 처음엔 천천히, 끝으로 갈수록 확 빨라짐
            yield return null;
        }
        HalftoneController.SetOverride(darkHalftone);

        // 3) 가장 짙은 순간에 꿈 공간을 끄고 침실로 이동 (고개도 정면으로)
        dreamTurn.SetActive(false);
        eyeOpen.SetActive(false);
        SetDreamFog(false);
        dreamSpace.SetActive(false);
        for (int i = 0; i < mats.Length; i++) mats[i].SetColor(BaseColorId, startColors[i]);   // 다음을 위해 원래 색으로
        Teleport(bedPoint);
        if (cameraRoot != null) cameraRoot.localRotation = Quaternion.identity;
        shakeStrength = bedShake;                // 깨어나는 순간: 크게 흔들리던 게 확 줄어듦 (꿈과 현실의 경계)
        shakeSpeedScale = 1f;
        if (soundSearch != null) soundSearch.StartBarking(outsideBarkCutoff);   // 현실: 창밖에서 짖는 소리
        yield return new WaitForSeconds(T(darkHoldTime));

        // 4) 하프톤이 원래(해금 기준) 값으로 돌아오고, 흔들림도 작고 느리게 가라앉음
        float startShake = shakeStrength;
        float startSpeed = shakeSpeedScale;
        for (float t = 0f; t < 1f; t += Time.deltaTime / T(halftoneOutTime))
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            HalftoneController.SetOverride(Mathf.Lerp(darkHalftone, HalftoneController.CurrentValue, k));
            shakeStrength = Mathf.Lerp(startShake, 0f, k);
            shakeSpeedScale = Mathf.Lerp(startSpeed, 1f, k);
            yield return null;
        }
        HalftoneController.ClearOverride();
        shakeStrength = 0f;
        shakeSpeedScale = 1f;
        if (shakeCamera != null) shakeCamera.localRotation = Quaternion.identity;

        // 5) 미니게임 1이 꺼 뒀던 것(PlayerInteractor, 조준점 등) 다시 켜기 → 대사 (공용 UI 대화창)
        if (soundSearch != null)
            foreach (Behaviour b in soundSearch.disableWhilePlaying) if (b != null) b.enabled = true;
        if (wakeDialogue != null && !Fast)
        {
            wakeDialogue.Play();
            yield return new WaitUntil(() => !wakeDialogue.IsPlaying);   // 대사가 다 끝나고 입력이 돌아올 때까지
        }

        // 6) 카메라 회전 해금 → 해금 알림은 UnlockNotice가 띄움
        OptionState.Instance.Unlock(OptionType.RotateCamera);
        onWakeFinished.Invoke();
        Debug.Log("미니게임 2 끝: RotateCamera 해금");

        // 7) 창밖 짖는 소리가 서서히 사라짐
        if (soundSearch != null) soundSearch.FadeOutBarking(outsideBarkFadeTime);
    }

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    // 렌더러들의 머티리얼을 모두 모은다 (.materials = 이 오브젝트 전용 복사본이라 원본 파일은 안 바뀜)
    static Material[] CollectMaterials(Renderer[] renderers)
    {
        var list = new System.Collections.Generic.List<Material>();
        foreach (Renderer r in renderers)
            foreach (Material m in r.materials)
                if (m.HasProperty(BaseColorId)) list.Add(m);
        return list.ToArray();
    }

    // 개의 머티리얼 투명도 바꾸기 (머티리얼의 Surface Type이 Transparent여야 보임)
    void SetAlpha(Renderer[] renderers, float alpha)
    {
        foreach (Renderer r in renderers)
        {
            foreach (Material m in r.materials)          // .materials = 이 개 전용 복사본 (원본 파일은 그대로)
            {
                if (!m.HasProperty(BaseColorId)) continue;
                Color c = m.GetColor(BaseColorId);
                c.a = alpha;
                m.SetColor(BaseColorId, c);
            }
        }
    }

    // on = true: 꿈 안개 켜기 (지금 설정은 기억) / false: 기억해 둔 침실 설정으로 되돌리기
    void SetDreamFog(bool on)
    {
        if (on)
        {
            savedFog = RenderSettings.fog;
            savedFogColor = RenderSettings.fogColor;
            savedFogMode = RenderSettings.fogMode;
            savedFogStart = RenderSettings.fogStartDistance;
            savedFogEnd = RenderSettings.fogEndDistance;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
        }
        else
        {
            RenderSettings.fog = savedFog;
            RenderSettings.fogColor = savedFogColor;
            RenderSettings.fogMode = savedFogMode;
            RenderSettings.fogStartDistance = savedFogStart;
            RenderSettings.fogEndDistance = savedFogEnd;
        }
    }

    // CharacterController는 켜져 있으면 위치를 직접 바꿔도 되돌려 버려서, 잠깐 끄고 옮긴다
    void Teleport(Transform target)
    {
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.SetPositionAndRotation(target.position, target.rotation);
        if (cc != null) cc.enabled = true;
    }
}
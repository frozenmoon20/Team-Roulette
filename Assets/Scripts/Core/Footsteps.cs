using UnityEngine;

// 플레이어 발소리. Player(CharacterController + PlayerMotionFeel이 있는 맨 위 오브젝트)에 붙인다.
// 원리:
//   - PlayerMotionFeel이 걸을 때 카메라를 출렁이게 하면서, 발을 디디는 순간(카메라가 가장 낮을 때)마다 OnFootstep을 보낸다.
//     이 스크립트는 그 신호를 듣고 그 순간에 발소리를 낸다 → 화면 출렁임과 발소리 박자가 항상 맞는다.
//   - 걸음 빠르기는 PlayerMotionFeel의 Step Distance로 조절한다 (출렁임과 발소리가 같이 바뀜).
//   - 발소리 여러 개(Step Clips)를 무작위로 고르고 높낮이도 조금씩 바꿔서 기계적인 반복을 피한다.
//   - 달릴 때(Run Speed 이상)는 조금 더 크게.
//   - 공중에 있다가 땅에 닿으면 착지 소리(Land Clip) 한 번.
//   - 소리는 효과음(SFX) 해금 전에는 안 나고, 설정창 효과음 볼륨을 따른다 (AudioManager.Volume).
// PlayerMotionFeel·FirstPersonPlayer는 건드리지 않는다 (이벤트를 듣기만 함).
[RequireComponent(typeof(CharacterController))]
public class Footsteps : MonoBehaviour
{
    [Header("소리")]
    public AudioClip[] stepClips;            // 발소리 여러 개 (1개만 넣어도 됨)
    public AudioClip landClip;               // 착지 소리 (비워 둬도 됨)

    [Header("크기")]
    public float runSpeed = 5f;              // 이 속도(m/s) 이상이면 달리기로 봄 (FirstPersonPlayer: 걷기 4, 달리기 6)
    [Range(0f, 1f)] public float walkVolume = 0.35f;
    [Range(0f, 1f)] public float runVolume = 0.55f;
    public float pitchJitter = 0.08f;        // 높낮이를 이만큼 무작위로 흔듦

    [Header("착지")]
    public float minAirTime = 0.25f;         // 이보다 오래 공중에 있다가 내려와야 착지 소리
    [Range(0f, 1f)] public float landVolume = 0.6f;

    PlayerMotionFeel motion;
    CharacterController controller;
    AudioSource source;                      // 높낮이를 바꾸려고 발소리 전용 AudioSource를 따로 씀
    float airTime;
    int lastIndex = -1;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        motion = GetComponent<PlayerMotionFeel>();
        if (motion == null) Debug.LogWarning("Footsteps: 같은 오브젝트에 PlayerMotionFeel이 없어서 발소리가 안 나요.", this);
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;            // 내 발소리라 2D
    }

    void OnEnable() { if (motion != null) motion.OnFootstep += Step; }
    void OnDisable() { if (motion != null) motion.OnFootstep -= Step; }

    // PlayerMotionFeel이 "발을 디뎠다"고 알려 줄 때
    void Step()
    {
        Vector3 v = controller.velocity;
        v.y = 0f;
        bool running = v.magnitude >= runSpeed;
        Play(PickStep(), running ? runVolume : walkVolume);
    }

    // 착지 소리
    void Update()
    {
        if (!controller.enabled) { airTime = 0f; return; }
        if (!controller.isGrounded) { airTime += Time.deltaTime; return; }
        if (airTime >= minAirTime) Play(landClip, landVolume);
        airTime = 0f;
    }

    // 직전과 다른 발소리를 무작위로
    AudioClip PickStep()
    {
        if (stepClips == null || stepClips.Length == 0) return null;
        if (stepClips.Length == 1) return stepClips[0];
        int i;
        do { i = Random.Range(0, stepClips.Length); } while (i == lastIndex);
        lastIndex = i;
        return stepClips[i];
    }

    void Play(AudioClip clip, float volume)
    {
        if (clip == null) return;
        float v = AudioManager.Volume;
        if (v <= 0f) return;
        source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        source.PlayOneShot(clip, volume * v);
    }
}
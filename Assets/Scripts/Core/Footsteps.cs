
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class Footsteps : MonoBehaviour
{
    public AudioClip[] stepClips;
    public AudioClip landClip;

    public float runSpeed = 5f;
    [Range(0f, 1f)] public float walkVolume = 0.35f;
    [Range(0f, 1f)] public float runVolume = 0.55f;
    public float pitchJitter = 0.08f;

    public float minAirTime = 0.25f;
    [Range(0f, 1f)] public float landVolume = 0.6f;

    PlayerMotionFeel motion;
    CharacterController controller;
    AudioSource source;
    float airTime;
    int lastIndex = -1;

   
void Awake()
{
    controller = GetComponent<CharacterController>();
    motion = GetComponent<PlayerMotionFeel>();

    if (motion == null)
        Debug.LogWarning("Footsteps: PlayerMotionFeel이 없습니다.", this);

    source = GetComponent<AudioSource>();

    if (source == null)
        source = gameObject.AddComponent<AudioSource>();

    if (source == null)
    {
        Debug.LogError("Footsteps: AudioSource 초기화 실패", this);
        enabled = false;
        return;
    }

    source.playOnAwake = false;
    source.spatialBlend = 0f;
}


    void OnEnable()
    {
        if (motion != null)
            motion.OnFootstep += Step;
    }

    void OnDisable()
    {
        if (motion != null)
            motion.OnFootstep -= Step;
    }

    void Step()
    {
        Vector3 v = controller.velocity;
        v.y = 0f;

        bool running = v.magnitude >= runSpeed;
        Play(PickStep(), running ? runVolume : walkVolume);
    }

    void Update()
    {
        if (!controller.enabled)
        {
            airTime = 0f;
            return;
        }

        if (!controller.isGrounded)
        {
            airTime += Time.deltaTime;
            return;
        }

        if (airTime >= minAirTime)
            Play(landClip, landVolume);

        airTime = 0f;
    }

    AudioClip PickStep()
    {
        if (stepClips == null || stepClips.Length == 0)
            return null;

        if (stepClips.Length == 1)
            return stepClips[0];

        int i;
        do
        {
            i = Random.Range(0, stepClips.Length);
        }
        while (i == lastIndex);

        lastIndex = i;
        return stepClips[i];
    }

    void Play(AudioClip clip, float volume)
    {
        if (clip == null)
            return;

        float v = AudioManager.Volume;

        if (v <= 0f)
            return;

        source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        source.PlayOneShot(clip, volume * v);
    }
}

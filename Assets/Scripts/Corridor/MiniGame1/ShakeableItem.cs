using UnityEngine;

public class ShakeableItem : MonoBehaviour
{
    public float minimumMouseSpeed = 3f;
    public float shakeAngle = 8f;
    public float returnSpeed = 12f;

    public AudioClip shakeSound;
    public float soundCooldown = 0.15f;

    public float discoverTime = 2f;

    GrabbableItem grabbable;
    AudioSource audioSource;

    Quaternion restRotation;
    int lastDirection;
    float lastSoundTime;

    float shakeTimer;
    bool discovered;

    public bool IsDiscovered => discovered;

    void Awake()
    {
        grabbable = GetComponent<GrabbableItem>();
        audioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        PlayerHand hand = PlayerHand.Instance;

        if (hand == null || hand.Held != grabbable)
        {
            shakeTimer = 0f;
            return;
        }

        float mouseX = Input.GetAxisRaw("Mouse X");
        float mouseY = Input.GetAxisRaw("Mouse Y");

        Vector2 mouseMove = new Vector2(mouseX, mouseY);

        if (mouseMove.magnitude >= minimumMouseSpeed)
        {
            float xAngle = Mathf.Clamp(mouseY * 2f, -shakeAngle, shakeAngle);
            float zAngle = Mathf.Clamp(-mouseX * 2f, -shakeAngle, shakeAngle);

            transform.localRotation =
                restRotation * Quaternion.Euler(xAngle, 0f, zAngle);

            int direction;

            if (Mathf.Abs(mouseX) > Mathf.Abs(mouseY))
                direction = mouseX > 0 ? 1 : -1;
            else
                direction = mouseY > 0 ? 2 : -2;

            if (lastDirection != 0 &&
                direction != lastDirection &&
                Time.time >= lastSoundTime + soundCooldown)
            {
                if (audioSource != null && shakeSound != null)
                    audioSource.PlayOneShot(shakeSound);

                lastSoundTime = Time.time;
            }

            lastDirection = direction;

            if (!discovered)
            {
                shakeTimer += Time.deltaTime;

                if (shakeTimer >= discoverTime)
                {
                    discovered = true;
                    PlayerInteractor.ShowHint("안에 무언가 있는 것 같다.");
                }
            }
        }
        else
        {
            transform.localRotation = Quaternion.Slerp(
                transform.localRotation,
                restRotation,
                returnSpeed * Time.deltaTime
            );
        }
    }

    void OnEnable()
    {
        restRotation = transform.localRotation;
    }
}
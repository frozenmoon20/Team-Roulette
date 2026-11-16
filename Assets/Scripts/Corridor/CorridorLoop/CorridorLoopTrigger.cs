
using UnityEngine;
using TeamRoulette.UI;

public class LoopCheckTrigger : MonoBehaviour
{
    public CorridorLoopManager manager;
    public GameObject minigame1Root;

    [Header("반복 복도 판정 효과음")]
    public AudioClip correctSound;
    public AudioClip wrongSound;
    public AudioClip clearSound;

    [Range(0f, 1f)]
    public float soundVolume = 0.8f;

    [Header("효과음 중복 방지")]
    [Min(0f)]
    public float soundCooldown = 0.2f;

    private float lastSoundTime = -999f;

    bool started = false;
    bool currentForwardIsTriggerForward = true;

    private void OnTriggerEnter(Collider other)
    {
        FirstPersonPlayer player =
            other.GetComponentInParent<FirstPersonPlayer>();

        if (manager == null || player == null)
            return;

        // 반복 복도 클리어 후에는 모든 판정 중지
        if (manager.IsCleared)
            return;

        CharacterController controller =
            player.GetComponent<CharacterController>();

        if (controller == null)
            return;

        Vector3 moveDirection =
            controller.velocity.normalized;

        bool physicalForward =
            Vector3.Dot(
                moveDirection,
                transform.forward
            ) > 0f;

        // 처음 반복 복도에 진입했을 때
        if (!started)
        {
            started = true;

            currentForwardIsTriggerForward =
                physicalForward;

            SceneUIContent ui =
                FindFirstObjectByType<SceneUIContent>();

            if (ui != null)
            {
                ui.CompleteObjective("corridor_start");
                ui.RevealObjective("corridor_loop");
            }

            manager.PrepareNextLoop();

            if (minigame1Root != null)
                minigame1Root.SetActive(false);

            Debug.Log(
                "반복 복도 시작 - 미니게임 1 비활성화"
            );

            return;
        }

        // 기존 방향 판정 유지
        bool goingForward =
            physicalForward ==
            currentForwardIsTriggerForward;

        bool correct;

        if (manager.hasAnomaly)
            correct = !goingForward;
        else
            correct = goingForward;

        // 기존 성공 / 실패 처리 유지
        if (correct)
            manager.successCount++;
        else
            manager.successCount = 0;

        Debug.Log(
            correct
                ? "정답! 현재 성공 횟수: " +
                  manager.successCount
                : "오답! 성공 횟수 초기화"
        );

        currentForwardIsTriggerForward =
            physicalForward;

        // 다음 복도 준비 또는 최종 클리어
        manager.PrepareNextLoop();

        // 최종 클리어 여부를 확인한 후
        // 우선순위에 따라 한 종류만 재생
        if (manager.IsCleared)
        {
            PlaySound(clearSound);
        }
        else if (correct)
        {
            PlaySound(correctSound);
        }
        else
        {
            PlaySound(wrongSound);
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null)
            return;

        if (Time.time - lastSoundTime <
            soundCooldown)
            return;

        lastSoundTime = Time.time;

        AudioManager.Play(
            clip,
            soundVolume
        );
    }
}

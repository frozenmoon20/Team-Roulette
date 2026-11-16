
using UnityEngine;
using TeamRoulette.UI;

public class Minigame1EntryTrigger : MonoBehaviour
{
    public GameObject mazeRoot;
    public GameObject entranceBlocker;
    public MazeFogController fogController;

    [Header("미로 배경음악")]
    public CorridorMazeBgm mazeBgm;

    [Header("미니게임1 입장 효과음")]
    public AudioClip entrySound;

    [Range(0f, 1f)]
    public float entrySoundVolume = 1f;

    bool activated;

    void OnTriggerEnter(Collider other)
    {
        if (activated)
            return;

        FirstPersonPlayer player =
            other.GetComponentInParent<FirstPersonPlayer>();

        if (player == null)
            return;

        activated = true;

        // 미로 BGM 페이드아웃
        if (mazeBgm != null)
            mazeBgm.FadeOutAndStop();

        // 미니게임1 입장 효과음
        // 기존 AudioManager의 SFX 해금 조건 활용
        if (entrySound != null)
            AudioManager.Play(entrySound, entrySoundVolume);

        SceneUIContent ui =
            FindFirstObjectByType<SceneUIContent>();

        if (ui != null)
        {
            ui.CompleteObjective("maze");
            ui.RevealObjective("minigame1");

            Debug.Log(
                "미로 완료 → 미니게임1 목표 공개"
            );
        }
        else
        {
            Debug.LogError(
                "SceneUIContent를 찾지 못했습니다."
            );
        }

        if (fogController != null)
            fogController.ClearFog();

        if (mazeRoot != null)
            Destroy(mazeRoot);

        if (entranceBlocker != null)
            entranceBlocker.SetActive(true);
    }
}

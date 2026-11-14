
using UnityEngine;

// 복도 컷신 전용: 공용 Core 스크립트를 수정하지 않고
// 발소리와 카메라 걷기 흔들림을 일시 중지한다.
public class CorridorCutsceneWalkSilencer : MonoBehaviour
{
    private FirstPersonPlayer player;
    private PlayerMotionFeel motion;
    private Footsteps footsteps;

    private bool previousMotionEnabled;
    private bool previousFootstepsEnabled;

    private int lockCount;

    public static CorridorCutsceneWalkSilencer GetOrCreate(
        FirstPersonPlayer targetPlayer)
    {
        if (targetPlayer == null)
            return null;

        CorridorCutsceneWalkSilencer silencer =
            targetPlayer.GetComponent<CorridorCutsceneWalkSilencer>();

        if (silencer == null)
        {
            silencer =
                targetPlayer.gameObject
                    .AddComponent<CorridorCutsceneWalkSilencer>();
        }

        silencer.player = targetPlayer;
        return silencer;
    }

    public void Begin()
    {
        if (lockCount > 0)
        {
            lockCount++;
            return;
        }

        if (player == null)
            player = GetComponent<FirstPersonPlayer>();

        if (player == null)
            return;

        motion = player.GetComponent<PlayerMotionFeel>();
        footsteps = player.GetComponent<Footsteps>();

        previousMotionEnabled =
            motion != null && motion.enabled;

        previousFootstepsEnabled =
            footsteps != null && footsteps.enabled;

        // 플레이어의 남아 있는 이동 가속도 제거
        player.StopMovement();

        // 현재 흔들림만 계산해서 카메라 원래 위치 복원
        if (motion != null)
        {
            Transform root = motion.cameraRoot;

            if (root != null && previousMotionEnabled)
            {
                float phase = motion.Phase;
                float amount = motion.BobAmount;

                float x =
                    Mathf.Cos(phase) *
                    motion.bobSide * amount;

                float y =
                    (Mathf.Abs(Mathf.Sin(phase)) - 0.64f) *
                    motion.bobHeight * amount;

                root.localPosition -=
                    new Vector3(x, y, 0f);
            }

            motion.enabled = false;
        }

        // 새 발소리 발생 차단
        if (footsteps != null)
            footsteps.enabled = false;

        // 이미 재생 중인 발소리도 즉시 정지
        // Footsteps는 Player에 있는 AudioSource를 사용한다.
        if (previousFootstepsEnabled)
        {
            AudioSource source =
                player.GetComponent<AudioSource>();

            if (source != null)
                source.Stop();
        }

        lockCount = 1;
    }

    public void End()
    {
        if (lockCount <= 0)
            return;

        lockCount--;

        if (lockCount > 0)
            return;

        if (motion != null)
            motion.enabled = previousMotionEnabled;

        if (footsteps != null)
            footsteps.enabled = previousFootstepsEnabled;
    }
}

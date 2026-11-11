
using UnityEngine;

public class JumpMapRespawn : MonoBehaviour
{
    [Header("리스폰 위치")]
    public Transform respawnPoint;

    [Header("플레이어")]
    public FirstPersonPlayer player;

    [Header("리스폰 설정")]
    public bool resetRotation = true;
    public string respawnMessage = "다시 도전해 보자.";

    private void OnTriggerEnter(Collider other)
    {
        FirstPersonPlayer target =
            other.GetComponentInParent<FirstPersonPlayer>();

        if (target == null)
            return;

        if (player != null && target != player)
            return;

        if (respawnPoint == null)
        {
            Debug.LogWarning("Respawn Point가 연결되지 않았습니다.", this);
            return;
        }

        CharacterController controller =
            target.GetComponent<CharacterController>();

        if (controller != null)
            controller.enabled = false;

        target.transform.position = respawnPoint.position;

        if (resetRotation)
            target.transform.rotation = respawnPoint.rotation;

        if (controller != null)
            controller.enabled = true;

        PlayerInteractor.ShowHint(respawnMessage);

        Debug.Log("점프맵 낙하! 리스폰 완료.");
    }
}

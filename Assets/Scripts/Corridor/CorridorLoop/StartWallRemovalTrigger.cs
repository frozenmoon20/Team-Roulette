
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class StartWallRemovalTrigger : MonoBehaviour
{
    [Header("제거할 시작 벽")]
    public GameObject startWall;

    bool activated = false;

    void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (activated)
            return;

        FirstPersonPlayer player =
            other.GetComponentInParent<FirstPersonPlayer>();

        if (player == null)
            return;

        activated = true;

        if (startWall != null)
        {
            startWall.SetActive(false);
            Debug.Log("시작 벽 제거 완료");
        }
    }
}

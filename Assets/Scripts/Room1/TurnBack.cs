using System.Collections;
using UnityEngine;

// 가면 안 되는 곳(예: 복귀 후 침실 문 앞)에 닿으면: 혼잣말 + 몸을 돌려 가야 할 쪽(계단)을 바라보게 하고 살짝 물러나게 한다.
// 닿을 때마다 매번 반복된다 (밖으로 나갔다가 다시 들어오면 또).
// 원리: 이 오브젝트의 트리거(Is Trigger 체크한 Collider)에 플레이어가 들어오면
//       1) 대사 재생  2) 잠깐 조작을 막고 몸(좌우)을 Look Target 쪽으로 부드럽게 돌리고 고개(위아래)는 정면으로
//       3) 돌아선 방향으로 Push Back 만큼 살짝 걸어 나가게 해서 트리거 밖으로 내보냄  4) 조작을 돌려줌
// 사용: 침실 문 앞에 투명 큐브(Mesh Renderer 끔, Box Collider의 Is Trigger 체크)를 만들어 붙이고,
//       Look Target에 계단 쪽 빈 오브젝트(또는 Stairs)를, Dialogue에 "…이제는 거실로 가야 해." DialogueStep을 넣는다.
//       복귀할 때만 동작해야 하면 오브젝트를 꺼 두고 ReturnEntry의 On Return에서 SetActive(true)로 켠다.
[RequireComponent(typeof(Collider))]
public class TurnBack : MonoBehaviour
{
    public Transform lookTarget;             // 돌아서 바라볼 곳 (계단 입구 등)
    public DialogueStep dialogue;            // 닿을 때마다 나올 대사 (비워도 됨)
    public float turnTime = 0.8f;            // 몸을 돌리는 시간 (초)
    public float pushBack = 0.8f;            // 돌아선 뒤 그쪽으로 물러나는 거리 (m). 트리거 밖으로 나갈 만큼

    bool busy;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (busy || lookTarget == null) return;
        FirstPersonPlayer player = other.GetComponentInParent<FirstPersonPlayer>();
        if (player == null) return;
        StartCoroutine(Turn(player));
    }

    IEnumerator Turn(FirstPersonPlayer player)
    {
        busy = true;
        if (dialogue != null && !dialogue.IsPlaying) dialogue.Play();

        // 조작 잠깐 막기
        bool couldMove = player.canMove, couldLook = player.canLook;
        player.canMove = false;
        player.canLook = false;
        player.StopMovement();

        Transform body = player.transform;
        CharacterController cc = player.GetComponent<CharacterController>();
        Vector3 to = lookTarget.position - body.position;
        to.y = 0f;
        Quaternion fromRot = body.rotation;
        Quaternion toRot = to.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(to) : fromRot;
        float fromPitch = player.Pitch;
        Vector3 dir = to.normalized;
        float moved = 0f;

        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(turnTime, 0.01f))
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            body.rotation = Quaternion.Slerp(fromRot, toRot, k);
            player.SetPitch(Mathf.Lerp(fromPitch, 0f, k));          // 고개는 정면으로

            // 돌면서 계단 쪽으로 살짝 걸어 나감 (벽은 CharacterController가 막아 줌)
            float want = pushBack * k - moved;
            if (cc != null && cc.enabled && want > 0f) { cc.Move(dir * want); moved += want; }
            yield return null;
        }
        body.rotation = toRot;
        player.SetPitch(0f);

        player.canMove = couldMove;
        player.canLook = couldLook;
        busy = false;
    }
}
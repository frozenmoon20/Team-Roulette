using System.Collections;
using UnityEngine;

// 이어하기용: 클릭커를 손에 쥐여 주는 부품 (복도 미니게임4 지점에서 이어할 때)
// 원리: 원래 게임에서는 금고 안 클릭커를 "잡는" 순간(손에 들린 순간) ClickerPocketController가
//       "획득"으로 처리한다 (주머니 넣기/꺼내기, 딸깍 사용 가능).
//       그래서 이어하기 때도 똑같이 클릭커를 손에 들려 주기만 하면, 나머지는 원래 스크립트가 알아서 한다.
//       (신비 스크립트는 고치지 않음)
// 사용: 아무 오브젝트(예: SP_LongCorridor)에 붙이고 Clicker 칸에 금고 안의 클릭커(GrabbableItem)를 넣는다.
//       SavePoint의 On Continued에 ClickerRestore.GiveClicker 연결.
public class ClickerRestore : MonoBehaviour
{
    [Tooltip("금고 안의 클릭커 (Dogtrainingclicker의 GrabbableItem)")]
    public GrabbableItem clicker;

    public void GiveClicker()
    {
        if (clicker == null) { Debug.LogWarning("ClickerRestore: Clicker 칸이 비어 있어요.", this); return; }
        StartCoroutine(GiveRoutine());
    }

    IEnumerator GiveRoutine()
    {
        // 손이 화면에 나올 때까지 잠깐 기다림 (최대 1초)
        for (float t = 0f; t < 1f; t += Time.deltaTime)
        {
            if (PlayerHand.Instance != null && PlayerHand.Instance.IsVisible) break;
            yield return null;
        }

        PlayerHand hand = PlayerHand.Instance;
        if (hand == null || !hand.IsVisible || hand.Held != null)
        {
            Debug.LogWarning("ClickerRestore: 손이 준비되지 않았거나 이미 뭔가 들고 있어서 클릭커를 못 쥐여 줬어요.", this);
            yield break;
        }

        // 금고가 숨겨 둔 클릭커를 꺼내서 손바닥에
        clicker.gameObject.SetActive(true);
        clicker.enabled = true;
        if (hand.Hold(clicker))
        {
            clicker.AttachTo(hand.palmPoint != null ? hand.palmPoint : hand.transform);
            Debug.Log("이어하기: 클릭커를 손에 쥐여 줌", this);
        }
        else
        {
            Debug.LogWarning("ClickerRestore: 손에 쥐여 주기 실패", this);
        }
    }
}
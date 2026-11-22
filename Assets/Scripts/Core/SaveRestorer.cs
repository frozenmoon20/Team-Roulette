using System.Collections;
using UnityEngine;

// 이어하기 때 SaveSystem이 잠깐 만들었다가 지우는 오브젝트. 씬에 붙이지 않는다.
// 원리: 씬이 열리고 1프레임 뒤(각 스크립트의 Start, DevSkip이 끝난 뒤) 목표창을 저장된 모양 그대로 덮어쓴다.
//       SavePoint가 플레이어를 옮기는 것(2프레임 뒤)보다 먼저 해야, 옮겨진 자리에서 입장 트리거가 바꾼 목표를
//       덮어쓰지 않는다.
public class SaveRestorer : MonoBehaviour
{
    IEnumerator Start()
    {
        yield return null;
        SaveSystem.RestoreObjectives();
        Destroy(gameObject);
    }
}
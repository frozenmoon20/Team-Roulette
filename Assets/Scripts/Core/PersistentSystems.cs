using UnityEngine;

// 이 오브젝트와 그 자식들을 씬 전환 시에도 유지시킨다.
// 공용 시스템(OptionState, AudioManager 등)을 담는 _Systems 오브젝트에 붙인다.
public class PersistentSystems : MonoBehaviour
{
    static PersistentSystems instance;

    void Awake()
    {
        // 이전 씬에서 넘어온 게 이미 있으면 새로 로드된 쪽은 파괴한다
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }
}

using UnityEngine;

// (선택) 조준했을 때 화면 아래에 이름을 띄우고 싶은 상호작용 오브젝트에 붙인다.
public class InteractName : MonoBehaviour, IHasName
{
    public string displayName = "";

    public string DisplayName => displayName;   // IHasName 약속: 이름을 돌려줌
}
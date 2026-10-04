// 화면 아래에 이름을 띄울 수 있는 오브젝트의 약속
// (InteractName, 나중에 GrabbableItem, PlaceSpot이 이 약속을 지킴)
public interface IHasName
{
    string DisplayName { get; }
}
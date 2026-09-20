using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    public Camera playerCamera;
    public float InteractRange = 3f;


    void LateUpdate()
    {
        if(Input.GetMouseButtonDown(0))
        {
            TryInteract();
        }
        

    }

    void TryInteract()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        Debug.DrawRay(ray.origin, ray.direction * InteractRange, Color.red, 1f);

        // 레이케스트 발사해서 콜리전에 부딪히면 그 부딪힌 콜리전의 오브젝트에 IInteractable을 구현한
        // 컴포넌트가 있는지 확인, 있다면 그 타깃의 Interact()가 실행된다.
        if(Physics.Raycast(ray, out RaycastHit hit, InteractRange))
        {
            IInteractable target = hit.collider.GetComponent<IInteractable>();
            if(target != null)
            {
                target.Interact();
            }
        }
    }


}

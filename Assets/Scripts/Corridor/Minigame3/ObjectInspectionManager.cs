
using UnityEngine;

public class ObjectInspectionManager : MonoBehaviour
{
    public static ObjectInspectionManager Instance { get; private set; }

    [Header("플레이어 연결")]
    public Camera viewCamera;
    public FirstPersonPlayer player;
    public PlayerInteractor interactor;

    [Header("조사 화면 설정")]
    public float inspectionDistance = 1.2f;
    public float objectDisplaySize = 0.65f;
    public float rotationSpeed = 4f;
    public float zoomSpeed = 0.5f;
    public float minZoomDistance = 0.5f;
    public float maxZoomDistance = 2.5f;

    private Transform inspectionPivot;
    private InspectableObject inspecting;

    private Transform inspectedTransform;
    private Transform originalParent;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private Vector3 originalLocalScale;

    private Collider[] colliders;
    private bool[] colliderStates;

    private bool previousCanMove;
    private bool previousCanLook;
    private bool previousCanJump;
    private bool previousCanSprint;
    private bool previousInteractorEnabled;

    private GameObject handModel;
    private bool previousHandVisible;

    private void Awake()
    {
        Instance = this;
    }

    public void BeginInspection(InspectableObject item)
    {
        if (inspecting != null || item == null)
            return;

        if (viewCamera == null)
            viewCamera = Camera.main;

        if (viewCamera == null)
        {
            Debug.LogWarning("조사용 카메라가 연결되지 않았습니다.");
            return;
        }

        if (player == null)
            player = viewCamera.GetComponentInParent<FirstPersonPlayer>();

        if (interactor == null)
            interactor = FindFirstObjectByType<PlayerInteractor>();

        Renderer[] renderers = item.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            Debug.LogWarning("렌더러가 없는 오브젝트입니다.", item);
            return;
        }

        if (inspectionPivot == null)
        {
            GameObject pivotObject = new GameObject("InspectionPivot");
            inspectionPivot = pivotObject.transform;
        }

        inspectionPivot.SetParent(viewCamera.transform, false);
        inspectionPivot.localPosition =
            new Vector3(0f, 0f, inspectionDistance);
        inspectionPivot.localRotation = Quaternion.identity;
        inspectionPivot.localScale = Vector3.one;

        inspecting = item;
        inspectedTransform = item.transform;

        // 원래 배치 기록
        originalParent = inspectedTransform.parent;
        originalLocalPosition = inspectedTransform.localPosition;
        originalLocalRotation = inspectedTransform.localRotation;
        originalLocalScale = inspectedTransform.localScale;

        // 물체의 실제 크기와 중심 계산
        Bounds bounds = GetBounds(renderers);
        float longestSide = Mathf.Max(
            bounds.size.x,
            bounds.size.y,
            bounds.size.z
        );

        // 조사 중에는 충돌 및 클릭 판정 중지
        colliders = item.GetComponentsInChildren<Collider>(true);
        colliderStates = new bool[colliders.Length];

        for (int i = 0; i < colliders.Length; i++)
        {
            colliderStates[i] = colliders[i].enabled;
            colliders[i].enabled = false;
        }

        // 카메라 앞에 물체 배치
        inspectedTransform.SetParent(inspectionPivot, true);

        inspectedTransform.position +=
            inspectionPivot.position - bounds.center;

        if (longestSide > 0.0001f)
        {
            float factor = objectDisplaySize / longestSide;
            inspectedTransform.localScale *= factor;
        }

        // 크기 변경 후 중심 다시 정렬
        Bounds newBounds = GetBounds(renderers);

        inspectedTransform.position +=
            inspectionPivot.position - newBounds.center;

        // 플레이어 이동과 시점 고정
        if (player != null)
        {
            previousCanMove = player.canMove;
            previousCanLook = player.canLook;
            previousCanJump = player.canJump;
            previousCanSprint = player.canSprint;

            player.canMove = false;
            player.canLook = false;
            player.canJump = false;
            player.canSprint = false;
        }

        // 조사 중 일반 클릭 상호작용 잠시 중지
        if (interactor != null)
        {
            previousInteractorEnabled = interactor.enabled;
            interactor.enabled = false;
        }

        // 조사할 때 손 모델 숨기기
        PlayerHand hand = PlayerHand.Instance;

        handModel = hand != null ? hand.handModel : null;

        if (handModel != null)
        {
            previousHandVisible = handModel.activeSelf;
            handModel.SetActive(false);
        }

        Debug.Log("오브젝트 조사 시작: " + item.name);
    }

    private Bounds GetBounds(Renderer[] renderers)
    {
        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return bounds;
    }

    private void Update()
    {
        if (inspecting == null)
            return;

        // 우클릭 또는 ESC로 조사 종료
        if (Input.GetMouseButtonDown(1) ||
            Input.GetKeyDown(KeyCode.Escape))
        {
            EndInspection();
            return;
        }

        // 좌클릭 드래그로 물체 회전
        if (Input.GetMouseButton(0))
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            inspectionPivot.Rotate(
                Vector3.up,
                -mouseX * rotationSpeed,
                Space.Self
            );

            inspectionPivot.Rotate(
                Vector3.right,
                mouseY * rotationSpeed,
                Space.Self
            );
        }

        // 휠 확대 / 축소
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (Mathf.Abs(scroll) > 0.001f)
        {
            float newDistance = Mathf.Clamp(
                inspectionPivot.localPosition.z - scroll * zoomSpeed,
                minZoomDistance,
                maxZoomDistance
            );

            inspectionPivot.localPosition =
                new Vector3(0f, 0f, newDistance);
        }
    }

    public void EndInspection()
    {
        if (inspecting == null)
            return;

        // 물체를 원래 자리로 복구
        inspectedTransform.SetParent(originalParent, false);
        inspectedTransform.localPosition = originalLocalPosition;
        inspectedTransform.localRotation = originalLocalRotation;
        inspectedTransform.localScale = originalLocalScale;

        // Collider 복구
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = colliderStates[i];
        }

        // 플레이어 조작 복구
        if (player != null)
        {
            player.canMove = previousCanMove;
            player.canLook = previousCanLook;
            player.canJump = previousCanJump;
            player.canSprint = previousCanSprint;
        }

        if (interactor != null)
            interactor.enabled = previousInteractorEnabled;

        if (handModel != null)
            handModel.SetActive(previousHandVisible);

        inspecting = null;
        inspectedTransform = null;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("오브젝트 조사 종료");
    }

    private void OnDestroy()
    {
        if (inspecting != null)
            EndInspection();

        if (Instance == this)
            Instance = null;
    }
}

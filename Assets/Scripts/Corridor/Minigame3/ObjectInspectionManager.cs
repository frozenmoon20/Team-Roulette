
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using TMPro;

[DefaultExecutionOrder(1000)]
public class ObjectInspectionManager : MonoBehaviour
{
    public static ObjectInspectionManager Instance { get; private set; }

    [Header("Player")]
    public Camera viewCamera;
    public FirstPersonPlayer player;
    public PlayerInteractor interactor;

    [Header("Inspection")]
    public float displaySize = 1.5f;
    public float rotationSpeed = 4f;
    public float zoomSpeed = 0.6f;
    public float minOrthographicSize = 0.65f;
    public float maxOrthographicSize = 2.2f;

    [Header("Blur background only")]
    public Volume inspectionBlurVolume;

    [Header("Inspection controls hint")]
    public string inspectionHintText =
        "드래그 : 회전   |   휠 : 확대·축소   |   우클릭 / ESC : 돌아가기";

    public TMP_FontAsset inspectionHintFont;
    public float inspectionHintFontSize = 22f;

    private const float PreviewWorldY = -5000f;

    private InspectableObject inspecting;
    private GameObject runtimeRoot;
    private Camera inspectionCamera;
    private Transform rotationPivot;
    private RenderTexture inspectionTexture;

    private Renderer[] originalRenderers;
    private bool[] originalEnabled;

    private readonly List<Renderer> previewRenderers =
        new List<Renderer>();

    private readonly List<Mesh> bakedMeshes =
        new List<Mesh>();

    private PlayerMotionFeel motionFeel;
    private GameObject handModel;

    private bool previousPlayerEnabled;
    private bool previousInteractorEnabled;
    private bool previousMotionEnabled;
    private bool previousHandEnabled;
    private bool previousCrosshairEnabled;
    private bool previousHandIconEnabled;
    private bool previousNameTextEnabled;

    private bool previousCanMove;
    private bool previousCanLook;
    private bool previousCanJump;
    private bool previousCanSprint;

    private float previousBlurWeight;

    private bool dragReady;
    private bool controlsCaptured;
    private bool isClosing;

    public bool IsInspecting => inspecting != null;

    private void Awake()
    {
        Instance = this;
    }

    public void BeginInspection(InspectableObject item)
    {
        if (item == null || inspecting != null || isClosing)
            return;

        try
        {
            if (viewCamera == null)
                viewCamera = Camera.main;

            if (viewCamera == null)
            {
                Debug.LogError(
                    "Inspection: View Camera is missing.",
                    this
                );
                return;
            }

            if (player == null)
            {
                player =
                    viewCamera.GetComponentInParent<FirstPersonPlayer>();
            }

            if (interactor == null)
            {
                interactor =
                    FindFirstObjectByType<PlayerInteractor>();
            }

            originalRenderers =
                item.GetComponentsInChildren<Renderer>(true);

            if (originalRenderers.Length == 0)
            {
                Debug.LogWarning(
                    "Inspection: No renderers found.",
                    item
                );
                return;
            }

            int layer = FindPreviewLayer();

            Vector3 center =
                new Vector3(0f, PreviewWorldY, 0f);

            runtimeRoot =
                new GameObject("Inspection_Runtime");

            rotationPivot =
                new GameObject("Inspection_RotationPivot").transform;

            rotationPivot.SetParent(runtimeRoot.transform);
            rotationPivot.position = center;

            previewRenderers.Clear();
            bakedMeshes.Clear();

            Transform visualRoot = CopyVisual(
                item.transform,
                rotationPivot,
                true,
                layer
            );

            if (previewRenderers.Count == 0)
            {
                CleanupRuntime();

                Debug.LogWarning(
                    "Inspection: No visible renderers found.",
                    item
                );
                return;
            }

            Bounds bounds = GetPreviewBounds();

            float longestSide = Mathf.Max(
                bounds.size.x,
                bounds.size.y,
                bounds.size.z
            );

            if (longestSide > 0.00001f)
    visualRoot.localScale *=
        (displaySize * item.inspectionScale) / longestSide;

            bounds = GetPreviewBounds();

            visualRoot.position +=
                center - bounds.center;

            CreateInspectionCamera(layer, center);
            CreateInspectionOverlay();

            originalEnabled =
                new bool[originalRenderers.Length];

            for (int i = 0; i < originalRenderers.Length; i++)
            {
                Renderer renderer = originalRenderers[i];

                if (renderer == null)
                    continue;

                originalEnabled[i] = renderer.enabled;
                renderer.enabled = false;
            }

            inspecting = item;
            dragReady = false;

            LockPlayerAndBlur();

            Debug.Log(
                "조사 시작: " + item.gameObject.name
            );
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            EndInspection();
        }
    }

    private void CreateInspectionCamera(
        int layer,
        Vector3 center
    )
    {
        inspectionTexture = new RenderTexture(
            Mathf.Max(256, Screen.width),
            Mathf.Max(256, Screen.height),
            24,
            RenderTextureFormat.ARGB32
        );

        inspectionTexture.name =
            "Inspection_Transparent_Texture";

        inspectionTexture.Create();

        GameObject cameraObject =
            new GameObject("Inspection_Camera");

        cameraObject.transform.SetParent(
            runtimeRoot.transform
        );

        inspectionCamera =
            cameraObject.AddComponent<Camera>();

        inspectionCamera.transform.position =
            center + new Vector3(0f, 0f, -8f);

        inspectionCamera.transform.rotation =
            Quaternion.identity;

        inspectionCamera.orthographic = true;
        inspectionCamera.orthographicSize = 1.25f;
        inspectionCamera.nearClipPlane = 0.01f;
        inspectionCamera.farClipPlane = 20f;

        inspectionCamera.clearFlags =
            CameraClearFlags.SolidColor;

        inspectionCamera.backgroundColor =
            new Color(0f, 0f, 0f, 0f);

        inspectionCamera.cullingMask = 1 << layer;
        inspectionCamera.allowHDR = false;
        inspectionCamera.targetTexture =
            inspectionTexture;

        UniversalAdditionalCameraData urp =
            cameraObject.GetComponent<
                UniversalAdditionalCameraData>();

        if (urp == null)
        {
            urp = cameraObject.AddComponent<
                UniversalAdditionalCameraData>();
        }

        urp.renderType = CameraRenderType.Base;
        urp.renderPostProcessing = false;
        urp.renderShadows = false;

        GameObject lightObject =
            new GameObject("Inspection_Light");

        lightObject.transform.SetParent(
            runtimeRoot.transform
        );

        lightObject.transform.rotation =
            Quaternion.Euler(40f, -35f, 0f);

        Light light = lightObject.AddComponent<Light>();

        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.shadows = LightShadows.None;
        light.cullingMask = 1 << layer;
    }

    private void CreateInspectionOverlay()
    {
        GameObject canvasObject =
            new GameObject("Inspection_Overlay");

        canvasObject.transform.SetParent(
            runtimeRoot.transform
        );

        Canvas canvas =
            canvasObject.AddComponent<Canvas>();

        canvas.renderMode =
            RenderMode.ScreenSpaceOverlay;

        canvas.sortingOrder = 1000;

        canvasObject.AddComponent<CanvasScaler>();

        GameObject imageObject =
            new GameObject("Inspection_Image");

        imageObject.transform.SetParent(
            canvasObject.transform,
            false
        );

        RawImage image =
            imageObject.AddComponent<RawImage>();

        image.texture = inspectionTexture;
        image.color = Color.white;
        image.raycastTarget = false;

        RectTransform rect = image.rectTransform;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        CreateInspectionHint(
            canvasObject.transform
        );
    }

    private void CreateInspectionHint(
        Transform canvasTransform
    )
    {
        if (string.IsNullOrWhiteSpace(inspectionHintText))
            return;

        GameObject panel =
            new GameObject("Inspection_Controls_Hint");

        panel.transform.SetParent(
            canvasTransform,
            false
        );

        Image background =
            panel.AddComponent<Image>();

        background.color =
            new Color(0.04f, 0.04f, 0.04f, 0.65f);

        background.raycastTarget = false;

        RectTransform panelRect =
            background.rectTransform;

        panelRect.anchorMin =
            new Vector2(0.08f, 0f);

        panelRect.anchorMax =
            new Vector2(0.92f, 0f);

        panelRect.pivot =
            new Vector2(0.5f, 0f);

        panelRect.anchoredPosition =
            new Vector2(0f, 24f);

        panelRect.sizeDelta =
            new Vector2(0f, 45f);

        GameObject labelObject =
            new GameObject("Inspection_Controls_Text");

        labelObject.transform.SetParent(
            panel.transform,
            false
        );

        TextMeshProUGUI label =
            labelObject.AddComponent<TextMeshProUGUI>();

        label.text = inspectionHintText;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.enableAutoSizing = true;

        label.fontSize = Mathf.Min(
            inspectionHintFontSize,
            Mathf.Max(
                13f,
                Screen.height * 0.036f
            )
        );

        label.fontSizeMin = 10f;

        label.fontSizeMax = Mathf.Max(
            13f,
            inspectionHintFontSize
        );

        label.overflowMode =
            TextOverflowModes.Ellipsis;

        if (inspectionHintFont != null)
        {
            label.font = inspectionHintFont;
        }
        else if (
            interactor != null &&
            interactor.nameText != null &&
            interactor.nameText.font != null
        )
        {
            label.font = interactor.nameText.font;
        }

        RectTransform labelRect =
            label.rectTransform;

        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;

        labelRect.offsetMin =
            new Vector2(14f, 0f);

        labelRect.offsetMax =
            new Vector2(-14f, 0f);
    }

    private int FindPreviewLayer()
    {
        int named =
            LayerMask.NameToLayer("InspectionPreview");

        if (named >= 0)
            return named;

        for (int i = 31; i >= 8; i--)
        {
            if (
                string.IsNullOrEmpty(
                    LayerMask.LayerToName(i)
                )
            )
            {
                return i;
            }
        }

        return 31;
    }

    private Transform CopyVisual(
        Transform original,
        Transform parent,
        bool isRoot,
        int layer
    )
    {
        GameObject obj =
            new GameObject("Inspect_" + original.name);

        obj.layer = layer;

        Transform t = obj.transform;
        t.SetParent(parent, false);

        if (isRoot)
        {
            t.localPosition = Vector3.zero;

            t.localRotation =
                Quaternion.Inverse(
                    viewCamera.transform.rotation
                ) * original.rotation;

            t.localScale = original.lossyScale;
        }
        else
        {
            t.localPosition = original.localPosition;
            t.localRotation = original.localRotation;
            t.localScale = original.localScale;
        }

        if (original.gameObject.activeInHierarchy)
        {
            MeshFilter srcFilter =
                original.GetComponent<MeshFilter>();

            MeshRenderer srcRenderer =
                original.GetComponent<MeshRenderer>();

            if (
                srcFilter != null &&
                srcRenderer != null &&
                srcFilter.sharedMesh != null &&
                srcRenderer.enabled
            )
            {
                MeshFilter dstFilter =
                    obj.AddComponent<MeshFilter>();

                dstFilter.sharedMesh =
                    srcFilter.sharedMesh;

                MeshRenderer dstRenderer =
                    obj.AddComponent<MeshRenderer>();

                dstRenderer.sharedMaterials =
                    srcRenderer.sharedMaterials;

                dstRenderer.shadowCastingMode =
                    ShadowCastingMode.Off;

                dstRenderer.receiveShadows = false;

                MaterialPropertyBlock block =
                    new MaterialPropertyBlock();

                srcRenderer.GetPropertyBlock(block);

                dstRenderer.SetPropertyBlock(block);

                previewRenderers.Add(dstRenderer);
            }

            SkinnedMeshRenderer skin =
                original.GetComponent<SkinnedMeshRenderer>();

            if (
                skin != null &&
                skin.enabled &&
                skin.sharedMesh != null
            )
            {
                Mesh baked = new Mesh();

                skin.BakeMesh(baked);
                bakedMeshes.Add(baked);

                MeshFilter dstFilter =
                    obj.AddComponent<MeshFilter>();

                dstFilter.sharedMesh = baked;

                MeshRenderer dstRenderer =
                    obj.AddComponent<MeshRenderer>();

                dstRenderer.sharedMaterials =
                    skin.sharedMaterials;

                dstRenderer.shadowCastingMode =
                    ShadowCastingMode.Off;

                dstRenderer.receiveShadows = false;

                previewRenderers.Add(dstRenderer);
            }

            SpriteRenderer sprite =
                original.GetComponent<SpriteRenderer>();

            if (
                sprite != null &&
                sprite.enabled &&
                sprite.sprite != null
            )
            {
                SpriteRenderer newSprite =
                    obj.AddComponent<SpriteRenderer>();

                newSprite.sprite = sprite.sprite;
                newSprite.color = sprite.color;

                newSprite.sharedMaterial =
                    sprite.sharedMaterial;

                previewRenderers.Add(newSprite);
            }
        }

        foreach (Transform child in original)
        {
            if (child.gameObject.activeInHierarchy)
            {
                CopyVisual(
                    child,
                    t,
                    false,
                    layer
                );
            }
        }

        return t;
    }

    private Bounds GetPreviewBounds()
    {
        Bounds bounds =
            previewRenderers[0].bounds;

        for (int i = 1; i < previewRenderers.Count; i++)
        {
            bounds.Encapsulate(
                previewRenderers[i].bounds
            );
        }

        return bounds;
    }

    private void LockPlayerAndBlur()
    {
        if (player != null)
        {
            previousPlayerEnabled = player.enabled;
            previousCanMove = player.canMove;
            previousCanLook = player.canLook;
            previousCanJump = player.canJump;
            previousCanSprint = player.canSprint;

            motionFeel =
                player.GetComponent<PlayerMotionFeel>();

            if (motionFeel != null)
            {
                previousMotionEnabled =
                    motionFeel.enabled;
            }
        }

        if (interactor != null)
        {
            previousInteractorEnabled =
                interactor.enabled;

            previousCrosshairEnabled =
                interactor.crosshairDot != null &&
                interactor.crosshairDot.activeSelf;

            previousHandIconEnabled =
                interactor.handIcon != null &&
                interactor.handIcon.activeSelf;

            previousNameTextEnabled =
                interactor.nameText != null &&
                interactor.nameText.gameObject.activeSelf;
        }

        PlayerHand hand = PlayerHand.Instance;

        handModel =
            hand != null ? hand.handModel : null;

        if (handModel != null)
        {
            previousHandEnabled =
                handModel.activeSelf;
        }

        if (inspectionBlurVolume != null)
        {
            previousBlurWeight =
                inspectionBlurVolume.weight;
        }

        controlsCaptured = true;

        if (player != null)
        {
            player.canMove = false;
            player.canLook = false;
            player.canJump = false;
            player.canSprint = false;
            player.enabled = false;
        }

        if (motionFeel != null)
            motionFeel.enabled = false;

        if (interactor != null)
        {
            interactor.enabled = false;

            if (interactor.crosshairDot != null)
                interactor.crosshairDot.SetActive(false);

            if (interactor.handIcon != null)
                interactor.handIcon.SetActive(false);

            if (interactor.nameText != null)
            {
                interactor.nameText.gameObject.SetActive(
                    false
                );
            }
        }

        if (handModel != null)
            handModel.SetActive(false);

        if (inspectionBlurVolume != null)
            inspectionBlurVolume.weight = 1f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (inspecting == null || isClosing)
            return;

        if (
            Input.GetKeyDown(KeyCode.Escape) ||
            Input.GetMouseButtonDown(1)
        )
        {
            EndInspection();
            return;
        }

        if (!Input.GetMouseButton(0))
        {
            dragReady = true;
        }
        else if (dragReady && rotationPivot != null)
        {
            float dx = Input.GetAxis("Mouse X");
            float dy = Input.GetAxis("Mouse Y");

            rotationPivot.Rotate(
                Vector3.up,
                -dx * rotationSpeed,
                Space.World
            );

            rotationPivot.Rotate(
                viewCamera.transform.right,
                dy * rotationSpeed,
                Space.World
            );
        }

        if (inspectionCamera != null)
        {
            float wheel =
                Input.GetAxis("Mouse ScrollWheel");

            if (Mathf.Abs(wheel) > 0.0001f)
            {
                inspectionCamera.orthographicSize =
                    Mathf.Clamp(
                        inspectionCamera.orthographicSize -
                        wheel * zoomSpeed,
                        minOrthographicSize,
                        maxOrthographicSize
                    );
            }
        }
    }

    public void EndInspection()
    {
        if (isClosing)
            return;

        if (
            inspecting == null &&
            runtimeRoot == null &&
            !controlsCaptured
        )
            return;

        isClosing = true;

        try
        {
            if (originalRenderers != null)
            {
                for (
                    int i = 0;
                    i < originalRenderers.Length;
                    i++
                )
                {
                    if (
                        originalRenderers[i] != null &&
                        originalEnabled != null &&
                        i < originalEnabled.Length
                    )
                    {
                        originalRenderers[i].enabled =
                            originalEnabled[i];
                    }
                }
            }

            RestorePlayerControls();

            Debug.Log("조사 종료 및 플레이어 조작 복구");
        }
        finally
        {
            inspecting = null;
            dragReady = false;

            originalRenderers = null;
            originalEnabled = null;

            CleanupRuntime();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            isClosing = false;
        }
    }

    private void RestorePlayerControls()
    {
        if (!controlsCaptured)
            return;

        if (player != null)
        {
            player.canMove = previousCanMove;
            player.canLook = previousCanLook;
            player.canJump = previousCanJump;
            player.canSprint = previousCanSprint;

            player.enabled = previousPlayerEnabled;
        }

        if (motionFeel != null)
        {
            motionFeel.enabled =
                previousMotionEnabled;
        }

        if (interactor != null)
        {
            interactor.enabled =
                previousInteractorEnabled;

            if (interactor.crosshairDot != null)
            {
                interactor.crosshairDot.SetActive(
                    previousCrosshairEnabled
                );
            }

            if (interactor.handIcon != null)
            {
                interactor.handIcon.SetActive(
                    previousHandIconEnabled
                );
            }

            if (interactor.nameText != null)
            {
                interactor.nameText.gameObject.SetActive(
                    previousNameTextEnabled
                );
            }
        }

        if (handModel != null)
        {
            handModel.SetActive(
                previousHandEnabled
            );
        }

        if (inspectionBlurVolume != null)
        {
            inspectionBlurVolume.weight =
                previousBlurWeight;
        }

        handModel = null;
        motionFeel = null;
        controlsCaptured = false;
    }

    private void CleanupRuntime()
    {
        if (inspectionCamera != null)
        {
            inspectionCamera.targetTexture = null;
        }

        if (runtimeRoot != null)
        {
            Destroy(runtimeRoot);
        }

        runtimeRoot = null;
        rotationPivot = null;
        inspectionCamera = null;

        if (inspectionTexture != null)
        {
            inspectionTexture.Release();
            Destroy(inspectionTexture);
            inspectionTexture = null;
        }

        foreach (Mesh mesh in bakedMeshes)
        {
            if (mesh != null)
                Destroy(mesh);
        }

        bakedMeshes.Clear();
        previewRenderers.Clear();
    }

    private void OnDisable()
    {
        if (
            inspecting != null ||
            runtimeRoot != null ||
            controlsCaptured
        )
        {
            EndInspection();
        }
    }

    private void OnDestroy()
    {
        if (
            inspecting != null ||
            runtimeRoot != null ||
            controlsCaptured
        )
        {
            EndInspection();
        }

        if (Instance == this)
            Instance = null;
    }
}

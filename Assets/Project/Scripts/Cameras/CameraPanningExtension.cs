using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

public class CameraPanningExtension : CinemachineExtension
{
    [Header("Camera active sans pupitre")]
    [SerializeField] private bool startWithFreeCameraEnabled = false;

    [Header("Vertical Panning")]
    [SerializeField] private float verticalOffset = 5f;
    [SerializeField] private float verticalPanSpeed = 10f;
    [SerializeField] private float initialRiseDuration = 2f;

    [Header("View Toggle")]
    [SerializeField] private CinemachineCamera normalCamera;
    [SerializeField] private CinemachineCamera lowViewCamera;
    [SerializeField] private int activePriority = 20;
    [SerializeField] private int inactivePriority = 0;

    [Header("Start Zone")]
    [SerializeField] private CountdownManager countdownManager;

    [Header("Obstacle Hiding")]
    [SerializeField] private float playerHeightOffset = 0.5f;
    [SerializeField] private LayerMask obstacleHidingLayers;
    [SerializeField] private Material transparentMaterial;

    [Header("Target Group Radius")]
    [SerializeField] private CinemachineTargetGroup targetGroup;
    [SerializeField] private float maxRadius = 3f;
    [SerializeField] private float radiusGrowSpeed = 3f;

    private Vector3 currentPanOffset;
    private bool isHighPosition = false;
    public bool isLowView = false;
    private Transform playerTransform;
    private Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
    private HashSet<Renderer> occludedRenderers = new HashSet<Renderer>();
    private Camera mainCamera;
    private float baseRadius;
    private float lastPlayerZ;
    private CinemachineCamera activePrimaryCamera;

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            playerTransform = player.transform;

        mainCamera = Camera.main;
        if (mainCamera == null)
            mainCamera = FindObjectOfType<Camera>();

        if (countdownManager == null)
            countdownManager = FindObjectOfType<CountdownManager>();

        currentPanOffset = Vector3.zero;
        activePrimaryCamera = normalCamera;

        if (lowViewCamera != null) lowViewCamera.Priority = inactivePriority;
        if (normalCamera != null) normalCamera.Priority = activePriority;

        if (startWithFreeCameraEnabled)
        {
            isHighPosition = true;
            isLowView = false;
            Debug.Log("[CameraPanning] Camera libre activee des le depart");
        }
        else
        {
            isHighPosition = false;
            isLowView = false;
        }

        if (targetGroup != null && targetGroup.Targets.Count > 0)
            baseRadius = targetGroup.Targets[0].Radius;

        if (playerTransform != null)
            lastPlayerZ = playerTransform.position.z;

        Debug.Log($"[CameraPanning] START - isHighPosition: {isHighPosition}, isLowView: {isLowView}");
    }

    public void OnPlayerExitStartZone()
    {
        Debug.Log("[CameraPanning] Player sorti de startzone - Montee camera !");
        isHighPosition = true;
    }

    public void SetPrimaryCamera(CinemachineCamera newPrimary)
    {
        if (activePrimaryCamera != null) activePrimaryCamera.Priority = inactivePriority;

        activePrimaryCamera = newPrimary;

        if (isLowView)
        {
            if (lowViewCamera != null) lowViewCamera.Priority = activePriority;
            if (activePrimaryCamera != null) activePrimaryCamera.Priority = inactivePriority;
        }
        else
        {
            if (lowViewCamera != null) lowViewCamera.Priority = inactivePriority;
            if (activePrimaryCamera != null) activePrimaryCamera.Priority = activePriority;
        }
    }

    private void Update()
    {
        if (isHighPosition && PlayerInputManager.Instance.ToggleCameraViewPressed)
        {
            isLowView = !isLowView;

            if (isLowView)
            {
                if (lowViewCamera != null) lowViewCamera.Priority = activePriority;
                if (activePrimaryCamera != null) activePrimaryCamera.Priority = inactivePriority;
            }
            else
            {
                if (lowViewCamera != null) lowViewCamera.Priority = inactivePriority;
                if (activePrimaryCamera != null) activePrimaryCamera.Priority = activePriority;
            }

            Debug.Log($"[CameraPanning] Toggle view - isLowView: {isLowView}");
        }

        if (playerTransform != null && targetGroup != null && targetGroup.Targets.Count > 0)
        {
            float playerZDelta = playerTransform.position.z - lastPlayerZ;
            lastPlayerZ = playerTransform.position.z;

            float radiusOffset = targetGroup.Targets[0].Radius - baseRadius;

            if (playerZDelta < 0f)
            {
                radiusOffset += -playerZDelta;
                radiusOffset = Mathf.Min(radiusOffset, maxRadius - baseRadius);
            }
            else if (playerZDelta > 0f)
            {
                radiusOffset -= playerZDelta;
                radiusOffset = Mathf.Max(0f, radiusOffset);
            }

            targetGroup.Targets[0] = new CinemachineTargetGroup.Target
            {
                Object = targetGroup.Targets[0].Object,
                Weight = targetGroup.Targets[0].Weight,
                Radius = baseRadius + radiusOffset
            };
        }
    }

    private void LateUpdate()
    {
        if (isHighPosition && isLowView && playerTransform != null && mainCamera != null)
        {
            Vector3 cameraPos = mainCamera.transform.position;
            Vector3 playerCenter = playerTransform.position + Vector3.up * playerHeightOffset;
            Vector3 direction = playerCenter - cameraPos;
            float distance = direction.magnitude;

            Debug.DrawRay(cameraPos, direction, Color.red, 0.1f);

            HashSet<Renderer> currentlyBlocking = new HashSet<Renderer>();

            RaycastHit[] hits = Physics.SphereCastAll(cameraPos, 0.1f, direction.normalized, distance, obstacleHidingLayers);

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.gameObject != playerTransform.gameObject && hit.distance < distance)
                {
                    Renderer rend = hit.collider.GetComponent<Renderer>();
                    if (rend != null)
                    {
                        currentlyBlocking.Add(rend);
                        if (!occludedRenderers.Contains(rend))
                        {
                            SaveAndSwapToTransparent(rend);
                            occludedRenderers.Add(rend);
                        }
                    }
                }
            }

            List<Renderer> toRestore = new List<Renderer>();
            foreach (Renderer rend in occludedRenderers)
            {
                if (rend != null && !currentlyBlocking.Contains(rend))
                    toRestore.Add(rend);
            }

            foreach (Renderer rend in toRestore)
            {
                RestoreMaterials(rend);
                occludedRenderers.Remove(rend);
            }
        }
        else
        {
            RestoreAllMaterials();
        }
    }

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (stage == CinemachineCore.Stage.Body)
        {
            Vector3 targetOffset = Vector3.zero;
            float speed = verticalPanSpeed;

            if (isHighPosition)
            {
                targetOffset = Vector3.up * verticalOffset;
                speed = 1f / initialRiseDuration;
            }

            currentPanOffset = Vector3.Lerp(currentPanOffset, targetOffset, speed * deltaTime);
            state.PositionCorrection += currentPanOffset;
        }
    }

    private void SaveAndSwapToTransparent(Renderer renderer)
    {
        if (!originalMaterials.ContainsKey(renderer))
            originalMaterials[renderer] = renderer.sharedMaterials;

        Material[] transparents = new Material[renderer.sharedMaterials.Length];
        for (int i = 0; i < transparents.Length; i++)
            transparents[i] = transparentMaterial;

        renderer.sharedMaterials = transparents;
    }

    private void RestoreMaterials(Renderer renderer)
    {
        if (renderer == null)
        {
            originalMaterials.Remove(renderer);
            return;
        }

        if (originalMaterials.TryGetValue(renderer, out Material[] original))
        {
            renderer.sharedMaterials = original;
            originalMaterials.Remove(renderer);
        }
    }

    private void RestoreAllMaterials()
    {
        List<Renderer> toProcess = new List<Renderer>(occludedRenderers);
        foreach (Renderer rend in toProcess)
        {
            if (rend != null)
                RestoreMaterials(rend);
        }

        occludedRenderers.Clear();

        List<Renderer> nullKeys = new List<Renderer>();
        foreach (var kvp in originalMaterials)
        {
            if (kvp.Key == null)
                nullKeys.Add(kvp.Key);
        }

        foreach (var key in nullKeys)
            originalMaterials.Remove(key);
    }

    private void OnDisable()
    {
        RestoreAllMaterials();
    }
}
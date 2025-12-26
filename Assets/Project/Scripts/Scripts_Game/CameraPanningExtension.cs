using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

public class CameraPanningExtension : CinemachineExtension
{
    [Header("Vertical Panning")]
    [SerializeField] private float verticalOffset = 5f;
    [SerializeField] private float verticalPanSpeed = 10f;
    [SerializeField] private float initialRiseDuration = 2f;

    [Header("View Toggle")]
    [SerializeField] private float lowCameraOffset = -5f;
    [SerializeField] private float viewToggleDuration = 2f;
    [SerializeField] private float lowCameraOffsetInWater = 2f;

    [Header("Lateral Panning")]
    [SerializeField] private float lateralOffset = 5f;
    [SerializeField] private float lateralPanDuration = 1f;
    [SerializeField] private float lateralThreshold = 0.7f;

    [Header("Start Zone")]
    [SerializeField] private CountdownManager countdownManager;

    [Header("Obstacle Hiding")]
    [SerializeField] private float playerHeightOffset = 0.5f;
    [SerializeField] private LayerMask obstacleHidingLayers;
    [SerializeField] private Material wireframeMaterial;

    private Vector3 currentPanOffset;
    private Vector3 currentLateralOffset = Vector3.zero;
    private bool isHighPosition = false;
    private bool isLowView = false;
    private Transform playerTransform;
    // NOUVEAU
    private Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
    private HashSet<Renderer> occludedRenderers = new HashSet<Renderer>();
    private Camera mainCamera;
    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindObjectOfType<Camera>();
        }

        if (countdownManager == null)
        {
            countdownManager = FindObjectOfType<CountdownManager>();
        }

        currentPanOffset = Vector3.zero;
        currentLateralOffset = Vector3.zero;
        isHighPosition = false;
        isLowView = false;

        Debug.Log("[CameraPanning] START - isHighPosition: false, isLowView: false");
    }

    public void OnPlayerExitStartZone()
    {
        Debug.Log("[CameraPanning] Player sorti de startzone - Montee camera !");
        isHighPosition = true;
    }

    private void Update()
    {
        

        if (isHighPosition && PlayerInputManager.Instance.ToggleCameraViewPressed)
        {
            isLowView = !isLowView;
            Debug.Log($"[CameraPanning] Toggle view - isLowView: {isLowView}");
        }
    }

    private void LateUpdate()
    {
        if (isHighPosition && isLowView && playerTransform != null && mainCamera != null)
        {
            if (wireframeMaterial == null)
            {
                Debug.LogWarning("[Camera Occlusion] Wireframe material not assigned!");
                return;
            }

            Vector3 cameraPos = mainCamera.transform.position;
            Vector3 playerPos = playerTransform.position + Vector3.up * playerHeightOffset;
            Vector3 direction = playerPos - cameraPos;
            float distance = direction.magnitude;

            Debug.DrawRay(cameraPos, direction, Color.red, 0.1f);

            HashSet<Renderer> currentlyBlocking = new HashSet<Renderer>();
            RaycastHit[] hits = Physics.RaycastAll(cameraPos, direction.normalized, distance, obstacleHidingLayers);

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.gameObject != playerTransform.gameObject)
                {
                    Renderer rend = hit.collider.GetComponent<Renderer>();
                    if (rend != null)
                    {
                        currentlyBlocking.Add(rend);

                        if (!occludedRenderers.Contains(rend))
                        {
                            SaveAndSwapToWireframe(rend);
                            occludedRenderers.Add(rend);
                        }
                    }
                }
            }

            // Restaurer les renderers qui n'occludent plus
            List<Renderer> toRestore = new List<Renderer>();
            foreach (Renderer rend in occludedRenderers)
            {
                if (rend != null && !currentlyBlocking.Contains(rend))
                {
                    toRestore.Add(rend);
                }
            }

            foreach (Renderer rend in toRestore)
            {
                RestoreMaterials(rend);
                occludedRenderers.Remove(rend);
            }
        }
        else
        {
            // Restaurer tous les matériaux si pas en vue basse
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
            

            Vector2 lookInput = PlayerInputManager.Instance.LookInput;
            Vector3 targetOffset = Vector3.zero;  //  INITIALISATION
            float speed = verticalPanSpeed;        //  INITIALISATION

            if (isHighPosition)
            {
                if (isLowView)
                {
                    // NOUVEAU : Détecter si dans l'eau
                    bool isInWater = false;
                    if (playerTransform != null)
                    {
                        PlayerPitInteractable pitInteractable = playerTransform.GetComponent<PlayerPitInteractable>();
                        isInWater = pitInteractable != null && pitInteractable.IsInWater();
                    }

                    // Utiliser offset adapté selon si dans l'eau ou non
                    float offsetToUse = isInWater ? lowCameraOffsetInWater : lowCameraOffset;
                    targetOffset = Vector3.up * offsetToUse;
                    speed = 1f / viewToggleDuration;
                }
                else
                {
                    targetOffset = Vector3.up * verticalOffset;
                    speed = 1f / initialRiseDuration;
                }
            }
            else
            {
                targetOffset = Vector3.zero;
                speed = verticalPanSpeed;
            }


            Vector3 targetLateralOffset = currentLateralOffset;

            // Pan actif seulement si countdown termine OU sorti de startzone
            bool canPan = isHighPosition || (countdownManager != null && countdownManager.countdownFinished);

            if (canPan && Mathf.Abs(lookInput.y) < 0.5f)
            {
                Vector3 cameraRight = state.RawOrientation * Vector3.right;
                cameraRight.y = 0f;
                cameraRight.Normalize();

                float horizontalInput = lookInput.x;
                float deadzone = 0.15f;

                if (Mathf.Abs(horizontalInput) < deadzone)
                {
                    horizontalInput = 0f;
                }
                else
                {
                    float sign = Mathf.Sign(horizontalInput);
                    float absValue = Mathf.Abs(horizontalInput);

                    if (absValue >= lateralThreshold)
                    {
                        horizontalInput = sign * 1f;
                    }
                    else
                    {
                        horizontalInput = sign * Mathf.InverseLerp(deadzone, lateralThreshold, absValue);
                    }
                }

                if (Mathf.Abs(horizontalInput) > 0.01f)
                {
                    targetLateralOffset = -cameraRight * lateralOffset * horizontalInput;
                }
            }

            float lateralSpeed = 1f / lateralPanDuration;
            currentLateralOffset = Vector3.Lerp(currentLateralOffset, targetLateralOffset, lateralSpeed * deltaTime);

            currentPanOffset = Vector3.Lerp(currentPanOffset, targetOffset, speed * deltaTime);
            state.PositionCorrection += currentPanOffset + currentLateralOffset;
        }
    }

    private void SaveAndSwapToWireframe(Renderer renderer)
{
    if (!originalMaterials.ContainsKey(renderer))
    {
        // Sauvegarder les matériaux originaux
        originalMaterials[renderer] = renderer.sharedMaterials;
        
        // Préparer le mesh avec barycentriques (NOUVEAU)
        MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.mesh != null)
        {
            PrepareMeshForWireframe(meshFilter.mesh);
        }
    }

    // Swapper tous les matériaux par le wireframe
    Material[] wireframes = new Material[renderer.sharedMaterials.Length];
    for (int i = 0; i < wireframes.Length; i++)
    {
        wireframes[i] = wireframeMaterial;
    }
    renderer.sharedMaterials = wireframes;
}

private void PrepareMeshForWireframe(Mesh mesh)
{
    // Vérifier si déjà préparé
    if (mesh.uv2 != null && mesh.uv2.Length > 0)
        return;
    
    int[] tris = mesh.triangles;
    Vector3[] verts = mesh.vertices;
    Vector3[] bary = new Vector3[mesh.vertexCount];

    for (int i = 0; i < tris.Length; i += 3)
    {
        int i0 = tris[i];
        int i1 = tris[i + 1];
        int i2 = tris[i + 2];

        Vector3 p0 = verts[i0];
        Vector3 p1 = verts[i1];
        Vector3 p2 = verts[i2];

        float d01 = Vector3.Distance(p0, p1);
        float d12 = Vector3.Distance(p1, p2);
        float d20 = Vector3.Distance(p2, p0);

        float maxDist = Mathf.Max(d01, Mathf.Max(d12, d20));

        float maskZ = (d01 >= maxDist) ? 1f : 0f;
        float maskX = (d12 >= maxDist) ? 1f : 0f;
        float maskY = (d20 >= maxDist) ? 1f : 0f;

        bary[i0] = new Vector3(1, maskY, maskZ);
        bary[i1] = new Vector3(maskX, 1, maskZ);
        bary[i2] = new Vector3(maskX, maskY, 1);
    }

    mesh.SetUVs(1, bary);
}

    private void RestoreMaterials(Renderer renderer)
    {
        // NULL CHECK
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
        // Copier la liste pour éviter modification pendant iteration
        List<Renderer> toProcess = new List<Renderer>(occludedRenderers);

        foreach (Renderer rend in toProcess)
        {
            if (rend != null)
            {
                RestoreMaterials(rend);
            }
        }

        occludedRenderers.Clear();

        // Cleanup des entrées null dans le dictionnaire
        List<Renderer> nullKeys = new List<Renderer>();
        foreach (var kvp in originalMaterials)
        {
            if (kvp.Key == null)
            {
                nullKeys.Add(kvp.Key);
            }
        }

        foreach (var key in nullKeys)
        {
            originalMaterials.Remove(key);
        }
    }

    private void OnDisable()
    {
        RestoreAllMaterials();
    }
}
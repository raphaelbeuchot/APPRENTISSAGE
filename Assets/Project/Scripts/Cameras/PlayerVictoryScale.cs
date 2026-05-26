using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class PlayerVictoryScale : MonoBehaviour
{
    [SerializeField] private Material victoryMaterial;
    [SerializeField] private int waveCount = 10;
    [SerializeField] private float delayBetweenWaves = 0.8f;
    [SerializeField] private Color[] colors = new Color[10];

    [Header("Skip")]
    [Tooltip("Delai avant que le skip soit possible (evite le declenchement accidentel)")]
    [SerializeField] private float skipGracePeriod = 0.3f;

    // Event declenche quand tous les ghosts sont affiches (naturellement ou par skip)
    public event Action OnVictoryScaleComplete;

    public bool IsComplete { get; private set; }

    private SkinnedMeshRenderer playerMeshRenderer;
    private GameUIManager gameUIManager;

    private Mesh bakedMesh;
    private List<GameObject> ghosts = new List<GameObject>();
    private Vector3 cachedSpawnPos;

    private bool isScaling = false;
    private bool skipRequested = false;
    private float scalingStartTime;

    // ============================================
    // UNITY LIFECYCLE
    // ============================================

    private void Start()
    {
        gameUIManager = FindObjectOfType<GameUIManager>();
        playerMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
    }

    private void Update()
    {
        // Detecter le skip : n'importe quel bouton, apres la grace period
        if (isScaling && !skipRequested &&
            Time.realtimeSinceStartup - scalingStartTime > skipGracePeriod &&
            Input.anyKeyDown)
        {
            skipRequested = true;
            Debug.Log("[VictoryScale] Skip demande.");
        }
    }

    // ============================================
    // API PUBLIQUE
    // ============================================

    /// <summary>Re-affiche le mesh du joueur (appele par PostVictorySequencer avant la TransitionRoom).</summary>
    public void ShowPlayerMesh()
    {
        if (playerMeshRenderer != null)
            playerMeshRenderer.enabled = true;
    }

    public void TriggerScale()
    {
        if (gameUIManager != null) gameUIManager.HideGameplayBars();

        bakedMesh = new Mesh();
        playerMeshRenderer.BakeMesh(bakedMesh);
        cachedSpawnPos = playerMeshRenderer.transform.position;
        playerMeshRenderer.enabled = false;

        IsComplete = false;
        skipRequested = false;
        scalingStartTime = Time.realtimeSinceStartup;

        StartCoroutine(WaveCoroutine());
    }

    // ============================================
    // COROUTINE
    // ============================================

    private IEnumerator WaveCoroutine()
    {
        isScaling = true;

        for (int i = 0; i < waveCount; i++)
        {
            SpawnGhost(i);

            bool isLast = (i == waveCount - 1);
            if (isLast) break;

            // Attendre delayBetweenWaves, avec sortie anticipee sur skip
            float elapsed = 0f;
            while (elapsed < delayBetweenWaves)
            {
                if (skipRequested)
                {
                    // Spawner immediatement tous les ghosts restants
                    for (int j = i + 1; j < waveCount; j++)
                        SpawnGhost(j);

                    CompleteScale();
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        // Fin naturelle : tous les ghosts spawnes
        CompleteScale();
    }

    // ============================================
    // COMPLETION
    // ============================================

    private void CompleteScale()
    {
        isScaling = false;
        IsComplete = true;
        // Les ghosts restent en vie — VictoryUI les slide et les detruit
        Debug.Log("[VictoryScale] Complete — main passee a VictoryUI.");
        OnVictoryScaleComplete?.Invoke();
    }

    // ============================================
    // SPAWN GHOST
    // ============================================

    private void SpawnGhost(int index)
    {
        Vector3 meshCenter = bakedMesh.bounds.center;
        float scale = Mathf.Pow(2f, index);

        GameObject ghost = new GameObject("VictoryGhost_" + index);
        ghost.transform.position = cachedSpawnPos + meshCenter;
        ghost.transform.localScale = new Vector3(scale, scale, scale);

        GameObject meshGO = new GameObject("Mesh");
        meshGO.transform.SetParent(ghost.transform);
        meshGO.transform.localPosition = -meshCenter;
        meshGO.transform.localRotation = Quaternion.identity;
        meshGO.transform.localScale = Vector3.one;

        MeshFilter mf = meshGO.AddComponent<MeshFilter>();
        mf.mesh = bakedMesh;
        MeshRenderer mr = meshGO.AddComponent<MeshRenderer>();

        Material instanceMat = new Material(victoryMaterial);
        // renderQueue 3000+ (transparent range) -> s'affiche apres le bgQuad (2990) de VictoryUI
        instanceMat.renderQueue = 3000 + (waveCount - 1 - index);
        mr.material = instanceMat;

        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        Color color = (colors != null && index < colors.Length) ? colors[index] : Color.white;
        mpb.SetColor("_Color", color);
        mr.SetPropertyBlock(mpb);

        ghost.AddComponent<VictoryGhostBillboard>();
        ghosts.Add(ghost);
    }
}

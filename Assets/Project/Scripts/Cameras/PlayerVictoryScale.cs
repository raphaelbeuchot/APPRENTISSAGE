using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerVictoryScale : MonoBehaviour
{
    [SerializeField] private SkinnedMeshRenderer playerMeshRenderer;
    [SerializeField] private Material victoryMaterial;
    [SerializeField] private int waveCount = 10;
    [SerializeField] private float delayBetweenWaves = 0.8f;
    [SerializeField] private Color[] colors = new Color[10];
    [SerializeField] private GameUIManager gameUIManager;

    private Mesh bakedMesh;
    private List<GameObject> ghosts = new List<GameObject>();
    private Vector3 cachedSpawnPos;

    public void TriggerScale()
    {
        if (gameUIManager != null) gameUIManager.HideGameplayBars();
        bakedMesh = new Mesh();
        playerMeshRenderer.BakeMesh(bakedMesh);
        cachedSpawnPos = playerMeshRenderer.transform.position;
        playerMeshRenderer.enabled = false;
        StartCoroutine(WaveCoroutine());
    }

    private IEnumerator WaveCoroutine()
    {
        for (int i = 0; i < waveCount; i++)
        {
            SpawnGhost(i);
            yield return new WaitForSecondsRealtime(delayBetweenWaves);
        }
    }

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
        // Ghost 0 devant (renderQueue le plus haut), ghost N derriere
        instanceMat.renderQueue = victoryMaterial.renderQueue + (waveCount - 1 - index);
        mr.material = instanceMat;

        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        Color color = (colors != null && index < colors.Length) ? colors[index] : Color.white;
        mpb.SetColor("_Color", color);
        mr.SetPropertyBlock(mpb);

        ghost.AddComponent<VictoryGhostBillboard>();
        ghosts.Add(ghost);
    }
}
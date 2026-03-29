using UnityEngine;
using System.Collections;
using System.Collections.Generic;
public class PlayerVictoryScale : MonoBehaviour
{
    [SerializeField] private SkinnedMeshRenderer playerMeshRenderer;
    [SerializeField] private Material victoryMaterial;
    [SerializeField] private float scaleSpeed = 10f;
    [SerializeField] private float targetScale = 100f;
    [SerializeField] private float moveSpeed = 0f;
    [SerializeField] private int waveCount = 10;
    [SerializeField] private float delayBetweenWaves = 0.5f;
    [SerializeField] private Color[] colors = new Color[10];
    private Mesh bakedMesh;
    private List<GameObject> ghosts = new List<GameObject>();
    private Vector3 cachedSpawnPos;
    public void TriggerScale()
    {
        bakedMesh = new Mesh();
        playerMeshRenderer.BakeMesh(bakedMesh);
        cachedSpawnPos = playerMeshRenderer.transform.position;
        playerMeshRenderer.enabled = false;
        SpawnGhost(0);
        if (waveCount > 1)
            StartCoroutine(WaveCoroutine());
    }
    private IEnumerator WaveCoroutine()
    {
        for (int i = 1; i < waveCount; i++)
        {
            yield return new WaitForSecondsRealtime(delayBetweenWaves);
            SpawnGhost(i);
        }
    }
    private void SpawnGhost(int index)
    {
        // Centre du mesh bake
        Vector3 meshCenter = bakedMesh.bounds.center;

        // Parent GO au centre du mesh
        GameObject ghost = new GameObject("VictoryGhost_" + index);
        ghost.transform.position = cachedSpawnPos + meshCenter;
        ghost.transform.localScale = Vector3.one;

        // Child GO avec le mesh, offset inverse du centre
        GameObject meshGO = new GameObject("Mesh");
        meshGO.transform.SetParent(ghost.transform);
        meshGO.transform.localPosition = -meshCenter;
        meshGO.transform.localRotation = Quaternion.identity;
        meshGO.transform.localScale = Vector3.one;

        MeshFilter mf = meshGO.AddComponent<MeshFilter>();
        mf.mesh = bakedMesh;
        MeshRenderer mr = meshGO.AddComponent<MeshRenderer>();
        Material instanceMat = new Material(victoryMaterial);
        instanceMat.renderQueue = victoryMaterial.renderQueue + index;
        mr.material = instanceMat;
        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        Color color = (colors != null && index < colors.Length) ? colors[index] : Color.white;
        mpb.SetColor("_Color", color);
        mr.SetPropertyBlock(mpb);

        ghost.AddComponent<VictoryGhostBillboard>();
        ghosts.Add(ghost);
        StartCoroutine(ScaleCoroutine(ghost));
    }
    private IEnumerator ScaleCoroutine(GameObject ghost)
    {
        float elapsed = 0f;
        Vector3 startPos = ghost.transform.position;
        Vector3 screenCenter = Camera.main.ViewportToWorldPoint(
            new Vector3(0.5f, 0.5f, Camera.main.WorldToViewportPoint(startPos).z)
        );
        while (ghost.transform.localScale.x < targetScale)
        {
            float scale = ghost.transform.localScale.x + scaleSpeed * Time.unscaledDeltaTime;
            scale = Mathf.Min(scale, targetScale);
            ghost.transform.localScale = new Vector3(scale, scale, scale);
            ghost.transform.position = Vector3.MoveTowards(
                ghost.transform.position,
                screenCenter,
                moveSpeed * Time.unscaledDeltaTime
            );
            yield return new WaitForSecondsRealtime(0f);
        }
        Destroy(ghost);
        ghosts.Remove(ghost);
       
    }
}
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerVictoryScale : MonoBehaviour
{
    [SerializeField] private SkinnedMeshRenderer playerMeshRenderer;
    [SerializeField] private Material victoryMaterial;
    [SerializeField] private float duration = 3f;
    [SerializeField] private float targetScale = 100f;
    [SerializeField] private float moveSpeed = 0f;
    [SerializeField] private int waveCount = 10;
    [SerializeField] private float delayBetweenWaves = 0.5f;
    [SerializeField] private Color[] colors = new Color[10];

    private Mesh bakedMesh;
    private List<GameObject> ghosts = new List<GameObject>();

    public void TriggerScale()
    {
        bakedMesh = new Mesh();
        playerMeshRenderer.BakeMesh(bakedMesh);
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
        GameObject ghost = new GameObject("VictoryGhost_" + index);
        Vector3 spawnPos = playerMeshRenderer.transform.position;
        spawnPos.y += 0.6f;
        ghost.transform.position = spawnPos;
        ghost.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        ghost.transform.localScale = Vector3.one;

        MeshFilter mf = ghost.AddComponent<MeshFilter>();
        mf.mesh = bakedMesh;

        MeshRenderer mr = ghost.AddComponent<MeshRenderer>();
        mr.material = victoryMaterial;

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

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            float scale = 1f + (targetScale - 1f) * t;
            ghost.transform.localScale = new Vector3(scale, scale, 0f);
            ghost.transform.position = Vector3.MoveTowards(
                ghost.transform.position,
                screenCenter,
                moveSpeed * Time.unscaledDeltaTime
            );
            yield return null;
        }

        Destroy(ghost);
        ghosts.Remove(ghost);
    }
}
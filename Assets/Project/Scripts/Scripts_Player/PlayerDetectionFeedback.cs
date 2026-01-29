using System.Collections;
using UnityEngine;

public class PlayerDetectionFeedback : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AudioClip detectionSound;

    private SkinnedMeshRenderer[] playerMeshRenderers;
    private Material[][] originalMaterials;
    private Material whiteMaterial;
    public bool isCurrentlyDetected = false; // CHANGE DE private A public
    private AudioSource audioSource;

    void Start()
    {
        // Recuperer AudioSource du GameManager
        GameManager gm = FindObjectOfType<GameManager>();
        if (gm != null)
        {
            audioSource = gm.GetComponent<AudioSource>();
        }

        // Creer material blanc
        whiteMaterial = new Material(Shader.Find("Unlit/Color"));
        whiteMaterial.color = Color.white;

        // Recuperer TOUS les SkinnedMeshRenderer dans les enfants
        playerMeshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();

        // Sauvegarder les materials originaux de chaque renderer
        originalMaterials = new Material[playerMeshRenderers.Length][];
        for (int i = 0; i < playerMeshRenderers.Length; i++)
        {
            originalMaterials[i] = playerMeshRenderers[i].materials;
        }
    }

    public void OnDetected()
    {
        if (isCurrentlyDetected) return;

        isCurrentlyDetected = true;

        // Son d'alarme
        if (audioSource != null && detectionSound != null)
        {
            audioSource.PlayOneShot(detectionSound);
        }

        // Silhouette blanche
        SetWhiteSilhouette(true);
    }

    public void OnNoLongerDetected()
    {
        if (!isCurrentlyDetected) return;

        isCurrentlyDetected = false;

        // Restaurer materials normaux
        SetWhiteSilhouette(false);
    }

    void SetWhiteSilhouette(bool white)
    {
        if (playerMeshRenderers == null || originalMaterials == null) return;

        for (int i = 0; i < playerMeshRenderers.Length; i++)
        {
            if (white)
            {
                // Remplacer tous les materials par du blanc
                Material[] whiteMats = new Material[originalMaterials[i].Length];
                for (int j = 0; j < whiteMats.Length; j++)
                {
                    whiteMats[j] = whiteMaterial;
                }
                playerMeshRenderers[i].materials = whiteMats;
            }
            else
            {
                // Restaurer materials originaux
                playerMeshRenderers[i].materials = originalMaterials[i];
            }
        }
    }
}
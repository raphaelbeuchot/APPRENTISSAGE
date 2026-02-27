using System.Collections;
using UnityEngine;

public class PlayerDetectionFeedback : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AudioClip detectionSound;
    [Header("Shot Flash Settings")]
    [SerializeField] private float shotFlashDuration = 0.4f;
    [Header("Silhouette Materials")]
    [SerializeField] private Material whiteMaterial;
    [SerializeField] private Material redMaterial;

    private SkinnedMeshRenderer[] playerMeshRenderers;
    private Material[][] originalMaterials;


    [HideInInspector]
    public bool isCurrentlyDetected = false;
    private AudioSource audioSource;

    void Start()
    {
        // Recuperer AudioSource du GameManager
        GameManager gm = FindObjectOfType<GameManager>();
        if (gm != null)
        {
            audioSource = gm.GetComponent<AudioSource>();
        }

       
        // Recuperer TOUS les SkinnedMeshRenderer dans les enfants
        playerMeshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();

        // Sauvegarder les materials originaux de chaque renderer
        originalMaterials = new Material[playerMeshRenderers.Length][];
        for (int i = 0; i < playerMeshRenderers.Length; i++)
        {
            originalMaterials[i] = playerMeshRenderers[i].materials;
        }
    }
    // NOUVELLE METHODE : Flash rouge quand touché par sentinelle
    public void OnShotBySentinel()
    {
        // Stopper toute détection en cours
        StopAllCoroutines();
        isCurrentlyDetected = false;

        // Lancer le flash rouge
        StartCoroutine(ShotFlashCoroutine());
    }

  
    private IEnumerator ShotFlashCoroutine()
    {
        // Appliquer rouge
        SetColorSilhouette(redMaterial);

        // Attendre
        yield return new WaitForSeconds(shotFlashDuration);

        // Restaurer (soit blanc si détecté, soit normal)
        if (isCurrentlyDetected)
        {
            SetWhiteSilhouette(true);
        }
        else
        {
            SetWhiteSilhouette(false);
        }
    }

    // NOUVELLE METHODE HELPER : Appliquer n'importe quel material de couleur
    void SetColorSilhouette(Material colorMaterial)
    {
        if (playerMeshRenderers == null || originalMaterials == null) return;

        for (int i = 0; i < playerMeshRenderers.Length; i++)
        {
            Material[] colorMats = new Material[originalMaterials[i].Length];
            for (int j = 0; j < colorMats.Length; j++)
            {
                colorMats[j] = colorMaterial;
            }
            playerMeshRenderers[i].materials = colorMats;
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
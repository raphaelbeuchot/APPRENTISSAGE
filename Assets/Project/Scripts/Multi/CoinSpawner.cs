using UnityEngine;

// Coin Race : fait apparaitre une piece a un point aleatoire toutes les spawnInterval secondes.
// spawnArea sert uniquement de zone (ses bounds en X/Z), pas besoin d'etre un vrai trigger physique.
// La hauteur n'est pas prise dans les bounds : un raycast vers le bas, filtre sur le layer Ground,
// trouve la vraie surface au sol a cet endroit. Ca exclut naturellement les pits/lave (le rim Ground
// a un trou a leur emplacement, cf. SplinePitZone) et autorise les plateformes mobiles/tournantes
// (toujours en layer Ground par convention du projet) sans code specifique pour l'un ou l'autre.
public class CoinSpawner : MonoBehaviour
{
    [SerializeField] private GameObject coinPrefab;
    [SerializeField] private BoxCollider spawnArea;
    [SerializeField] private float spawnInterval = 15f;
    [Tooltip("Hauteur au-dessus de la surface trouvee a laquelle la piece apparait.")]
    [SerializeField] private float spawnYOffset = 0.5f;
    [Tooltip("Marge ajoutee au-dessus/en dessous de spawnArea pour que le raycast parte au-dessus et aille assez bas.")]
    [SerializeField] private float raycastMargin = 2f;
    [SerializeField] private int maxAttemptsPerSpawn = 20;

    private int groundMask;

    private void Start()
    {
        if (coinPrefab == null || spawnArea == null)
        {
            Debug.LogWarning("[CoinSpawner] coinPrefab ou spawnArea non assigne, spawner inactif");
            return;
        }

        groundMask = 1 << LayerMask.NameToLayer("Ground");

        InvokeRepeating(nameof(SpawnCoin), 0f, spawnInterval);
    }

    public void StopSpawning()
    {
        CancelInvoke(nameof(SpawnCoin));
    }

    private void SpawnCoin()
    {
        Bounds bounds = spawnArea.bounds;
        float rayOriginY = bounds.max.y + raycastMargin;
        float rayDistance = bounds.size.y + raycastMargin * 2f;

        for (int i = 0; i < maxAttemptsPerSpawn; i++)
        {
            float x = Random.Range(bounds.min.x, bounds.max.x);
            float z = Random.Range(bounds.min.z, bounds.max.z);
            Vector3 origin = new Vector3(x, rayOriginY, z);

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayDistance, groundMask))
            {
                Vector3 spawnPos = hit.point + Vector3.up * spawnYOffset;
                Instantiate(coinPrefab, spawnPos, Quaternion.identity);
                return;
            }
        }

        Debug.LogWarning("[CoinSpawner] Aucun point de spawn valide trouve apres " + maxAttemptsPerSpawn + " tentatives");
    }
}

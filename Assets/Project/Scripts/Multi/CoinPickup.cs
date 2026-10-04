using UnityEngine;

// Coin Race : piece ramassable au sol. Meme detection que KeyCollectible (layer Human),
// mais credite le PlayerCoinWallet du joueur touche au lieu de debloquer une porte.
[RequireComponent(typeof(Collider))]
public class CoinPickup : MonoBehaviour
{
    [SerializeField] private int value = 1;

    [Header("Feedback")]
    [SerializeField] private AudioClip collectSound;
    [SerializeField] private GameObject collectParticlesPrefab;

    [Header("Animation")]
    [SerializeField] private float rotationSpeed = 90f;

    private bool collected;

    // Permet au tas de pieces largue a la mort (PlayerCoinWallet) de porter un montant
    // different de la valeur par defaut du prefab (value, utilisee par le spawn normal).
    public void SetValue(int amount)
    {
        value = amount;
    }

    private void Start()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Update()
    {
        transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected) return;
        if (other.gameObject.layer != LayerMask.NameToLayer("Human")) return;

        PlayerCoinWallet wallet = other.GetComponentInParent<PlayerCoinWallet>();
        if (wallet == null) return;

        Collect(wallet);
    }

    private void Collect(PlayerCoinWallet wallet)
    {
        collected = true;

        wallet.AddCoins(value);

        if (collectParticlesPrefab != null)
            Instantiate(collectParticlesPrefab, transform.position, Quaternion.identity);

        if (collectSound != null)
        {
            GameObject tempAudio = new GameObject("TempAudio_CoinCollect");
            tempAudio.transform.position = transform.position;
            AudioSource tempAS = tempAudio.AddComponent<AudioSource>();
            tempAS.clip = collectSound;
            tempAS.spatialBlend = 0f;
            tempAS.Play();
            Destroy(tempAudio, collectSound.length + 0.1f);
        }

        Destroy(gameObject);
    }
}

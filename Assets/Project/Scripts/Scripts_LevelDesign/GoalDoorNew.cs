using UnityEngine;
using System;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class GoalDoorNew : MonoBehaviour
{
    [Header("Configuration")]
    public bool isActive = false;
    [SerializeField] private float unlockDelay = 1f;

    

    [HideInInspector] public Renderer doorRenderer;
    [Header("Visuels")]

    public Material activeMaterial;
    public Material inactiveMaterial;
    [Tooltip("Duree du fondu de l'alpha du materiau actif a zero quand le joueur atteint la porte (0 = pas de fondu). Materiau en Surface Transparent")]
    [SerializeField] private float reachedFadeDuration = 1f;
    [Tooltip("Bond de l'emission au contact, en intensite HDR (meme unite que le champ Intensity du selecteur de couleur : +1 = x2). Puis descente a zero pendant le fondu")]
    [SerializeField] private float reachedEmissionBoost = 5f;
    private Coroutine fadeRoutine;

    [Header("Audio")]
    public AudioClip doorReachedSound;
    public AudioClip doorUnlockedSound; // son quand la cle est ramassee et la porte s'ouvre
    public AudioClip doorLockedSound;   // son feedback si le joueur arrive sans cle
    private AudioSource audioSource;

    //public GameObject victoryEffectPrefab;
    //public bool destroyPlayerOnReach = false;

    public event Action<GameObject> OnPlayerReached;

    private bool hasBeenReached = false;
    // Multi : plusieurs joueurs distincts peuvent atteindre la porte (le second dans le delai de fin de course).
    // hasBeenReached continue de proteger les effets "une seule fois" (son, arret sentinelle...).
    private readonly System.Collections.Generic.HashSet<GameObject> playersReached = new System.Collections.Generic.HashSet<GameObject>();

    void Start()
    {

        if (doorRenderer == null)
            doorRenderer = GetComponent<Renderer>();
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
        else
            Debug.LogError("[GoalDoorNew] Pas de Collider trouve!");

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        

        UpdateVisuals();

    }

    void OnTriggerEnter(Collider other)
    {
        if (playersReached.Contains(other.gameObject)) return;

        PlayerPhysicsMovement player = other.GetComponent<PlayerPhysicsMovement>();
        if (player == null) return;

        if (!isActive)
        {
            // feedback porte verrouill�e
            if (audioSource != null && doorLockedSound != null)
                audioSource.PlayOneShot(doorLockedSound);
            Debug.Log("[GoalDoorNew] Porte verrouillee - ramassez la cle!");
            return;
        }

        ReachGoal(other.gameObject);
    }

    void ReachGoal(GameObject player)
    {
        bool isFirstReach = !hasBeenReached;
        hasBeenReached = true;
        playersReached.Add(player);
        Debug.Log("[GoalDoorNew] VICTOIRE!");

        // Fige la pose (transform ET animation, pas seulement le mouvement) : sans ca l'animator continue
        // de jouer la derniere intention (ex: course) meme si le personnage ne bouge plus physiquement.
        // Animation figee en multi seulement : en solo, PostVictorySequencer reactive le joueur sans remettre
        // animator.speed a 1, il resterait fige dans la salle de transition.
        PlayerPhysicsMovement playerMovement = player.GetComponent<PlayerPhysicsMovement>();
        if (playerMovement != null)
            playerMovement.enabled = false;

        Animator playerAnimator = player.GetComponentInChildren<Animator>();
        if (playerAnimator != null && IsMultiRace())
            playerAnimator.speed = 0f;

        PlayerDetectionFeedback pdf = player.GetComponent<PlayerDetectionFeedback>();
        if (pdf != null)
            pdf.ResetForVictory();

        OnPlayerReached?.Invoke(player);

        // Effets "une seule fois", au premier joueur qui atteint la porte seulement.
        if (isFirstReach)
        {
            if (audioSource != null && doorReachedSound != null)
                audioSource.PlayOneShot(doorReachedSound);

            if (reachedFadeDuration > 0f && doorRenderer != null)
                fadeRoutine = StartCoroutine(FadeOutDoor());

            //if (victoryEffectPrefab != null)
              //  Instantiate(victoryEffectPrefab, transform.position, Quaternion.identity);

            SentinelCycleManager sentinelCycle = FindObjectOfType<SentinelCycleManager>();
            if (sentinelCycle != null)
                sentinelCycle.StopCycle();
            else
                Debug.LogWarning("[GoalDoorNew] SentinelCycleManager introuvable!");

            foreach (BombSpawner b in FindObjectsOfType<BombSpawner>())
                b.StopChargeAudio();
        }

        //if (destroyPlayerOnReach)
            //Destroy(player, 2f);
    }

    // Alpha du materiau (instance propre a cette porte) ramene a zero : _BaseColor (URP Lit), sinon _Color.
    // L'emission bondit (reachedEmissionBoost) puis baisse en meme temps : avec Preserve Specular Lighting (URP),
    // elle n'est pas multipliee par l'alpha et la porte resterait lumineuse a alpha 0.
    private IEnumerator FadeOutDoor()
    {
        Material mat = doorRenderer.material;
        string colorProperty = mat.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
        if (!mat.HasProperty(colorProperty)) yield break;

        Color color = mat.GetColor(colorProperty);
        float startAlpha = color.a;
        bool hasEmission = mat.HasProperty("_EmissionColor");
        // Bond immediat de l'emission, puis descente depuis cette valeur
        Color startEmission = hasEmission ? mat.GetColor("_EmissionColor") * Mathf.Pow(2f, reachedEmissionBoost) : Color.black;
        if (hasEmission) mat.SetColor("_EmissionColor", startEmission);
        float elapsed = 0f;

        while (elapsed < reachedFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / reachedFadeDuration);
            color.a = Mathf.Lerp(startAlpha, 0f, t);
            mat.SetColor(colorProperty, color);
            if (hasEmission) mat.SetColor("_EmissionColor", Color.Lerp(startEmission, Color.black, t));
            yield return null;
        }

        color.a = 0f;
        mat.SetColor(colorProperty, color);
        if (hasEmission) mat.SetColor("_EmissionColor", Color.black);
        fadeRoutine = null;
    }

    public void UnlockWithKey()
    {
        if (isActive) return;
        StartCoroutine(UnlockAfterDelay());
    }

    private bool IsMultiRace()
    {
        LevelManager levelManager = FindObjectOfType<LevelManager>();
        return levelManager != null && levelManager.multiRaceMode;
    }

    private IEnumerator UnlockAfterDelay()
    {
        yield return new WaitForSeconds(unlockDelay);

        isActive = true;
        UpdateVisuals();

        if (audioSource != null && doorUnlockedSound != null)
            audioSource.PlayOneShot(doorUnlockedSound);

        Debug.Log("[GoalDoorNew] Cle ramassee - porte deverrouillee!");
    }

    public void SetActive(bool active)
    {
        isActive = active;
        UpdateVisuals();
    }

    void UpdateVisuals()
    {
        if (doorRenderer == null) return;

        if (isActive && activeMaterial != null)
            doorRenderer.material = activeMaterial;
        else if (!isActive && inactiveMaterial != null)
            doorRenderer.material = inactiveMaterial;
    }

    public void ResetDoor()
    {
        hasBeenReached = false;
        isActive = false;
        // UpdateVisuals remet le materiau d'origine (alpha plein)
        if (fadeRoutine != null) { StopCoroutine(fadeRoutine); fadeRoutine = null; }
        UpdateVisuals();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = isActive ? Color.green : Color.red;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 2f);

        if (isActive)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 3f);
        }
    }
}
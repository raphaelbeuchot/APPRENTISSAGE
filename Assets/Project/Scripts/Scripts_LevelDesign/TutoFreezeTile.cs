using UnityEngine;
using System.Collections;
using TMPro;

public class TutoFreezeTile : MonoBehaviour
{
    [Header("Dalle")]
    [SerializeField] private MeshRenderer dalleRenderer;

    [Header("Marquee Lights")]
    [SerializeField] private SpriteRenderer marquee1;
    [SerializeField] private SpriteRenderer marquee2;
    [SerializeField] private SpriteRenderer marquee3;

    [Header("Materiaux")]
    [SerializeField] private Material matInactive;
    [SerializeField] private Material matWaiting;
    [SerializeField] private Material matActive;

    [Header("Chaine")]
    [SerializeField] private ChainRenderer chainRenderer;
    [SerializeField] private Transform ceilingAnchor;
    [SerializeField] private Transform corpseChainPoint;

    [Header("Spawn Cadavre")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private GameObject corpseObject;
    [SerializeField] private float corpseForce = 20f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip soundCountdown;
    [SerializeField] private AudioClip soundSuccess;
    [SerializeField] private AudioClip soundFail;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI messageText;

    [Header("Settings")]
    [SerializeField] private float contactWindowDuration = 0.5f;
    [SerializeField] private float movementThreshold = 0.1f;

    private bool tileActive = false;
    private bool playerOnTile = false;
    private bool testRunning = false;
    private int spawnCount = 0;
    private const int maxSpawns = 5;

    private Rigidbody playerRb;
    private Transform player;

    void Start()
    {
        PlayerPhysicsMovement p = FindObjectOfType<PlayerPhysicsMovement>();
        if (p != null)
        {
            player = p.transform;
            playerRb = p.GetComponent<Rigidbody>();
        }

        SetDalleMaterial(matInactive);
        SetAllMarquees(matInactive);

        if (messageText != null)
            messageText.gameObject.SetActive(false);
    }

    // Appele par PupitreTuto en callback quand toutes les pages sont lues
    public void ActivateTile()
    {
        tileActive = true;
        SetDalleMaterial(matWaiting);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!tileActive || testRunning) return;
        if (!other.CompareTag("Player")) return;

        playerOnTile = true;
        SetDalleMaterial(matActive);
        SetAllMarquees(matWaiting);
        StartCoroutine(CountdownAndSpawn());
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerOnTile = false;
        SetDalleMaterial(matWaiting);
    }

    private IEnumerator CountdownAndSpawn()
    {
        testRunning = true;

        if (spawnCount >= maxSpawns)
        {
            ShowMessage("Nettoie ton bazar avant de reessayer !");
            SetDalleMaterial(matWaiting);
            testRunning = false;
            yield break;
        }

        // Preparer le corps au plafond
        GameObject newCorpse = Instantiate(corpseObject, ceilingAnchor.position, corpseObject.transform.rotation);
        newCorpse.SetActive(true);

        Rigidbody corpseRb = newCorpse.GetComponent<Rigidbody>();
        if (corpseRb != null)
        {
            corpseRb.isKinematic = true;
            corpseRb.linearVelocity = Vector3.zero;
            corpseRb.angularVelocity = Vector3.zero;
        }

        // Brancher la chaine sur ce nouveau corps
        if (chainRenderer != null)
        {
            corpseChainPoint = newCorpse.transform;
            chainRenderer.zombieNeck = corpseChainPoint;
            chainRenderer.GetComponent<LineRenderer>().enabled = true;
        }

        SetAllMarquees(matWaiting);

        // Descente reguliere sur 3s avec extinction marquees
        float totalDuration = 3f;
        float elapsed = 0f;
        int marqueeStep = 0;

        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / totalDuration;

            // Lerp position corps du plafond vers spawn point
            newCorpse.transform.position = Vector3.Lerp(ceilingAnchor.position, spawnPoint.position, t);

            // Extinction marquees au 1/3 et 2/3
            if (marqueeStep == 0 && elapsed >= 1f)
            {
                SetMarquee(marquee3, matInactive);
                PlaySound(soundCountdown);
                marqueeStep = 1;
            }
            else if (marqueeStep == 1 && elapsed >= 2f)
            {
                SetMarquee(marquee2, matInactive);
                PlaySound(soundCountdown);
                marqueeStep = 2;
            }
            else if (marqueeStep == 2 && elapsed >= 3f)
            {
                SetMarquee(marquee1, matInactive);
                PlaySound(soundCountdown);
                marqueeStep = 3;
            }

            yield return null;
        }

        // Position finale exacte
        newCorpse.transform.position = spawnPoint.position;

        // Chaine remonte sans le corps
        if (chainRenderer != null)
            StartCoroutine(RetractChain());

        // Courte pause puis lancement
        yield return new WaitForSeconds(0.3f);

        spawnCount++;
        LaunchCorpse(newCorpse, corpseRb);
    }

    private IEnumerator RetractChain()
    {
        LineRenderer lr = chainRenderer.GetComponent<LineRenderer>();
        float elapsed = 0f;
        float duration = 0.4f;
        Vector3 startPos = chainRenderer.zombieNeck.position;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // Deplace le point zombieNeck vers l'anchor pour simuler remontee
            chainRenderer.zombieNeck.position = Vector3.Lerp(startPos, ceilingAnchor.position, t);
            yield return null;
        }

        lr.enabled = false;
    }

    private void LaunchCorpse(GameObject corpse, Rigidbody rb)
    {
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        TutoCorpseProjectile proj = corpse.GetComponent<TutoCorpseProjectile>();
        if (proj == null)
            proj = corpse.AddComponent<TutoCorpseProjectile>();

        Vector3 direction = (player != null)
            ? (player.position - spawnPoint.position).normalized
            : transform.forward;

        proj.Init(this, direction * corpseForce);
    }

    // Appele par TutoCorpseProjectile au contact avec le player
    public void OnCorpseHitPlayer()
    {
        StartCoroutine(CheckMovementWindow());
    }

    private IEnumerator CheckMovementWindow()
    {
        float elapsed = 0f;
        bool failed = false;

        while (elapsed < contactWindowDuration)
        {
            if (playerRb != null && playerRb.linearVelocity.magnitude > movementThreshold)
            {
                failed = true;
                break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (failed)
            StartCoroutine(OnFail());
        else
            StartCoroutine(OnSuccess());
    }

    private IEnumerator OnSuccess()
    {
        PlaySound(soundSuccess);
        ShowMessage("Bien joue !");
        yield return new WaitForSeconds(2f);
        HideMessage();
        ResetTile();
    }

    private IEnumerator OnFail()
    {
        PlaySound(soundFail);
        ShowMessage("Reste immobile !");
        yield return new WaitForSeconds(2f);
        HideMessage();
        ResetTile();
    }

    private void ResetTile()
    {
        testRunning = false;
        SetDalleMaterial(playerOnTile ? matActive : matWaiting);
        SetAllMarquees(matInactive);

        if (playerOnTile)
            StartCoroutine(CountdownAndSpawn());
    }

    // --- Helpers ---

    private void SetDalleMaterial(Material mat)
    {
        if (dalleRenderer != null)
            dalleRenderer.material = mat;
    }

    private void SetAllMarquees(Material mat)
    {
        SetMarquee(marquee1, mat);
        SetMarquee(marquee2, mat);
        SetMarquee(marquee3, mat);
    }

    private void SetMarquee(SpriteRenderer sr, Material mat)
    {
        if (sr != null)
            sr.material = mat;
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    private void ShowMessage(string msg)
    {
        if (messageText == null) return;
        messageText.text = msg;
        messageText.gameObject.SetActive(true);
    }

    private void HideMessage()
    {
        if (messageText != null)
            messageText.gameObject.SetActive(false);
    }
}
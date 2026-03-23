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

        // Countdown 3 marquees
        SetAllMarquees(matWaiting);

        yield return new WaitForSeconds(1f);
        SetMarquee(marquee3, matInactive);
        PlaySound(soundCountdown);

        yield return new WaitForSeconds(1f);
        SetMarquee(marquee2, matInactive);
        PlaySound(soundCountdown);

        yield return new WaitForSeconds(1f);
        SetMarquee(marquee1, matInactive);
        PlaySound(soundCountdown);

        yield return new WaitForSeconds(0.5f);

        // Spawn et projection cadavre
        spawnCount++;
        StartCoroutine(SpawnCorpseCoroutine());
    }

    private IEnumerator SpawnCorpseCoroutine()
    {
        if (corpseObject == null || spawnPoint == null) yield break;

        GameObject newCorpse = Instantiate(corpseObject, spawnPoint.position, spawnPoint.rotation);
        newCorpse.SetActive(true);

        yield return new WaitForFixedUpdate();

        Rigidbody rb = newCorpse.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        TutoCorpseProjectile proj = newCorpse.GetComponent<TutoCorpseProjectile>();
        if (proj == null)
            proj = newCorpse.AddComponent<TutoCorpseProjectile>();

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
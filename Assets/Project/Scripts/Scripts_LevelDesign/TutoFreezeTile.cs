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

    [Header("Spawn Cadavre")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private GameObject corpsePrefab;
    [SerializeField] private float corpseForce = 20f;

    [Header("Pousse Cadavre")]

    [SerializeField] private Transform slidingCube;
    [SerializeField] private AudioClip soundCubeSlide;

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
    [SerializeField] private float countdownDuration = 3f;
    [SerializeField] private float chainRetractDuration = 0.4f;

    private bool tileActive = false;
    private bool playerOnTile = false;
    private bool testRunning = false;
    private bool waitingForExit = false;
    private int spawnCount = 0;
    private const int maxSpawns = 5;

    private bool isPaused = false;

    private Rigidbody playerRb;
    private Transform player;

    private Transform chainDummy;
    private Coroutine countdownCoroutine;

    void Start()
    {
        PlayerPhysicsMovement p = FindObjectOfType<PlayerPhysicsMovement>();
        if (p != null)
        {
            player = p.transform;
            playerRb = p.GetComponent<Rigidbody>();
        }

        // Dummy pour la chaine, independant du corpse
        chainDummy = new GameObject("ChainDummy").transform;

        SetDalleMaterial(matInactive);
        SetAllMarquees(matInactive);

        if (chainRenderer != null)
            chainRenderer.GetComponent<LineRenderer>().enabled = false;

        if (messageText != null)
            messageText.gameObject.SetActive(false);
    }

    public void ActivateTile()
    {
        tileActive = true;
        SetDalleMaterial(matWaiting);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!tileActive) return;
        if (!other.CompareTag("Player")) return;
        if (waitingForExit) return;

        playerOnTile = true;

        // Si un countdown est en cours et en pause, on reprend
        if (testRunning && isPaused)
        {
            isPaused = false;
            SetDalleMaterial(matActive);
            return;
        }

        if (testRunning) return;

        if (spawnCount >= maxSpawns)
        {
            ShowMessage("Nettoie ton bazar avant de reessayer !");
            return;
        }

        SetDalleMaterial(matActive);
        SetAllMarquees(matActive);
        countdownCoroutine = StartCoroutine(CountdownAndSpawn());
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerOnTile = false;
        waitingForExit = false;

        if (testRunning)
        {
            isPaused = true;
            SetDalleMaterial(matWaiting);
        }
        else
        {
            SetDalleMaterial(matWaiting);
        }
    }

    private IEnumerator CountdownAndSpawn()
    {
        testRunning = true;

        // Instancier le corps au plafond, kinematic
        GameObject newCorpse = Instantiate(corpsePrefab, ceilingAnchor.position, corpsePrefab.transform.rotation);
        Rigidbody corpseRb = newCorpse.GetComponent<Rigidbody>();
        if (corpseRb != null)
        {
            corpseRb.isKinematic = true;
            corpseRb.linearVelocity = Vector3.zero;
            corpseRb.angularVelocity = Vector3.zero;
        }

        // Brancher le dummy de chaine sur la position du corps
        chainDummy.position = ceilingAnchor.position;
        if (chainRenderer != null)
        {
            chainRenderer.zombieNeck = chainDummy;
            chainRenderer.GetComponent<LineRenderer>().enabled = true;
        }

        // Descente reguliere + extinction marquees
        float elapsed = 0f;
        int marqueeStep = 0;

        while (elapsed < countdownDuration)
        {
            // Pause si le player a quitte la dalle
            while (isPaused)
            {
                yield return null;
            }

            elapsed += Time.deltaTime;
            float t = elapsed / countdownDuration;

            Vector3 pos = Vector3.Lerp(ceilingAnchor.position, spawnPoint.position, t);
            newCorpse.transform.position = pos;
            chainDummy.position = pos;

            if (marqueeStep == 0 && elapsed >= countdownDuration / 3f)
            {
                SetMarquee(marquee3, matInactive);
                PlaySound(soundCountdown);
                marqueeStep = 1;
            }
            else if (marqueeStep == 1 && elapsed >= (countdownDuration / 3f) * 2f)
            {
                SetMarquee(marquee2, matInactive);
                PlaySound(soundCountdown);
                marqueeStep = 2;
            }
            else if (marqueeStep == 2 && elapsed >= countdownDuration)
            {
                SetMarquee(marquee1, matInactive);
                PlaySound(soundCountdown);
                marqueeStep = 3;
            }

            yield return null;
        }

        // Position finale exacte
        newCorpse.transform.position = spawnPoint.position;
        chainDummy.position = spawnPoint.position;

        // Chaine remonte sans le corps
        StartCoroutine(RetractChain());

        // Courte pause puis lancement
        yield return new WaitForSeconds(0.3f);

        spawnCount++;
        testRunning = false;
        waitingForExit = true;

        SetDalleMaterial(matWaiting);
        SetAllMarquees(matInactive);

        LaunchCorpse(newCorpse, corpseRb);
    }

    private IEnumerator RetractChain()
    {
        if (chainRenderer == null) yield break;

        LineRenderer lr = chainRenderer.GetComponent<LineRenderer>();
        float elapsed = 0f;
        Vector3 startPos = chainDummy.position;

        while (elapsed < chainRetractDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / chainRetractDuration;
            chainDummy.position = Vector3.Lerp(startPos, ceilingAnchor.position, t);
            yield return null;
        }

        chainDummy.position = ceilingAnchor.position;
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

        if (slidingCube != null)
            StartCoroutine(SlideCube());
    }

    public void OnCorpseHitPlayer()
    {
        StartCoroutine(CheckMovementWindow());
    }

    private IEnumerator SlideCube()
    {
        PlaySound(soundCubeSlide);

        Vector3 startPos = slidingCube.position;
        Vector3 targetPos = startPos + Vector3.right;
        float elapsed = 0f;

        // Aller : 0.2s
        while (elapsed < 0.2f)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / 0.2f);
            slidingCube.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        slidingCube.position = targetPos;

        // Retour : 1s
        elapsed = 0f;
        while (elapsed < 1f)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / 1f);
            slidingCube.position = Vector3.Lerp(targetPos, startPos, t);
            yield return null;
        }

        slidingCube.position = startPos;
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
    }

    private IEnumerator OnFail()
    {
        PlaySound(soundFail);
        ShowMessage("Reste immobile !");
        yield return new WaitForSeconds(2f);
        HideMessage();
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
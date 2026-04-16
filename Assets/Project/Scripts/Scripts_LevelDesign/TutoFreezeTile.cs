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
    [SerializeField] private AudioClip soundActivate;
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
    private bool cycleRunning = false;
    private bool isPaused = false;
    private bool waitingForFinalExit = false;
    private int spawnCount = 0;
    private const int maxSpawns = 5;

    private Rigidbody playerRb;
    private Transform player;
    private Transform chainDummy;

    void Start()
    {
        PlayerPhysicsMovement p = FindObjectOfType<PlayerPhysicsMovement>();
        if (p != null)
        {
            player = p.transform;
            playerRb = p.GetComponent<Rigidbody>();
        }

        chainDummy = new GameObject("ChainDummy").transform;

        SetDalleMaterial(matInactive);
        SetAllMarquees(matInactive);

        if (chainRenderer != null)
            chainRenderer.GetComponent<LineRenderer>().enabled = false;

        if (messageText != null)
            messageText.gameObject.SetActive(false);
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
            audioSource.spatialBlend = 0f;
    }

    public void ActivateTile()
    {
        tileActive = true;
        SetDalleMaterial(matWaiting);
        PlaySound(soundActivate);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerOnTile = true;

        if (waitingForFinalExit) return;

        // Reprise apres pause mid-countdown
        if (cycleRunning && isPaused)
        {
            isPaused = false;
            SetDalleMaterial(matActive);
            return;
        }

        if (!tileActive || cycleRunning) return;

        SetDalleMaterial(matActive);
        SetAllMarquees(matActive);
        StartCoroutine(RunCycles());
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerOnTile = false;

        if (waitingForFinalExit)
        {
            ShutdownPermanently();
            return;
        }

        if (cycleRunning)
        {
            isPaused = true;
            SetDalleMaterial(matWaiting);
        }
    }

    private IEnumerator RunCycles()
    {
        cycleRunning = true;

        while (spawnCount < maxSpawns)
        {
            yield return StartCoroutine(CountdownAndSpawn());

            // Delai 1s post-projection
            float elapsed = 0f;
            bool playerLeft = false;
            while (elapsed < 1f)
            {
                if (!playerOnTile) { playerLeft = true; break; }
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (playerLeft)
            {
                // Player parti pendant le delai : on sort proprement
                // OnTriggerEnter restartera RunCycles au retour
                cycleRunning = false;
                isPaused = false;
                yield break;
            }

            // Player encore la et spawns restants : on reboucle
            if (spawnCount < maxSpawns)
            {
                SetDalleMaterial(matActive);
                SetAllMarquees(matActive);
            }
        }

        // 5 spawns effectues
        cycleRunning = false;
        CommentPanel.Show("Sweep the corpses away");

        if (playerOnTile)
            waitingForFinalExit = true;
        else
            ShutdownPermanently();
    }

    private IEnumerator CountdownAndSpawn()
    {
        GameObject newCorpse = Instantiate(corpsePrefab, ceilingAnchor.position, corpsePrefab.transform.rotation);
        Rigidbody corpseRb = newCorpse.GetComponent<Rigidbody>();
        if (corpseRb != null)
        {
            corpseRb.isKinematic = true;
            corpseRb.linearVelocity = Vector3.zero;
            corpseRb.angularVelocity = Vector3.zero;
        }

        chainDummy.position = ceilingAnchor.position;
        if (chainRenderer != null)
        {
            chainRenderer.zombieNeck = chainDummy;
            chainRenderer.GetComponent<LineRenderer>().enabled = true;
        }

        float elapsed = 0f;
        int marqueeStep = 0;

        while (elapsed < countdownDuration)
        {
            while (isPaused) yield return null;

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

        newCorpse.transform.position = spawnPoint.position;
        chainDummy.position = spawnPoint.position;

        StartCoroutine(RetractChain());

        yield return new WaitForSeconds(0.3f);

        spawnCount++;
        SetDalleMaterial(matInactive);
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

        CorpseProjectile proj = corpse.GetComponent<CorpseProjectile>();

        Vector3 direction = (player != null)
            ? (player.position - spawnPoint.position).normalized
            : transform.forward;

        proj.Init(this, direction * corpseForce);

        if (slidingCube != null)
            StartCoroutine(SlideCube());
    }

    private void ShutdownPermanently()
    {
        waitingForFinalExit = false;
        tileActive = false;
        SetDalleMaterial(matInactive);
        SetAllMarquees(matInactive);
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

        while (elapsed < 0.1f)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / 0.1f);
            slidingCube.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        slidingCube.position = targetPos;

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

    private void OnEnable()
    {
        CorpsePitHandler.OnCorpseCleaned += HandleCorpseCleaned;
    }

    private void OnDisable()
    {
        CorpsePitHandler.OnCorpseCleaned -= HandleCorpseCleaned;
    }

    private void HandleCorpseCleaned(CorpsePitHandler corpse)
    {
        if (CleaningBonusManager.Instance != null)
            CleaningBonusManager.Instance.PlayCleanFeedback();
    }
}
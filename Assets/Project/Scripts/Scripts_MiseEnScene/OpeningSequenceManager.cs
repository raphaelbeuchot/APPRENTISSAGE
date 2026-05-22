using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

public class OpeningSequenceManager : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private PlayerPhysicsMovement playerMovement;
    [SerializeField] private Animator playerAnimator;

    [Header("Bouton")]
    [SerializeField] private Transform boutonTransform;
    [SerializeField] private float interactionRange = 1f;

    [Header("Porte")]
    [SerializeField] private Transform door;
    [SerializeField] private float doorRiseHeight = 2f;
    [SerializeField] private float doorRiseDuration = 1.2f;

    [Header("Cameras")]
    [SerializeField] private GameObject camStartZoneGO;
    [SerializeField] private CinemachineCamera camStartRoomVcam;
    [SerializeField] private CameraConfiner cameraConfiner;
    [SerializeField] private GameObject cameraPanningGO;

    [Header("Scene")]
    [SerializeField] private GameObject celluleGO;
    [SerializeField] private Collider[] blockerColliders;

    [Header("Sons")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip sonBouton;
    [SerializeField] private AudioClip sonPorte;
    [SerializeField] private AudioClip sonGrille;

    [Header("Ecran Noir")]
    [SerializeField] private CanvasGroup ecranNoir;
    [SerializeField] private float dureeEcranNoir = 4f;
    [SerializeField] private float dureeFadeOut = 1f;

    [Header("Lever")]
    [SerializeField] private float delaiAvantPrompt = 2f;
    [SerializeField] private string textePrompt = "Press X";

    private Transform player;
    private bool wakeUpDone = false;
    private bool boutonActive = false;
    private bool seuilFranchi = false;
    private bool isRestart = false;

    private void Awake()
    {
        isRestart = PlayerPrefs.GetInt("AutoStartCountdown", 0) == 1;
    }

    private void Start()
    {
        player = playerMovement.transform;

        if (isRestart)
        {
            if (camStartRoomVcam != null) camStartRoomVcam.Priority = 0;
            if (ecranNoir != null) ecranNoir.alpha = 0f;
            wakeUpDone = true;
            return;
        }

        playerMovement.enabled = false;

        foreach (Collider c in blockerColliders)
            c.enabled = false;

        camStartZoneGO.SetActive(false);
        cameraConfiner.enabled = false;

        if (ecranNoir != null) ecranNoir.alpha = 1f;

        playerAnimator.Play("StandUpOPENING", 0, 0f);
        playerAnimator.speed = 0f;
        StartCoroutine(AttendreFinLever());
    }

    private IEnumerator AttendreFinLever()
    {
        yield return null;

        if (ecranNoir != null)
        {
            ecranNoir.alpha = 1f;
            yield return new WaitForSeconds(dureeEcranNoir);

            float elapsed = 0f;
            while (elapsed < dureeFadeOut)
            {
                elapsed += Time.deltaTime;
                ecranNoir.alpha = Mathf.Lerp(1f, 0f, elapsed / dureeFadeOut);
                yield return null;
            }
            ecranNoir.alpha = 0f;
        }

        yield return new WaitForSeconds(delaiAvantPrompt);

        CommentPanel.ShowPersistent(textePrompt);

        yield return new WaitUntil(() => PlayerInputManager.Instance.InteractPressed);

        CommentPanel.Hide();
        playerAnimator.speed = 1f;

        yield return new WaitUntil(() =>
        {
            AnimatorStateInfo info = playerAnimator.GetCurrentAnimatorStateInfo(0);
            return info.IsName("StandUpOPENING") && info.normalizedTime >= 0.99f;
        });

        playerMovement.enabled = true;
        wakeUpDone = true;
    }

    private void Update()
    {
        if (!wakeUpDone || boutonActive || player == null) return;
        if (boutonTransform == null) return;

        float distance = Vector3.Distance(boutonTransform.position, player.position);
        if (distance <= interactionRange && PlayerInputManager.Instance.InteractPressed)
            ActiverBouton();
    }

    private void ActiverBouton()
    {
        boutonActive = true;

        boutonTransform.GetComponent<InteractBubble>()?.Hide();
        boutonTransform.GetComponent<PupitreStartRoomFX>()?.Stop();

        if (sonBouton != null)
            audioSource.PlayOneShot(sonBouton);

        if (camStartRoomVcam != null) camStartRoomVcam.Priority = 0;
        camStartZoneGO.SetActive(true);
        cameraConfiner.enabled = true;

        StartCoroutine(MonterPorte());
    }

    private IEnumerator MonterPorte()
    {
        if (sonPorte != null)
            audioSource.PlayOneShot(sonPorte);

        Vector3 startPos = door.position;
        Vector3 endPos = startPos + Vector3.up * doorRiseHeight;
        float elapsed = 0f;

        while (elapsed < doorRiseDuration)
        {
            elapsed += Time.deltaTime;
            door.position = Vector3.Lerp(startPos, endPos, elapsed / doorRiseDuration);
            yield return null;
        }

        door.position = endPos;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (seuilFranchi || !other.CompareTag("Player")) return;

        seuilFranchi = true;
        StartCoroutine(FranchiSeuil());
    }

    private IEnumerator FranchiSeuil()
    {
        yield return new WaitForSeconds(0.3f);

        if (sonGrille != null)
            audioSource.PlayOneShot(sonGrille);

        celluleGO.SetActive(false);

        foreach (Collider c in blockerColliders)
            c.enabled = true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        if (boutonTransform != null)
            Gizmos.DrawWireSphere(boutonTransform.position, interactionRange);
    }
}

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

    private Transform player;
    private bool wakeUpDone = false;
    private bool boutonActive = false;
    private bool seuilFranchi = false;

    private void Start()
    {
        player = playerMovement.transform;

        playerMovement.canMove = false;

        foreach (Collider c in blockerColliders)
            c.enabled = false;

        camStartZoneGO.SetActive(false);
        cameraConfiner.enabled = false;

        // TEMPORAIRE : a remplacer par les deux lignes commentees quand StandUpNEW est pret
        playerMovement.canMove = true;
        wakeUpDone = true;

        // playerAnimator.Play("StandUpNEW");
        // StartCoroutine(AttendreFinLever());
    }

    private IEnumerator AttendreFinLever()
    {
        yield return null;
        yield return new WaitUntil(() =>
        {
            AnimatorStateInfo info = playerAnimator.GetCurrentAnimatorStateInfo(0);
            return info.IsName("StandUpNEW") && info.normalizedTime >= 1f;
        });

        playerMovement.canMove = true;
        wakeUpDone = true;
    }

    private void Update()
    {
        if (!wakeUpDone || boutonActive || player == null) return;

        if (boutonTransform == null) { Debug.LogError("[OSM] boutonTransform non assigne !"); return; }
        float distance = Vector3.Distance(boutonTransform.position, player.position);
        Debug.Log($"[OSM] dist={distance:F2} range={interactionRange} interact={PlayerInputManager.Instance?.InteractPressed}");
        if (distance <= interactionRange && PlayerInputManager.Instance.InteractPressed)
            ActiverBouton();
    }

    private void ActiverBouton()
    {
        boutonActive = true;

        boutonTransform.GetComponent<InteractBubble>()?.Hide();

        if (sonBouton != null)
            audioSource.PlayOneShot(sonBouton);

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
        if (camStartRoomVcam != null) camStartRoomVcam.Priority = 0;
        camStartZoneGO.SetActive(true);
        cameraConfiner.enabled = true;

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
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Interrupteur reutilisable : le joueur s'approche, appuie sur Interact -> On Pressed.
/// A poser sur un objet qui a deja son InteractBubble (affichage du bouton) : la portee est celle
/// de la bulle (Detection Range), comme DoorBasic.
///
/// Verrouillage : "Lock After Press" coche, l'interrupteur se verrouille apres un appui (bulle cachee,
/// appuis ignores) jusqu'a Unlock(), a appeler par la sequence declenchee (ex. pince de la poutre).
/// Decoche : utilisable a chaque appui.
/// </summary>
[RequireComponent(typeof(InteractBubble))]
public class InteractSwitch : MonoBehaviour
{
    [Header("Verrouillage")]
    [Tooltip("Verrouille apres un appui, jusqu'a Unlock() (appele par ce qui a ete declenche).")]
    [SerializeField] private bool lockAfterPress = true;

    [Header("Enfoncement a l'appui (0 = aucun) : descend puis remonte")]
    [SerializeField] private float pressDistance = 0.1f;
    [SerializeField] private float pressDuration = 0.15f;

    [Header("Effets")]
    [SerializeField] private UnityEvent onPressed;

    public bool IsLocked { get; private set; }

    private InteractBubble bubble;
    private Transform player;
    private Vector3 restLocalPos;
    private Coroutine pressRoutine;

    private void Awake()
    {
        bubble = GetComponent<InteractBubble>();
        restLocalPos = transform.localPosition;
    }

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;
        else Debug.LogWarning("[InteractSwitch] Player introuvable : " + name);
    }

    private void Update()
    {
        if (IsLocked || player == null || PlayerInputManager.Instance == null) return;

        bool inRange = Vector3.Distance(transform.position, player.position) <= bubble.DetectionRange;
        if (inRange && PlayerInputManager.Instance.InteractPressed)
            Press();
    }

    /// <summary>Appui (aussi appelable depuis un evenement ou un autre script).</summary>
    public void Press()
    {
        if (IsLocked) return;

        if (lockAfterPress) Lock();

        if (pressDistance > 0f)
        {
            if (pressRoutine != null) StopCoroutine(pressRoutine);
            pressRoutine = StartCoroutine(PressCoroutine());
        }

        Debug.Log("[InteractSwitch] Appui : " + name);
        onPressed?.Invoke();
    }

    /// <summary>Verrouille : appuis ignores, bulle cachee.</summary>
    public void Lock()
    {
        IsLocked = true;
        bubble.Hide();
        bubble.enabled = false;
    }

    /// <summary>Deverrouille : de nouveau utilisable, la bulle revient a portee.</summary>
    public void Unlock()
    {
        IsLocked = false;
        bubble.enabled = true;
    }

    private IEnumerator PressCoroutine()
    {
        Vector3 down = restLocalPos + Vector3.down * pressDistance;
        float half = pressDuration * 0.5f;
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            transform.localPosition = Vector3.Lerp(restLocalPos, down, t / half);
            yield return null;
        }
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            transform.localPosition = Vector3.Lerp(down, restLocalPos, t / half);
            yield return null;
        }
        transform.localPosition = restLocalPos;
        pressRoutine = null;
    }
}

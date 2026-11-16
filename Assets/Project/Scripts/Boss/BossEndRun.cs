using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fin du boss Rush Hour IV, dans la salle infinie : le joueur perd la main, le perso court seul vers un
/// point repere puis en ligne droite (Run Direction) sans fin. Le generique = TMP poses dans la salle.
///
/// Branchement (Event "On Object Enter" des CinemachineTriggerAction, layer Human) :
/// - trigger A -> Play() : le joueur perd la main, course automatique ;
/// - trigger B, apres le dernier texte -> End() : fondu au noir (Fade Image) et volume des pas a zero
///   pendant "Fade Duration", puis le perso est fige et le panneau de fin (DemoEndPanel) s'affiche.
/// </summary>
public class BossEndRun : MonoBehaviour
{
    [Header("Course")]
    [Tooltip("Point vers lequel le perso court d'abord. Vide = directement en Run Direction.")]
    [SerializeField] private Transform waypoint;
    [Tooltip("Distance (XZ, m) a laquelle le point repere est considere atteint.")]
    [Min(0.05f)] [SerializeField] private float arriveRadius = 0.5f;
    [Tooltip("Direction de course apres le point repere (monde).")]
    [SerializeField] private Vector3 runDirection = Vector3.right;

    [Header("Fin")]
    [Tooltip("Image UI noire plein ecran, sous le panneau de fin (alpha remis a 0 au lancement).")]
    [SerializeField] private Image fadeImage;
    [Tooltip("Duree du passage au noir (s).")]
    [Min(0f)] [SerializeField] private float fadeDuration = 4f;
    [Tooltip("Panneau de fin (EndChoicesPanel), affiche une fois le noir atteint.")]
    [SerializeField] private DemoEndPanel endPanel;

    private PlayerPhysicsMovement player;
    private bool playing;
    private bool reachedWaypoint;
    private bool ending;

    private void Start()
    {
        SetFadeAlpha(0f);
        if (fadeImage != null) fadeImage.raycastTarget = false;
    }

    private void OnDestroy()
    {
        if (player != null) player.ClearForcedMove();
    }

    [ContextMenu("Play")]
    public void Play()
    {
        if (playing) return;

        player = FindObjectOfType<PlayerPhysicsMovement>();
        if (player == null)
        {
            Debug.LogWarning("[BOSS END RUN] Joueur introuvable", this);
            return;
        }

        playing = true;
        reachedWaypoint = waypoint == null;
        if (PlayerInputManager.Instance != null) PlayerInputManager.Instance.IsLocked = true;
        Debug.Log("[BOSS END RUN] Le joueur perd la main");
    }

    [ContextMenu("End")]
    public void End()
    {
        if (ending) return;
        ending = true;
        StartCoroutine(EndRoutine());
    }

    private IEnumerator EndRoutine()
    {
        Debug.Log("[BOSS END RUN] Fondu au noir");
        LevelManager lm = FindObjectOfType<LevelManager>();
        if (lm != null) lm.BlockPause();

        if (player == null) player = FindObjectOfType<PlayerPhysicsMovement>();
        AudioSource steps = null;
        if (player != null)
        {
            FootstepController fc = player.GetComponentInChildren<FootstepController>();
            if (fc != null) steps = fc.GetComponent<AudioSource>();
        }
        float stepsVolume = steps != null ? steps.volume : 0f;

        // 1. Noir et pas qui s'eteignent ensemble ; le perso continue de courir dessous
        float t0 = Time.unscaledTime;
        while (Time.unscaledTime - t0 < fadeDuration)
        {
            float p = (Time.unscaledTime - t0) / fadeDuration;
            SetFadeAlpha(p);
            if (steps != null) steps.volume = stepsVolume * (1f - p);
            yield return null;
        }
        SetFadeAlpha(1f);
        if (steps != null) steps.volume = 0f;

        // 2. Perso fige (plus de course, ni chute ni depart) ; le stick sert au menu
        playing = false;
        if (player != null)
        {
            player.ClearForcedMove();
            player.isFrozen = true;
            player.ResetAllInputs();
            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
        }

        // 3. Panneau de fin
        Debug.Log("[BOSS END RUN] Panneau de fin");
        if (endPanel != null) endPanel.Show();
        else Debug.LogWarning("[BOSS END RUN] Pas de panneau de fin assigne", this);
    }

    private void Update()
    {
        if (!playing || player == null) return;

        if (!reachedWaypoint)
        {
            Vector3 toTarget = waypoint.position - player.transform.position;
            toTarget.y = 0f;
            if (toTarget.magnitude > arriveRadius)
            {
                player.SetForcedMove(toTarget);
                return;
            }
            reachedWaypoint = true;
            Debug.Log("[BOSS END RUN] Point repere atteint : course en ligne droite");
        }

        player.SetForcedMove(runDirection);
    }

    private void SetFadeAlpha(float a)
    {
        if (fadeImage == null) return;
        Color c = fadeImage.color;
        c.a = a;
        fadeImage.color = c;
    }
}

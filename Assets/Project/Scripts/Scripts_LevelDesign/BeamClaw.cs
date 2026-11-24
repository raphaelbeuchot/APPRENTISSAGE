using System.Collections;
using UnityEngine;

/// <summary>
/// Poutre piege de Death Row I : une pince la recupere et la remet a sa place (appele par un InteractSwitch).
/// Feinte : tout ce qui est complique se passe hors champ (Height au-dessus).
///   1. La pince (objet de la scene, desactive) apparait Height au-dessus du point d accroche d enlevement et descend dessus.
///   2. Accroche : joint physique (rotule) entre le point d'accroche et la pince ; la poutre reste physique,
///      pend et se balance comme un tronc souleve par le milieu. Montee de Height.
///   3. Hors champ : la poutre passe horizontale (pose de depart) Height au-dessus de sa place, non physique,
///      et descend avec la pince jusqu'a sa place exacte.
///   4. Pose (ResetToStartPose.ResetPose), physique rendue ; la pince remonte et disparait ; pupitre deverrouille.
/// Setup : Lift Hook Point / Place Hook Point = vides enfants de la poutre (enlevement / reinstallation) ; Pince = pivot au bout des machoires, sans collider.
/// Poutre : ResetToStartPose dessus. Pupitre : InteractSwitch, Lock After Press coche, On Pressed -> StartSequence.
/// </summary>
public class BeamClaw : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody beam;
    [SerializeField] private ResetToStartPose beamReset;
    [Tooltip("Vide enfant de la poutre : la ou la pince la saisit pour l'enlever (montee, la poutre pend autour de ce point)")]
    [SerializeField] private Transform liftHookPoint;
    [Tooltip("Vide enfant de la poutre : la ou la pince la tient pour la reinstaller (descente, poutre horizontale)")]
    [SerializeField] private Transform placeHookPoint;
    [Tooltip("Pince : objet de la scene, desactive au depart. Pivot = bout des machoires.")]
    [SerializeField] private GameObject claw;
    [Tooltip("Pupitre a deverrouiller a la fin (vide = rien)")]
    [SerializeField] private InteractSwitch interactSwitch;

    [Header("Mouvements")]
    [Tooltip("Hauteur fixe d'apparition / de disparition (m) : utilisee si Auto Height est decoche, ou en secours (pas de camera, Max Auto Height atteint)")]
    [SerializeField] private float height = 8f;
    [SerializeField] private float clawDescendDuration = 2f;
    [SerializeField] private float liftDuration = 3f;
    [Tooltip("Pause hors champ avant la redescente (s)")]
    [SerializeField] private float hiddenPause = 0.5f;
    [SerializeField] private float dropDuration = 3f;
    [SerializeField] private float clawLeaveDuration = 1.5f;

    [Header("Hauteur auto (juste hors ecran)")]
    [Tooltip("Coche : a chaque phase, la hauteur est calculee avec la camera, juste assez pour sortir de l ecran (+ Margin). Decoche : Height fixe.")]
    [SerializeField] private bool autoHeight = true;
    [Tooltip("Marge ajoutee une fois hors ecran (m)")]
    [SerializeField] private float margin = 1f;
    [Tooltip("Securite : hauteur maximum cherchee (camera presque verticale : un point qui monte peut ne jamais sortir)")]
    [SerializeField] private float maxAutoHeight = 30f;

    [Header("Sons (joues une fois au depart du mouvement ; vide = rien)")]
    [Tooltip("Pince qui descend vers la poutre, puis poutre qui redescend a sa place")]
    [SerializeField] private AudioClip descendSound;
    [Tooltip("Poutre qui monte avec la pince, puis pince qui repart")]
    [SerializeField] private AudioClip ascendSound;
    [SerializeField] [Range(0f, 1f)] private float soundVolume = 1f;

    [Header("Balancement pendant la montee")]
    [Tooltip("Amortissement de rotation de la poutre accrochee (plus haut = balance moins)")]
    [SerializeField] private float hangingAngularDamping = 1f;

    private bool busy;
    private AudioSource audioSource;
    private Bounds beamStartBounds; // boite du visuel de la poutre a sa place de depart (capturee au lancement)
    private Renderer beamRenderer;
    private float clawYawOffset; // rotation Y de la pince par rapport a la longueur de la poutre (telles que posees dans la scene)

    private void Start()
    {
        if (beam != null) beamRenderer = beam.GetComponentInChildren<Renderer>();
        if (beamRenderer != null) beamStartBounds = beamRenderer.bounds;
        // La pince est posee dans la scene alignee sur la poutre a sa place : on retient cet ecart
        if (beam != null && claw != null) clawYawOffset = Mathf.DeltaAngle(BeamYaw(), claw.transform.eulerAngles.y);
    }

    [ContextMenu("Start Sequence")]
    public void StartSequence()
    {
        if (busy) return;
        if (beam == null || beamReset == null || liftHookPoint == null || placeHookPoint == null || claw == null)
        {
            Debug.LogWarning("[BeamClaw] Reference manquante", this);
            if (interactSwitch != null) interactSwitch.Unlock();
            return;
        }
        StartCoroutine(Sequence());
    }

    private IEnumerator Sequence()
    {
        busy = true;
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
        Transform clawTr = claw.transform;
        Rigidbody clawRb = claw.GetComponent<Rigidbody>();
        if (clawRb == null) clawRb = claw.AddComponent<Rigidbody>();
        clawRb.isKinematic = true;
        clawRb.useGravity = false;
        // Pas d'interpolation : la pince est aussi deplacee par son Transform (descentes)
        clawRb.interpolation = RigidbodyInterpolation.None;
        // Aucune collision de la pince (elle pousserait la poutre en repartant)
        foreach (Collider c in claw.GetComponentsInChildren<Collider>(true)) c.enabled = false;

        // 1. Apparition au-dessus du point d'accroche, descente
        Vector3 from = liftHookPoint.position + Vector3.up * HeightForPoint(liftHookPoint.position);
        clawTr.position = from;
        AlignClawOnBeam(clawTr);
        claw.SetActive(true);
        Play(descendSound);
        for (float t = 0f; t < clawDescendDuration; t += Time.deltaTime)
        {
            // Vise le point d'accroche tel qu'il est maintenant (la poutre peut encore bouger)
            clawTr.position = Vector3.Lerp(from, liftHookPoint.position, Mathf.SmoothStep(0f, 1f, t / clawDescendDuration));
            yield return null;
        }
        clawTr.position = liftHookPoint.position;
        clawRb.position = clawTr.position;

        // 2. Accroche : rotule au point d'accroche, la poutre reste physique
        beam.isKinematic = false;
        beam.WakeUp();
        float savedAngularDamping = beam.angularDamping;
        beam.angularDamping = hangingAngularDamping;
        ConfigurableJoint joint = beam.gameObject.AddComponent<ConfigurableJoint>();
        joint.connectedBody = clawRb;
        joint.autoConfigureConnectedAnchor = false;
        joint.anchor = beam.transform.InverseTransformPoint(liftHookPoint.position);
        joint.connectedAnchor = Vector3.zero;
        joint.xMotion = ConfigurableJointMotion.Locked;
        joint.yMotion = ConfigurableJointMotion.Locked;
        joint.zMotion = ConfigurableJointMotion.Locked;
        // Axe X du joint = longueur de la poutre : pas de rotation sur elle-meme (elle ne fait que basculer)
        joint.axis = LongAxisLocal();
        joint.secondaryAxis = joint.axis == Vector3.up ? Vector3.right : Vector3.up;
        joint.angularXMotion = ConfigurableJointMotion.Locked;
        joint.angularYMotion = ConfigurableJointMotion.Free;
        joint.angularZMotion = ConfigurableJointMotion.Free;

        Vector3 liftFrom = clawRb.position;
        Vector3 liftTo = liftFrom + Vector3.up * (beamRenderer != null ? HeightForBounds(beamRenderer.bounds) : height);
        Play(ascendSound);
        for (float t = 0f; t < liftDuration; t += Time.fixedDeltaTime)
        {
            clawRb.MovePosition(Vector3.Lerp(liftFrom, liftTo, Mathf.SmoothStep(0f, 1f, t / liftDuration)));
            yield return new WaitForFixedUpdate();
        }
        clawRb.MovePosition(liftTo);
        yield return new WaitForFixedUpdate();

        // 3. Hors champ : decroche, poutre horizontale au-dessus de sa place, non physique
        Destroy(joint);
        beam.angularDamping = savedAngularDamping;
        beam.linearVelocity = Vector3.zero;
        beam.angularVelocity = Vector3.zero;
        beam.isKinematic = true;
        Vector3 dropTo = beamReset.StartPosition;
        Vector3 dropFrom = dropTo + Vector3.up * (beamRenderer != null ? HeightForBounds(beamStartBounds) : height);
        beam.transform.SetPositionAndRotation(dropFrom, beamReset.StartRotation);
        AlignClawOnBeam(clawTr);
        clawRb.rotation = clawTr.rotation;
        clawTr.position = placeHookPoint.position;
        clawRb.position = clawTr.position;

        if (hiddenPause > 0f) yield return new WaitForSeconds(hiddenPause);

        Play(descendSound);
        for (float t = 0f; t < dropDuration; t += Time.deltaTime)
        {
            beam.transform.position = Vector3.Lerp(dropFrom, dropTo, Mathf.SmoothStep(0f, 1f, t / dropDuration));
            clawTr.position = placeHookPoint.position;
            yield return null;
        }

        // 4. Pose exacte, la poutre reste non physique tant que la pince n est pas partie
        beam.transform.SetPositionAndRotation(beamReset.StartPosition, beamReset.StartRotation);
        clawTr.position = placeHookPoint.position;

        Vector3 leaveFrom = clawTr.position;
        Vector3 leaveTo = leaveFrom + Vector3.up * HeightForPoint(leaveFrom);
        Play(ascendSound);
        for (float t = 0f; t < clawLeaveDuration; t += Time.deltaTime)
        {
            clawTr.position = Vector3.Lerp(leaveFrom, leaveTo, Mathf.SmoothStep(0f, 1f, t / clawLeaveDuration));
            yield return null;
        }
        claw.SetActive(false);

        // Pince partie : physique rendue, poutre immobile a sa place
        beam.isKinematic = false;
        beamReset.ResetPose();

        if (interactSwitch != null) interactSwitch.Unlock();
        busy = false;
        Debug.Log("[BeamClaw] Poutre remise en place");
    }

    // ============================================
    // HAUTEUR AUTO : juste hors ecran
    // ============================================

    // Axe local le plus long de la poutre (d apres son mesh)
    private Vector3 LongAxisLocal()
    {
        MeshFilter mf = beam.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return Vector3.right;
        Vector3 s = Vector3.Scale(mf.sharedMesh.bounds.size, mf.transform.lossyScale);
        if (s.x >= s.y && s.x >= s.z) return Vector3.right;
        if (s.y >= s.z) return Vector3.up;
        return Vector3.forward;
    }

    // Rotation Y de la longueur de la poutre (monde)
    private float BeamYaw()
    {
        Vector3 d = beam.transform.TransformDirection(LongAxisLocal());
        d.y = 0f;
        if (d.sqrMagnitude < 0.0001f) return 0f;
        return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
    }

    // Pince tournee comme la poutre en ce moment (garde son inclinaison X / Z de la scene)
    private void AlignClawOnBeam(Transform clawTr)
    {
        Vector3 e = clawTr.eulerAngles;
        clawTr.rotation = Quaternion.Euler(e.x, BeamYaw() + clawYawOffset, e.z);
    }

    private void Play(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip, soundVolume);
    }

    private const float AutoStep = 0.25f;

    // Hauteur pour qu un point (pivot de la pince) sorte de l ecran
    private float HeightForPoint(Vector3 p)
    {
        Camera cam = Camera.main;
        if (!autoHeight || cam == null) return height;
        for (float h = 0f; h <= maxAutoHeight; h += AutoStep)
            if (Side(cam, p + Vector3.up * h) != 0) return h + margin;
        return height;
    }

    // Hauteur pour qu une boite entiere (visuel de la poutre) sorte de l ecran : tous ses coins du meme cote
    private float HeightForBounds(Bounds b)
    {
        Camera cam = Camera.main;
        if (!autoHeight || cam == null) return height;
        for (float h = 0f; h <= maxAutoHeight; h += AutoStep)
        {
            Vector3 o = Vector3.up * h;
            int side = 0;
            bool allOut = true;
            for (int i = 0; i < 8 && allOut; i++)
            {
                Vector3 c = b.center + o + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                int s = Side(cam, c);
                if (s == 0 || (side != 0 && s != side)) allOut = false;
                side = s;
            }
            if (allOut) return h + margin;
        }
        return height;
    }

    // 0 = visible a l ecran ; sinon le cote par lequel il est sorti (1 haut, 2 bas, 3 gauche, 4 droite, 5 derriere la camera)
    private static int Side(Camera cam, Vector3 p)
    {
        Vector3 v = cam.WorldToViewportPoint(p);
        if (v.z <= 0f) return 5;
        if (v.y > 1f) return 1;
        if (v.y < 0f) return 2;
        if (v.x < 0f) return 3;
        if (v.x > 1f) return 4;
        return 0;
    }
}

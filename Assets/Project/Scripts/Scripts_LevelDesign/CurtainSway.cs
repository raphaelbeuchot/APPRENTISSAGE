using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Penche les rayures du rideau quand il glisse sur le cote (arnaque visuelle, aucun vertex ne bouge).
/// A poser sur chaque panneau (le GameObject qui porte le Renderer avec CurtainShader).
///
/// Mesure la vitesse du panneau lui-meme : marche quel que soit ce qui le deplace
/// (SlidingCurtain, Timeline, Animator...). Seul le mouvement le long de son axe X local compte :
/// un rideau qui monte (Lift) ne penche pas.
///
/// L'angle (_Sway du shader, en radians) suit la vitesse avec un ressort amorti : il penche pendant
/// le mouvement, puis revient a zero en oscillant un peu a l'arret.
/// Passe par un MaterialPropertyBlock : chaque panneau a son propre angle avec le meme materiau.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class CurtainSway : MonoBehaviour
{
    // Unites : les angles sont en radians (0,01 rad = environ 0,6 degre ; 0,05 rad = environ 3 degres).
    // Avec Size ~65 sur le materiau, 0,01 rad decale le bas du rideau d'environ une rayure.

    [Header("Inclinaison (pendant le mouvement)")]
    [Tooltip("Force de l'inclinaison : angle (radians) par m/s de vitesse laterale du panneau.\n" +
             "Plus haut = les rayures penchent plus quand le rideau glisse. 0 = pas d'inclinaison (flottement seul).\n" +
             "Repere : un rideau qui fait 3 m en 1 s atteint ~4,5 m/s au milieu de sa course.")]
    [FormerlySerializedAs("swayPerSpeed")]
    [SerializeField] private float inclinaisonParVitesse = 0.008f;

    [Tooltip("Plafond de l'angle total (inclinaison + flottement), en radians. A toujours le dernier mot :\n" +
             "a 0, rien ne bouge. Le garder un peu au-dessus de ce que donnent les deux autres reglages.\n" +
             "Avec Size ~65, rester sous 0,1.")]
    [FormerlySerializedAs("maxSway")]
    [Min(0f)] [SerializeField] private float angleMaximum = 0.06f;

    [Header("Flottement (pendant le mouvement)")]
    [Tooltip("Amplitude du flottement aleatoire, en radians, ajoute a l'inclinaison.\n" +
             "Toujours du meme cote que l'inclinaison : entre la verticale et ce cote, jamais au-dela. 0 = aucun.")]
    [FormerlySerializedAs("wobbleAmount")]
    [Min(0f)] [SerializeField] private float amplitudeFlottement = 0.02f;

    [Tooltip("Rapidite du flottement : environ le nombre de changements par seconde.\n" +
             "Bas = ondulation lente, haut = tremblement nerveux.")]
    [FormerlySerializedAs("wobbleFrequency")]
    [Min(0f)] [SerializeField] private float rapiditeFlottement = 3f;

    [Tooltip("Vitesse laterale (m/s) a partir de laquelle le flottement est a fond.\n" +
             "En dessous, il grandit avec la vitesse ; a l'arret, il n'y en a pas.")]
    [FormerlySerializedAs("wobbleFullSpeed")]
    [Min(0.01f)] [SerializeField] private float vitesseFlottementPlein = 2f;

    [Header("Retour en ressort (suivi et arret)")]
    [Tooltip("Raideur du ressort qui tient les rayures.\n" +
             "Haut = suivent vite et reviennent vite (oscillations rapides). Bas = mou, traine davantage.")]
    [FormerlySerializedAs("stiffness")]
    [Min(0f)] [SerializeField] private float raideurRessort = 60f;

    [Tooltip("Frein du ressort : combien d'oscillations a l'arret.\n" +
             "Avec une raideur de 60 : 3 = plusieurs allers-retours, 6 = un ou deux, 15 = retour net sans rebond.")]
    [FormerlySerializedAs("damping")]
    [Min(0f)] [SerializeField] private float amortissement = 6f;

    [Header("Sens")]
    [Tooltip("A cocher si les rayures penchent du mauvais cote (depend de la face vue et de l'orientation du panneau).\n" +
             "Retourne l'inclinaison et le flottement ensemble.")]
    [FormerlySerializedAs("invert")]
    [SerializeField] private bool inverserLeSens = false;

    private static readonly int SwayId = Shader.PropertyToID("_Sway");

    private Renderer rend;
    private MaterialPropertyBlock block;
    private Vector3 lastPosition;
    private float sway;
    private float swayVelocity;
    private float lastApplied = float.NaN;
    private float noiseSeed;
    private float noiseTime;

    private void Awake()
    {
        rend = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
        noiseSeed = Random.Range(0f, 1000f); // chaque panneau flotte differemment
    }

    private void OnEnable()
    {
        lastPosition = transform.position;
        sway = 0f;
        swayVelocity = 0f;
        Apply(0f);
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector3 velocity = (transform.position - lastPosition) / dt;
        lastPosition = transform.position;

        float lateral = Vector3.Dot(velocity, transform.right);
        // Cote vers lequel les rayures penchent (oppose au mouvement, ou l'inverse si Invert)
        float side = -Mathf.Sign(lateral) * (inverserLeSens ? -1f : 1f);
        float target = Mathf.Abs(lateral) * inclinaisonParVitesse * side;

        // Flottement aleatoire (bruit de Perlin), proportionnel a la vitesse : rien a l'arret.
        // D'un seul cote : entre la verticale et le cote de l'inclinaison, jamais au-dela de la verticale.
        float speedFactor = Mathf.Clamp01(Mathf.Abs(lateral) / vitesseFlottementPlein);
        if (amplitudeFlottement > 0f && speedFactor > 0f)
        {
            noiseTime += dt * rapiditeFlottement;
            float noise01 = Mathf.PerlinNoise(noiseSeed, noiseTime);
            target += noise01 * amplitudeFlottement * speedFactor * side;
        }
        target = Mathf.Clamp(target, -angleMaximum, angleMaximum);

        // Ressort amorti (Euler semi-implicite)
        swayVelocity += (raideurRessort * (target - sway) - amortissement * swayVelocity) * dt;
        sway += swayVelocity * dt;

        if (Mathf.Abs(sway) < 0.00005f && Mathf.Abs(swayVelocity) < 0.0005f && target == 0f)
        {
            sway = 0f;
            swayVelocity = 0f;
        }

        Apply(sway);
    }

    private void Apply(float value)
    {
        if (value == lastApplied) return;
        lastApplied = value;
        rend.GetPropertyBlock(block);
        block.SetFloat(SwayId, value);
        rend.SetPropertyBlock(block);
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

/// <summary>
/// Boss de Rush Hour IV : fait exploser un cercle d'ampoules de la sentinelle a chaque phase.
/// Explode, sur le cercle le plus exterieur restant :
/// 1. montee en charge : l'emission des ampoules grimpe de "Charge Intensity" crans (comme l'Intensity du selecteur
///    de couleur HDR : +1 = x2) pendant "Charge Duration", ampoules encore en place ;
/// 2. explosion : chaque ampoule (la vraie, meme aspect) est projetee vers le haut et vers l'exterieur avec de
///    l'aleatoire, retombe avec la gravite jusqu'au sol ; son emission redescend jusqu'a zero pendant "Lifetime",
///    puis elle retrecit et disparait. Etincelles : des explosions (prefab de particules) a des points au hasard du
///    haut du cercle, tournees face a la camera, separees des ampoules ; leur nombre monte a chaque phase.
/// Pas de Rigidbody ni de collider : aucune interaction avec le joueur ni les zombies.
/// Retrouve les cercles crees par le ConcentricCirclesSpawner (CirclesRuntimeContainer > Circle_N) sans y toucher.
/// Branchement : BossTransition.onSentinelShotLive -> Explode. Flow : BOSS_RUSH_HOUR_IV.md.
/// </summary>
public class BossCircleExploder : MonoBehaviour
{
    [Tooltip("Spawner des cercles de la sentinelle (ConcentricCircles Manager).")]
    [SerializeField] private ConcentricCirclesSpawner spawner;

    [Header("Montee en charge")]
    [Tooltip("Duree de la montee de luminosite avant l'explosion (s).")]
    [Min(0f)] [SerializeField] private float chargeDuration = 1.5f;
    [Tooltip("Crans d'emission gagnes pendant la charge, comme l'Intensity du selecteur HDR (+1 = x2, +4 = x16).")]
    [SerializeField] private float chargeIntensity = 4f;
    [Tooltip("Part de blanc dans la couleur d'emission en fin de charge (0 = couleur d'origine, 1 = blanc pur). " +
             "Monte avec la luminosite, puis reste pendant que l'emission redescend.")]
    [Range(0f, 1f)] [SerializeField] private float chargeWhiteness = 1f;

    [Header("Projection")]
    [Tooltip("Vitesse vers le haut (m/s), tiree entre min et max.")]
    [SerializeField] private Vector2 upSpeed = new Vector2(4f, 7f);
    [Tooltip("Vitesse vers l'exterieur du cercle (m/s), tiree entre min et max.")]
    [SerializeField] private Vector2 outwardSpeed = new Vector2(1f, 3f);
    [Tooltip("Vitesse horizontale au hasard en plus (m/s), dans n'importe quelle direction.")]
    [Min(0f)] [SerializeField] private float randomSpeed = 1f;
    [Tooltip("Gravite (m/s2).")]
    [SerializeField] private float gravity = 15f;

    [Header("Collisions")]
    [Tooltip("Layers sur lesquels les ampoules rebondissent (Ground et Obstacle par defaut). Ne pas mettre les zombies " +
             "ni le joueur. Rien touche (fosse) : elles continuent de tomber jusqu'a disparaitre.")]
    [FormerlySerializedAs("groundLayers")]
    [SerializeField] private LayerMask collisionLayers;
    [Tooltip("Part de la vitesse gardee dans la direction du choc (0 = s'ecrase, 1 = rebond parfait).")]
    [Range(0f, 1f)] [SerializeField] private float bounce = 0.4f;
    [Tooltip("Part de la vitesse perdue le long de la surface a chaque choc (0 = glisse, 1 = s'arrete).")]
    [Range(0f, 1f)] [SerializeField] private float friction = 0.3f;
    [Tooltip("Sous cette vitesse (m/s), une ampoule qui touche un sol s'y pose pour de bon.")]
    [Min(0f)] [SerializeField] private float settleSpeed = 0.8f;

    [Header("Disparition")]
    [Tooltip("Temps avant de commencer a retrecir, depuis l'explosion (s). L'emission redescend jusqu'a zero pendant ce temps.")]
    [Min(0f)] [SerializeField] private float lifetime = 2.5f;
    [Tooltip("Duree du retrecissement (s), puis l'ampoule est detruite.")]
    [Min(0.01f)] [SerializeField] private float shrinkDuration = 0.5f;

    [Header("Sons")]
    [Tooltip("Au debut de la montee en charge. Vide = pas de son.")]
    [SerializeField] private AudioClip chargeSound;
    [Tooltip("Au moment ou les ampoules s'envolent. Vide = pas de son.")]
    [SerializeField] private AudioClip explosionSound;

    [Header("Etincelles")]
    [Tooltip("Prefab de particules d'une explosion (le nombre de jets se regle dans le prefab). Pose sur le cercle, " +
             "tourne comme la camera (sa propre rotation en plus) : le regler vu de face, +Y = haut de l'ecran. Vide = pas d'etincelles.")]
    [SerializeField] private GameObject sparksPrefab;
    [Tooltip("Nombre d'explosions sur le premier cercle, tire entre min et max (inclus).")]
    [SerializeField] private Vector2Int sparksCount = new Vector2Int(1, 3);
    [Tooltip("Ajoute a min et max a chaque phase (cercle suivant).")]
    [Min(0)] [SerializeField] private int sparksCountPerPhase = 2;
    [Tooltip("Plafond du nombre d'explosions par cercle.")]
    [Min(0)] [SerializeField] private int sparksCountMax = 10;
    [Tooltip("Zone d'apparition : angle de part et d'autre du sommet du cercle (degres). 180 = tout le cercle.")]
    [Range(0f, 180f)] [SerializeField] private float sparksArcAngle = 60f;

    [Header("Fin du boss")]
    [Tooltip("Envol des ampoules du DERNIER cercle (plus aucun autre cercle). Ex : BossEndSequence.Play.")]
    [SerializeField] private UnityEvent onLastCircleExploded;

    // Nombre de cercles deja exploses : fait monter le nombre d'explosions par phase
    private int explodedCount;

    private class Bulb
    {
        public Transform transform;
        public Renderer renderer;
        public Color baseEmission;   // emission du materiau en place au debut de la charge
        public Vector3 velocity;
        public Vector3 baseScale;
        public float radius;         // l'ampoule se pose a cette hauteur au-dessus du sol (sinon a moitie dedans)
        public bool landed;
    }

    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
    private MaterialPropertyBlock block;

    // Au-dessus de cette pente (normale.y), la surface est un sol (on peut s'y poser) ; sinon un mur (on rebondit seulement)
    private const float MinGroundNormalY = 0.5f;

    // Valeur par defaut a l'ajout du composant (LayerMask.GetMask n'est pas permis dans un initialiseur de champ)
    private void Reset()
    {
        collisionLayers = LayerMask.GetMask("Ground", "Obstacle");
    }

    // Cercle en train de charger : encore en place, il ne doit pas etre repris par un second Explode
    private Transform chargingCircle;

    /// <summary>Fait exploser le cercle le plus exterieur restant (rien s'il n'en reste aucun).</summary>
    public void Explode()
    {
        Transform circle = FindOutermostCircle(out Vector3 center);
        if (circle == null)
        {
            Debug.LogWarning("[BossCircleExploder] Plus de cercle a faire exploser.", this);
            return;
        }
        if (block == null) block = new MaterialPropertyBlock();
        StartCoroutine(ExplodeSequence(circle, center));
    }

    /// <summary>
    /// DEV (depart a une phase plus loin) : retire tout de suite les "count" cercles les plus exterieurs, sans
    /// explosion. Compte comme des phases passees pour le nombre d'explosions d'etincelles.
    /// </summary>
    public void RemoveOutermostCircles(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Transform circle = FindOutermostCircle(out _);
            if (circle == null) break;
            // Detache tout de suite : Destroy n'agit qu'en fin de frame, le cercle serait retrouve au tour suivant
            circle.SetParent(null);
            Destroy(circle.gameObject);
            explodedCount++;
        }
        Debug.Log($"[BossCircleExploder] DEV : {count} cercle(s) retire(s) sans explosion", this);
    }

    private IEnumerator ExplodeSequence(Transform circle, Vector3 center)
    {
        chargingCircle = circle;
        var bulbs = new List<Bulb>();

        foreach (Transform t in circle)
        {
            // Coupe la reaction au cycle : elle remettrait materiau, emission et echelle a sa facon.
            // Son OnDisable efface le property block : on repart de l'emission du materiau en place.
            var reactive = t.GetComponent<CycleReactiveRenderer>();
            if (reactive != null) reactive.enabled = false;

            Renderer r = t.GetComponent<Renderer>();
            if (r == null) r = t.GetComponentInChildren<Renderer>();
            Color emission = Color.black;
            if (r != null && r.sharedMaterial != null && r.sharedMaterial.HasProperty(EmissionColorID))
                emission = r.sharedMaterial.GetColor(EmissionColorID);

            bulbs.Add(new Bulb { transform = t, renderer = r, baseEmission = emission });
        }

        // 1. Montee en charge, ampoules en place
        PlaySound(chargeSound);
        float chargedFactor = Mathf.Pow(2f, chargeIntensity);
        float start = Time.time;
        while (Time.time - start < chargeDuration)
        {
            float p = (Time.time - start) / chargeDuration;
            SetEmission(bulbs, Mathf.Lerp(1f, chargedFactor, p), chargeWhiteness * p);
            yield return null;
        }
        SetEmission(bulbs, chargedFactor, chargeWhiteness);

        // 2. Explosion
        PlaySound(explosionSound);
        SpawnSparks(circle, bulbs);
        explodedCount++;
        // Dernier cercle : aucun autre ne reste (celui-ci, en charge, est exclu de la recherche)
        bool isLast = FindOutermostCircle(out _) == null;

        foreach (var b in bulbs)
        {
            if (b.transform == null) continue;

            // Rayon de l'ampoule
            if (b.renderer != null)
            {
                Vector3 e = b.renderer.bounds.extents;
                b.radius = Mathf.Max(e.x, e.y, e.z); // le sprite est tourne vers la camera : on prend la plus grande
            }

            b.transform.SetParent(null, true); // la sentinelle peut se remettre a tourner : les ampoules n'y sont plus
            b.baseScale = b.transform.localScale;

            Vector3 outward = b.transform.position - center;
            outward.y = 0f;
            outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.zero;
            Vector2 jitter = Random.insideUnitCircle * randomSpeed;

            b.velocity = Vector3.up * Random.Range(upSpeed.x, upSpeed.y)
                       + outward * Random.Range(outwardSpeed.x, outwardSpeed.y)
                       + new Vector3(jitter.x, 0f, jitter.y);
        }

        chargingCircle = null;
        Destroy(circle.gameObject);
        if (isLast)
        {
            Debug.Log("[BossCircleExploder] Dernier cercle explose", this);
            onLastCircleExploded?.Invoke();
        }

        // Vol : emission qui redescend jusqu'a zero pendant lifetime, puis retrecissement
        start = Time.time;
        while (true)
        {
            float elapsed = Time.time - start;
            float fade = lifetime > 0f ? Mathf.Clamp01(elapsed / lifetime) : 1f;
            float shrink = Mathf.Clamp01((elapsed - lifetime) / shrinkDuration);
            float dt = Time.deltaTime;

            SetEmission(bulbs, chargedFactor * (1f - fade), chargeWhiteness);

            foreach (var b in bulbs)
            {
                if (b.transform == null) continue;

                if (!b.landed)
                {
                    b.velocity.y -= gravity * dt;
                    Vector3 from = b.transform.position;
                    Vector3 step = b.velocity * dt;
                    Vector3 pos = from + step;

                    // Un raycast par ampoule en vol, sur le trajet de la frame, prolonge du rayon de l'ampoule
                    // pour qu'elle touche par son bord (sinon a moitie dans la surface).
                    // Choc : vitesse renvoyee selon la normale (bounce), freinee le long de la surface (friction) ;
                    // sur un sol, sous settleSpeed, elle se pose pour de bon
                    float dist = step.magnitude;
                    if (dist > 0f
                        && Physics.Raycast(from, step / dist, out RaycastHit hit, dist + b.radius, collisionLayers, QueryTriggerInteraction.Ignore))
                    {
                        Vector3 n = hit.normal;
                        pos = hit.point + n * b.radius;

                        Vector3 normalPart = Vector3.Dot(b.velocity, n) * n;
                        Vector3 tangentPart = b.velocity - normalPart;
                        b.velocity = -normalPart * bounce + tangentPart * (1f - friction);

                        if (n.y >= MinGroundNormalY && b.velocity.magnitude < settleSpeed)
                        {
                            b.velocity = Vector3.zero;
                            b.landed = true;
                        }
                    }
                    b.transform.position = pos;
                }

                if (shrink > 0f) b.transform.localScale = b.baseScale * (1f - shrink);
            }

            if (shrink >= 1f) break;
            yield return null;
        }

        foreach (var b in bulbs)
            if (b.transform != null) Destroy(b.transform.gameObject);
    }

    // Explosions d'etincelles a des points au hasard du haut du cercle, avant que les ampoules ne s'envolent.
    // Cercle = plan XY local du Circle_N (vertical face a la camera) ; centre = moyenne des ampoules (reparties
    // regulierement), rayon = distance d'une ampoule au centre. Instances libres (pas d'enfant) : a detruire par
    // le prefab lui-meme (Stop Action = Destroy)
    private void SpawnSparks(Transform circle, List<Bulb> bulbs)
    {
        if (sparksPrefab == null) return;

        Vector3 center = Vector3.zero;
        int n = 0;
        foreach (var b in bulbs)
        {
            if (b.transform == null) continue;
            center += b.transform.position;
            n++;
        }
        if (n == 0) return;
        center /= n;

        Vector3 firstBulb = Vector3.zero;
        foreach (var b in bulbs)
            if (b.transform != null) { firstBulb = b.transform.position; break; }
        float radius = Vector3.Distance(firstBulb, center);

        int extra = sparksCountPerPhase * explodedCount;
        int min = Mathf.Min(sparksCount.x + extra, sparksCountMax);
        int max = Mathf.Min(Mathf.Max(sparksCount.y, sparksCount.x) + extra, sparksCountMax);
        int count = Random.Range(min, max + 1);

        // Rotation camera x rotation du prefab : le prefab se regle vu de face (vue Scene regardant vers +Z),
        // ce qu'on y voit monter vers +Y monte a l'ecran
        Camera cam = Camera.main;
        Quaternion facing = (cam != null ? cam.transform.rotation : Quaternion.identity) * sparksPrefab.transform.rotation;

        // Sommet = le haut du monde ramene dans le plan du cercle (si la sentinelle a tourne, le haut local n'est plus en haut)
        Vector3 top = Vector3.ProjectOnPlane(Vector3.up, circle.forward);
        top = top.sqrMagnitude > 0.0001f ? top.normalized : circle.up;
        Vector3 side = Vector3.Cross(circle.forward, top);

        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(-sparksArcAngle, sparksArcAngle) * Mathf.Deg2Rad;
            Vector3 dir = top * Mathf.Cos(angle) + side * Mathf.Sin(angle);
            Instantiate(sparksPrefab, center + dir * radius, facing);
        }
    }

    private AudioSource audioSource;

    // Son 2D sur l'AudioSource de cet objet (ajoutee si absente)
    private void PlaySound(AudioClip clip)
    {
        if (clip == null) return;
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
        }
        audioSource.PlayOneShot(clip);
    }

    // Emission de chaque ampoule = emission de son materiau, poussee vers le blanc (meme intensite) de "whiteness",
    // x factor. Property block : le materiau partage n'est pas touche
    private void SetEmission(List<Bulb> bulbs, float factor, float whiteness)
    {
        foreach (var b in bulbs)
        {
            if (b.renderer == null) continue;
            Color c = b.baseEmission;
            float intensity = Mathf.Max(c.r, c.g, c.b);
            Color white = new Color(intensity, intensity, intensity);
            b.renderer.GetPropertyBlock(block);
            block.SetColor(EmissionColorID, Color.Lerp(c, white, whiteness) * factor);
            b.renderer.SetPropertyBlock(block);
        }
    }

    // Cercle restant le plus grand (rayon mesure sur les ampoules, pas sur l'ordre ni le nom), hors cercle en charge
    private Transform FindOutermostCircle(out Vector3 center)
    {
        center = Vector3.zero;
        if (spawner == null) return null;

        Transform container = spawner.transform.Find("CirclesRuntimeContainer");
        if (container == null) return null;
        center = container.position;

        var circles = new List<(Transform circle, float radius)>();
        foreach (Transform circle in container)
        {
            if (circle.childCount == 0 || circle == chargingCircle) continue;
            circles.Add((circle, Vector3.Distance(circle.GetChild(0).position, container.position)));
        }

        if (circles.Count == 0) return null;
        circles.Sort((a, b) => b.radius.CompareTo(a.radius));
        return circles[0].circle;
    }
}

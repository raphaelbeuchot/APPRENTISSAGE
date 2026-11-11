using UnityEngine;
using UnityEngine.Splines;

// Obstacle qui roule le long d'une spline. En edition, il est tenu par sa spline : place en direct a son
// point de depart (Starting Progress + offsets), il suit la spline quand on la modifie et y revient si on le
// deplace a la main. Le comportement de jeu (mouvement, sons, poussee) ne tourne qu'en Play.
// Gizmo (case Afficher Gizmo) : l'empreinte au sol de l'obstacle, repetee le long de toute la spline.
[ExecuteAlways]
public class RoamingObstacle : MonoBehaviour, IMovingPlatform
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private bool reverseDirection = false;
    [SerializeField] private bool usePingPong = true;
    [Tooltip("Coche : immobile tant que le cycle de la sentinelle ne tourne pas (avant le pupitre de depart, " +
             "pendant les transitions du boss, apres la victoire), repart tout seul avec lui. " +
             "Decoche : bouge des le lancement.")]
    [SerializeField] private bool startWithCycle = false;

    [Header("Audio")]
    [SerializeField] private RoamingObstacleSettings settings;

    [Header("Proximity Detection")]
    [SerializeField] private float detectionRadius = 5f;
    [SerializeField] private AudioClip[] proximitySounds;
    [SerializeField] private float proximityVolume = 0.7f;
    [SerializeField] private float pitchVariation = 0.1f;

    [Header("Push Settings")]
    [SerializeField] private float speedThreshold = 1f;
    [SerializeField] private float knockdownThreshold = 3f;
    [SerializeField] private float sweepImpulseForce = 5f;
    [SerializeField] private AudioClip softPushSound;
    [SerializeField] private AudioClip pushSound;
    [SerializeField] private AudioClip sweepSound;
    [SerializeField] private float impactVolume = 1f;
    private const float PUSH_FORCE_MULTIPLIER = 0.5f;
    private const float PUSH_DURATION = 0.5f;


    [Header("Spline")]
    [SerializeField] private SplineContainer splineContainer;
    
    [Header("Instance Settings")]
    [Tooltip("Point de depart sur la spline (0 = debut, 1 = fin). L'obstacle s'y place en direct en edition.")]
    [SerializeField]
    [Range(0f, 1f)]
    private float startingProgress = 0f;
    [Tooltip("Decalage par rapport a la spline, dans le sens de la marche : X = sur le cote, Y = en hauteur, " +
             "Z = en avant / en arriere.")]
    [SerializeField] private Vector3 positionOffset = Vector3.zero;
    [Tooltip("Rotation ajoutee apres l'orientation dans le sens de la marche (degres).")]
    [SerializeField] private Vector3 rotationOffset = Vector3.zero;

    [Header("Gizmo")]
    [Tooltip("Empreinte au sol de l'obstacle, repetee le long de toute la spline : la zone qu'il balaie.")]
    [SerializeField] private bool showGizmo = false;
    [Tooltip("Ecart entre deux empreintes (m).")]
    [Min(0.1f)] [SerializeField] private float gizmoStep = 1f;
    [SerializeField] private Color gizmoColor = new Color(1f, 0.4f, 0f, 0.25f);

    // Lecture seule pour SplineCircles (largeur des cercles)
    public SplineContainer SplineContainer => splineContainer;

    private float currentProgress = 0f;
    private AudioSource audioSource;
    private float currentDirection = 1f;
    private Material material;
    private float scrollTime = 0f;

    // Pour calculer la velocite
    private Vector3 lastPosition;
    private Vector3 currentVelocity;

    // Gestion collision
    private float lastCollisionTime = -999f;
    private float collisionCooldown = 0.5f;

    // Detection proximite player
    private Transform playerTransform;
    private AudioSource proximityAudioSource;
    private bool hasPlayedProximitySound = false;

    void Start()
    {
        if (!Application.isPlaying) return;

        if (splineContainer == null)
        {
            Debug.LogError("[RoamingObstacle] SplineContainer non assigne !");
            enabled = false;
            return;
        }

        // Instance du material (propre a cet obstacle)
        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend != null)
            material = rend.material;

        // Initialiser la position de depart
        currentProgress = startingProgress;
        lastPosition = transform.position;

        // Setup audio seulement si settings assigne
        if (settings != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.loop = true;
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = settings.soundVolume;

            if (settings.movementSound != null)
            {
                audioSource.clip = settings.movementSound;
                audioSource.Play();
            }
        }

        // Setup audio proximite
        proximityAudioSource = gameObject.AddComponent<AudioSource>();
        proximityAudioSource.playOnAwake = false;
        proximityAudioSource.spatialBlend = 1f;
        proximityAudioSource.volume = proximityVolume;

        // Trouver le player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        else
        {
            Debug.LogWarning("[RoamingObstacle] Player non trouve !");
        }

        // Position initiale sur la spline
        UpdatePosition();

        // Initialiser lastPosition
        lastPosition = transform.position;

        if (startWithCycle)
        {
            cycleManager = FindFirstObjectByType<SentinelCycleManager>();
            if (cycleManager == null)
                Debug.LogWarning("[RoamingObstacle] Start With Cycle coche mais pas de SentinelCycleManager : l'obstacle ne bougera pas");
        }

        Debug.Log("[RoamingObstacle] Obstacle demarre sur spline");
    }

    // Start With Cycle
    private SentinelCycleManager cycleManager;
    private bool waitingForCycle = false;

    // Coche Start With Cycle et cycle arrete : obstacle immobile, vitesse a zero (il est IMovingPlatform :
    // sinon il transmettrait son dernier elan au joueur pose dessus), son de deplacement en pause
    private bool WaitForCycle()
    {
        bool wait = startWithCycle && (cycleManager == null || !cycleManager.IsGameStarted());
        if (wait != waitingForCycle)
        {
            waitingForCycle = wait;
            if (audioSource != null && audioSource.clip != null)
            {
                if (wait) audioSource.Pause();
                else audioSource.UnPause();
            }
        }
        if (wait)
        {
            currentVelocity = Vector3.zero;
            lastPosition = transform.position;
        }
        return wait;
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        if (splineContainer == null) return;
        if (WaitForCycle()) return;

        float splineLength = splineContainer.Spline.GetLength() * splineContainer.transform.lossyScale.x;
        float distanceThisFrame = moveSpeed * Time.deltaTime;
        float progressIncrement = distanceThisFrame / splineLength;

        // Appliquer direction
        if (reverseDirection)
            progressIncrement = -progressIncrement;

        // Appliquer direction ping-pong
        progressIncrement *= currentDirection;

        // Avancer
        currentProgress += progressIncrement;

        // Temps de defilement du shader : suit le sens reel du mouvement (continu, pas de saut)
        if (material != null)
        {
            float shaderDirection = reverseDirection ? -currentDirection : currentDirection;
            scrollTime += shaderDirection * Time.deltaTime;
            material.SetFloat("_ScrollTime", scrollTime);
        }

        // Gestion boucle ou ping-pong
        if (usePingPong)
        {
            // Aller-retour
            if (currentProgress >= 1f)
            {
                currentProgress = 1f;
                currentDirection = -1f;
            }
            else if (currentProgress <= 0f)
            {
                currentProgress = 0f;
                currentDirection = 1f;
            }
        }
        else
        {
            // Boucle fermee
            if (currentProgress > 1f)
                currentProgress -= 1f;
            else if (currentProgress < 0f)
                currentProgress += 1f;
        }

        UpdatePosition();

        // Calculer la velocite apres avoir bouge
        CalculateVelocity();

        // Detection proximite player
        if (playerTransform != null && proximitySounds != null && proximitySounds.Length > 0)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

            if (distanceToPlayer < detectionRadius && !hasPlayedProximitySound)
            {
                // Player entre dans le rayon : jouer un son aleatoire
                AudioClip randomClip = proximitySounds[Random.Range(0, proximitySounds.Length)];

                // Variation aleatoire de pitch
                proximityAudioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);

                proximityAudioSource.PlayOneShot(randomClip);
                hasPlayedProximitySound = true;
                Debug.Log("[RoamingObstacle] Player detecte - son proximite joue");
            }
            else if (distanceToPlayer >= detectionRadius && hasPlayedProximitySound)
            {
                // Player sort du rayon : reset le flag
                hasPlayedProximitySound = false;
                Debug.Log("[RoamingObstacle] Player sorti du rayon - flag reset");
            }
        }
    }

    private void UpdatePosition()
    {
        PlaceAt(currentProgress);
    }

    // Pose sur la spline : position du point, cap de la marche (tangente a plat), puis offsets.
    // Partage par le jeu, le placement en edition et le gizmo.
    private void GetSplinePose(float progress, out Vector3 splinePoint, out Quaternion heading)
    {
        splinePoint = splineContainer.EvaluatePosition(progress);
        Vector3 tangent = (Vector3)splineContainer.EvaluateTangent(progress);
        tangent.y = 0f;
        // Tangente nulle : on garde le cap actuel (sans l'offset de rotation, qui sera reapplique)
        heading = tangent.sqrMagnitude > 0.000001f
            ? Quaternion.LookRotation(tangent.normalized)
            : transform.rotation * Quaternion.Inverse(Quaternion.Euler(rotationOffset));
    }

    private void PlaceAt(float progress)
    {
        GetSplinePose(progress, out Vector3 position, out Quaternion heading);

        // Rotation d'abord : la hauteur de la base en depend des que l'offset de rotation incline l'obstacle
        transform.rotation = heading * Quaternion.Euler(rotationOffset);

        // Offset pour que la BASE de l'obstacle touche la spline
        Collider col = GetComponent<Collider>();
        float yOffset = 0f;

        if (col != null)
        {
            // En edition, les bounds ne suivent pas le transform tant que la physique n'est pas synchronisee
            if (!Application.isPlaying) Physics.SyncTransforms();
            yOffset = col.bounds.extents.y;
        }

        position += heading * positionOffset;
        position.y += yOffset;
        transform.position = position;
    }

    private void CalculateVelocity()
    {
        currentVelocity = (transform.position - lastPosition) / Time.deltaTime;
        lastPosition = transform.position;
    }

    // IMPLEMENTATION INTERFACE IMovingPlatform
    public Vector3 GetPlatformVelocity()
    {
        return currentVelocity;
    }

    public Transform GetTransform()
    {
        return transform;
    }

    void OnTriggerEnter(Collider other)
    {
        // Verifier si c'est un autre RoamingObstacle OU un objet sur layer "Obstacle"
        RoamingObstacle otherObstacle = other.GetComponent<RoamingObstacle>();
        bool isObstacleLayer = other.gameObject.layer == LayerMask.NameToLayer("Obstacle");

        if (otherObstacle != null || isObstacleLayer)
        {
            // Verifier cooldown pour eviter inversions multiples
            if (Time.time - lastCollisionTime > collisionCooldown)
            {
                // Inverser direction
                currentDirection *= -1f;
                lastCollisionTime = Time.time;

                string collisionType = otherObstacle != null ? "autre RoamingObstacle" : "obstacle";
                Debug.Log($"[RoamingObstacle] Obstacle collision avec {collisionType} detectee - inversion direction");
            }
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("Human"))
        {
            float speed = currentVelocity.magnitude;
            Debug.Log($"[RoamingObstacle] Vitesse mesuree a la collision : {speed}");

            if (speed < speedThreshold)
            {
                if (softPushSound != null)
                    proximityAudioSource.PlayOneShot(softPushSound, impactVolume);
                Debug.Log("[RoamingObstacle] Vitesse sous threshold - physique pure");
                return;
            }

            PlayerPhysicsMovement playerMovement = collision.gameObject.GetComponent<PlayerPhysicsMovement>();

            if (playerMovement != null)
            {
                Vector3 pushDirection = (collision.transform.position - transform.position).normalized;
                pushDirection.y = 0f;
                pushDirection.Normalize();

                float calculatedForce = speed * PUSH_FORCE_MULTIPLIER;

                if (speed >= knockdownThreshold)
                {
                    if (!playerMovement.isSweepImmune)
                    {
                        if (sweepSound != null)
                            proximityAudioSource.PlayOneShot(sweepSound, impactVolume);

                        Vector3 impulse = pushDirection * sweepImpulseForce;
                        impulse.y = 0f;
                        collision.rigidbody.AddForce(impulse, ForceMode.Impulse);

                        playerMovement.ApplyProgressivePush(pushDirection, calculatedForce, PUSH_DURATION);
                        playerMovement.TriggerSweepFromObstacle();
                    }
                }
                else
                {
                    if (pushSound != null)
                        proximityAudioSource.PlayOneShot(pushSound, impactVolume);
                    playerMovement.ApplyProgressivePush(pushDirection, calculatedForce, PUSH_DURATION);
                }
            }
        }
    }

    /*private System.Collections.IEnumerator PushPlayerCoroutine(Rigidbody playerRb, Vector3 direction)
    {
        isPushingPlayer = true;

        float elapsed = 0f;
        float forcePerFrame = pushForce / pushDuration;

        while (elapsed < pushDuration)
        {
            // Appliquer une fraction de la force chaque frame
            playerRb.AddForce(direction * forcePerFrame * Time.deltaTime, ForceMode.VelocityChange);

            elapsed += Time.deltaTime;
            yield return null; // Attendre la prochaine frame
        }

        isPushingPlayer = false;
        Debug.Log($"[RoamingObstacle] Fin poussee progressive du player");
    }*/

    // ============================================
    // EDITION : obstacle tenu par sa spline
    // ============================================

#if UNITY_EDITOR
    private Vector3 lastSnapPosition;
    private Quaternion lastSnapRotation;
    private Matrix4x4 lastSplineMatrix;

    void OnEnable()
    {
        if (Application.isPlaying) return;
        Spline.Changed += OnSplineChanged;
        UnityEditor.EditorApplication.update += EditorWatch;
        SnapInEditor();
    }

    void OnDisable()
    {
        Spline.Changed -= OnSplineChanged;
        UnityEditor.EditorApplication.update -= EditorWatch;
    }

    // Deplacer un transform pendant OnValidate provoque des avertissements : on decale d'une frame d'editeur
    void OnValidate()
    {
        if (Application.isPlaying) return;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null) SnapInEditor();
        };
    }

    private void OnSplineChanged(Spline spline, int knotIndex, SplineModification modification)
    {
        if (splineContainer != null && splineContainer.Spline == spline) SnapInEditor();
    }

    // Spline deplacee en bloc, ou obstacle deplace a la main : on le remet sur la spline
    private void EditorWatch()
    {
        if (this == null || Application.isPlaying || splineContainer == null) return;
        if (transform.position != lastSnapPosition || transform.rotation != lastSnapRotation ||
            splineContainer.transform.localToWorldMatrix != lastSplineMatrix)
            SnapInEditor();
    }

    private void SnapInEditor()
    {
        if (Application.isPlaying || splineContainer == null || splineContainer.Spline == null) return;
        // Prefab ouvert en mode Prefab sans sa spline : rien a faire
        if (splineContainer.Spline.Count < 2) return;

        PlaceAt(startingProgress);
        lastSnapPosition = transform.position;
        lastSnapRotation = transform.rotation;
        lastSplineMatrix = splineContainer.transform.localToWorldMatrix;
    }
#endif

    // Empreinte au sol (meshes de l'obstacle aplatis a la hauteur de la spline), tous les gizmoStep metres
    void OnDrawGizmos()
    {
        if (!showGizmo || splineContainer == null || splineContainer.Spline == null) return;
        if (splineContainer.Spline.Count < 2) return;

        float length = splineContainer.CalculateLength();
        if (length <= 0.001f) return;

        MeshFilter[] meshes = GetComponentsInChildren<MeshFilter>();
        if (meshes.Length == 0) return;

        // Garde-fou : pas plus de 500 empreintes par obstacle
        float step = Mathf.Max(gizmoStep, length / 500f);
        int count = Mathf.FloorToInt(length / step) + 1;
        Matrix4x4 rootToWorldInverse = transform.worldToLocalMatrix;
        Quaternion offsetRotation = Quaternion.Euler(rotationOffset);

        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.color = gizmoColor;

        for (int i = 0; i < count; i++)
        {
            float progress = Mathf.Clamp01(i * step / length);
            GetSplinePose(progress, out Vector3 point, out Quaternion heading);

            // Pose de la racine a cet endroit (hauteur sans importance : tout est aplati ensuite)
            Matrix4x4 rootPose = Matrix4x4.TRS(point + heading * positionOffset, heading * offsetRotation, transform.lossyScale);
            // Aplatissement sur le plan horizontal de la spline, un poil au-dessus pour ne pas clignoter avec le sol
            Matrix4x4 flatten = Matrix4x4.Translate(new Vector3(0f, point.y + 0.02f, 0f)) *
                                Matrix4x4.Scale(new Vector3(1f, 0.001f, 1f)) *
                                Matrix4x4.Translate(new Vector3(0f, -point.y, 0f));
            foreach (MeshFilter mf in meshes)
            {
                if (mf == null || mf.sharedMesh == null) continue;
                // Mesh relatif a la racine (worldToLocal retire deja l'echelle de la racine, remise par rootPose)
                Matrix4x4 relative = rootToWorldInverse * mf.transform.localToWorldMatrix;
                Gizmos.matrix = flatten * rootPose * relative;
                Gizmos.DrawMesh(mf.sharedMesh);
            }
        }

        Gizmos.matrix = previous;
    }

    void OnDestroy()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }
}
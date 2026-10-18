using System;
using UnityEngine;

// Deduit la cause de la mort du joueur au moment ou il meurt (PlayerHealth.OnDying), dans l'ordre :
//   1. cause explicite passee a PlayerHealth (Kill / TakeDamage) ;
//   2. sous la hauteur de KillBelowY -> outOfBoundsCause ;
//   3. dans une fosse spline dont le contenu a un DeathCause (lave, acide) -> celui-la ;
//   4. sinon -> defaultCause (sentinelle, seule autre facon de mourir aujourd'hui).
// Une nouvelle facon de mourir = une regle de plus ici, ou un champ DeathCause sur sa source.
//
// Exposition : premier degat recu dans une fosse dont le contenu a un DeathCause -> son + effet d'exposition.
// Elle se termine quand le joueur, vivant, n'est plus dans ce contenu (sortie de fosse, respawn multi).
// En cas de mort, elle continue sur le corps.
//
// Joue les sons / effets des DeathCause ; OnExposureStarted / OnExposureEnded / OnDeathResolved servent
// aux reactions plus specifiques (shader de glow, etc.).
[RequireComponent(typeof(PlayerHealth))]
public class PlayerDeathCause : MonoBehaviour
{
    [Tooltip("Mort par defaut quand rien d'autre ne s'applique (tir de sentinelle).")]
    [SerializeField] private DeathCause defaultCause;
    [Tooltip("Mort quand le joueur est sous la hauteur de KillBelowY.")]
    [SerializeField] private DeathCause outOfBoundsCause;

    public DeathCause Current { get; private set; }
    public DeathCause Exposure { get; private set; }

    public event Action<DeathCause> OnExposureStarted;
    public event Action<DeathCause> OnExposureEnded;
    public event Action<DeathCause> OnDeathResolved;

    private PlayerHealth health;
    private KillBelowY killBelowY;
    private PlayerPitInteractable pit;
    private AudioSource audioSource;

    private float lastHealth;
    // Point de contact avec la surface du contenu : suit le joueur en XZ, reste a la hauteur de la surface.
    // Porte l'effet d'exposition ; l'effet de mort y apparait aussi. Pas enfant du joueur.
    private Transform exposureAnchor;
    private float surfaceY;

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
        killBelowY = GetComponent<KillBelowY>();
        pit = GetComponent<PlayerPitInteractable>();

        // Sons en 2D, comme les sons de fosse des ennemis. Sur un enfant : les autres scripts du joueur
        // (pas, balai, tomates) prennent la premiere AudioSource du Player et peuvent en changer les reglages.
        GameObject audioHolder = new GameObject("DeathCauseAudio");
        audioHolder.transform.SetParent(transform, false);
        audioSource = audioHolder.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    private void Start()
    {
        lastHealth = health.GetCurrentHealth();
    }

    private void OnEnable()
    {
        health.OnDying += HandleDying;
        health.OnHealthChanged += HandleHealthChanged;
    }

    private void OnDisable()
    {
        health.OnDying -= HandleDying;
        health.OnHealthChanged -= HandleHealthChanged;
    }

    private void Update()
    {
        if (Exposure == null) return;
        if (exposureAnchor != null)
            exposureAnchor.position = SurfacePoint();
        if (health.IsDead()) return;

        PitContentType content = pit != null ? pit.CurrentSplinePitContent : null;
        if (content == null || content.deathCause != Exposure)
            EndExposure();
    }

    // Position du joueur ramenee a la surface du contenu (derniere hauteur connue si l'etat de fosse
    // a ete nettoye entre-temps, ex. a la mort)
    private Vector3 SurfacePoint()
    {
        if (pit != null && pit.TryGetSplineFillSurfaceY(out float y))
            surfaceY = y;
        Vector3 p = transform.position;
        return new Vector3(p.x, surfaceY, p.z);
    }

    // ---------------------------------------------------------------- Exposition

    // Appele avant Die() par PlayerHealth.TakeDamage : un degat fatal des le premier contact passe donc
    // quand meme par l'exposition avant la mort.
    private void HandleHealthChanged(float current, float max)
    {
        bool tookDamage = current < lastHealth;
        lastHealth = current;
        if (!tookDamage || Exposure != null) return;

        PitContentType content = pit != null ? pit.CurrentSplinePitContent : null;
        if (content != null && content.deathCause != null)
            StartExposure(content.deathCause);
    }

    private void StartExposure(DeathCause cause)
    {
        Exposure = cause;
        Debug.Log("[PlayerDeathCause] Exposition : " + cause.name);

        PlaySounds(cause.exposureSounds);

        surfaceY = transform.position.y;
        exposureAnchor = new GameObject("DeathCauseExposure_" + cause.name).transform;
        exposureAnchor.position = SurfacePoint();
        if (cause.exposureEffects != null)
            foreach (GameObject effect in cause.exposureEffects)
                if (effect != null)
                    Instantiate(effect, exposureAnchor.position, Quaternion.identity, exposureAnchor);

        OnExposureStarted?.Invoke(cause);
    }

    private void EndExposure()
    {
        DeathCause ended = Exposure;
        Exposure = null;
        Debug.Log("[PlayerDeathCause] Fin d'exposition : " + ended.name);

        if (exposureAnchor != null)
        {
            // Arret en douceur : plus d'emission, les particules deja la finissent leur vie
            float maxLifetime = 0f;
            foreach (ParticleSystem ps in exposureAnchor.GetComponentsInChildren<ParticleSystem>())
            {
                ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                maxLifetime = Mathf.Max(maxLifetime, ps.main.startLifetime.constantMax);
            }
            Destroy(exposureAnchor.gameObject, maxLifetime + 0.5f);
            exposureAnchor = null;
        }

        OnExposureEnded?.Invoke(ended);
    }

    // ---------------------------------------------------------------- Mort

    private void HandleDying(DeathCause explicitCause)
    {
        Current = Resolve(explicitCause);
        Debug.Log("[PlayerDeathCause] Mort : " + (Current != null ? Current.name : "generique"));

        if (Current != null)
        {
            PlaySounds(Current.deathSounds);
            if (Current.deathEffects != null)
                foreach (GameObject effect in Current.deathEffects)
                    if (effect != null)
                        SpawnOneShotEffect(effect);
        }

        OnDeathResolved?.Invoke(Current);
    }

    private void PlaySounds(AudioClip[] clips)
    {
        if (clips == null) return;
        foreach (AudioClip clip in clips)
            if (clip != null)
                audioSource.PlayOneShot(clip);
    }

    private void SpawnOneShotEffect(GameObject prefab)
    {
        // En exposition : au point de contact avec la surface ; sinon a la position du joueur
        Vector3 position = exposureAnchor != null ? exposureAnchor.position : transform.position;
        GameObject fx = Instantiate(prefab, position, Quaternion.identity);
        float duration = 0f;
        foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>())
            duration = Mathf.Max(duration, ps.main.duration + ps.main.startLifetime.constantMax);
        Destroy(fx, duration > 0f ? duration + 0.5f : 5f);
    }

    private DeathCause Resolve(DeathCause explicitCause)
    {
        if (explicitCause != null)
            return explicitCause;

        if (killBelowY != null && transform.position.y < killBelowY.KillY)
            return outOfBoundsCause;

        // Exposition en cours : la mort vient de la (le joueur a pu etre sorti du contenu entre-temps)
        if (Exposure != null)
            return Exposure;

        PitContentType content = pit != null ? pit.CurrentSplinePitContent : null;
        if (content != null && content.deathCause != null)
            return content.deathCause;

        return defaultCause;
    }
}

using UnityEngine;
using System.Collections.Generic;

// Multi : une pip bar par joueur et par ennemi (pas une seule partagee). Chaque copie est
// assignee a un layer dedie (EnemyPip_P1/EnemyPip_P2, culling mask exclusif par camera de
// joueur, voir MULTIJOUEUR_LOCAL.md) et billboard vers la camera de son propre joueur -- ca
// resout le probleme du Canvas Screen Space Overlay (limite a une seule camera de reference)
// sans avoir besoin d'un shader de billboard multi-camera. En solo il n'y a qu'un seul joueur
// trouve, donc une seule copie : comportement inchange.
public class EnemyHealthBarManager : MonoBehaviour
{
    public static EnemyHealthBarManager Instance;

    [Header("Prefab")]
    public GameObject healthBarPrefab;
    public Canvas canvas;

    public float verticalOffset = 2f;

    [Header("Multi : cameras par joueur")]
    [Tooltip("Camera du joueur cote gauche (PlayerScreenSide.Side.Left). Vide = Camera.main.")]
    [SerializeField] private Camera player1Camera;
    [Tooltip("Camera du joueur cote droit (PlayerScreenSide.Side.Right). Vide = Camera.main (sert seulement si un 2e joueur existe).")]
    [SerializeField] private Camera player2Camera;

    private class PlayerPipTarget
    {
        public Transform transform;
        public Camera camera;
        public PlayerScreenSide.Side side;
        public int pipLayer;
    }

    private List<PlayerPipTarget> playerTargets = new List<PlayerPipTarget>();
    private Dictionary<Transform, List<EnemyHealthBarUI>> healthBars = new Dictionary<Transform, List<EnemyHealthBarUI>>();
    private Camera mainCamera;
    private float displayRadius;
    private bool playersResolved = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        ResolvePlayerTargets();
    }

    // Idempotent, appelable depuis Start() (cas normal) ou en tout premier dans RegisterEnemy() :
    // Unity ne garantit pas que le Start() du manager tourne avant celui d'un ennemi qui
    // l'appelle deja dans son propre Start() (SetupHealthBar) -- sans ce garde-fou, un ennemi
    // enregistre trop tot se retrouverait avec une liste de pip bars vide pour toujours
    // (RegisterEnemy ne retente jamais une fois l'ennemi present dans le dictionnaire).
    private void ResolvePlayerTargets()
    {
        if (playersResolved) return;
        playersResolved = true;

        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindObjectOfType<Camera>();
        }

        int pipLayerP1 = LayerMask.NameToLayer("EnemyPip_P1");
        int pipLayerP2 = LayerMask.NameToLayer("EnemyPip_P2");

        PlayerPhysicsMovement[] players = FindObjectsOfType<PlayerPhysicsMovement>();
        foreach (PlayerPhysicsMovement player in players)
        {
            PlayerScreenSide screenSide = player.GetComponent<PlayerScreenSide>();
            PlayerScreenSide.Side side = screenSide != null ? screenSide.side : PlayerScreenSide.Side.Left;

            bool isRight = side == PlayerScreenSide.Side.Right;
            Camera cam = isRight ? player2Camera : player1Camera;
            if (cam == null) cam = mainCamera;
            int layer = isRight ? pipLayerP2 : pipLayerP1;

            playerTargets.Add(new PlayerPipTarget
            {
                transform = player.transform,
                camera = cam,
                side = side,
                pipLayer = layer
            });
        }

        if (players.Length > 0)
        {
            PlayerStats playerStats = players[0].stats;
            displayRadius = playerStats != null ? playerStats.lockOnRange : 6f;
        }
    }

    // Instancie une pip bar par joueur pour cet ennemi (une seule en solo) et les enregistre.
    // L'appelant (EnemyHealth, BrightEyesController, SwarmController_AStar...) n'a plus besoin
    // d'instancier lui-meme le prefab : juste a garder la liste retournee pour UpdateHealth/Show/Hide.
    public List<EnemyHealthBarUI> RegisterEnemy(Transform enemy, float baseMax, float realMax)
    {
        ResolvePlayerTargets();

        if (healthBars.TryGetValue(enemy, out List<EnemyHealthBarUI> existing))
            return existing;

        List<EnemyHealthBarUI> bars = new List<EnemyHealthBarUI>();

        if (healthBarPrefab != null)
        {
            foreach (PlayerPipTarget target in playerTargets)
            {
                GameObject barGO = Instantiate(healthBarPrefab, transform);
                barGO.layer = target.pipLayer;
                EnemyHealthBarUI bar = barGO.GetComponent<EnemyHealthBarUI>();
                if (bar == null) continue;

                bar.SetTargetCamera(target.camera);
                bar.Initialize(baseMax, realMax);
                bars.Add(bar);
            }
        }

        healthBars.Add(enemy, bars);
        return bars;
    }

    public void UnregisterEnemy(Transform enemy)
    {
        if (healthBars.TryGetValue(enemy, out List<EnemyHealthBarUI> bars))
        {
            foreach (EnemyHealthBarUI bar in bars)
            {
                if (bar != null)
                    Destroy(bar.gameObject);
            }
            healthBars.Remove(enemy);
        }
    }

    // Renvoie la copie assignee au joueur de ce cote (Left/Right). En solo (une seule copie,
    // toujours cote Left) renvoie cette copie unique quel que soit le side demande.
    public EnemyHealthBarUI GetBarForSide(Transform enemy, PlayerScreenSide.Side side)
    {
        if (!healthBars.TryGetValue(enemy, out List<EnemyHealthBarUI> bars) || bars.Count == 0)
            return null;

        for (int i = 0; i < playerTargets.Count && i < bars.Count; i++)
        {
            if (playerTargets[i].side == side)
                return bars[i];
        }

        return bars[0];
    }

    public void ClearAllOutlines()
    {
        foreach (var kvp in healthBars)
        {
            foreach (EnemyHealthBarUI bar in kvp.Value)
            {
                if (bar != null)
                    bar.SetLockedOutline(false);
            }
        }
    }

    private void LateUpdate()
    {
        if (mainCamera == null || playerTargets.Count == 0) return;

        bool shutterExists = FindObjectOfType<MetalShutter>() != null;

        foreach (var kvp in healthBars)
        {
            Transform enemy = kvp.Key;
            List<EnemyHealthBarUI> bars = kvp.Value;

            if (enemy == null) continue;

            EnemyAI_AStar ai = enemy.GetComponent<EnemyAI_AStar>();
            EnemyStats stats = ai != null ? ai.stats : null;
            bool isChasing = ai != null && (ai.currentState == EnemyAI_AStar.State.Chasing
                                          || ai.currentState == EnemyAI_AStar.State.Attacking);

            // recentlyHitBySentinel (flash "je viens de me faire tirer dessus") volontairement
            // retire de shouldIgnoreDistance (2026) : ce flag est sur l'ennemi, pas par joueur --
            // en multi il forcait l'affichage sur les DEUX copies, meme pour un joueur a l'autre
            // bout du niveau. Sans consequence notable en solo (le joueur qui vient de tirer
            // reste quasi toujours dans son propre rayon d'affichage).
            bool shouldIgnoreDistance = false;
            EnemyPitInteractable pitInt = enemy.GetComponent<EnemyPitInteractable>();
            if (pitInt != null && pitInt.shouldIgnoreHealthbarDistance)
                shouldIgnoreDistance = true;

            Vector3 worldPos = enemy.position + Vector3.up * verticalOffset;

            for (int i = 0; i < bars.Count; i++)
            {
                EnemyHealthBarUI bar = bars[i];
                if (bar == null) continue;

                if (shutterExists)
                {
                    bar.Hide();
                    continue;
                }

                bar.UpdateWorldPosition(worldPos, mainCamera, canvas);
                if (ai != null)
                    bar.SetChaseOutline(isChasing);

                float distance = i < playerTargets.Count
                    ? Vector3.Distance(playerTargets[i].transform.position, enemy.position)
                    : Vector3.Distance(playerTargets[0].transform.position, enemy.position);

                if (stats != null && stats.attackType == EnemyStats.AttackType.Blinder)
                {
                    if (distance <= stats.blinderHealthBarRange)
                        bar.Show();
                    else
                        bar.Hide();
                    continue;
                }

                if (shouldIgnoreDistance || distance <= displayRadius)
                    bar.Show();
                else
                    bar.Hide();
            }
        }
    }
}

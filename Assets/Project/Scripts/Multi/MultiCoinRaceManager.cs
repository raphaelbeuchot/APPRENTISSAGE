using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Coin Race : chrono de manche. A la fin, le joueur avec le plus de pieces gagne (egalite si meme score).
// Ecran de fin (VICTORY/YOU LOSE/EX AEQUO) sur le meme principe que MultiRaceManager, sans la notion de
// match (First to Ten) : Coin Race reste une manche isolee, pas de MultiMatchScore ni d'ecran MATCH WON.
public class MultiCoinRaceManager : MonoBehaviour
{
    [SerializeField] private float roundDuration = 180f;
    [Tooltip("Optionnel : affiche le temps restant (format MM:SS) sur chaque moitie d'ecran, meme valeur partout. Pose et stylise a la main comme le reste du HUD multi.")]
    [SerializeField] private List<TextMeshProUGUI> timerTexts;
    [Tooltip("Vide = auto-trouve dans la scene.")]
    [SerializeField] private CoinSpawner coinSpawner;
    [SerializeField] private TMP_FontAsset endScreenFont;
    [Tooltip("Delai (temps reel) avant que A (ou Entree au clavier) relance le niveau depuis l'ecran de fin.")]
    [SerializeField] private float restartPromptDelay = 2f;
    [Tooltip("Jingle joue a la fin de la manche (en 2D). Vide = pas de son.")]
    [SerializeField] private AudioClip endRoundSound;
    [Tooltip("Dans les X dernieres secondes, le chrono passe a la couleur ci-dessous.")]
    [SerializeField] private float lastSecondsThreshold = 10f;
    [SerializeField] private Color lastSecondsColor = Color.red;
    [Tooltip("Duree d'entree du texte des ecrans de fin : VICTORY descend, YOU LOSE monte, EX AEQUO tourne. 0 = apparition seche.")]
    [SerializeField] private float introDuration = 1f;

    private PlayerCoinWallet leftWallet;
    private PlayerCoinWallet rightWallet;
    private AudioSource audioSource;
    private readonly List<Color> timerBaseColors = new List<Color>();

    public float TimeRemaining { get; private set; }

    // Fraction du chrono ecoulee (0 au debut, 1 a la fin) - sert de base a la pression sentinelle liee au temps.
    public float ElapsedFraction => roundDuration > 0f ? Mathf.Clamp01((roundDuration - TimeRemaining) / roundDuration) : 1f;

    // Pose par l'ecran de lancement multi (MultiStartUI) avant SceneManager.LoadScene, en secondes.
    // Null = pas passe par le menu (test direct d'une scene en editeur) : roundDuration d'Inspector inchange.
    public static float? RoundDurationOverride;

    private void Start()
    {
        if (RoundDurationOverride.HasValue)
            roundDuration = RoundDurationOverride.Value;

        TimeRemaining = roundDuration;

        // Son en 2D : signal "meta" comme l'ecran de fin, entendu pareil des deux cotes
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f;

        // Couleur posee a la main sur chaque chrono, rendue tant qu'on n'est pas dans les dernieres secondes
        if (timerTexts != null)
            foreach (TextMeshProUGUI t in timerTexts)
                timerBaseColors.Add(t != null ? t.color : Color.white);

        foreach (PlayerCoinWallet wallet in FindObjectsByType<PlayerCoinWallet>(FindObjectsSortMode.None))
        {
            PlayerScreenSide side = wallet.GetComponent<PlayerScreenSide>();
            bool isRight = side != null && side.side == PlayerScreenSide.Side.Right;

            if (isRight) rightWallet = wallet;
            else leftWallet = wallet;
        }

        if (leftWallet == null || rightWallet == null)
            Debug.LogWarning("[MultiCoinRace] Un des deux PlayerCoinWallet n'a pas ete trouve");

        if (coinSpawner == null)
            coinSpawner = FindFirstObjectByType<CoinSpawner>();

        // Chrono cache jusqu'a StartRound (meme frame que ShowGameplayBars : apparait avec les autres barres)
        SetTimersVisible(false);
    }

    private void SetTimersVisible(bool visible)
    {
        if (timerTexts == null) return;
        foreach (TextMeshProUGUI t in timerTexts)
            if (t != null) t.gameObject.SetActive(visible);
    }

    // Appele par MultiLevelStart au moment ou le rideau commence a se lever (StartOpening()),
    // pas au Start() de la scene : le chrono ne doit pas tourner pendant "Get ready"/le rideau ferme.
    public void StartRound()
    {
        SetTimersVisible(true);
        StartCoroutine(CountdownRoutine());
    }

    private void Update()
    {
        if (timerTexts == null) return;

        int seconds = Mathf.CeilToInt(Mathf.Max(0f, TimeRemaining));
        string text = $"{seconds / 60:00}:{seconds % 60:00}";
        bool lastSeconds = TimeRemaining <= lastSecondsThreshold;

        for (int i = 0; i < timerTexts.Count; i++)
        {
            TextMeshProUGUI t = timerTexts[i];
            if (t == null) continue;
            t.text = text;
            t.color = lastSeconds ? lastSecondsColor : timerBaseColors[i];
        }
    }

    private IEnumerator CountdownRoutine()
    {
        while (TimeRemaining > 0f)
        {
            TimeRemaining -= Time.deltaTime;
            yield return null;
        }

        TimeRemaining = 0f;
        EndRound();
    }

    private void EndRound()
    {
        int leftScore = leftWallet != null ? leftWallet.Count : 0;
        int rightScore = rightWallet != null ? rightWallet.Count : 0;

        if (coinSpawner != null)
            coinSpawner.StopSpawning();

        SentinelCycleManager sentinelCycle = FindFirstObjectByType<SentinelCycleManager>();
        if (sentinelCycle != null)
            sentinelCycle.StopCycle();

        if (leftWallet != null) FreezePlayer(leftWallet.gameObject);
        if (rightWallet != null) FreezePlayer(rightWallet.gameObject);

        // Arret sur image au buzzer : ennemis, pieces, ragdolls, particules figes d'un coup.
        // Pause bloquee : sinon Resume() remettrait le temps a 1 sous l'ecran de fin.
        // Le temps repasse a 1 au rechargement (RestartOnButtonCoroutine, et GameManager.Start).
        LevelManager levelManager = FindFirstObjectByType<LevelManager>();
        if (levelManager != null)
            levelManager.BlockPause();
        Time.timeScale = 0f;

        if (endRoundSound != null)
            audioSource.PlayOneShot(endRoundSound);

        if (leftScore == rightScore)
        {
            Debug.Log($"[MultiCoinRace] Ex aequo, {leftScore} pieces chacun");
            if (leftWallet != null) ShowHalfScreenMessage(leftWallet.gameObject, "multi.tie", EndScreenTextIntro.Mode.Spin);
            if (rightWallet != null) ShowHalfScreenMessage(rightWallet.gameObject, "multi.tie", EndScreenTextIntro.Mode.Spin);
        }
        else
        {
            PlayerCoinWallet winner = leftScore > rightScore ? leftWallet : rightWallet;
            PlayerCoinWallet loser = leftScore > rightScore ? rightWallet : leftWallet;
            Debug.Log($"[MultiCoinRace] {(leftScore > rightScore ? "Left" : "Right")} gagne, {Mathf.Max(leftScore, rightScore)} contre {Mathf.Min(leftScore, rightScore)}");

            if (winner != null) ShowHalfScreenMessage(winner.gameObject, "multi.victory", EndScreenTextIntro.Mode.SlideDown);
            if (loser != null) ShowHalfScreenMessage(loser.gameObject, "multi.lose", EndScreenTextIntro.Mode.SlideUp);
        }

        StartCoroutine(RestartOnButtonCoroutine());
    }

    // Meme principe que GoalDoorNew.ReachGoal() : figer le transform ET l'animation,
    // sinon l'Animator continue de jouer la derniere intention (ex: course sur place).
    private static void FreezePlayer(GameObject player)
    {
        PlayerPhysicsMovement movement = player.GetComponent<PlayerPhysicsMovement>();
        if (movement != null)
            movement.enabled = false;

        Animator animator = player.GetComponentInChildren<Animator>();
        if (animator != null)
            animator.speed = 0f;
    }

    // Repris de MultiRaceManager.ShowHalfScreenMessage : overlay noir alpha 0.5 + texte,
    // sur la moitie d'ecran du joueur concerne (PlayerScreenSide, defaut Left si absent).
    private void ShowHalfScreenMessage(GameObject player, string message, EndScreenTextIntro.Mode intro)
    {
        PlayerScreenSide screenSide = player.GetComponent<PlayerScreenSide>();
        bool isRight = screenSide != null && screenSide.side == PlayerScreenSide.Side.Right;

        GameObject canvasObj = new GameObject("CoinRaceEndScreenCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObj.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = isRight ? new Vector2(0.5f, 0f) : new Vector2(0f, 0f);
        bgRect.anchorMax = isRight ? new Vector2(1f, 1f) : new Vector2(0.5f, 1f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        Image bg = bgObj.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.5f);

        GameObject textObj = new GameObject("EndScreenText");
        textObj.transform.SetParent(bgObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        if (endScreenFont != null) text.font = endScreenFont;
        text.text = Loc.Text(message);
        text.fontSize = 80f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;

        EndScreenTextIntro.Play(textObj, intro, introDuration);
    }

    // Repris de MultiRaceManager.RestartOnButtonCoroutine, sans la branche MultiMatchScore/MATCH WON :
    // Coin Race reste hors du score First to Ten, une manche = un ecran, pas de notion de match.
    private IEnumerator RestartOnButtonCoroutine()
    {
        // Temps reel : le jeu est en pause (timeScale 0), un WaitForSeconds ne finirait jamais
        yield return new WaitForSecondsRealtime(restartPromptDelay);

        while (!RestartButtonPressed())
            yield return null;

        while (RestartButtonHeld())
            yield return null;

        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private bool RestartButtonPressed()
    {
        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (gamepad.buttonSouth.wasPressedThisFrame)
                return true;
        }
        return Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame;
    }

    private bool RestartButtonHeld()
    {
        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (gamepad.buttonSouth.isPressed)
                return true;
        }
        return Keyboard.current != null && Keyboard.current.enterKey.isPressed;
    }
}

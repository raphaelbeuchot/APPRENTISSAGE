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
    [Tooltip("Delai avant que A (ou Entree au clavier) relance le niveau depuis l'ecran de fin.")]
    [SerializeField] private float restartPromptDelay = 2f;

    private PlayerCoinWallet leftWallet;
    private PlayerCoinWallet rightWallet;

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
    }

    // Appele par MultiLevelStart au moment ou le rideau commence a se lever (StartOpening()),
    // pas au Start() de la scene : le chrono ne doit pas tourner pendant "Get ready"/le rideau ferme.
    public void StartRound()
    {
        StartCoroutine(CountdownRoutine());
    }

    private void Update()
    {
        if (timerTexts == null) return;

        int seconds = Mathf.CeilToInt(Mathf.Max(0f, TimeRemaining));
        string text = $"{seconds / 60:00}:{seconds % 60:00}";

        foreach (TextMeshProUGUI t in timerTexts)
            if (t != null) t.text = text;
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

        if (leftScore == rightScore)
        {
            Debug.Log($"[MultiCoinRace] Ex aequo, {leftScore} pieces chacun");
            if (leftWallet != null) ShowHalfScreenMessage(leftWallet.gameObject, "EX AEQUO");
            if (rightWallet != null) ShowHalfScreenMessage(rightWallet.gameObject, "EX AEQUO");
        }
        else
        {
            PlayerCoinWallet winner = leftScore > rightScore ? leftWallet : rightWallet;
            PlayerCoinWallet loser = leftScore > rightScore ? rightWallet : leftWallet;
            Debug.Log($"[MultiCoinRace] {(leftScore > rightScore ? "Left" : "Right")} gagne, {Mathf.Max(leftScore, rightScore)} contre {Mathf.Min(leftScore, rightScore)}");

            if (winner != null) ShowHalfScreenMessage(winner.gameObject, "VICTORY");
            if (loser != null) ShowHalfScreenMessage(loser.gameObject, "YOU LOSE");
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
    private void ShowHalfScreenMessage(GameObject player, string message)
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
        text.text = message;
        text.fontSize = 80f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
    }

    // Repris de MultiRaceManager.RestartOnButtonCoroutine, sans la branche MultiMatchScore/MATCH WON :
    // Coin Race reste hors du score First to Ten, une manche = un ecran, pas de notion de match.
    private IEnumerator RestartOnButtonCoroutine()
    {
        yield return new WaitForSeconds(restartPromptDelay);

        while (!RestartButtonPressed())
            yield return null;

        while (RestartButtonHeld())
            yield return null;

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

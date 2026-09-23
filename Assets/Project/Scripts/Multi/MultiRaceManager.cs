using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

// Course multi (Versus) : enregistre le premier joueur qui atteint la porte.
// Le verrou "premiere arrivee" est deja dans GoalDoorNew (hasBeenReached), ici on ne fait que retenir le gagnant.
// Au premier arrivant : lance un compte a rebours pour l'autre joueur (loseCountdownDuration). Dans les deux
// cas c'est une defaite, ecran "YOU LOSE" sur sa moitie d'ecran : s'il atteint la porte a temps, deja fige
// par GoalDoorNew (comme le gagnant), juste l'ecran en plus. Sinon : elimination forcee, mort sans respawn
// (MultiRespawn desactive juste avant), feedback "tir" (son + flash + anim), puis l'ecran.
public class MultiRaceManager : MonoBehaviour
{
    [SerializeField] private GoalDoorNew goalDoor;
    [SerializeField] private float loseCountdownDuration = 5f;
    [SerializeField] private AudioClip eliminationSound;
    [FormerlySerializedAs("loseFont")]
    [SerializeField] private TMP_FontAsset endScreenFont;
    [Tooltip("Delai avant que A (ou Entree au clavier) relance le niveau depuis l'ecran YOU LOSE.")]
    [SerializeField] private float restartPromptDelay = 2f;

    public GameObject Winner { get; private set; }
    public event Action<GameObject> OnRaceWon;

    private GameObject pendingLoser;
    private AudioSource audioSource;

    private void Start()
    {
        // Son en 2D (pas PlayClipAtPoint) : signal "meta" comme l'ecran YOU LOSE, doit s'entendre
        // pareil quelle que soit la position du perdant par rapport a l'unique Audio Listener de la scene.
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f;

        if (goalDoor == null)
            goalDoor = FindFirstObjectByType<GoalDoorNew>();

        if (goalDoor == null)
        {
            Debug.LogWarning("[MultiRace] Aucune GoalDoorNew trouvee dans la scene");
            return;
        }

        goalDoor.OnPlayerReached += HandlePlayerReached;
    }

    private void OnDestroy()
    {
        if (goalDoor != null)
            goalDoor.OnPlayerReached -= HandlePlayerReached;
    }

    private void HandlePlayerReached(GameObject player)
    {
        if (Winner == null)
        {
            Winner = player;
            Debug.Log($"[MultiRace] {player.name} gagne la course");

            PlayerScreenSide winnerScreenSide = player.GetComponent<PlayerScreenSide>();
            PlayerScreenSide.Side winnerSide = winnerScreenSide != null ? winnerScreenSide.side : PlayerScreenSide.Side.Left;
            MultiMatchScore.AddPoint(winnerSide);

            ShowHalfScreenMessage(player, "VictoryScreenCanvas", "VictoryText", "VICTORY");

            OnRaceWon?.Invoke(player);

            pendingLoser = FindTheOtherPlayer(player);
            if (pendingLoser != null)
                StartCoroutine(LoseCountdownCoroutine(pendingLoser));
            return;
        }

        // Le joueur qui restait a fini a temps : deja fige par GoalDoorNew, mais reste une defaite
        // (arrive 2e) donc meme ecran YOU LOSE que l'elimination forcee.
        if (player == pendingLoser)
        {
            pendingLoser = null;
            ShowLoseScreen(player);
        }
    }

    private GameObject FindTheOtherPlayer(GameObject winner)
    {
        PlayerHealth[] allPlayers = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        foreach (PlayerHealth ph in allPlayers)
        {
            if (ph.gameObject != winner)
                return ph.gameObject;
        }
        return null;
    }

    private IEnumerator LoseCountdownCoroutine(GameObject loser)
    {
        yield return new WaitForSeconds(loseCountdownDuration);

        // A fini entre-temps (pendingLoser efface par HandlePlayerReached) : rien a faire.
        if (pendingLoser != loser) yield break;

        PlayerHealth loserHealth = loser.GetComponent<PlayerHealth>();
        if (loserHealth == null || loserHealth.IsDead()) yield break;

        Debug.Log($"[MultiRace] {loser.name} n'a pas fini a temps : elimination");

        // Desactive le respawn AVANT la mort forcee : sans ca MultiRespawn le remettrait en jeu normalement.
        MultiRespawn respawn = loser.GetComponent<MultiRespawn>();
        if (respawn != null)
            respawn.enabled = false;

        // Feedback "tir" (son + flash rouge + anim "Shot"), sans laser : la sentinelle est deja arretee
        // a ce stade (un joueur a deja gagne), donc pas de position de tir credible a montrer.
        PlayerDetectionFeedback feedback = loser.GetComponent<PlayerDetectionFeedback>();
        if (feedback != null)
            feedback.OnShotBySentinel();
        if (eliminationSound != null)
            audioSource.PlayOneShot(eliminationSound);

        loserHealth.TakeHeadshot();

        ShowLoseScreen(loser);
    }

    private void ShowLoseScreen(GameObject loser)
    {
        ShowHalfScreenMessage(loser, "LoseScreenCanvas", "LoseText", "YOU LOSE");
        StartCoroutine(RestartOnButtonCoroutine());
    }

    // Overlay noir alpha 0.5 + texte, sur la moitie d'ecran du joueur concerne (PlayerScreenSide,
    // defaut Left si absent). Partage entre l'ecran VICTORY (gagnant) et YOU LOSE (perdant).
    private void ShowHalfScreenMessage(GameObject player, string canvasName, string textName, string message)
    {
        PlayerScreenSide screenSide = player.GetComponent<PlayerScreenSide>();
        bool isRight = screenSide != null && screenSide.side == PlayerScreenSide.Side.Right;

        GameObject canvasObj = new GameObject(canvasName);
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

        GameObject textObj = new GameObject(textName);
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

    // A (croix Sud manette) relance le niveau depuis l'ecran YOU LOSE, apres un delai (evite un
    // appui accidentel juste apres l'affichage). A est aussi le bouton Sprint : on attend qu'il
    // soit relache avant de recharger la scene, sinon Sprint pourrait se redeclencher tout seul
    // des l'activation des inputs dans la scene rechargee (bouton toujours physiquement enfonce).
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

using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Course multi (Versus) : enregistre le premier joueur qui atteint la porte.
// Le verrou "premiere arrivee" est deja dans GoalDoorNew (hasBeenReached), ici on ne fait que retenir le gagnant.
// Au premier arrivant : lance un compte a rebours pour l'autre joueur (loseCountdownDuration). S'il atteint
// la porte a temps, rien de plus (deja fige par GoalDoorNew, comme le gagnant). Sinon : mort forcee sans
// respawn (MultiRespawn desactive juste avant) + ecran "YOU LOSE" sur sa moitie d'ecran.
public class MultiRaceManager : MonoBehaviour
{
    [SerializeField] private GoalDoorNew goalDoor;
    [SerializeField] private float loseCountdownDuration = 5f;

    public GameObject Winner { get; private set; }
    public event Action<GameObject> OnRaceWon;

    private GameObject pendingLoser;

    private void Start()
    {
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
            OnRaceWon?.Invoke(player);

            pendingLoser = FindTheOtherPlayer(player);
            if (pendingLoser != null)
                StartCoroutine(LoseCountdownCoroutine(pendingLoser));
            return;
        }

        // Le joueur qui restait a fini a temps : rien de plus (deja fige par GoalDoorNew).
        if (player == pendingLoser)
            pendingLoser = null;
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

        loserHealth.TakeHeadshot();

        ShowLoseScreen(loser);
    }

    private void ShowLoseScreen(GameObject loser)
    {
        PlayerScreenSide screenSide = loser.GetComponent<PlayerScreenSide>();
        bool isRight = screenSide != null && screenSide.side == PlayerScreenSide.Side.Right;

        GameObject canvasObj = new GameObject("LoseScreenCanvas");
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

        GameObject textObj = new GameObject("LoseText");
        textObj.transform.SetParent(bgObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = "YOU LOSE";
        text.fontSize = 80f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
    }
}

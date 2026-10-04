using System.Collections;
using TMPro;
using UnityEngine;

// Coin Race : chrono de manche. A la fin, le joueur avec le plus de pieces gagne (egalite si meme score).
// Pour l'instant juste logue le resultat en console - l'ecran de fin (façon VICTORY/YOU LOSE de MultiRaceManager)
// viendra en polish une fois le coeur du mode valide.
public class MultiCoinRaceManager : MonoBehaviour
{
    [SerializeField] private float roundDuration = 180f;
    [Tooltip("Optionnel : affiche le temps restant (format MM:SS). Pose et stylise a la main comme le reste du HUD multi.")]
    [SerializeField] private TextMeshProUGUI timerText;

    private PlayerCoinWallet leftWallet;
    private PlayerCoinWallet rightWallet;

    public float TimeRemaining { get; private set; }

    // Fraction du chrono ecoulee (0 au debut, 1 a la fin) - sert de base a la pression sentinelle liee au temps.
    public float ElapsedFraction => roundDuration > 0f ? Mathf.Clamp01((roundDuration - TimeRemaining) / roundDuration) : 1f;

    private void Start()
    {
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

        StartCoroutine(CountdownRoutine());
    }

    private void Update()
    {
        if (timerText == null) return;

        int seconds = Mathf.CeilToInt(Mathf.Max(0f, TimeRemaining));
        timerText.text = $"{seconds / 60:00}:{seconds % 60:00}";
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

        if (leftScore == rightScore)
            Debug.Log($"[MultiCoinRace] Ex aequo, {leftScore} pieces chacun");
        else if (leftScore > rightScore)
            Debug.Log($"[MultiCoinRace] Left gagne, {leftScore} contre {rightScore}");
        else
            Debug.Log($"[MultiCoinRace] Right gagne, {rightScore} contre {leftScore}");
    }
}

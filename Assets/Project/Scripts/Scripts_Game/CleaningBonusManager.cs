using UnityEngine;

public class CleaningBonusManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyIconsUI enemyIconsUI;

    private int cleaningScore = 0;

    private void OnEnable()
    {
        CorpsePitHandler.OnCorpseCleaned += HandleCorpseCleaned;
    }

    private void OnDisable()
    {
        CorpsePitHandler.OnCorpseCleaned -= HandleCorpseCleaned;
    }

    private void HandleCorpseCleaned(EnemyHealth enemy)
    {
        cleaningScore++;
        Debug.Log("[CleaningBonus] Score nettoyage : " + cleaningScore);

        if (enemyIconsUI != null && enemy != null)
            enemyIconsUI.SetIconCleaned(enemy);
    }

    public int GetCleaningScore() => cleaningScore;
}
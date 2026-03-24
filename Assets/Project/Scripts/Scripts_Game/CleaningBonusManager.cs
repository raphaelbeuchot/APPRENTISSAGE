using UnityEngine;

public class CleaningBonusManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyIconsUI enemyIconsUI;

    [Header("Audio")]
    [SerializeField] private AudioClip cleanSound;
    private AudioSource audioSource;

    private int cleaningScore = 0;

    private void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f;
        audioSource.playOnAwake = false;
    }

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
        if (audioSource != null && cleanSound != null)
            audioSource.PlayOneShot(cleanSound);
        CommentPanel.Show("Bonus Nettoyage !");
        Debug.Log("[CleaningBonus] Score nettoyage : " + cleaningScore);

        if (enemyIconsUI != null && enemy != null)
            enemyIconsUI.SetIconCleaned(enemy);
    }

    public int GetCleaningScore() => cleaningScore;
}
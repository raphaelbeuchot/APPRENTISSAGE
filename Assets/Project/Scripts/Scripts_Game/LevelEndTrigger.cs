using UnityEngine;

/// <summary>
/// Trigger qui déclenche la fin du niveau sans passer par Victory UI
/// Utilisé pour les niveaux tuto
/// </summary>
public class LevelEndTrigger : MonoBehaviour
{
    private LevelManager levelManager;

    void Start()
    {
        levelManager = FindObjectOfType<LevelManager>();

        if (levelManager == null)
        {
            Debug.LogError("LevelEndTrigger: Aucun LevelManager trouvé!");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && levelManager != null)
        {
            levelManager.OnPlayerReachedLevelEnd(other.gameObject);
        }
    }
}
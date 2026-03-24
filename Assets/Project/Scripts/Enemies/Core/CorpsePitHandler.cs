using UnityEngine;

public class CorpsePitHandler : MonoBehaviour
{
    [HideInInspector] public EnemyHealth enemyRef;
    private bool hasBeenCleaned = false;

    public static event System.Action<EnemyHealth> OnCorpseCleaned;

    public void OnEnterPit(PitContentType.ContentCategory category)
    {
        if (hasBeenCleaned) return;

        if (category == PitContentType.ContentCategory.Water ||
            category == PitContentType.ContentCategory.InstantKill ||
            category == PitContentType.ContentCategory.Spikes)
        {
            hasBeenCleaned = true;
            Debug.Log("[Nettoyage] Corpse nettoyé ! Catégorie : " + category);
            OnCorpseCleaned?.Invoke(enemyRef);

            if (category != PitContentType.ContentCategory.Water)
                Destroy(gameObject, 0.5f);
        }
    }
}
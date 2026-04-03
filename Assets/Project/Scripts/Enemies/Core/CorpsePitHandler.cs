using UnityEngine;
public class CorpsePitHandler : MonoBehaviour
{
    private bool hasBeenCleaned = false;
    public static event System.Action<CorpsePitHandler> OnCorpseCleaned;
    public EnemyHealth sourceEnemy;
    public void OnEnterPit(PitContentType.ContentCategory category)
    {
        if (hasBeenCleaned) return;
        if (category == PitContentType.ContentCategory.Water ||
            category == PitContentType.ContentCategory.InstantKill ||
            category == PitContentType.ContentCategory.Spikes)
        {
            hasBeenCleaned = true;
            Debug.Log("[Nettoyage] Corpse nettoye ! Categorie : " + category);
            OnCorpseCleaned?.Invoke(this);
            if (category != PitContentType.ContentCategory.Water)
                Destroy(gameObject, 0.5f);
        }
    }
}
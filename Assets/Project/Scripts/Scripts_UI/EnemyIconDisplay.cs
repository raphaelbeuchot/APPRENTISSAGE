using UnityEngine;
using UnityEngine.UI;

public class EnemyIconDisplay : MonoBehaviour
{
    [SerializeField] private Image aliveIcon;
    [SerializeField] private Image deadIcon;

    private EnemyHealth trackedEnemy;
    private EnemyIconsUI manager;

    public void Initialize(EnemyHealth enemy, EnemyIconsUI uiManager)
    {
        trackedEnemy = enemy;
        manager = uiManager;

        // S'abonner aux événements
        if (trackedEnemy != null)
        {
            trackedEnemy.OnTakeDamage += HandleDamage;
            trackedEnemy.OnDeath += HandleDeath;
        }

        // État initial : vivant visible, mort caché
        aliveIcon.gameObject.SetActive(true);
        deadIcon.gameObject.SetActive(false);
    }

    private void HandleDamage()
    {
        if (manager != null)
        {
            manager.FlashIconRed(this);
        }
    }

    private void HandleDeath()
    {
        // Remplacer le rond par le X
        aliveIcon.gameObject.SetActive(false);
        deadIcon.gameObject.SetActive(true);
    }

    public Image GetAliveImage()
    {
        return aliveIcon;
    }

    private void OnDestroy()
    {
        // Se désabonner
        if (trackedEnemy != null)
        {
            trackedEnemy.OnTakeDamage -= HandleDamage;
            trackedEnemy.OnDeath -= HandleDamage;
        }
    }
}
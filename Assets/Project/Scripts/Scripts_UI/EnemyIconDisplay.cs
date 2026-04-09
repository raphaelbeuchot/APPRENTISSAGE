using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class EnemyIconDisplay : MonoBehaviour
{
    [SerializeField] private Image aliveIcon;
    [SerializeField] private Image deadIcon;
    [SerializeField] private GameObject broomImpactUIPrefab;
    [SerializeField] private Sprite[] broomFrames;
    [SerializeField] private float frameDuration = 0.08f;

    private EnemyHealth trackedEnemy;
    private EnemyIconsUI manager;

    public EnemyHealth GetTrackedEnemy()
    {
        return trackedEnemy;
    }

    public void Initialize(EnemyHealth enemy, EnemyIconsUI uiManager)
    {
        trackedEnemy = enemy;
        manager = uiManager;

        if (trackedEnemy != null)
        {
            trackedEnemy.OnTakeDamage += HandleDamage;
            trackedEnemy.OnDeath += HandleDeath;
        }

        aliveIcon.gameObject.SetActive(true);
        deadIcon.gameObject.SetActive(false);
    }

    private void HandleDamage()
    {
        if (manager != null)
            manager.FlashIconRed(this);
    }

    private void HandleDeath()
    {
        aliveIcon.gameObject.SetActive(false);
        deadIcon.gameObject.SetActive(true);
    }

    public Image GetAliveImage()
    {
        return aliveIcon;
    }

    public void SetCleaned()
    {
        aliveIcon.gameObject.SetActive(false);
        deadIcon.gameObject.SetActive(false);
        StartCoroutine(PlayBroomThenDestroy());
    }

    private IEnumerator PlayBroomThenDestroy()
    {
        if (broomImpactUIPrefab != null && broomFrames != null && broomFrames.Length > 0)
        {
            GameObject broomGO = Instantiate(broomImpactUIPrefab, transform);
            Image broomImage = broomGO.GetComponent<Image>();

            if (broomImage != null)
            {
                foreach (Sprite frame in broomFrames)
                {
                    broomImage.sprite = frame;
                    yield return new WaitForSeconds(frameDuration);
                }
            }
        }

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (trackedEnemy != null)
        {
            trackedEnemy.OnTakeDamage -= HandleDamage;
            trackedEnemy.OnDeath -= HandleDeath;
        }
    }
}
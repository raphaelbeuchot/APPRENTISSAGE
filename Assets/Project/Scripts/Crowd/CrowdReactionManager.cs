using System.Collections;
using UnityEngine;

public class CrowdReactionManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private SentinelCycleManager sentinelCycleManager;
    [SerializeField] private AudioSource audioSource;

    [Header("Crowd Breath - Near Stop")]
    [SerializeField] private AudioClip[] crowdBreathClips;
    [SerializeField] private float breathWindowDuration = 0.5f;

    private bool soundPlayedThisCycle = false;
    private bool wasMovingLastFrame = false;
    private Coroutine monitorCoroutine;

    private void OnEnable()
    {
        SentinelCycleManager.OnCycleChanged += OnCycleChanged;
    }

    private void OnDisable()
    {
        SentinelCycleManager.OnCycleChanged -= OnCycleChanged;
    }

    private void OnCycleChanged(SentinelCycleManager.GameState newState)
    {
        if (newState == SentinelCycleManager.GameState.GreenLight)
        {
            soundPlayedThisCycle = false;
            if (monitorCoroutine != null)
                StopCoroutine(monitorCoroutine);
            return;
        }

        if (newState == SentinelCycleManager.GameState.Alert)
        {
            if (monitorCoroutine != null)
                StopCoroutine(monitorCoroutine);
            monitorCoroutine = StartCoroutine(MonitorWindow());
        }
    }

    private IEnumerator MonitorWindow()
    {
        // Attendre alertDuration - breathWindowDuration avant de commencer a surveiller
        yield return null; // laisse le temps a alertDuration d'etre calcule
        float waitDuration = sentinelCycleManager.alertDuration - breathWindowDuration;
        if (waitDuration > 0f)
            yield return new WaitForSeconds(waitDuration);

        // Initialiser wasMovingLastFrame avant la fenetre
        wasMovingLastFrame = gameManager.IsPlayerMoving();

        // Surveiller pendant breathWindowDuration avant RedLight
        float elapsed = 0f;
        while (elapsed < breathWindowDuration)
        {
            elapsed += Time.deltaTime;
            bool isMovingNow = gameManager.IsPlayerMoving();

            if (wasMovingLastFrame && !isMovingNow && !soundPlayedThisCycle)
            {
                TryPlayCrowdBreath();
                yield break;
            }

            wasMovingLastFrame = isMovingNow;
            yield return null;
        }

        // Continuer a surveiller pendant breathWindowDuration apres debut RedLight
        elapsed = 0f;
        while (elapsed < breathWindowDuration)
        {
            elapsed += Time.deltaTime;
            bool isMovingNow = gameManager.IsPlayerMoving();

            if (wasMovingLastFrame && !isMovingNow && !soundPlayedThisCycle)
            {
                TryPlayCrowdBreath();
                yield break;
            }

            wasMovingLastFrame = isMovingNow;
            yield return null;
        }
    }

    private void TryPlayCrowdBreath()
    {
        if (soundPlayedThisCycle) return;
        soundPlayedThisCycle = true;
        PlayCrowdBreath();
    }

    private void PlayCrowdBreath()
    {
        if (crowdBreathClips == null || crowdBreathClips.Length == 0) return;
        if (audioSource == null) return;

        AudioClip clip = crowdBreathClips[Random.Range(0, crowdBreathClips.Length)];
        if (clip != null)
            audioSource.PlayOneShot(clip);
    }
}
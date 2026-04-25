using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SplinePitFillController : MonoBehaviour
{
    [HideInInspector] public PitContentType fillContentType;
    private SplinePitZone pitZone;
    private Dictionary<GameObject, Coroutine> activeCoroutines = new Dictionary<GameObject, Coroutine>();

    private void Start()
    {
        pitZone = GetComponentInParent<SplinePitZone>();
        if (pitZone == null)
            Debug.LogError("[SplinePitFill] No SplinePitZone found in parent!");
    }

    private void OnTriggerEnter(Collider other)
    {
        IPitInteractable interactable = other.GetComponent<IPitInteractable>();
        if (interactable == null) return;
        if (!interactable.CanTakePitDamage()) return;

        GameObject go = interactable.GetGameObject();

        EnemyPitInteractable enemyPit = go.GetComponent<EnemyPitInteractable>();
        if (enemyPit != null && pitZone != null)
            enemyPit.OnEnterSplinePit(pitZone.fillType, pitZone.depth);

        PlayerPitInteractable playerPit = go.GetComponent<PlayerPitInteractable>();
        if (playerPit != null && pitZone != null && pitZone.fillType == SplinePitZone.FillType.Water)
        {
            playerPit.OnEnterSplineFill(pitZone);
            return; // Pas de degats en water pour le player
        }

        if (!activeCoroutines.ContainsKey(go))
        {
            Coroutine c = StartCoroutine(DamageCoroutine(go, interactable));
            activeCoroutines[go] = c;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        IPitInteractable interactable = other.GetComponent<IPitInteractable>();
        if (interactable == null) return;

        GameObject go = interactable.GetGameObject();

        PlayerPitInteractable playerPit = go.GetComponent<PlayerPitInteractable>();
        if (playerPit != null)
        {
            if (playerPit.IsClimbingOut()) return; // AJOUTER
            playerPit.OnExitSplineFill();
        }

        if (activeCoroutines.ContainsKey(go))
        {
            if (activeCoroutines[go] != null)
                StopCoroutine(activeCoroutines[go]);
            activeCoroutines.Remove(go);
        }
    }

    private IEnumerator DamageCoroutine(GameObject go, IPitInteractable interactable)
    {
        if (fillContentType == null) yield break;

        float tickRate = 0.1f;
        float duration = fillContentType.deathDuration;
        float elapsed = 0f;
        float ticksNeeded = duration / tickRate;
        float damagePercentPerTick = 100f / ticksNeeded;

        while (elapsed < duration)
        {
            if (go == null || interactable == null) yield break;
            if (!interactable.CanTakePitDamage())
            {
                CheckAndCleanup(go);
                yield break;
            }

            EnemyHealth eh = go.GetComponent<EnemyHealth>();
            if (eh != null) eh.deathByPit = true;

            interactable.TakePitDamage(damagePercentPerTick, PitDamageType.InstantKill);

            if (!interactable.CanTakePitDamage())
            {
                CheckAndCleanup(go);
                yield break;
            }

            yield return new WaitForSeconds(tickRate);
            elapsed += tickRate;
        }

        if (go != null && interactable != null && interactable.CanTakePitDamage())
        {
            EnemyHealth eh = go.GetComponent<EnemyHealth>();
            if (eh != null) eh.deathByPit = true;

            interactable.TakePitDamage(100f, PitDamageType.InstantKill);
            CheckAndCleanup(go);
        }

        if (activeCoroutines.ContainsKey(go))
            activeCoroutines.Remove(go);
    }

    private void CheckAndCleanup(GameObject go)
    {
        if (go == null || fillContentType == null) return;

        EnemyHealth eh = go.GetComponent<EnemyHealth>();
        if (eh == null || !eh.IsDead()) return;

        CorpsePitHandler handler = go.GetComponent<CorpsePitHandler>();
        if (handler == null)
            handler = go.AddComponent<CorpsePitHandler>();

        handler.sourceEnemy = eh;
        handler.OnEnterPit(fillContentType.category);

        if (eh.destroyOnDeath && go.layer != LayerMask.NameToLayer("Human"))
            Destroy(go, fillContentType.destroyDelay);
    }
}
using UnityEngine;

public class EnemyAnimationEvents : MonoBehaviour
{
    private HitAttack hitAttack;

    void Start()
    {
        hitAttack = GetComponent<HitAttack>();
    }

    public void OnHitLand()
    {
        if (hitAttack != null)
            hitAttack.OnHitLand();
    }

    public void OnWindupComplete()
    {
        if (hitAttack != null)
            hitAttack.OnWindupComplete();
    }
}
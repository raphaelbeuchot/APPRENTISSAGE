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
}
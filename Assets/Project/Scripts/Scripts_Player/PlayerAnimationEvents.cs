using UnityEngine;

public class PlayerAnimationEvents : MonoBehaviour
{
    private BroomAttackSystem broomAttack;

    void Start()
    {
        broomAttack = GetComponentInParent<BroomAttackSystem>();
        meleeAttack = GetComponentInParent<MeleeAttackSystem>();

    }

    public void OnBroomHit()
    {
        if (broomAttack != null)
            broomAttack.OnBroomHit();
    }
    private MeleeAttackSystem meleeAttack;

  
    public void OnSprayHit()
    {
        if (meleeAttack != null)
            meleeAttack.OnSprayHit();
    }
}
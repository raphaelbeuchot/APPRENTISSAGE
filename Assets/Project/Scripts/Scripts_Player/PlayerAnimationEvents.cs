using UnityEngine;

public class PlayerAnimationEvents : MonoBehaviour
{
    private BroomAttackSystem broomAttack;
    private PlayerPhysicsMovement playerMovement;


    void Start()
    {
        broomAttack = GetComponentInParent<BroomAttackSystem>();
        meleeAttack = GetComponentInParent<MeleeAttackSystem>();
        playerMovement = GetComponentInParent<PlayerPhysicsMovement>();

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
    public void OnGroggyStart()
    {
        if (playerMovement != null)
            playerMovement.OnGroggyStart();
    }
}
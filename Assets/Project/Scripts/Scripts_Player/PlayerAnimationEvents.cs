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
    public void OnDashSound()
    {
        PlayerPhysicsMovement movement = GetComponentInParent<PlayerPhysicsMovement>();
        if (movement == null) return;
        AudioSource audio = GetComponentInChildren<AudioSource>();
        if (audio != null && movement.stats.dashSound != null)
            audio.PlayOneShot(movement.stats.dashSound);
    }
}
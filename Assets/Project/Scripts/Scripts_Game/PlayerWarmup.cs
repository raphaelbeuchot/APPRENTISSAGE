using System.Collections;
using UnityEngine;

public class PlayerWarmup : MonoBehaviour
{
    private Animator animator;
    private Rigidbody rb;
    private PlayerPhysicsMovement playerMovement;

    private void Awake()
    {
        Debug.Log("[WARMUP] Debut warmup player...");
        float startTime = Time.realtimeSinceStartup;

        // Recuperer les references
        animator = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody>();
        playerMovement = GetComponent<PlayerPhysicsMovement>();

        // 1. WARMUP ANIMATOR
        if (animator != null)
        {
            animator.SetFloat("SpeedX", 0.5f);
            animator.SetFloat("SpeedZ", 0.5f);
            animator.Update(0f); // Force update
            animator.SetFloat("SpeedX", 0f);
            animator.SetFloat("SpeedZ", 0f);
            Debug.Log("[WARMUP] Animator prechauffe");
        }

        // 2. WARMUP RIGIDBODY
        if (rb != null)
        {
            rb.WakeUp();
            Debug.Log("[WARMUP] Rigidbody prechauffe");
        }

       

        // 4. WARMUP RAYCAST PHYSICS
        RaycastHit hit;
        Physics.Raycast(transform.position, Vector3.forward, out hit, 0.1f);
        Debug.Log("[WARMUP] Raycast physics initialise");

        float elapsed = Time.realtimeSinceStartup - startTime;
        Debug.Log($"[WARMUP] Termine en {elapsed:F3} secondes");
    }

    private void Start()
    {
        StartCoroutine(FreezePlayerCoroutine());
    }

    private IEnumerator FreezePlayerCoroutine()
    {
        // Bloquer le mouvement pendant le chargement
        if (playerMovement != null)
        {
            playerMovement.canMove = false;
            Debug.Log("[WARMUP] Player immobilise pendant 0.5s");
        }

        // Attendre le chargement complet
        yield return new WaitForSeconds(0.5f);

        // Debloquer le mouvement
        if (playerMovement != null)
        {
            playerMovement.canMove = true;
            Debug.Log("[WARMUP] Player debloque, pret a jouer");
        }
    }
}
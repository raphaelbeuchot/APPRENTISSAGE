using UnityEngine;

public class StartZone : MonoBehaviour
{
    private bool playerInZone = false;
    private bool hasShownMessage = false;

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerPhysicsMovement>() != null)
        {
            playerInZone = true;
            if (!hasShownMessage)
            {
                Debug.Log("[StartZone] Joueur dans la zone - Approchez-vous du pupitre et appuyez sur X !");
                hasShownMessage = true;
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<PlayerPhysicsMovement>() != null)
        {
            playerInZone = false;
            Debug.Log("[StartZone] Joueur sorti de la zone");
        }
    }
}
using UnityEngine;

public class StartZone : MonoBehaviour
{
    public CountdownManager countdownManager;

    private bool playerInZone = false;
    private bool gameStarted = false;

    void Start()
    {
        if (countdownManager == null)
        {
            countdownManager = FindAnyObjectByType<CountdownManager>();
        }
    }

    void Update()
    {
        if (playerInZone && !gameStarted && PlayerInputManager.Instance.InteractPressed)
        {
            Debug.Log("O presse! Lancement du countdown!");
            gameStarted = true;
            countdownManager.StartCountdown();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerPhysicsMovement>() != null)
        {
            playerInZone = true;
            Debug.Log("Joueur dans la zone! Appuyez sur O");
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<PlayerPhysicsMovement>() != null)
        {
            playerInZone = false;
            Debug.Log("Joueur sorti de la zone!");

            // Activer montée caméra
            FindObjectOfType<CameraPanningExtension>()?.OnPlayerExitStartZone();
        }
    }
}
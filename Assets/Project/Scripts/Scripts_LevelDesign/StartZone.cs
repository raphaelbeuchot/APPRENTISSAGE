using UnityEngine;

[RequireComponent(typeof(Collider))]
public class StartZone : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;

    void Start()
    {
        GetComponent<Collider>().isTrigger = true;

        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();
    }

    void OnTriggerExit(Collider other)
    {
        if (other.gameObject.layer != LayerMask.NameToLayer("Human")) return;

        CountdownManager countdown = FindObjectOfType<CountdownManager>();
        if (countdown != null)
        {
            countdown.StopAmbient();
            AudioSource cdAudio = countdown.GetComponent<AudioSource>();
            if (cdAudio != null)
                cdAudio.Stop();
        }

        if (gameManager != null)
            gameManager.StartGameCycle();

        Debug.Log("[StartZone] Joueur sorti - cycle lance");
    }
}
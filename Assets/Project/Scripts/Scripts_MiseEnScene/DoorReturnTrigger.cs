using System.Collections;
using UnityEngine;

public class DoorReturnTrigger : MonoBehaviour
{
    [SerializeField] private Transform door;
    [SerializeField] private float descentDuration = 1.2f;

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip sonDescente;
    [SerializeField] private PupitreStartRoomFX sphereFX;

    [Header("Condition caméra")]
    [Tooltip("Objet dont le X world sert de seuil : la porte descend quand la caméra le dépasse")]
    [SerializeField] private Transform cameraXThreshold;
    [Tooltip("Laisser vide pour utiliser Camera.main")]
    [SerializeField] private Camera targetCamera;

    private Vector3 initialDoorPosition;
    private bool triggerActivated = false;
    private bool descentStarted = false;

    private void Start()
    {
        if (door != null)
            initialDoorPosition = door.position;

        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void Update()
    {
        if (!triggerActivated || descentStarted) return;
        if (cameraXThreshold == null || targetCamera == null) return;

        if (targetCamera.transform.position.x > cameraXThreshold.position.x)
        {
            descentStarted = true;
            StartCoroutine(DescendreDoor());
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggerActivated || !other.CompareTag("Player")) return;

        triggerActivated = true;

        // Si pas de seuil caméra assigné, on déclenche directement (fallback)
        if (cameraXThreshold == null)
        {
            descentStarted = true;
            StartCoroutine(DescendreDoor());
        }
    }

    private IEnumerator DescendreDoor()
    {
        if (sonDescente != null)
            audioSource.PlayOneShot(sonDescente);

        Vector3 startPos = door.position;
        float elapsed = 0f;

        while (elapsed < descentDuration)
        {
            elapsed += Time.deltaTime;
            door.position = Vector3.Lerp(startPos, initialDoorPosition, elapsed / descentDuration);
            yield return null;
        }

        door.position = initialDoorPosition;

        if (sphereFX != null)
            sphereFX.OnPorteFermee();
    }
}

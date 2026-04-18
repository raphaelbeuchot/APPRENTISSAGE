using UnityEngine;
public class PlayerLoadout : MonoBehaviour
{
    [Header("Equipment")]
    public bool hasBroom = true;
    public bool hasSpray = true;
    [Header("Weapon GameObjects")]
    [SerializeField] private GameObject broomObject;
    [SerializeField] private GameObject sprayObject;
    private void Awake()
    {
        if (PlayerInputManager.Instance != null)
            PlayerInputManager.Instance.SetLoadout(hasBroom, hasSpray);
    }
    private void Start()
    {
        if (broomObject != null) broomObject.SetActive(hasBroom);
        if (sprayObject != null) sprayObject.SetActive(hasSpray);
    }
}
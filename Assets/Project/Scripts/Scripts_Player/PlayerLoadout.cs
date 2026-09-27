using UnityEngine;
// [DisallowMultipleComponent] : evite qu'un second PlayerLoadout se retrouve sur le meme
// GameObject (ex. glisse-depose du script en double, copie de composant malheureuse). Un
// doublon non lie au prefab a deja rendu la case Has Broom inoperante sans erreur visible
// (voir MULTIJOUEUR_LOCAL.md, "Bugs et comportements bizarres").
[DisallowMultipleComponent]
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
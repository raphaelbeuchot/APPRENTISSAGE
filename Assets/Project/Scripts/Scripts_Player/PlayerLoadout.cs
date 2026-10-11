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

    // Loadout du niveau, retenu pour pouvoir y revenir apres un forcage (DevPanel)
    private bool levelBroom;
    private bool levelSpray;

    private void Awake()
    {
        // Niveau de la campagne : le catalogue decide (les cases ci-dessus ne servent qu'hors catalogue : multi, tests, tutos)
        LevelCatalog catalog = LevelCatalog.Get();
        LevelCatalogEntry entry = catalog != null ? catalog.FindByScenePath(gameObject.scene.path) : null;
        if (entry != null)
        {
            hasBroom = entry.hasBroom;
            hasSpray = entry.hasSpray;
        }

        levelBroom = hasBroom;
        levelSpray = hasSpray;
        if (PlayerInputManager.Instance != null)
            PlayerInputManager.Instance.SetLoadout(hasBroom, hasSpray);
    }
    private void Start()
    {
        if (broomObject != null) broomObject.SetActive(hasBroom);
        if (sprayObject != null) sprayObject.SetActive(hasSpray);
    }

    // Change le loadout en cours de partie (input + armes visibles)
    public void ApplyLoadout(bool broom, bool spray)
    {
        hasBroom = broom;
        hasSpray = spray;
        if (PlayerInputManager.Instance != null)
            PlayerInputManager.Instance.SetLoadout(hasBroom, hasSpray);
        if (broomObject != null) broomObject.SetActive(hasBroom);
        if (sprayObject != null) sprayObject.SetActive(hasSpray);
    }

    public void RestoreLevelLoadout()
    {
        ApplyLoadout(levelBroom, levelSpray);
    }
}
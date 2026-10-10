using UnityEngine;
using UnityEngine.SceneManagement;

public class CountdownTextConfig : MonoBehaviour
{
    [Header("Message")]
    [Tooltip("Uniquement pour les scenes hors campagne (multi, tests, tutos) : les niveaux du LevelCatalog prennent son Launch Text")]
    [SerializeField] private string titleMessage = "Super Panopticon!";

    [Header("Animation")]
    [SerializeField] private float rotationDuration = 2f;
    [SerializeField] private float fadeOutDuration = 1f;
    [SerializeField] private bool rotateClockwise = true;

    public bool IsClockwise()
    {
        return rotateClockwise;
    }
    // Scene de la campagne : le catalogue decide seul (texte vide = oubli, signale dans l'Inspector du catalogue).
    // Scene hors catalogue (multi, tests, tutos) : texte local.
    public string GetTitle()
    {
        LevelCatalog catalog = LevelCatalog.Get();
        if (catalog != null)
        {
            LevelCatalogEntry entry = catalog.FindByScenePath(SceneManager.GetActiveScene().path);
            if (entry != null)
                return entry.launchText ?? "";
        }
        return titleMessage;
    }

    public float GetRotationDuration()
    {
        return rotationDuration;
    }

    public float GetFadeOutDuration()
    {
        return fadeOutDuration;
    }
}
using UnityEngine;
using UnityEngine.UI;
public class CorpseIconDisplay : MonoBehaviour
{
    [SerializeField] private Image presentIcon;
    [SerializeField] private Image cleanedIcon;
    private CorpsePitHandler trackedCorpse;
    public CorpsePitHandler GetTrackedCorpse() => trackedCorpse;
    public void Initialize(CorpsePitHandler corpse)
    {
        trackedCorpse = corpse;
        presentIcon.gameObject.SetActive(true);
        if (cleanedIcon != null)
            cleanedIcon.gameObject.SetActive(false);
    }
    public void SetCleaned()
    {
        presentIcon.gameObject.SetActive(false);
        if (cleanedIcon != null)
            cleanedIcon.gameObject.SetActive(true);
    }
    public Image GetPresentImage() => presentIcon;
}
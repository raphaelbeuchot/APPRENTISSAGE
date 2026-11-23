using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Image noire du fondu entre scenes, posee dans l'UI de la scene juste AVANT le cadre TV dans la
/// Hierarchy (dessinee sous le cadre). Le SceneFader l'assombrit a la place de son propre noir
/// (Canvas a 9999, qui passait devant le cadre). Sans ScreenFadeTarget dans la scene (MainMenu...),
/// le SceneFader garde son noir a lui.
///
/// Setup : Image plein ecran noire (alpha indifferent, remis par le script), Raycast Target decoche,
/// ce script dessus. Une seule par scene.
/// </summary>
[RequireComponent(typeof(Image))]
public class ScreenFadeTarget : MonoBehaviour
{
    public static ScreenFadeTarget Current { get; private set; }

    private Image image;

    private void Awake()
    {
        image = GetComponent<Image>();
        image.raycastTarget = false;
        Current = this;
        // Scene qui arrive pendant une transition : deja noire, le SceneFader l'eclaircit ensuite
        bool fading = SceneFader.Instance != null && SceneFader.Instance.IsFading;
        SetAlpha(fading ? 1f : 0f);
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    public void SetAlpha(float a)
    {
        if (image == null) return;
        Color c = image.color;
        c.a = a;
        image.color = c;
    }
}

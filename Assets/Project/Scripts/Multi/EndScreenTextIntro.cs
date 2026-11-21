using UnityEngine;

// Entree du texte d'un ecran de fin multi (VICTORY / YOU LOSE / EX AEQUO), pose en code par
// MultiRaceManager et MultiCoinRaceManager. Temps reel : marche aussi pendant l'arret sur image
// de la Coin Race (timeScale 0). Mouvement lineaire, le texte finit a sa place d'origine.
public class EndScreenTextIntro : MonoBehaviour
{
    public enum Mode { SlideDown, SlideUp, Spin }

    private Mode mode;
    private float duration = 1f;
    private float elapsed;
    private float distance;
    private RectTransform rect;
    private Vector2 restPosition;

    public static void Play(GameObject textObj, Mode mode, float duration)
    {
        EndScreenTextIntro intro = textObj.AddComponent<EndScreenTextIntro>();
        intro.mode = mode;
        intro.duration = duration;
        // Hors champ des cette frame (distance provisoire), sinon le texte s'afficherait une frame a sa place finale
        intro.distance = 1080f;
        intro.Apply(0f);
    }

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        restPosition = rect.anchoredPosition;
    }

    private void Start()
    {
        // Hauteur de la moitie d'ecran (le fond parent) : le texte part juste hors champ.
        // Lue ici et pas a la creation : le canvas vient d'etre cree, sa taille n'est connue qu'une frame plus tard.
        RectTransform parent = rect.parent as RectTransform;
        distance = parent != null && parent.rect.height > 0f ? parent.rect.height : 1080f;
        Apply(0f);
    }

    private void Update()
    {
        if (duration <= 0f) { Finish(); return; }

        elapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        Apply(t);

        if (t >= 1f) Finish();
    }

    private void Apply(float t)
    {
        switch (mode)
        {
            case Mode.SlideDown:
                rect.anchoredPosition = restPosition + Vector2.up * Mathf.Lerp(distance, 0f, t);
                break;
            case Mode.SlideUp:
                rect.anchoredPosition = restPosition + Vector2.down * Mathf.Lerp(distance, 0f, t);
                break;
            case Mode.Spin:
                rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 360f, t));
                break;
        }
    }

    private void Finish()
    {
        rect.anchoredPosition = restPosition;
        rect.localRotation = Quaternion.identity;
        Destroy(this);
    }
}

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

// Ecran titre : panneau pose dans la scene MainMenu, par-dessus le menu.
// Affiche au premier passage au MainMenu du lancement du jeu ; un appui sur n'importe quelle
// touche / bouton le ferme et le menu prend la main. Au retour d'un niveau, il ne revient pas.
// MainMenuUI lit TitleScreen.IsShowing pour masquer ses lignes et ignorer l'input pendant ce temps.
public class TitleScreen : MonoBehaviour
{
    [Tooltip("Delai avant d'accepter un appui (evite de passer l'ecran avec une touche deja enfoncee au lancement)")]
    [SerializeField] private float delaiAvantAppui = 0.5f;

    // Vrai tant que l'ecran titre est a l'ecran : le menu attend
    public static bool IsShowing { get; private set; }

    private static bool shownThisSession;

    private bool pressed;
    private IDisposable listener;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsShowing = false;
        shownThisSession = false;
    }

    void Awake()
    {
        if (shownThisSession)
        {
            IsShowing = false;
            gameObject.SetActive(false);
            return;
        }

        shownThisSession = true;
        IsShowing = true;
    }

    void Start()
    {
        StartCoroutine(Run());
    }

    void OnDestroy()
    {
        listener?.Dispose();
        IsShowing = false;
    }

    private IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(delaiAvantAppui);

        // Clavier, souris ou manette : le premier bouton appuye
        listener = InputSystem.onAnyButtonPress.CallOnce(_ => pressed = true);
        while (!pressed) yield return null;
        listener = null;

        // Masque tout de suite ; le menu reprend l'input une image plus tard,
        // pour que l'appui qui ferme l'ecran ne valide pas aussi la premiere ligne du menu
        foreach (Transform child in transform) child.gameObject.SetActive(false);
        var image = GetComponent<UnityEngine.UI.Image>();
        if (image != null) image.enabled = false;

        yield return null;
        IsShowing = false;
        gameObject.SetActive(false);
    }
}

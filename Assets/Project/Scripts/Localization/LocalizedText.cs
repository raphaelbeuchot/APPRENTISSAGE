using TMPro;
using UnityEngine;

// A poser sur un texte TMP fixe (menu, panneau de tuto...) : affiche le texte de la cle
// dans la langue courante et se met a jour quand la langue change.
// Pour un texte rempli par le code, ne pas poser ce composant : le code appelle Loc.Get().
[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    [Tooltip("Cle de la Sheet inventaire_VF_demo (colonne cle), ex. menu.newgame")]
    [SerializeField] private string key;

    private TMP_Text text;

    public string Key => key;

    void Awake()
    {
        text = GetComponent<TMP_Text>();
    }

    void OnEnable()
    {
        Loc.LanguageChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        Loc.LanguageChanged -= Refresh;
    }

    public void SetKey(string newKey)
    {
        key = newKey;
        Refresh();
    }

    public void Refresh()
    {
        if (string.IsNullOrEmpty(key)) return;
        if (text == null) text = GetComponent<TMP_Text>();
        text.text = Loc.Get(key);
    }
}

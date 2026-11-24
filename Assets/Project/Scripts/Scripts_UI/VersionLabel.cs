using TMPro;
using UnityEngine;

// A poser sur un texte TMP : affiche la version lue dans les Player Settings (champ Version),
// pour qu'elle ne soit jamais fausse. Ex. "Demo v0.1.0".
[RequireComponent(typeof(TMP_Text))]
public class VersionLabel : MonoBehaviour
{
    [Tooltip("Texte place avant le numero de version")]
    [SerializeField] private string prefixe = "Demo v";

    void Awake()
    {
        GetComponent<TMP_Text>().text = prefixe + Application.version;
    }
}

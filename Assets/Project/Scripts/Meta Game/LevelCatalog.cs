using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

#if UNITY_EDITOR
// Resultat d'une verification observee (voir Scripts/Editor/CatalogChecks.cs)
[System.Serializable]
public class ObservedCheck
{
    public string id;
    public int status;    // 0 absent, 1 a completer, 2 correct
    public string detail; // objet trouve ou probleme (bulle au survol)
}
#endif

[System.Serializable]
public class LevelCatalogEntry
{
#if UNITY_EDITOR
    [Tooltip("Glisser la scene depuis le Project")]
    public SceneAsset scene;
#endif

    [Tooltip("Nom affiche dans l'ecran LevelSelect (optionnel : nom de la scene si vide)")]
    public string levelName;

    [Tooltip("Texte qui tourne au lancement du niveau (optionnel : texte du CountdownTextConfig de la scene si vide)")]
    public string launchText;

    [Tooltip("Loadout du joueur dans ce niveau (le catalogue decide : la case du PlayerLoadout de la scene est ignoree)")]
    public bool hasBroom = true;
    public bool hasSpray = true;

#if UNITY_EDITOR
    // Observe dans la scene par "Analyser les scenes" (Game Catalog / LD Catalog) : affichage seulement, jamais lu en jeu
    [HideInInspector] public List<ObservedCheck> observed = new List<ObservedCheck>();
#endif

    [Tooltip("Rempli automatiquement depuis la scene (utilise en jeu)")]
    public string scenePath;

    public string[] collectibleIDs;
}

// Ordre de la campagne solo : une ligne par niveau, la scene par reference (plus de numero de Build Settings).
// Range dans Resources/ : lisible par n'importe quel script via LevelCatalog.Get(), sans reference a brancher.
[CreateAssetMenu(fileName = "LevelCatalog", menuName = "1-2-3 Soleil/Level Catalog")]
public class LevelCatalog : ScriptableObject
{
    public List<LevelCatalogEntry> levels = new List<LevelCatalogEntry>();

#if UNITY_EDITOR
    // Date de la derniere analyse des scenes (Game Catalog)
    [HideInInspector] public string lastAnalysis;
#endif

    private static LevelCatalog cached;

    public static LevelCatalog Get()
    {
        if (cached == null)
            cached = Resources.Load<LevelCatalog>("LevelCatalog");
        return cached;
    }

    // Ligne du catalogue pour une scene (chemin Assets/...unity), null si la scene n'est pas dans la campagne
    public LevelCatalogEntry FindByScenePath(string scenePath)
    {
        foreach (LevelCatalogEntry entry in levels)
        {
            if (entry != null && entry.scenePath == scenePath)
                return entry;
        }
        return null;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        RefreshScenePaths();
    }

    // Recalcule les chemins depuis les scenes (utile si une scene a ete renommee ou deplacee)
    public void RefreshScenePaths()
    {
        foreach (LevelCatalogEntry entry in levels)
        {
            if (entry == null) continue;
            entry.scenePath = entry.scene != null ? AssetDatabase.GetAssetPath(entry.scene) : "";
        }
    }
#endif
}

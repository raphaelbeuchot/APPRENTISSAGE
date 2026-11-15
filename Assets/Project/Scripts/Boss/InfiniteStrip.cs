using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Salle infinie en +X (fin du boss Rush Hour IV) : une bande de modules identiques (sol + portes),
/// colles bout a bout en X. Le joueur reste toujours sur le module du milieu : quand il depasse le milieu
/// vers +X, le module le plus a gauche passe tout a droite ; en sens inverse, le plus a droite repasse a
/// gauche, mais jamais plus a gauche que la position de depart (mur / couloir de ce cote).
/// Rien n'est cree ni detruit, le joueur n'est jamais teleporte.
///
/// Setup : ce script sur un GameObject a part, modules dans la liste (ordre indifferent), Module Width =
/// pas entre deux modules colles. Le recyclage doit se faire hors champ : assez de modules pour couvrir
/// ce que voit la camera, plus un de chaque cote.
/// Portes dans les modules : DoorBasic travaille en position locale, elles suivent leur module.
/// </summary>
public class InfiniteStrip : MonoBehaviour
{
    [Tooltip("Modules de la bande (meme largeur), colles bout a bout en X. Ordre indifferent.")]
    [SerializeField] private List<Transform> modules = new List<Transform>();
    [Tooltip("Pas entre deux modules colles, en X (m).")]
    [Min(0.1f)] [SerializeField] private float moduleWidth = 10f;
    [Tooltip("Marge avant de recycler (m) : evite les allers-retours si le joueur pietine pile sur la limite.")]
    [Min(0f)] [SerializeField] private float margin = 0.5f;
    [Tooltip("Vide = l'objet tague Player.")]
    [SerializeField] private Transform player;

    private float startLeftX;

    private void Start()
    {
        modules.RemoveAll(m => m == null);
        SortModules();
        if (modules.Count < 3)
            Debug.LogWarning("[InfiniteStrip] Moins de 3 modules : le recyclage se verra a l'ecran.", this);
        if (modules.Count > 0) startLeftX = modules[0].position.x;
    }

    private void LateUpdate()
    {
        if (modules.Count < 2) return;
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) return;
            player = p.transform;
        }

        float px = player.position.x;
        float shift = moduleWidth * modules.Count;
        float half = moduleWidth * 0.5f;

        // Une seule passe par image suffit en marchant ; la boucle rattrape un deplacement brusque
        for (int guard = 0; guard < modules.Count; guard++)
        {
            float midX = modules[modules.Count / 2].position.x;

            if (px > midX + half + margin)
            {
                Transform first = modules[0];
                modules.RemoveAt(0);
                first.position += Vector3.right * shift;
                modules.Add(first);
            }
            else if (px < midX - half - margin && modules[0].position.x - moduleWidth >= startLeftX - 0.01f)
            {
                Transform last = modules[modules.Count - 1];
                modules.RemoveAt(modules.Count - 1);
                last.position -= Vector3.right * shift;
                modules.Insert(0, last);
            }
            else break;
        }
    }

    private void SortModules()
    {
        modules.Sort((a, b) => a.position.x.CompareTo(b.position.x));
    }
}

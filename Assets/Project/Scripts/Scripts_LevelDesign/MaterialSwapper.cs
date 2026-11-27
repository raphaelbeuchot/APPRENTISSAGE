using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Remplace d'un coup les materiaux de tous les MeshRenderer / SkinnedMeshRenderer sous des racines
/// par un seul materiau (ex. gris unlit), sauf exceptions (GO glisses ou layers, enfants compris).
/// Le joueur est toujours epargne. Particules, LineRenderer, TMP, sprites : jamais touches.
/// Swap() / Restore() a appeler depuis un UnityEvent, une Timeline (Signal) ou le menu (trois points) du composant,
/// en Play uniquement : hors Play, Swap est refuse.
/// </summary>
public class MaterialSwapper : MonoBehaviour
{
    [Header("Cible")]
    [Tooltip("Racines dont tous les renderers (enfants compris) recoivent le materiau de remplacement")]
    [SerializeField] private GameObject[] roots;
    [Tooltip("Materiau pose dans tous les slots des renderers concernes")]
    [SerializeField] private Material replacement;

    [Header("Exceptions")]
    [Tooltip("GOs epargnes, enfants compris")]
    [SerializeField] private GameObject[] keepObjects;
    [Tooltip("Layers epargnes : un renderer est epargne si lui OU un de ses parents est sur un de ces layers")]
    [SerializeField] private LayerMask keepLayers;

    private readonly Dictionary<Renderer, Material[]> originals = new Dictionary<Renderer, Material[]>();

    public bool IsSwapped => originals.Count > 0;

    [ContextMenu("Swap")]
    public void Swap()
    {
        // Hors Play, le remplacement s'ecrirait dans la scene a la sauvegarde (incident du 2026-11-27)
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[MaterialSwapper] Swap refuse hors Play : il modifierait la scene sauvegardee.", this);
            return;
        }
        if (replacement == null)
        {
            Debug.LogWarning("[MaterialSwapper] Pas de materiau de remplacement.", this);
            return;
        }
        if (IsSwapped) return;

        Transform player = null;
        PlayerPhysicsMovement movement = FindObjectOfType<PlayerPhysicsMovement>();
        if (movement != null) player = movement.transform;

        int kept = 0;
        HashSet<Renderer> seen = new HashSet<Renderer>();
        foreach (GameObject root in roots)
        {
            if (root == null) continue;
            foreach (Renderer rend in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!seen.Add(rend)) continue;
                if (!(rend is MeshRenderer) && !(rend is SkinnedMeshRenderer)) continue;
                if (IsKept(rend.transform, player)) { kept++; continue; }

                Material[] mats = rend.sharedMaterials;
                originals[rend] = mats;
                Material[] swapped = new Material[mats.Length];
                for (int i = 0; i < swapped.Length; i++) swapped[i] = replacement;
                rend.sharedMaterials = swapped;
            }
        }
        Debug.Log($"[MaterialSwapper] {originals.Count} renderer(s) remplace(s), {kept} epargne(s) par exception.", this);
    }

    [ContextMenu("Restore")]
    public void Restore()
    {
        foreach (KeyValuePair<Renderer, Material[]> pair in originals)
            if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
        if (originals.Count > 0)
            Debug.Log($"[MaterialSwapper] {originals.Count} renderer(s) remis d'origine.", this);
        originals.Clear();
    }

    private bool IsKept(Transform t, Transform player)
    {
        if (player != null && t.IsChildOf(player)) return true;
        foreach (GameObject keep in keepObjects)
            if (keep != null && t.IsChildOf(keep.transform)) return true;
        for (Transform p = t; p != null; p = p.parent)
            if ((keepLayers.value & (1 << p.gameObject.layer)) != 0) return true;
        return false;
    }

    // Remettre les materiaux si l'objet est detruit (rechargement de scene, sortie du Play) :
    // sharedMaterials sur des renderers de scene ne touche pas les assets, mais on reste propre.
    void OnDestroy() => Restore();
}

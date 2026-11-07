using System.Collections;
using UnityEngine;

// Renderers qui passent au materiau de windup (Sphere, Cube, BallStack...).
// Le materiau vient du SO (windupMaterial, commun au type d'ennemi), la liste vient du prefab :
// chaque variante de skin complete la sienne.
// Composant absent ou liste vide : HitAttack / GrabAttack gardent l'ancien comportement (premier renderer trouve).
public class WindupFlashTargets : MonoBehaviour
{
    [SerializeField] private Renderer[] targets;

    private Material[][] originals;
    private bool isFlashing;
    private Coroutine timedFlash;

    public bool HasTargets => targets != null && targets.Length > 0;

    // Appelable a chaque frame : reapplique le materiau si un autre feedback l'a remplace entre-temps
    public void Flash(Material mat)
    {
        if (mat == null || !HasTargets) return;

        if (!isFlashing)
        {
            originals = new Material[targets.Length][];
            for (int i = 0; i < targets.Length; i++)
                if (targets[i] != null) originals[i] = targets[i].sharedMaterials;
            isFlashing = true;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] == null || targets[i].sharedMaterial == mat) continue;
            Material[] mats = new Material[originals[i].Length];
            for (int j = 0; j < mats.Length; j++) mats[j] = mat;
            targets[i].sharedMaterials = mats;
        }
    }

    public void Restore()
    {
        if (timedFlash != null)
        {
            StopCoroutine(timedFlash);
            timedFlash = null;
        }
        if (!isFlashing) return;

        for (int i = 0; i < targets.Length; i++)
            if (targets[i] != null && originals[i] != null) targets[i].sharedMaterials = originals[i];
        isFlashing = false;
    }

    // Flash a duree fixe, tenu par ce composant : survit aux StopAllCoroutines du script appelant (ChargeAttack)
    public void FlashFor(Material mat, float duration)
    {
        if (mat == null || !HasTargets) return;
        if (timedFlash != null) StopCoroutine(timedFlash);
        timedFlash = StartCoroutine(FlashForCoroutine(mat, duration));
    }

    private IEnumerator FlashForCoroutine(Material mat, float duration)
    {
        Flash(mat);
        yield return new WaitForSeconds(duration);
        timedFlash = null;
        Restore();
    }
}

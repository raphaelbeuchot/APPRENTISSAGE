using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// Variante world-space de EnemyHealthBarPipsUI : des SpriteRenderer positionnes directement
// dans le monde (pas de Canvas/RectTransform), avec un billboard simple qui oriente tout le
// GameObject vers targetCamera. Une seule camera assignee par instance (jamais deux a la fois) :
// EnemyHealthBarManager cree une instance par joueur, chacune sur son layer dedie avec un
// culling mask exclusif par camera (voir MULTIJOUEUR_LOCAL.md), donc ce billboard scripte simple
// est correct sans avoir besoin d'un shader multi-camera.
public class EnemyHealthBarPipsWorldUI : EnemyHealthBarUI
{
    [Header("Config (unites monde)")]
    [SerializeField] private float pipDiameter = 0.3f;
    [SerializeField] private float outlineExtra = 0.05f;
    [SerializeField] private float lockOutlineExtra = 0.12f;
    [SerializeField] private float overlapRatio = 0.5f;
    [SerializeField] private float healthPerPip = 50f;

    [Header("Sprite")]
    [SerializeField] private Sprite pipSprite;
    [SerializeField] private Sprite lockOutlineSprite;

    [Header("Colors")]
    [SerializeField] private Color pipColorActive = new Color(1f, 0.5f, 0f);
    [SerializeField] private Color pipColorChase = new Color(1f, 0.2f, 0f);
    [SerializeField] private Color outlineColorChase = Color.red;

    [SerializeField] private Color bonusColorActive = new Color(0.5f, 0f, 1f);
    [SerializeField] private Color outlineColorDefault = Color.black;
    [SerializeField] private Color lockOutlineColor = Color.white;
    [SerializeField] private Color damageFlashColor = Color.red;
    [SerializeField] private float damageFlashDuration = 0.15f;
    [SerializeField] private float damageFadeDuration = 0.4f;

    [Header("Billboard")]
    [Tooltip("Vide = Camera.main. Assignee par EnemyHealthBarManager a la camera du joueur proprietaire de cette copie.")]
    [SerializeField] private Camera targetCamera;

    [Header("Taille a l'ecran")]
    [Tooltip("Distance en-dessous de laquelle la taille apparente arrete de grossir (clamp). Au-dela, perspective normale (plus petit si loin, plus gros si proche).")]
    [SerializeField] private float minScaleDistance = 4f;
    [Tooltip("Securite anti-division par zero / anti-taille-nulle si la camera est exactement sur le pip.")]
    [SerializeField] private float minScaleFloor = 0.1f;

    private Coroutine fadeCoroutineMain;

    private List<SpriteRenderer> lockOutlineRenderers = new List<SpriteRenderer>();
    private List<SpriteRenderer> outlineRenderers = new List<SpriteRenderer>();
    private List<SpriteRenderer> pipRenderers = new List<SpriteRenderer>();
    private List<bool> isBonus = new List<bool>();
    private List<bool> pipActive = new List<bool>();
    private List<Coroutine> fadeCoroutines = new List<Coroutine>();
    private int totalPips = 0;
    private int normalPipCount = 0;
    private GameObject lockOutlinesGO;

    public override void SetTargetCamera(Camera camera)
    {
        targetCamera = camera;
    }

    // Billboard + clamp de taille apparente : un objet monde grossit naturellement en
    // perspective quand la camera approche (normal, pas un bug), mais devient illisible si
    // l'ennemi passe tout pres de la camera. En-dessous de minScaleDistance, on reduit la
    // taille monde en proportion de la distance reelle pour figer la taille a l'ecran a ce
    // qu'elle valait a minScaleDistance, plutot que de continuer a grossir.
    private void LateUpdate()
    {
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null) return;

        transform.rotation = cam.transform.rotation;

        float distance = Vector3.Distance(transform.position, cam.transform.position);
        float scale = distance < minScaleDistance ? (distance / minScaleDistance) : 1f;
        scale = Mathf.Max(scale, minScaleFloor);
        transform.localScale = Vector3.one * scale;
    }

    public override void UpdateWorldPosition(Vector3 worldPosition, Camera screenCamera, Canvas screenCanvas)
    {
        transform.position = worldPosition;
    }

    public override void Initialize(float baseMax, float realMax)
    {
        foreach (Transform child in transform)
            DestroyImmediate(child.gameObject);

        gameObject.SetActive(false);

        lockOutlineRenderers.Clear();
        outlineRenderers.Clear();
        pipRenderers.Clear();
        isBonus.Clear();
        pipActive.Clear();
        fadeCoroutines.Clear();

        lockOutlinesGO = new GameObject("LockOutlinesContainer");
        lockOutlinesGO.layer = gameObject.layer;
        lockOutlinesGO.transform.SetParent(transform, false);
        lockOutlinesGO.SetActive(false);

        GameObject outlinesGO = new GameObject("OutlinesContainer");
        outlinesGO.layer = gameObject.layer;
        outlinesGO.transform.SetParent(transform, false);

        GameObject pipsGO = new GameObject("PipsContainer");
        pipsGO.layer = gameObject.layer;
        pipsGO.transform.SetParent(transform, false);

        normalPipCount = Mathf.Max(1, Mathf.RoundToInt(baseMax / healthPerPip));
        int bonusPipCount = realMax > baseMax ? Mathf.Max(1, Mathf.RoundToInt((realMax - baseMax) / healthPerPip)) : 0;
        totalPips = normalPipCount + bonusPipCount;

        float step = pipDiameter * overlapRatio;
        float outlineDiameter = pipDiameter + outlineExtra;
        float lockOutlineDiameter = pipDiameter + lockOutlineExtra;

        for (int i = 0; i < totalPips; i++)
        {
            float x = i * step;
            bool bonus = i >= normalPipCount;
            int drawOrder = totalPips - i;

            SpriteRenderer lockOutlineSr = CreatePipSprite("LockOutline_" + i, lockOutlinesGO.transform, lockOutlineSprite, lockOutlineDiameter, x, drawOrder);
            lockOutlineSr.color = lockOutlineColor;
            lockOutlineRenderers.Add(lockOutlineSr);

            SpriteRenderer outlineSr = CreatePipSprite("Outline_" + i, outlinesGO.transform, pipSprite, outlineDiameter, x, totalPips + drawOrder);
            outlineSr.color = outlineColorDefault;
            outlineRenderers.Add(outlineSr);

            SpriteRenderer pipSr = CreatePipSprite("Pip_" + i, pipsGO.transform, pipSprite, pipDiameter, x, 2 * totalPips + drawOrder);
            pipSr.color = bonus ? bonusColorActive : pipColorActive;
            pipRenderers.Add(pipSr);

            isBonus.Add(bonus);
            pipActive.Add(true);
            fadeCoroutines.Add(null);
        }
    }

    private SpriteRenderer CreatePipSprite(string name, Transform parent, Sprite sprite, float diameter, float x, int sortingOrder)
    {
        GameObject go = new GameObject(name);
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = sortingOrder;

        if (sprite != null)
        {
            Vector2 nativeSize = sprite.bounds.size;
            float scaleX = nativeSize.x > 0f ? diameter / nativeSize.x : 1f;
            float scaleY = nativeSize.y > 0f ? diameter / nativeSize.y : 1f;
            go.transform.localScale = new Vector3(scaleX, scaleY, 1f);
        }

        return sr;
    }

    public override void UpdateHealth(float currentHealth, float baseMax, float realMax)
    {
        if (pipRenderers.Count == 0) return;

        int activePips = Mathf.CeilToInt(Mathf.Max(0f, currentHealth) / healthPerPip);
        activePips = Mathf.Clamp(activePips, 0, totalPips);

        for (int i = 0; i < totalPips; i++)
        {
            bool shouldBeActive = i < activePips;

            if (!shouldBeActive && pipActive[i])
            {
                pipActive[i] = false;
                if (fadeCoroutines[i] != null)
                    StopCoroutine(fadeCoroutines[i]);
                fadeCoroutines[i] = StartCoroutine(DamageFlashFade(i));
            }
            else if (shouldBeActive && !pipActive[i])
            {
                pipActive[i] = true;
                if (fadeCoroutines[i] != null)
                {
                    StopCoroutine(fadeCoroutines[i]);
                    fadeCoroutines[i] = null;
                }
                pipRenderers[i].color = isBonus[i] ? bonusColorActive : pipColorActive;
            }
        }
    }

    private IEnumerator DamageFlashFade(int index)
    {
        SpriteRenderer pip = pipRenderers[index];
        pip.color = damageFlashColor;
        yield return new WaitForSeconds(damageFlashDuration);

        float elapsed = 0f;
        Color start = pip.color;
        Color end = new Color(start.r, start.g, start.b, 0f);
        while (elapsed < damageFadeDuration)
        {
            elapsed += Time.deltaTime;
            pip.color = Color.Lerp(start, end, elapsed / damageFadeDuration);
            yield return null;
        }
        pip.color = end;
        fadeCoroutines[index] = null;
    }

    public override void SetLockedOutline(bool locked)
    {
        if (lockOutlinesGO != null)
            lockOutlinesGO.SetActive(locked);
    }

    private void ForceDeadPipsInvisible()
    {
        for (int i = 0; i < pipRenderers.Count; i++)
        {
            if (!pipActive[i] && pipRenderers[i] != null)
            {
                Color c = pipRenderers[i].color;
                c.a = 0f;
                pipRenderers[i].color = c;
            }

            if (fadeCoroutines[i] != null)
            {
                StopCoroutine(fadeCoroutines[i]);
                fadeCoroutines[i] = null;
            }
        }
    }

    public override void Show()
    {
        if (this == null || gameObject == null) return;
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            // Unity tue toute coroutine en cours quand le GameObject est desactive -- si
            // Hide() a coupe un DamageFlashFade en plein vol, le pip reste figue a une couleur
            // intermediaire (rouge visible) au lieu de finir son fondu vers transparent. On
            // force les pips deja "morts" a rester invisibles a chaque reactivation, qu'ils
            // aient fini leur fondu normalement ou pas.
            ForceDeadPipsInvisible();
        }
        if (fadeCoroutineMain != null) StopCoroutine(fadeCoroutineMain);
        fadeCoroutineMain = StartCoroutine(FadeGroup(1f, 0.25f, false));
    }

    public override void Hide()
    {
        if (this == null || gameObject == null) return;
        if (!gameObject.activeSelf) return;
        if (fadeCoroutineMain != null) StopCoroutine(fadeCoroutineMain);
        fadeCoroutineMain = StartCoroutine(FadeGroup(0f, 0.25f, true));
    }

    // Fondu de groupe (Show/Hide) : anime l'outline et le lock-outline sans condition, mais
    // seulement les pips encore "actifs" (pas deja etints par les degats) -- sinon un Show()
    // ressusciterait a l'oeil des pips deja perdus. Remplace le multiplicateur qu'un CanvasGroup
    // donnait gratuitement en UI.
    private IEnumerator FadeGroup(float targetAlpha, float duration, bool disableOnEnd)
    {
        float startAlpha = outlineRenderers.Count > 0 ? outlineRenderers[0].color.a : 1f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float a = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            ApplyGroupAlpha(a);
            yield return null;
        }
        ApplyGroupAlpha(targetAlpha);
        if (disableOnEnd)
            gameObject.SetActive(false);
        fadeCoroutineMain = null;
    }

    private void ApplyGroupAlpha(float alpha)
    {
        foreach (SpriteRenderer sr in lockOutlineRenderers)
            SetAlpha(sr, alpha);
        foreach (SpriteRenderer sr in outlineRenderers)
            SetAlpha(sr, alpha);
        for (int i = 0; i < pipRenderers.Count; i++)
        {
            if (pipActive[i])
                SetAlpha(pipRenderers[i], alpha);
        }
    }

    private void SetAlpha(SpriteRenderer sr, float alpha)
    {
        Color c = sr.color;
        c.a = alpha;
        sr.color = c;
    }

    public override void SetChaseOutline(bool isChasing)
    {
        // Seuls les pips encore actifs (vie restante) passent en rouge -- un pip deja perdu
        // garde son outline par defaut, sinon son anneau (jamais fondu par DamageFlashFade,
        // seul le remplissage l'est) redevient visible/rouge et donne l'impression d'un pip
        // de vie perdue qui "revient".
        for (int i = 0; i < outlineRenderers.Count; i++)
        {
            if (outlineRenderers[i] == null) continue;
            Color c = (isChasing && pipActive[i]) ? outlineColorChase : outlineColorDefault;
            c.a = outlineRenderers[i].color.a;
            outlineRenderers[i].color = c;
        }

        for (int i = 0; i < pipRenderers.Count; i++)
        {
            if (pipRenderers[i] != null && pipActive[i])
            {
                Color target = isChasing ? pipColorChase : (isBonus[i] ? bonusColorActive : pipColorActive);
                target.a = pipRenderers[i].color.a;
                pipRenderers[i].color = target;
            }
        }
    }
}

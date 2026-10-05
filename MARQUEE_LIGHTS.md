# Marquee Lights — Shader Graph (Unity URP)

Contexte projet pour Claude Code. Langue de travail : français.

## Objectif

Une ligne d'ampoules (marquee lights) dessinée par un seul shader sur un quad.
Le nombre d'ampoules s'adapte automatiquement à la longueur du mesh, l'espacement se règle en mètres.

## État

- [x] v1 — Espacement paramétrable, nombre d'ampoules automatique (fonctionne)
- [ ] Chenillard (lumière qui court le long de la ligne)
- [ ] Vacillement individuel par ampoule

## Setup

- **Pipeline** : URP. Graph de type *Unlit*, Surface Type *Transparent*.
- **Mesh** : Quad Unity 1×1 + MeshRenderer (pas de SpriteRenderer).
  - `Scale.x` = longueur de la ligne en mètres
  - `Scale.y` = diamètre des ampoules en mètres
  - Static Batching **désactivé** sur ces objets (sinon le shader perd le Scale objet).
- **Texture** : l'ampoule seule, carrée, centrée, 1–2 px de marge transparente, PNG.
  - Texture Type: Default · Alpha Source: Input Texture Alpha · Alpha Is Transparency: on
  - Wrap Mode: Clamp · Filter: Bilinear · Generate Mipmaps: off · sRGB: on
  - Compression: Normal Quality (ou None si artefacts sur les dégradés)

## Propriétés du Blackboard

| Nom       | Type        | Reference   | Défaut                        |
|-----------|-------------|-------------|-------------------------------|
| MainTex   | Texture2D   | `_MainTex`  | texture de l'ampoule          |
| Tint      | Color (HDR) | `_Tint`     | blanc                         |
| Spacing   | Float       | `_Spacing`  | 0.3 (mètres, centre à centre) |

## Logique du graph v1 (équivalent HLSL)

```hlsl
// Scale objet (node Object > Scale)
float3 scale = float3(length(UNITY_MATRIX_M._m00_m10_m20),
                      length(UNITY_MATRIX_M._m01_m11_m21),
                      length(UNITY_MATRIX_M._m02_m12_m22));
float len    = scale.x;
float height = scale.y;

float count     = max(1.0, round(len / _Spacing)); // nombre entier d'ampoules
float tiled     = uv.x * count;
float localX    = frac(tiled);                     // position dans la cellule (0..1)
float bulbId    = floor(tiled);                    // numéro de l'ampoule (0, 1, 2…)
float cellWidth = len / count;                     // espacement réel après arrondi
float ratio     = cellWidth / height;
float bulbU     = (localX - 0.5) * ratio + 0.5;    // recentrage, ampoule ronde

float4 tex  = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, float2(bulbU, uv.y), 0);
float  mask = 1.0 - step(0.5, abs(bulbU - 0.5));   // coupe hors de l'ampoule

float3 color = tex.rgb * _Tint.rgb;
float  alpha = tex.a * mask;
```

### Chaîne de nodes correspondante

1. Object (Scale) → Split : R = Length, G = Height. Length ÷ Spacing → Round → Maximum(1) = **Count**
2. UV → Split : R = U, G = V. U × Count → Fraction = **LocalX**
3. Length ÷ Count = CellWidth ; CellWidth ÷ Height = Ratio. LocalX − 0.5 → × Ratio → + 0.5 = **BulbU**
4. Vector2(BulbU, V) → **Sample Texture 2D LOD** (LOD 0)
5. BulbU − 0.5 → Absolute → Step(Edge 0.5) → One Minus = **Mask**
6. RGB × Tint → Base Color ; A × Mask → Alpha

### Choix techniques

- `Round` : nombre entier d'ampoules, pas d'ampoule coupée en bout de ligne.
- `Sample Texture 2D LOD` niveau 0 : la discontinuité de `frac` crée des lignes parasites avec les mipmaps.
- `−0.5 / ×ratio / +0.5` : étirement depuis le centre de la cellule, pas depuis le bord.
- `bulbId` (Floor) : identifiant stable par ampoule, base du chenillard et du vacillement.

## Spacing différent par objet

Le Spacing appartient au material. Deux options :

1. Dupliquer le material par variante (préféré, garde le SRP Batcher).
2. Script `BulbSpacing` avec `MaterialPropertyBlock` (désactive le SRP Batcher pour ces objets) :

```csharp
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public class BulbSpacing : MonoBehaviour
{
    public float spacing = 0.3f;

    void OnValidate() => Apply();
    void OnEnable() => Apply();

    void Apply()
    {
        var block = new MaterialPropertyBlock();
        var rend = GetComponent<Renderer>();
        rend.GetPropertyBlock(block);
        block.SetFloat("_Spacing", spacing);
        rend.SetPropertyBlock(block);
    }
}
```

## Prochaines étapes

### Chenillard

`luminosité = motif(temps × vitesse − bulbId × décalage)` — signe moins : vers la droite.

| Motif                    | Rendu                         | Base                                        |
|--------------------------|-------------------------------|---------------------------------------------|
| Onde douce               | ondulation                    | `sin(...) * 0.5 + 0.5`                      |
| Tout ou rien en séquence | enseigne de cinéma            | une ampoule sur N allumée, décalée d'un cran |
| Lumière qui court        | comète avec traînée           | une ampoule/groupe parcourt la ligne         |

Paramètres prévus : vitesse, décalage, période (N), direction, mix (0 = off).

### Vacillement

- `rand1 = RandomRange(seed = bulbId)`, `rand2 = RandomRange(seed = bulbId + (37.1, 11.7))`
- vitesse propre : `FlickerSpeed × lerp(1 − SpeedVariation, 1 + SpeedVariation, rand2)`
- `noise = SimpleNoise(float2(rand1 × 100, time × vitesse))`
- `flicker = lerp(1, noise, FlickerAmount)`

Luminosité finale = chenillard × vacillement. Halo : Tint HDR + Bloom URP.

## Notes pour Claude Code

- Les `.shadergraph` sont du JSON avec GUIDs : ne pas les éditer à la main. Décrire les modifications sous forme de chaîne de nodes, ou fournir du HLSL pour un Custom Function node.
- L'utilisateur débute en shaders : expliquer le *pourquoi* des opérations, avec des exemples chiffrés.
- Performance actuelle : 1 lecture de texture + ~15 opérations par pixel. Point de vigilance : overdraw de la transparence.

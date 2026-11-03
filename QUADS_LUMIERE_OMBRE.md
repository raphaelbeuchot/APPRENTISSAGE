# Quads de lumière et d'ombre (halos, glory light, ombres en biais)

Réflexion du 2026-11-03, issue de la note de review « Super Panopticon » (points 2 et 3). Rien n'est construit, rien n'est tranché.

## Le besoin

- **Halos** : un quad transparent avec une texture de halo pour simuler une lumière.
- **Glory light, ombres dramatiques en biais** (un peu façon Inside) : des quads avec un dégradé du noir (ou de la couleur) vers le transparent.
- **Problème commun** : quand le joueur entre dedans, ou quand le quad touche le décor, on voit une coupure nette, et on « lit » le quad.

## Ce qu'il y a dans le projet aujourd'hui

- Profil URP actif : **PC_RPAsset**, avec la **Depth Texture activée** et l'Opaque Texture activée. Le profil Mobile les a désactivées, mais il n'est pas utilisé.
- Shaders transparents existants : `TransparentGlow` et `InsideGlow` (Shader Graph, Transparent, ZTest normal, pas de ZWrite). Aucun n'utilise la profondeur de la scène.

## Pourquoi on voit le quad

1. **La coupure nette** : là où le quad croise le sol, un mur ou le joueur, le transparent s'arrête net sur une ligne. C'est l'effet « quad coupé ».
2. **Le quad vu par la tranche** : un halo ou un rayon est un plan. De biais, on voit que c'est une carte plate.
3. **Les bords de la texture** : s'ils n'arrivent pas parfaitement à alpha 0, on devine le rectangle.

## Les remèdes (tous dans le shader)

| Remède | Ce qu'il fait | Corrige |
|---|---|---|
| **Fondu de profondeur** (Depth Fade, « soft particles ») | Compare la profondeur du quad à celle de la scène derrière lui et s'efface en douceur près des intersections. Le joueur qui traverse un halo l'estompe autour de lui au lieu de le couper. | 1 (le remède principal) |
| **Rapprochement vers la caméra** | Avance le quad de quelques dizaines de cm vers la caméra, dans le shader seulement. Il passe devant ce qui le touche, sans se voir à travers les murs (contrairement à un ZTest Always, « toujours devant », qui traverse tout). | 1 |
| **Fondu de tranche** | Plus le quad est vu de biais, plus il s'efface. Pour les rayons et ombres en biais qui ne font pas face à la caméra. | 2 |
| **Billboard** (option) | Le quad tourne toujours face à la caméra. Idéal pour un halo autour d'une lampe. | 2 |
| **Dégradé calculé** au lieu d'une texture | Dégradé radial (halo) ou linéaire (rayon, ombre) qui tombe forcément à 0 sur les bords. Pas de texture à préparer. | 3 |

Mode de mélange : **Additif** pour la lumière (le halo éclaircit sans jamais assombrir), **Alpha** pour les ombres (le noir assombrit).

## Suggestion (non tranchée)

**Un seul shader graph** pour les deux usages, nom provisoire « quad doux » :

- mode de mélange au choix sur le matériau : Additif (lumière, glory light) ou Alpha (ombres) ;
- couleur ; dégradé radial ou linéaire, ou une texture au choix ;
- distance de fondu de profondeur ;
- rapprochement caméra ;
- fondu de tranche ;
- case billboard.

Ça reste un simple quad, léger, dans l'esprit « jeu vu de loin » : pas de lumière temps réel en plus, pas de volumétrique.

## Prochaine étape

Valider l'approche, puis détailler les propriétés avant de construire le shader.

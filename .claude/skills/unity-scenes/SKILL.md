---
name: unity-scenes
description: Lire les scenes et prefabs Unity du projet sans grep a la main - guid vers asset, qui utilise un prefab ou un script, overrides lisibles d'une instance de prefab dans une scene, fileID vers nom de GameObject. A utiliser des qu'une enquete passe par les fichiers .unity / .prefab (comportement different entre deux niveaux, "c'est un override ?", audit scene par scene, "dans quelles scenes est X ?").
---

# Lire les scenes et prefabs Unity

Script en lecture seule : il ne modifie jamais un fichier. Il vit hors d'Assets, Unity ne le voit pas.

Lancer depuis n'importe quel dossier du repo :

```
perl "$(git rev-parse --show-toplevel)/.claude/skills/unity-scenes/unityscan.pl" <commande> ...
```

Chemins acceptes : relatifs a la racine du repo, relatifs a Assets, ou absolus. Un prefab ou un script peut aussi
etre donne par son nom sans extension (`UI`, `StartZone_8`, `LevelEntranceDoor`) ou par son guid.

## Commandes

| Commande | Question | Exemple |
|---|---|---|
| `who <guid>` | A quel asset correspond ce guid ? | `who 4d19d98467aff354da8990f2e9cc18ba` -> UI.prefab |
| `uses <asset>` | Quelles scenes / prefabs / assets le referencent ? (avec le nombre de references) | `uses StartZone_8` |
| `overrides <scene> <prefab>` | Qu'est-ce que cette scene change sur ce prefab ? | `overrides Project/Scenes/Level_WesternFlow.unity UI` |
| `object <fichier> <fileID>` | C'est quoi cet objet ? -> `NomDuGO [Composant]` | `object Project/Prefabs/UI_Prefabs/UI.prefab 2288864631848179854` |

## Lire la sortie de `overrides`

- Groupe par objet cible : `StatsText [TMPro.TextMeshProUGUI]` puis ses proprietes `m_Enabled = 0`.
- `-> X [Y]` : la propriete pointe vers un objet (resolu en nom).
- `(vide)` : valeur vide, texte ou reference.
- `<imbrique : X.prefab>` : l'objet vient d'un prefab imbrique dans le prefab.
- `override orphelin` : l'objet n'existe plus dans le prefab. Unity garde l'override mais il n'a plus d'effet ;
  ce n'est jamais la cause d'un bug visible.
- Overrides normaux a ignorer en general : position / rotation de la racine, `m_Name`, `m_IsActive = 1` de la racine.

## Pieges

- `uses` sur un script compte les scenes ou il est pose ou surcharge directement. Un script pose dans un prefab
  apparait sous le prefab, pas sous les scenes : faire `uses` sur le prefab ensuite (ex. LevelEntranceDoor est
  dans StartZone_8, lui-meme dans 23 scenes).
- Un override n'est pas forcement ce qu'on voit en jeu : pour un niveau de la campagne, le LevelCatalog passe
  devant certaines valeurs de scene (titre du compte a rebours = Launch Text du catalogue, loadout balai/spray).
  Verifier dans le code qui lit la valeur avant de conclure "mauvais reglage".
- Les dossiers `_Recovery` sortent dans les resultats : copies de secours Unity, a ignorer.
- Une scene non sauvegardee dans Unity : le fichier sur disque est l'ancienne version. Demander de sauvegarder
  avant de conclure.

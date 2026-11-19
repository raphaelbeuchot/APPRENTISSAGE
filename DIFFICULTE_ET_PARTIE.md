# Difficulté et partie

> Chantier ouvert le 2026-11-19. À reprendre dans une nouvelle session.

## Ce qui est fait (2026-11-18/19)

- Trois difficultés dans `OptionsManager.Difficulty` : `Banco` (Easy), `Superbanco` (Normal, par défaut), `Hard`. Sauvegardée en int dans PlayerPrefs (`Difficulty`) : ajouter à la fin, ne jamais réordonner.
- Effets :
  - Easy : la sentinelle retire la moitié de la vie du joueur (deux touches).
  - Normal et Hard : sentinelle one-shot.
  - Hard : vie des ennemis ×2 (`GetEnemyHealthMultiplier`), appliquée sur la **vie de base** (`EnemyHealth.GetBaseMaxHealth`) → deux fois plus de pips normaux, pas de pips bonus. Le modificateur de niveau (`ModifierApplier`, ancienne roue de la fortune) reste par-dessus, en pips bonus.
  - Concerne tout ce qui passe par `EnemyHealth` : Hitters, Grabbers, Bloats, Blinders, zombies du boss. Pas le Swarm ni BrightEyes (vie gérée à part). Le multi suit.
- Menu Options : libellé Easy / Normal / Hard (`OptionsPanelUI.UpdateTexts`).
- Validé en jeu le 2026-11-19 (lancé depuis le MainMenu, difficulté changée avant le chargement du niveau).

## À faire tout de suite

- Ligne de Sheet VF à ajouter (FR à écrire par l'utilisateur) :

| Clé | EN | FR | Contexte | Source |
|---|---|---|---|---|
| options.hard | Hard | | Options : valeur de difficulté | OptionsPanelUI.cs |

  et mettre à jour le commentaire de `options.difficulty` : `{0}` = Easy / Normal / Hard.

## Constats

- La vie des ennemis est fixée à leur apparition ; les dégâts de la sentinelle sur le joueur sont lus à chaque tir. Changer la difficulté depuis la pause donne donc un niveau « à moitié » changé jusqu'au Restart.
- `OptionsManager` n'existe que dans le MainMenu : un niveau lancé directement depuis l'éditeur reste en ×1 et le panneau Options de la pause ne fait rien (textes figés, gauche / droite sans effet). Piste : le faire se créer tout seul s'il manque (non décidé).
- Dans le panneau Options, une valeur se change avec gauche / droite ; l'appui sur le bouton ne fait rien.

## Direction (décidée le 2026-11-19)

- La difficulté ne se change pas en cours de jeu : c'est un **choix de partie**, pour que les complétionnistes ne puissent pas tricher (repasser en Easy sur certains niveaux).
- Retirer la ligne de la pause ne suffit pas (on peut repasser par le menu principal) : le verrou complet passe par la **sauvegarde de partie** (difficulté choisie à « Nouvelle partie », enregistrée avec elle, non modifiable).

## Questions ouvertes (non tranchées)

1. Existe-t-il déjà un écran « Nouvelle partie » ou des emplacements de sauvegarde ? (Mémo : la sauvegarde par identifiant du catalogue de niveaux était encore à faire avant le build testeurs.)
2. Pour la démo, en attendant : retirer la difficulté de la pause et la laisser seulement dans le menu principal ?
3. Un « terminé en Hard » est-il suivi / affiché quelque part (lien avec la notation de performance, `project-notation-performance`) ?

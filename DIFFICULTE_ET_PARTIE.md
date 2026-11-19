# Difficulté et partie

> Chantier ouvert le 2026-11-19. Reprise : « on reprend la difficulté par partie ».

## Ce qui est fait

- Trois difficultés dans `OptionsManager.Difficulty` : `Banco` (Easy), `Superbanco` (Normal), `Hard`. Sauvegardée en int dans PlayerPrefs (`Difficulty`) : ajouter à la fin, ne jamais réordonner.
- Effets (inversés le 2026-11-19, validés en jeu) :

| | Sentinelle | Vie des ennemis |
|---|---|---|
| Easy | 2 touches (moitié de la vie) | ×1 |
| Normal | 2 touches | ×2 |
| Hard | one-shot | ×2 |

  - Vie ×2 appliquée sur la **vie de base** (`EnemyHealth.GetBaseMaxHealth`) → deux fois plus de pips normaux, pas de pips bonus. Le modificateur de niveau (`ModifierApplier`, ancienne roue de la fortune) reste par-dessus, en pips bonus.
  - Concerne tout ce qui passe par `EnemyHealth` : Hitters, Grabbers, Bloats, Blinders, zombies du boss. Pas le Swarm ni BrightEyes (vie gérée à part). Le multi suit.
- **Difficulté = choix de partie, façon Isaac** (commit 10203ab8, validé en jeu) :
  - Solo > New Game : les lignes du menu laissent la place à Easy / Normal / Hard (côte à côte, gauche / droite, Normal présélectionné). Valider enregistre la difficulté et lance la partie ; Retour revient au menu.
  - Continue et l'écran de choix de niveaux relisent la difficulté enregistrée.
  - Ligne Difficulté retirée de tous les panneaux Options (menu et pause) : masquée au lancement par `OptionsPanelUI`, les lignes suivantes remontent. Plus moyen de la changer en cours de partie.
  - Les trois lignes (Easy, Normal, Hard) sont dans le panneau Solo du MainMenu, branchées sur `SoloPanelUI`. Sans elles, New Game lance directement (avertissement Console).

## À faire tout de suite

- Ligne de Sheet VF à ajouter (FR à écrire par l'utilisateur) :

| Clé | EN | FR | Contexte | Source |
|---|---|---|---|---|
| options.hard | Hard | | Choix de difficulté à New Game | SoloPanelUI.cs |

  `options.easy` et `options.normal` sont maintenant lues par `SoloPanelUI` (source à mettre à jour dans la Sheet). `options.difficulty` n'est plus affichée nulle part.

## Suite (décidée le 2026-11-19)

1. **Trace permanente « jeu terminé en X »** : écrite quand on finit le dernier niveau de la campagne. Doit survivre à New Game (`ResetProgression`), donc stockée à part ; Easy, Normal, Hard peuvent s'accumuler au fil des parties. Pas de marque par niveau : c'est la partie complète qui compte.
2. **Affichage sur l'écran de choix de niveaux** (dispo en fin de jeu) : un seul emplacement pour toute la partie, pas par ligne (pas de souci avec la liste déroulante). Visuel à décider par l'utilisateur.

## Constats

- `OptionsManager` n'existe que dans le MainMenu : un niveau lancé directement depuis l'éditeur reste en ×1 et sentinelle par défaut. Piste : le faire se créer tout seul s'il manque (non décidé).
- Dans le panneau Options, une valeur se change avec gauche / droite ; l'appui sur le bouton ne fait rien.

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

## Bilan de partie et marques de fin de jeu (codé et validé le 2026-11-19)

Pas de marque par niveau : c'est la partie complète qui compte.

| Marque | Condition sur la partie |
|---|---|
| Finished | dernier niveau de la campagne réussi |
| Survivor | chaque niveau réussi au premier essai (`LevelStatsTracker.AttemptCount` : mort, Restart, quitter / Continuer rechargent le niveau = un essai) |
| Ghost | l'essai réussi de chaque niveau sans détection (recommencer est permis) |
| Ghost Survivor | les deux dans la même partie |

- `LevelProgressionManager` : bilan de partie (`RunSurvivor`, `RunGhost`, `RunUnranked`, `RunFinished`) enregistré avec la sauvegarde, remis à neuf à New Game, mis à jour à chaque niveau réussi. Au dernier niveau, les marques s'ajoutent à la difficulté de la partie (`GetEndingMarks(difficulté)`), **permanentes** : ni New Game ni le changement d'ordre du catalogue ne les effacent.
- Pas de bilan pour un niveau lancé hors MainMenu, ni en rejouant après la fin du jeu.
- Toute aide du DevPanel (option cochée, « Essais remis à 1 ») → partie non classée, aucune marque.
- Boss Rush Hour IV : pas d'écran de victoire classique ; la victoire est enregistrée par `BossEndRun.End` (trigger B). **Branchement pas encore vu en jeu** : à vérifier la prochaine fois que le boss est joué en entier depuis le MainMenu (Console : « Jeu termine en … »).
- Dernier niveau = dernier du catalogue : le boss en build démo, le dernier de la liste complète sinon.
- Test : DevPanel (F1) > « Simuler la fin du jeu (marques) » (bilan de la partie en cours, aides du DevPanel ignorées), « Voir les marques », « Effacer les marques ».

## Écran de fin : piste « fiche participant » (2026-11-19, rien de tranché)

Idée de l'utilisateur : plutôt qu'un texte organique, une fiche comptable dans l'esprit du lore (l'émission note son participant). Partie fixe = la fiche (lisible), le ton passe par les mentions et le verdict.

> Fiche participant — bilan de votre prestation
> Nombre de détections : 26 (si zéro : « Spectre »)
> Nombre de morts : 3 (si zéro : « Trompe la mort ! »)
> Verdict : …

Exemples de verdicts donnés : Spectre + Trompe la mort en Easy → « Trop facile ! Vous devriez essayer en mode Normal » (même logique en Normal → Hard) ; trop de morts → « Pas terrible » ; trop de détections → « Un éléphant dans un magasin de porcelaine ».

Spectre + Trompe la mort = Ghost Survivor, identifiable d'un coup d'œil.

**À décider** :
1. « Morts » = essais ratés (mort + Restart + quitter, cohérent avec Survivor, mais un Restart s'affiche comme une mort) ou vraies morts seulement (plus honnête, mais Restart avant de mourir → faux « Trompe la mort »). Variante : deux lignes, morts et abandons, « Trompe la mort » si les deux sont à zéro.
2. Détections = dans les essais réussis seulement (zéro = Spectre = Ghost, cohérent avec le code) ou dans tous les essais (chiffre plus parlant, Spectre plus dur que Ghost).
3. Règles du verdict, dans l'ordre (ébauche, textes à l'utilisateur) : Spectre + Trompe la mort en Easy / Normal / Hard ; morts > seuil ; détections > seuil ; sinon neutre ; partie non classée (DevPanel). Seuils réglables dans l'Inspector. Les deux à la fois (trop de morts et de détections) : premier verdict qui colle, ou texte dédié ?

Côté code : il faudra deux compteurs cumulés sur la partie (aujourd'hui le bilan ne garde que des oui / non).

## Suite

1. **Écran de fin du boss** plus travaillé (aujourd'hui `DemoEndPanel`, simple écran d'options) : un texte selon la difficulté, plus Ghost / Survivor / Ghost Survivor de la partie (`RunGhost`, `RunSurvivor`, `RunUnranked`). Textes et forme : l'utilisateur.
2. **Affichage des marques sur l'écran de choix de niveaux** (dispo en fin de jeu) : un seul emplacement, pas par ligne. Visuel à décider par l'utilisateur.

## Constats

- `OptionsManager` n'existe que dans le MainMenu : un niveau lancé directement depuis l'éditeur reste en ×1 et sentinelle par défaut. Piste : le faire se créer tout seul s'il manque (non décidé).
- Dans le panneau Options, une valeur se change avec gauche / droite ; l'appui sur le bouton ne fait rien.

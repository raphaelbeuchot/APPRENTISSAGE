# Multijoueur local — discussion & décisions

> Doc de design vivant. Recap de la discussion initiale, à compléter au fil des sessions.

---

## Décisions actées

- **Mode multi séparé du mode histoire solo** — le solo n'est pas modifié, le multi est un chemin à part.
- **Sélection de niveaux façon "course"** — le joueur choisit un niveau dédié au multi, dans un menu séparé du mode histoire.
- **Deux sous-modes possibles** :
  - **Coop** — victoire si les deux joueurs arrivent vivants.
  - **Versus** — victoire pour le premier joueur arrivé.
- **Deux manettes séparées** (pas de partage clavier).
- **Écran d'assignation manette → joueur** avant de lancer une partie (pas de pairing automatique).
- **Split-screen vertical** (deux colonnes gauche/droite), raccord avec le level design et les caméras existantes — décidé plutôt qu'une caméra unique partagée.
- **Caméra dupliquée par joueur**, pas de caméra à cadrage dynamique multi-cibles.
- **Priorité au fonctionnement brut** — l'esthétique (UI, feedback, polish) n'est volontairement pas traitée pour l'instant.
- **IA ennemie à gérer**, mais pas en priorité 1 — les niveaux multi réutiliseront potentiellement les ennemis/combat, donc l'IA devra être rendue multi-aware, mais ce n'est pas le premier chantier.

---

## Pourquoi split-screen plutôt que caméra partagée

- Le script caméra existant (`CameraFollow.cs`) suit déjà une cible unique — en split-screen, il suffit de dupliquer ce script tel quel (2 instances, chacune avec son `target` et son `Rect` de viewport), sans logique nouvelle.
- Une caméra partagée aurait demandé un algorithme neuf (bounding box des deux joueurs, zoom dynamique, gestion de l'écartement) — problème particulièrement aigu en versus/course, où le but est justement de se distancer de l'adversaire (zoom à l'infini illisible, ou murs invisibles/rubber-banding à inventer).
- Le jeu a un rendu léger : le coût de rendu d'une deuxième caméra n'est pas un problème.
- Contrepartie assumée : HUD à dupliquer/repositionner par moitié d'écran (pas traité pour l'instant, cf. priorité au fonctionnement brut).

---

## État des lieux du code — points de friction identifiés

*(Audit fait sur l'architecture actuelle, single-player. Sert de base pour scoper le chantier.)*

### Input
- `PlayerInputManager` (`Assets/Project/Scripts/Scripts_Game/PlayerInputManager.cs`) est un **singleton global** (`Instance`), lu directement par ~30 scripts (mouvement, attaques, caméra, UI...).
- Un seul flux d'input pour "le joueur" — pas de control schemes définis dans l'asset Input Actions, pas de pairing device→joueur.
- **Bloquant direct pour le multi** : nécessite une refonte de l'input (une instance par joueur, ou un système qui route par device).

### Caméra
- `CameraFollow.cs` est fait main (pas Cinemachine), suit un `Transform` unique, lit l'input singleton directement pour l'orbite/look.
- Plusieurs scripts caméra utilisent `Camera.main` (incompatible split-screen) ou `FindGameObjectWithTag("Player")`.
- Cinemachine existant dans le projet est utilisé uniquement pour les caméras scriptées/cutscenes (victoire, transitions), pas le gameplay.

### Résolution du joueur ("qui est le joueur ?")
- ~30 fichiers (IA ennemie, triggers de niveau, UI, mise en scène) résolvent "le joueur" via `FindObjectOfType<PlayerHealth>()` ou tag "Player", chacun séparément — pas de registre central.
- Avec 2 joueurs, ces lookups prendraient un joueur arbitraire, pas les deux.
- Comme le mode multi est séparé du solo, pas besoin de toucher à tous ces scripts si les niveaux multi n'en dépendent pas — mais ceux qui seront réutilisés (ennemis, combat) devront être adaptés.

### IA ennemie
- `EnemyAI_AStar.cs` : la boucle de détection scanne déjà plusieurs `PlayerHealth` via `Physics.OverlapSphere` et prend le plus proche — **presque prête pour le multi**, mais garde un raccourci `FindObjectOfType<PlayerHealth>()` à côté qui casserait ça.
- Attaques spéciales (`GrabAttack.cs` — mash pour s'échapper) lisent l'input global plutôt que celui du joueur spécifiquement attrapé.
- Autres scripts ennemis (`EnemyAI_PitMode`, `BrightEyesController`, `BombProjectile`, `SwarmController_AStar`) résolvent aussi "le joueur" individuellement via `FindObjectOfType`/tag.

### UI / HUD
- Un seul jeu de barres (vie, stamina, spray...) pour tout l'écran, pas de notion de HUD par joueur.
- Le prompt "mash to escape" ne distingue pas quel joueur est attrapé.
- *(Non prioritaire pour l'instant — cf. priorité au fonctionnement brut.)*

### Fin de niveau
- `LevelManager.cs` gère victoire/défaite sur un seul joueur wired en Inspector — à étendre pour gérer les deux conditions (coop : les deux vivants à l'arrivée ; versus : premier arrivé).

---

## Questions ouvertes / à creuser en prochaine session

- Écran d'assignation manette → joueur : comment chaque joueur "réclame" son slot (appui sur un bouton) ?
- Que se passe-t-il si une manette se déconnecte en cours de partie ?
- Comment fonctionne le menu de sélection de niveau pour le mode multi (liste, structure façon "course") ?
- Ordre de chantier technique une fois qu'on attaque l'implémentation (probablement : pairing input → duplication caméra/viewport → conditions de victoire par mode → adaptation IA).

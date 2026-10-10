# Ce qui reste à faire

> Memo vivant — idées, bugs, polish, game design.
> Mise à jour au fil des sessions.

---

## Légende priorités
- 🔴 Critique / bloquant
- 🟡 Important, à faire bientôt
- ⚪ Quand on aura le temps / polish

---

## 🐛 BUGS

- [ ] 🟡 **PauseMenuUI** — désactiver l'accès au pause menu dans la StartRoom, ou au moins bloquer tant que le fade-in initial n'est pas terminé
- [ ] 🟡 **Strange Attractor — silhouette blanche à la première attraction** (noté le 2026-10-09) : la première fois que le joueur est attiré, sa silhouette apparaît en **blanc** (comme une détection de la sentinelle) au lieu du **jaune** attendu ; les attractions suivantes sont bien en jaune. Non diagnostiqué.
- [ ] 🔴 **Bowling — joueur passé à travers le sol** (noté le 2026-10-09) : quand une boule de bowling est descendue du plafond, le joueur est passé à travers le sol. La sécurité existe : `EpervierManager.Update()` (utilisé dans Bowling_001, Epervier01/02, LavaTrain01/02/03 ; variante `EpervierManagerLoop` dans EpervierLoop01) lance chaque frame **un seul raycast vertical**, depuis le centre du joueur à 1,4 m de haut, sur 2 m (`escapeRaycastHeight`), layer `Obstacle` ; s'il touche une boule d'une ligne en état `Dropping`/`Rearranging`, `EscapeCoroutine` pousse le joueur vers l'avant (`ApplyProgressivePush`). **Pistes non vérifiées** : (1) un seul rayon central → une boule qui tombe légèrement décalée chevauche le joueur sans toucher le rayon ; (2) une boule rapide peut passer de « au-dessus de 3,4 m » à « dans le joueur » en une frame (pas de détection) ; (3) la boule n'est pas dans l'état `Dropping`/`Rearranging` au moment du contact. Piste de correctif : un `SphereCast` ou `OverlapCapsule` de la taille du joueur au lieu d'un rayon. Au passage : `Debug.Log("[Epervier] Raycast ne touche rien")` est écrit à chaque frame (spam console).
- [ ] 🟡 **Sentinelle / RotatingPlatform** — dans le niveau bombs+rotplat : un ennemi projeté par une bombe sur une rotating platform tourne correctement mais n'est plus détecté par la sentinelle — vérifier les états de l'ennemi après impact (état "stunned" / "on platform" qui coupe la détection ?)

---

## 🎮 GAME DESIGN

- [ ] 🟡 **Victory — textes adaptatifs** : remplacer "Well Done" et le texte du nombre d'essais par des textes variables selon le contexte (nb d'essais, performance, niveau…) — définir les règles de sélection des textes
- [ ] 🟡 **Victory sequence — ennemis survivants** — que faire des ennemis visibles mais non tués au moment de la victoire ? (les freezer ? les faire fuir ? les ignorer ? les faire disparaître ?)
- [ ] 🟡 **Ramassage du balai (narratif)** — InteractBubble sur le balai en StartRoom → appel `EquipBroom()` à ajouter dans `PlayerLoadout` (faisable à la volée : `SetLoadout` est déjà publique, il suffit d'activer le GO + notifier `PlayerInputManager`)
- [ ] ⚪ **EnemyAI_Astar** — revoir le comportement après hit raté : garder l'ennemi en Chase plutôt que retomber en Idle
- [ ] 🔴 **Boss final** — ajouter un boss de fin de jeu : à définir (arène, pattern d'attaques, phases, mise en scène d'intro/mort, musique dédiée)

---

## 🏗️ TECH / ARCHITECTURE

- [ ] ⚪ **KeeponTruckin SDF.asset — toujours dirty** : font TMP en mode Dynamic (m_AtlasPopulationMode: 1), Unity régénère la glyph table à chaque session. Fix rapide : `git update-index --skip-worktree` sur le fichier. Fix propre : passer en mode Static dans Unity (bake tous les caractères utilisés)

- [ ] ⚪ **PostVictorySequencer — ajout « montée des cercles concentriques » probablement redondant** (2026-10-09) : ajouté pour faire monter les `ConcentricCirclesSpawner` quand `Skip Decor Exit` est coché. Une fois `LevelEssentiels` tagué `Sentinel` dans toutes les scènes avec `VictoryManager`, les cercles montent déjà avec le groupe (ils sont sous `LevelEssentiels` partout sauf dans `StrangeAttractor`). À nettoyer : retirer l'ajout (`RiseAndHide` + boucle dans `Step5c5d`) après avoir rangé `StrangeAttractor` comme les autres niveaux.
- [ ] 🟡 **Victory sequence** — switch off des éléments de décor mobiles à la victoire : rotating platforms, conveyors, roaming obstacles

---

## 🎨 POLISH

*(son, feel, visuel, animations, feedbacks)*

- [ ] 🟡 **Player — animations tomates** : stance de visée (lock-on actif) + animation de lancer au moment du tir
- [ ] ⚪ **KeyCollectible — particules ramassage** : effet radial au pickup + petites étoiles qui suivent le joueur s'il se déplace après ramassage
- [ ] 🟡 **GoalDoor — animation au contact** : au lieu de disparaître dans la VictorySequence, déclencher une animation immédiate dès le OnTriggerEnter du collider (ouverture, explosion, feedback visuel fort)
- [ ] 🟡 **Audio — GoalDoor** : killer le crowd reaction dès que le joueur touche la GoalDoor, et déclencher un son dédié sur GoalDoor reach à la place
- [ ] ⚪ **Audio — BroomAttack sur Prop** : bruitage au hit d'un GO layer "Prop" — champ à assigner dans l'inspecteur de `PhysicsProp`. Backlog : envisager des ScriptableObjects par type de prop pour varier les sons

---

## 🗺️ NIVEAUX

*(idées propres à un niveau spécifique)*

- [ ] ⚪ ...

---

## 💡 BACKLOG / IDÉES VAGUES

*(pas encore décidé, "un jour peut-être")*

- [ ] 🟡 **Multijoueur local** — décisions actées, voir `MULTIJOUEUR_LOCAL.md` (mode séparé du solo, coop + versus, split-screen vertical, 2 manettes, IA à adapter)
- [ ] ...

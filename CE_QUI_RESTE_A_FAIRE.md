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
- [ ] 🟡 **Sentinelle / RotatingPlatform** — dans le niveau bombs+rotplat : un ennemi projeté par une bombe sur une rotating platform tourne correctement mais n'est plus détecté par la sentinelle — vérifier les états de l'ennemi après impact (état "stunned" / "on platform" qui coupe la détection ?)

---

## 🎮 GAME DESIGN

- [ ] 🟡 **Victory — textes adaptatifs** : remplacer "Well Done" et le texte du nombre d'essais par des textes variables selon le contexte (nb d'essais, performance, niveau…) — définir les règles de sélection des textes
- [ ] 🟡 **Victory sequence — ennemis survivants** — que faire des ennemis visibles mais non tués au moment de la victoire ? (les freezer ? les faire fuir ? les ignorer ? les faire disparaître ?)
- [ ] 🟡 **Ramassage du balai (narratif)** — InteractBubble sur le balai en StartRoom → appel `EquipBroom()` à ajouter dans `PlayerLoadout` (faisable à la volée : `SetLoadout` est déjà publique, il suffit d'activer le GO + notifier `PlayerInputManager`)
- [ ] ⚪ **EnemyAI_Astar** — revoir le comportement après hit raté : garder l'ennemi en Chase plutôt que retomber en Idle

---

## 🏗️ TECH / ARCHITECTURE

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

- [ ] ...

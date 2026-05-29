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

- [ ] 🟡 **Sentinelle / RotatingPlatform** — dans le niveau bombs+rotplat : un ennemi projeté par une bombe sur une rotating platform tourne correctement mais n'est plus détecté par la sentinelle — vérifier les états de l'ennemi après impact (état "stunned" / "on platform" qui coupe la détection ?)

---

## 🎮 GAME DESIGN

- [ ] ⚪ **EnemyAI_Astar** — revoir le comportement après hit raté : garder l'ennemi en Chase plutôt que retomber en Idle

---

## 🏗️ TECH / ARCHITECTURE

- [ ] 🟡 **Victory sequence** — switch off des éléments de décor mobiles à la victoire : rotating platforms, conveyors, roaming obstacles

---

## 🎨 POLISH

*(son, feel, visuel, animations, feedbacks)*

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

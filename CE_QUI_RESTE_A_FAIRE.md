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
- [x] 🟡 **Strange Attractor — silhouette blanche à la première attraction** — **résolu le 2026-10-12** (noté le 2026-10-09) : la première fois que le joueur est attiré, sa silhouette apparaît en **blanc** (comme une détection de la sentinelle) au lieu du **jaune** attendu ; les attractions suivantes sont bien en jaune. **Diagnostiqué et corrigé le 2026-10-12** : le prefab `Player/Prefabs/Player.prefab` avait `isCurrentlyDetected: 1` enregistré (champ public de `PlayerDetectionFeedback`, donc sauvegardé) → chaque niveau démarrait « déjà détecté par la sentinelle » sans aucun `OnDetected`. Invisible tant que le premier rafraîchissement de la silhouette vient de la sentinelle (qui lève la détection) ; dans Strange Attractor, il vient du BrightEyes → le blanc gagne sur le jaune jusqu'au feu vert. Correctif : `isCurrentlyDetected` en `[System.NonSerialized]` (idem `EnemyDetectionFeedback`). Piste : le **laser de sentinelle blanc au respawn** en multi (`MULTIJOUEUR_LOCAL.md`, clos sans reproduction) lit ce même champ (`SentinelLaserManager`) → peut-être la même cause.
- [ ] 🔴 **Bowling — joueur passé à travers le sol** (noté le 2026-10-09) : quand une boule de bowling est descendue du plafond, le joueur est passé à travers le sol. La sécurité existe : `EpervierManager.Update()` (utilisé dans Bowling_001, Epervier01/02, LavaTrain01/02/03 ; variante `EpervierManagerLoop` dans EpervierLoop01) lance chaque frame **un seul raycast vertical**, depuis le centre du joueur à 1,4 m de haut, sur 2 m (`escapeRaycastHeight`), layer `Obstacle` ; s'il touche une boule d'une ligne en état `Dropping`/`Rearranging`, `EscapeCoroutine` pousse le joueur vers l'avant (`ApplyProgressivePush`). **Pistes non vérifiées** : (1) un seul rayon central → une boule qui tombe légèrement décalée chevauche le joueur sans toucher le rayon ; (2) une boule rapide peut passer de « au-dessus de 3,4 m » à « dans le joueur » en une frame (pas de détection) ; (3) la boule n'est pas dans l'état `Dropping`/`Rearranging` au moment du contact. Piste de correctif : un `SphereCast` ou `OverlapCapsule` de la taille du joueur au lieu d'un rayon. Au passage : `Debug.Log("[Epervier] Raycast ne touche rien")` est écrit à chaque frame (spam console).
- [ ] 🟡 **Sentinelle / RotatingPlatform** — dans le niveau bombs+rotplat : un ennemi projeté par une bombe sur une rotating platform tourne correctement mais n'est plus détecté par la sentinelle — vérifier les états de l'ennemi après impact (état "stunned" / "on platform" qui coupe la détection ?)

---

## 🎮 GAME DESIGN

- [ ] 🟡 **Victory — textes adaptatifs** : remplacer "Well Done" et le texte du nombre d'essais par des textes variables selon le contexte (nb d'essais, performance, niveau…) — définir les règles de sélection des textes
- [x] 🟡 **Victory sequence — ennemis survivants** — que faire des ennemis visibles mais non tués au moment de la victoire ? (les freezer ? les faire fuir ? les ignorer ? les faire disparaître ?) — **Décidé le 2026-10-12 : figés au contact de la GoalDoor, puis désactivés quand le fond de victoire couvre la scène** (ils continuaient à poursuivre le joueur). **Fait et validé le 2026-10-12** : `PostVictorySequencer.FreezeEnemies()` (appelé par `LevelManager.OnPlayerReachedGoal`, pas en course multi) — trouve zombies, essaims et `SwarmSpawner`, BrightEyes, `BombSpawner` et bombes par leurs composants ; coupe leurs scripts **et leurs coroutines** (désactiver un script ne les arrête pas : ex. détection du `BombSpawner`), met les animations en pause, rend les rigidbodies cinématiques ; retire l'attraction BrightEyes du joueur ; **désinscrit les pips de vie** des ennemis figés (éléments d'UI sous le Canvas, pas enfants des ennemis : `EnemyHealthBarManager.UnregisterEnemy`). Désactivation dans `OnVictoryScreenCovered`.
- [ ] 🟡 **Ramassage du balai (narratif)** — InteractBubble sur le balai en StartRoom → appel `EquipBroom()` à ajouter dans `PlayerLoadout` (faisable à la volée : `SetLoadout` est déjà publique, il suffit d'activer le GO + notifier `PlayerInputManager`)
  - **Direction du 2026-10-15** : le balai devient un accessoire d'épisode, comme les tomates → **socle de prise** placé selon le niveau (début ou milieu) + **socle de dépôt** en sortie (« Pour continuer, merci de déposer votre balai », hyper rapide et arcade) ; en multi, balai toujours équipé. Voir `STRUCTURE_CAMPAGNE.md`.
- [ ] ⚪ **EnemyAI_Astar** — revoir le comportement après hit raté : garder l'ennemi en Chase plutôt que retomber en Idle
- [ ] 🔴 **Boss final** — ajouter un boss de fin de jeu : à définir (arène, pattern d'attaques, phases, mise en scène d'intro/mort, musique dédiée)

---

## 🏗️ TECH / ARCHITECTURE

- [ ] ⚪ **KeeponTruckin SDF.asset — toujours dirty** : font TMP en mode Dynamic (m_AtlasPopulationMode: 1), Unity régénère la glyph table à chaque session. Fix rapide : `git update-index --skip-worktree` sur le fichier. Fix propre : passer en mode Static dans Unity (bake tous les caractères utilisés)

- [ ] 🟡 **Ordre de la campagne solo fragile** (2026-10-10) : la liste vit en overrides du `LevelProgressionManager` dans `MainMenu` et désigne les niveaux par **numéro de Build Settings** (`sceneIndex`) → réordonner les Build Settings décale la campagne sans prévenir. **Deuxième liste divergente** dans le prefab `Resources/LevelProgressionManager` (16 niveaux, ancien ordre), utilisée quand le manager s'auto-instancie (niveau lancé directement en éditeur) → « niveau suivant » différent du jeu réel. Piste : un catalogue unique par nom de scène.
  - **En cours (2026-10-10)** : asset `Project/Resources/LevelCatalog.asset` (24 niveaux, scène par référence, alertes dans l'Inspector), lu par le jeu (`LevelProgressionManager`, menu « Scene », texte de lancement). La liste de MainMenu et celle du prefab `Resources` ne servent plus. Suite et décisions dans `PASSAGE_EN_PRODUCTION.md` (section « Catalogue de niveaux »).
- [x] ⚪ **Doublon de nom « Bowling II »** dans la campagne (Level_Epervier01 et Level_Epervier02) — corrigé le 2026-10-10 dans le catalogue : Epervier02 renommé « Bowling III ».
- [ ] ⚪ **Code mort de l'ancien parcours tuto** : les tutos ne sont plus atteignables (« New Game » charge le premier niveau de la campagne), mais `LevelManager` traite encore le build n° 3 (TutoNew) à part, et `TutoChoiceUI` charge des scènes par des numéros de build qui ne correspondent plus.
- [x] ⚪ **PostVictorySequencer — ajout « montée des cercles concentriques » probablement redondant** — **retiré définitivement le 2026-10-12** : colonne Cercles du LD Catalog propre (tous les cercles sous un objet tagué Sentinel, qui monte avec ses enfants). Avant : retiré puis rétabli le 2026-10-11 (montée de victoire unifiée abandonnée, voir `PASSAGE_EN_PRODUCTION.md`). Historique (2026-10-09) : ajouté pour faire monter les `ConcentricCirclesSpawner` quand `Skip Decor Exit` est coché. Une fois `LevelEssentiels` tagué `Sentinel` dans toutes les scènes avec `VictoryManager`, les cercles montent déjà avec le groupe (ils sont sous `LevelEssentiels` partout sauf dans `StrangeAttractor`). À nettoyer : retirer l'ajout (`RiseAndHide` + boucle dans `Step5c5d`) après avoir rangé `StrangeAttractor` comme les autres niveaux.
- [ ] ⚪ **Ranger les hiérarchies avec des parents vides : ce qui peut casser** (analyse du 2026-10-10). En général sans risque : les références assignées dans l'Inspector, `FindObjectOfType`, les tags et layers (propres à chaque objet, jamais hérités) survivent au déplacement. **Points sensibles** :
  - `transform.root` : `PostVictorySequencer` fait monter la **racine** de l'objet tagué `Sentinel` (donc tout `LevelEssentiels`) ; `DecorExitSequencer` traite le décor **par racine** (une racine qui contient Player / Camera / Canvas / EndCurtain est exclue en entier) et exclut tout ce qui a un **ancêtre** sur le layer `Ground`. Regrouper le décor sous un parent commun change ces résultats.
  - `transform.Find("nom")` : recherche d'enfants par nom/chemin, mais toujours à l'intérieur d'un même objet (pits, ponts du Western Canyon, ragdolls, entrées de LevelSelect) → ne pas renommer/déplacer les enfants **internes** de ces objets.
  - `GameObject.Find("MainCamera")` dans `PlayerPhysicMovement` : recherche par **nom** → ne pas renommer la caméra.
  - `GetComponentInParent` / `GetComponentsInChildren` (une trentaine de fichiers) : surtout à l'intérieur d'un personnage ou d'un prefab ; un parent vide ajouté au-dessus n'y change rien tant qu'il ne porte pas de composant.
  - Dans une instance de prefab, on ne peut pas déplacer les enfants hérités (règle Unity).
- [ ] 🟡 **Victory sequence** — switch off des éléments de décor mobiles à la victoire : rotating platforms, conveyors, roaming obstacles

---

## 🎨 POLISH

*(son, feel, visuel, animations, feedbacks)*

- [ ] 🟡 **Player — animations tomates** : stance de visée (lock-on actif) + animation de lancer au moment du tir
- [ ] ⚪ **KeyCollectible — particules ramassage** : effet radial au pickup + petites étoiles qui suivent le joueur s'il se déplace après ramassage
- [ ] 🟡 **GoalDoor — animation au contact** : au lieu de disparaître dans la VictorySequence, déclencher une animation immédiate dès le OnTriggerEnter du collider (ouverture, explosion, feedback visuel fort)
  - **Idée précisée (2026-10-10)** : au contact, **glow qui saute à +1.5 puis redescend, en même temps qu'un fade out** de la porte. Faisable dans `GoalDoorNew.ReachGoal()` (coroutine). Points à vérifier avant : le shader/matériau de la porte a-t-il une émission (glow) et supporte-t-il la transparence (fade : Surface Type Transparent, ou dissolve/dither dans un Shader Graph) ? Aujourd'hui la porte est détruite à l'étape 5a de `PostVictorySequencer` (après le wipe) → à adapter si elle disparaît déjà au contact.
- [ ] 🟡 **Écran de fin du victory scale à revoir** (2026-10-10) : l'écran de victoire (`VictoryUI` : fond coloré + « LEVEL COMPLETE » + nombre d'essais, attente d'un input, puis wipe) ne plaît pas. À repenser (lien avec « Victory — textes adaptatifs » et le lore : texte de la machine ?).
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

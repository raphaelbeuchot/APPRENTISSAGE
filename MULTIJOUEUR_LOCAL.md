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

## Décisions de scope pour la V1 (première version jouable)

- **Un seul niveau vitrine** pour valider que ça fonctionne, avant d'investir dans plusieurs niveaux.
- **Un seul sous-mode d'abord : Versus** (premier arrivé gagne) — plus simple que la coop (pas de double condition "les deux vivants"), et évite de toucher à l'IA ennemie pour cette première version.
- **Niveau vitrine adapté d'un niveau solo existant**, plutôt qu'un niveau construit de zéro — réutilise du level design déjà fait, focus sur la technique (pairing, caméra, victoire).
- **Point d'entrée dans le menu** : pas encore tranché — décision reportée après validation technique (cf. méthode petites étapes : on ne décide pas l'intégration menu avant d'avoir un truc qui marche).

## Cœur du jeu : cycles de détection de la sentinelle ("1, 2, 3 Soleil")

*C'est LE mécanisme central du jeu (pas un ajout) — priorité de vérification avant tout le reste.*

- **Audit du code (2026-09-18)** : `SentinelDetector.cs` scanne via `Physics.OverlapSphere` + un `Dictionary<GameObject, TargetTrackingData>` — architecture déjà multi-cibles par nature (c'est comme ça qu'il gère ennemis + joueur solo aujourd'hui). La résolution du "tir"/capture cible tout objet avec un composant `PlayerHealth`, pas une référence figée — donc un 2e joueur humain avec son propre `PlayerHealth` a de bonnes chances d'être déjà détectable/capturable sans code neuf.
- **Manque identifié** : `GameManager` n'a qu'**une seule** référence "player" (`PlayerPhysicsMovement`/`PlayerHealth`/`PlayerDetectionFeedback`, tout au singulier). `SentinelDetector` s'appuie dessus pour des raffinements réservés à ce joueur unique : immunité en s'accroupissant, immunité si attrapé (grab), retour visuel "tu es détecté", check sur le singleton `PlayerInputManager` (état balai bas). **Décision : ces raffinements devront être présents pour les deux joueurs** — pas acceptable qu'un des deux joueurs n'ait pas l'immunité crouch ou le feedback de détection.
- **Rythme vert/rouge (`SentinelCycleManager.cs`)** : le cycle lui-même est global (partagé, pas de duplication nécessaire), mais sa **durée dynamique** se calcule sur la distance d'un seul `playerTransform` à l'objectif — ignore totalement un 2e joueur. **Piste à confirmer plus tard** : calculer sur la moyenne (ou une autre combinaison) des positions des deux joueurs, plutôt que sur un seul.
- **✅ Test concret effectué (2026-09-18)**, dans `NIVEAUTESTMECANIQUES.unity` : capsule de test ajoutée avec un `PlayerHealth` (champs `stats` et `settings` copiés depuis le `PlayerHealth` du joueur solo — sans ça, `Awake()` bloque ou `TakeSentinelShot` crash en null reference). **La détection fonctionne** : la capsule se fait repérer et tirer pendant le Red Light, comme le joueur solo.
- **Bug croisé confirmé et localisé** : quand la capsule se fait tirer, c'est le **joueur 1** qui joue l'animation "is shot", pas la capsule. Cause exacte : `SentinelShooter.ShootPlayer()` (`Assets/Project/Scripts/Scripts_Game/SentinelShooter.cs:107-113`) appelle sans condition `gm.playerDetectionFeedback.OnShotBySentinel()` (référence unique câblée sur le joueur 1) et démarre `PlayerStunBySentinel()` qui met `gm.stunBySentinel = true` **globalement**, quel que soit qui a été touché. Seuls les dégâts/knockback (`humanHealth.TakeSentinelShot(...)`, ligne 114) sont correctement scopés sur le bon objet. **Confirme et précise** le "manque identifié" plus haut : `playerDetectionFeedback` et `stunBySentinel` devront devenir des références/états par joueur, pas globaux sur `GameManager`.
- **✅ Fix `stunBySentinel` (2026-09-18)** : déplacé de `GameManager` (bool globale) vers un champ local sur chaque instance de `PlayerPhysicsMovement`. `SentinelShooter.PlayerStunBySentinel()` prend maintenant en parametre le `PlayerPhysicsMovement` du joueur precisement touche et n'ecrit que sur lui. Points de lecture bascules en consequence : `PlayerPhysicsMovement.cs` (gel du mouvement, blocage du dash, feedback de sweep local via `GetComponent<PlayerDetectionFeedback>()`), `SentinelDetector.cs` (immunite pendant le stun, lit desormais `gm.player.stunBySentinel`), `GrabAttack.cs` (annulation de grab, lit desormais `player.stunBySentinel` au lieu du champ mort sur `GameManager`). Champ `GameManager.stunBySentinel` supprime (plus aucun lecteur/ecrivain).
- **✅ Fix animation croisee (2026-09-18)** : `SentinelShooter.ShootPlayer()` resolvait `gm.playerDetectionFeedback` (reference unique cablee sur joueur 1) au lieu du `PlayerDetectionFeedback` du `human` reellement passe en parametre — meme pattern que `ShootEnemy` qui fait deja `enemy.GetComponent<EnemyDetectionFeedback>()` correctement. Corrige en resolvant `human.GetComponent<PlayerDetectionFeedback>()` localement dans la methode.
- **✅ Test concret de validation (2026-09-18)** : 2e joueur = duplicata complet du vrai `Player.prefab` (pas une capsule bricolee) place expose au champ de vision de la sentinelle dans `NIVEAUTESTMECANIQUES`, pendant que le joueur 1 reste cache derriere un obstacle (input partage via le singleton `PlayerInputManager`, donc les deux bougent en miroir — seule la position/LOS differencie qui se fait tirer). Resultat : joueur 2 touche se gele correctement (stun scope) et joue sa propre animation "Shot" (feedback scope) ; joueur 1 cache reste libre de bouger pendant ce temps. Les deux bugs confirmes plus haut sont resolus.
- **Reste non traite (identite encore singuliere sur `gm.player`)** : le halo/silhouette "en cours de detection" (`isCurrentlyDetected`, avant meme le tir) dans `SentinelDetector` reste gate par `col.gameObject == gm.player.gameObject` — ne se declenchera donc qu'au joueur 1 pour l'instant. Idem pour l'immunite crouch, l'immunite sweep/groggy, le calcul de mouvement specifique joueur, et `playerAlarmTriggered` (toujours une bool unique sur le detector, pas par joueur). `ExecutePlayerShotOnGroggy` (tir special sur groggy) reste aussi cable uniquement sur `gm.player`/`gm.playerHealth`. **Prochain chantier** : generaliser ces comparaisons d'identite en detection par composant (`col.GetComponent<PlayerHealth>() != null`), comme deja fait ailleurs dans le meme fichier pour la resolution du tir.

## Ambition à terme (pas un bloquant V1) : combat joueur-joueur

- **Idéal exprimé (2026-09-18)** : les deux joueurs partagent le même espace physique en Versus et peuvent se gêner — se pousser, se frapper au balai, se lancer des tomates, etc. C'est l'horizon du mode Versus, pas juste une course parallèle sans contact.
- **Pourquoi ce n'est pas dans la V1** : les scripts d'attaque actuels (`BroomAttack`, `SprayAttack`, `ThrowTomato`, `GrabAttack`) ciblent des ennemis IA ou LE joueur solo — pas "l'autre joueur humain". Faire qu'un coup touche effectivement le bon joueur (et pas soi-même, ni un ennemi absent du niveau) est un chantier à part entière, plus gros que le trigger de fin de course.
- **Validation explicite** : "déjà si on peut faire la course, c'est top" — la course simple (sans interaction) est un jalon suffisant et satisfaisant en soi, le combat viendra après.

## Journal d'avancement — scène de test technique

*Scène : `Level_Multi_Test`. Objectif : valider isolément le pairing input à deux joueurs, avant de construire quoi que ce soit d'autre.*

- **Contrainte matérielle de test** : un seul pad disponible + clavier (pas deux pads). Ça ne contredit pas la décision "deux manettes séparées, pas de partage clavier" (qui vise à éviter que deux joueurs partagent un seul clavier) — ici chaque joueur a un device entier à lui (clavier pour l'un, pad pour l'autre). À re-tester avec un second pad avant la version visée pour de vrai.
- **✅ Étape 1a validée (2026-09-18)** : pairing par device fonctionnel. Deux capsules de test apparaissent (une par device rejoint via le composant natif Unity `PlayerInputManager`, join-by-device), chacune pilotée indépendamment — l'une par le clavier, l'autre par le pad, sans interférence.
  - Setup : composant `PlayerInput` (Unity) sur chaque capsule, pointant vers l'asset `PlayerInputActions` **du projet** (celui du solo) — pas l'asset "InputSystem_Actions" project-wide que Unity assigne par défaut (Unity déconseille ce dernier avec `PlayerInput` car c'est une référence singleton partagée, à l'opposé de l'isolation par joueur qu'on veut). Default Map réglé sur "Player" (le seul map existant dans l'asset). Default Scheme laissé à `<Any>` car aucun control scheme n'est défini dans l'asset.
  - Script de test : `Assets/Project/Scripts/Multi/MultiTestMover.cs` — lit l'action "Movement" de sa propre instance `PlayerInput`, déplace la capsule, log le device associé.
  - Point notable : l'asset `PlayerInputActions.inputactions` n'a **aucun control scheme défini** (toutes les bindings clavier/pad sont sur les mêmes actions, sans séparation) — et ça n'a pas empêché l'isolation par device de fonctionner. Bonne surprise, pas besoin de complexifier l'asset pour l'instant.
  - Aucune modification du `PlayerInputManager` singleton du solo — le multi reste un chemin technique à part, comme décidé.
- **✅ Étape 1b validée (2026-09-18)** : vérification de non-régression sur le solo — le pad continue de piloter normalement le solo player (le singleton `PlayerInputManager` écoute toujours n'importe quel device, comme avant, non affecté par l'ajout des composants `PlayerInput` du test multi). Déconnexion du pad en cours de route testée manuellement : ne fait pas planter le jeu, absorbée par défaut sans code spécifique.
- **✅ Étape 2a validée (2026-09-18)** : split-screen vertical fonctionnel avec deux caméras fixes (`Cam_Player1`/`Cam_Player2`), chacune avec un `Viewport Rect` réglé sur une moitié d'écran (X=0/W=0.5 et X=0.5/W=0.5), une seule avec `Audio Listener` actif. Point noté : les deux capsules spawnent à la même position (celle du prefab, pas de spawn points distincts encore) — à traiter plus tard. Caméras volontairement immobiles à cette étape (pas de suivi), comportement attendu.
- **Correction d'architecture caméra** : le script `CameraFollow.cs` trouvé initialement est un vieux script plus utilisé. Le gameplay actuel utilise **Cinemachine** — un vcam `CM_NormalMode` avec une extension `CameraPanningExtension.cs` (montée verticale de départ, toggle vue basse, masquage d'obstacles, target group dynamique — tout ça lié au solo/single-player, pas reproduit pour le test). Pour le test multi, on est resté volontairement sur du Cinemachine simple (juste `Follow`, sans extension), l'ajout des comportements avancés étant un chantier séparé, plus tard.
- **✅ Étape 2b validée (2026-09-18)** : suivi dynamique par joueur en split-screen fonctionnel. Setup : un `CinemachineBrain` sur chaque caméra physique (`Cam_Player1`/`Cam_Player2`), deux `CinemachineCamera` simples (`CM_P1`/`CM_P2`, sans extension), séparées par **canal** (Output Channel sur les vcams, Channel Mask sur les Brains — mécanisme Cinemachine natif pour le split-screen, sans quoi les deux Brains se disputent le même vcam prioritaire). Le champ `Follow` ne peut pas être assigné dans l'Inspector à l'avance (les capsules n'existent qu'au runtime, créées à la jonction du device) — assigné dynamiquement via un nouveau script `Assets/Project/Scripts/Multi/MultiTestCameraRig.cs`, appelé depuis `MultiTestMover.OnEnable()` avec le `playerIndex` du `PlayerInput` qui vient de spawn. Léger effet de latence/rattrapage au repositionnement quand la capsule s'arrête : normal, dû au damping par défaut de Cinemachine (amortissement volontaire), pas un bug.

- **✅ Étape 3a validée (2026-09-18)** : détection de ligne d'arrivée fonctionnelle — trigger (`Assets/Project/Scripts/Multi/MultiTestFinishLine.cs`) qui identifie quel `playerIndex` l'a atteint. Piège rencontré : `OnTriggerEnter` ne se déclenchait pas du tout tant que la capsule n'avait pas de `Rigidbody` (kinématique) — Unity exige qu'au moins un des deux colliders en ait un pour émettre les events de trigger, même si le déplacement est géré à la main via script. À reproduire sur le vrai prefab joueur multi plus tard.
- **✅ Étape 3b validée (2026-09-18)** : seul le premier passage compte (`WinnerPlayerIndex` stocké une fois, ignoré ensuite) — décision volontaire de rester minimal : les deux joueurs restent jouables après la victoire, pas de gel/écran de fin pour l'instant (à détailler plus tard, pas utile de le faire maintenant).

**→ Les trois chantiers techniques identifiés au départ (pairing input, caméra split-screen, condition de victoire) sont tous validés dans la scène de test `Level_Multi_Test`. La course à deux joueurs fonctionne de bout en bout.**

## Questions ouvertes / à creuser en prochaine session

- Écran d'assignation manette → joueur : comment chaque joueur "réclame" son slot (appui sur un bouton) ? *(en partie répondu par le join-by-device testé, mais l'UI/feedback visuel de cet écran reste à faire)*
- Que se passe-t-il si une manette se déconnecte en cours de partie ? *(testé de façon minimale sur le solo — ne plante pas — mais pas testé en contexte multi avec deux joueurs actifs)*
- Comment fonctionne le menu de sélection de niveau pour le mode multi (liste, structure façon "course") ?
- Spawn points distincts pour les deux joueurs (actuellement superposés au même point).
- Dupliquer par joueur les raffinements de détection sentinelle (`playerDetectionFeedback`, `stunBySentinel`, immunité crouch/grab) et revoir le calcul de durée des cycles (moyenne des deux joueurs plutôt qu'un seul).
- Chantier ultérieur (pas V1) : combat joueur-joueur (pousser, frapper, tomates).

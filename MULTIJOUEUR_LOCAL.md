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
- **⚠️ Commits a revert en cas de regression solo** (ils touchent du code partage avec le solo, `git revert <hash>` annule chacun proprement sans toucher au reste) :
  - `e7ea6292` — "sentinelle multi : stun et feedback de detection scopes par joueur" : `stunBySentinel` par joueur, anim "Shot" sur le bon joueur, `GrabAttack` adapte. Contient aussi la scene `NIVEAUTESTMECANIQUES` avec le 2e joueur de test.
  - `7f4e00e0` — "sentinelle multi : detection par composant au lieu de gm.player" : immunites, mouvement, halo de detection, `playerAlarmTriggered` par joueur, groggy/stun/laser. **Depend de `e7ea6292`** : si on revert les deux, commencer par `7f4e00e0`.
  - `e010d236` — "input multi : PlayerLocalInput et lecture d'input par joueur" : `PlayerPhysicsMovement` lit un input local s'il existe, sinon le singleton ; `SentinelDetector` lit l'input du joueur concerne. Solo teste OK (a noter : en solo, sprint (A) accroupi = se relever, pas de lunge, comportement d'origine). **Depend de `7f4e00e0`** (sa modif de `SentinelDetector` utilise `humanMovement`) : ordre de revert `e010d236`, puis `7f4e00e0`, puis `e7ea6292`.
  - `f54e98fc` — "input multi : PlayerLocalInput autonome, restreint a des devices" : reecrit `PlayerLocalInput` (composant multi uniquement, aucun effet en solo). Peut se revert seul (retour a la 1re version de `PlayerLocalInput`), mais dans un revert complet il passe **avant** `e010d236`. Ordre complet : `f54e98fc`, `e010d236`, `7f4e00e0`, `e7ea6292`.
  - `1dba98a8` — "victoire multi : LevelManager.multiRaceMode et MultiRaceManager" : ajoute une garde dans `LevelManager.OnPlayerReachedGoal` (champ `multiRaceMode`, désactivé par défaut) + le nouveau `MultiRaceManager`. Solo testé OK (victoire et niveau suivant inchangés). Indépendant des autres. Ordre complet de revert : `1dba98a8`, `f54e98fc`, `e010d236`, `7f4e00e0`, `e7ea6292`.
  - `f30ab445` — "respawn multi : MultiRespawn, immunite Red Light du kill, remise a zero glace/accroupi" : ajoute `PlayerHealth.Revive`, `PlayerRagdoll.Deactivate`, `DeathSequence.Restore`, une garde `multiRaceMode` dans `LevelManager.OnPlayerDeath`, le drapeau `respawnImmune` (`PlayerPhysicsMovement`) et sa lecture dans `SentinelDetector`. Tout est inerte en solo (jamais appele hors `MultiRespawn`). Solo teste OK (mort, rechargement via pupitre, vignette rouge inchangee). Independant des autres. **Ordre complet de revert : `f30ab445`, `1dba98a8`, `f54e98fc`, `e010d236`, `7f4e00e0`, `e7ea6292`.**
  - Non concernes (multi seulement, ne touchent pas le solo) : `4b372ba4` (scene de test `Level_Multi_Test` + scripts `Multi/`) et les commits de doc.
- **✅ Fix `stunBySentinel` (2026-09-18)** : déplacé de `GameManager` (bool globale) vers un champ local sur chaque instance de `PlayerPhysicsMovement`. `SentinelShooter.PlayerStunBySentinel()` prend maintenant en parametre le `PlayerPhysicsMovement` du joueur precisement touche et n'ecrit que sur lui. Points de lecture bascules en consequence : `PlayerPhysicsMovement.cs` (gel du mouvement, blocage du dash, feedback de sweep local via `GetComponent<PlayerDetectionFeedback>()`), `SentinelDetector.cs` (immunite pendant le stun, lit desormais `gm.player.stunBySentinel`), `GrabAttack.cs` (annulation de grab, lit desormais `player.stunBySentinel` au lieu du champ mort sur `GameManager`). Champ `GameManager.stunBySentinel` supprime (plus aucun lecteur/ecrivain).
- **✅ Fix animation croisee (2026-09-18)** : `SentinelShooter.ShootPlayer()` resolvait `gm.playerDetectionFeedback` (reference unique cablee sur joueur 1) au lieu du `PlayerDetectionFeedback` du `human` reellement passe en parametre — meme pattern que `ShootEnemy` qui fait deja `enemy.GetComponent<EnemyDetectionFeedback>()` correctement. Corrige en resolvant `human.GetComponent<PlayerDetectionFeedback>()` localement dans la methode.
- **✅ Test concret de validation (2026-09-18)** : 2e joueur = duplicata complet du vrai `Player.prefab` (pas une capsule bricolee) place expose au champ de vision de la sentinelle dans `NIVEAUTESTMECANIQUES`, pendant que le joueur 1 reste cache derriere un obstacle (input partage via le singleton `PlayerInputManager`, donc les deux bougent en miroir — seule la position/LOS differencie qui se fait tirer). Resultat : joueur 2 touche se gele correctement (stun scope) et joue sa propre animation "Shot" (feedback scope) ; joueur 1 cache reste libre de bouger pendant ce temps. Les deux bugs confirmes plus haut sont resolus.
- **✅ Generalisation par composant (2026-09-19)** : dans `SentinelDetector`, toutes les comparaisons `col.gameObject == gm.player.gameObject` sont remplacees par la presence du composant (`PlayerPhysicsMovement` / `PlayerDetectionFeedback` recuperes une fois par collider en debut de boucle). Couvre l'immunite crouch, sweep/groggy et grab, le calcul de mouvement raffine (plateformes mobiles, balai-bas), et le bloc `isInDanger` (halo de detection continu, qui ne tournait avant que pour le joueur 1). `playerAlarmTriggered` passe du detector (bool unique) a `PlayerPhysicsMovement` (par joueur) — verifie par grep qu'aucun autre systeme ne le lisait. `ResetAllTracking()` reset le feedback de tous les joueurs trackes. `SentinelShooter` : fin de `PlayerStunBySentinel` cible le joueur touche, `ExecutePlayerShotOnGroggy(PlayerPhysicsMovement)` prend le joueur en parametre (appele avec `this` depuis `OnGroggyStart`). `SentinelLaserManager` : `isPlayer` = presence de `PlayerDetectionFeedback` sur la cible (champs `gameManager`/`player` inutilises retires). Teste OK : 2 joueurs + solo sans anomalie.
- **Reste non traite** : `SentinelDetector.IsPlayerInSentinelLOS()` reste sur `gm.player` (consomme par `CrowdReactionManager`, avec `GameManager.IsPlayerMoving()` — ambiance/foule, pas urgent). `PlayerInputManager.Instance` (singleton) est toujours lu dans le calcul de mouvement joueur (exception balai-bas) : appartient au chantier input multi. `GameManager.player`/`playerHealth`/`playerDetectionFeedback` existent toujours (references "principales", utilisees par le cycle et quelques systemes) — a remplacer par une liste de joueurs quand on aura besoin de N joueurs cote GameManager (duree de cycle sur la moyenne des positions, condition de victoire).

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

## Input par joueur — décision (2026-09-19)

*Audit de `PlayerInputManager` : singleton (`Instance`) qui écoute tous les devices, 81 lectures dans 32 fichiers. Trois familles : mouvement joueur (`PlayerPhysicsMovement` 13 lectures, `TestClimbDetection`, `BroomLowFeedback`, `BroomLowWall`, plus 2 dans `SentinelDetector`), attaques/interactions (`MeleeAttackSystem`, `BroomAttackSystem`, `TomatoThrowSystem`, `TargetLockSystem`, `GrabAttack`, `PlayerPitInteractable`), et le reste (menus, pause, tutos, caméra, portes, cinématiques : reste sur le singleton, pas de sens par joueur).*

- **Option retenue : un petit composant d'input par joueur, séparé de `PlayerInputManager`** (qui n'est pas touché, donc solo protégé). Il expose seulement déplacement, sprint, accroupi. **Version 1 (`e010d236`) : adossé au `PlayerInput` natif (device apparié, validé en Étape 1a).** ⚠️ Limite découverte à l'audit (2026-09-19) : l'étape 1a ne validait que le flux avec le `PlayerInputManager` natif (join par device) qui crée les joueurs. Pour des `PlayerInput` posés à la main, sans control scheme dans l'asset, le 1er prend tous les devices libres, le 2e aucun, et ses actions (liées à aucun utilisateur) écoutent alors tous les devices : pas d'isolation. **Version 2 : composant autonome**, sans `PlayerInput` : copie propre de `PlayerInputActions` restreinte via `devices` (choix clavier+souris / manette + index dans l'Inspector, méthode `SetDevices` pour un futur écran d'assignation). **Validé (2026-09-19)** dans `Level_TestCameraMulti` (copie de `Level_Ice`, joueur 1 clavier+souris, joueur 2 manette, posés dans la scène) : chaque joueur répond à son device seulement, déplacement/sprint/accroupi indépendants. Note : pas de multi-manettes testé (une seule manette dispo).
- **À traiter plus tard (constaté en test)** : l'interaction pupitre ne marche qu'avec un joueur. Cause probable : `PupitreInteraction`, `PupitreTuto` et `PupitreShutterOnly` lisent `PlayerInputManager.Instance.InteractPressed` (singleton, tous devices) au lieu de l'input du joueur à portée. Fait partie du chantier « interactions/attaques par joueur ». `PlayerPhysicsMovement` lira ce composant s'il existe sur le joueur, sinon le singleton comme avant (en solo, aucun composant local : comportement identique).
- **Option écartée : rendre `PlayerInputManager` instanciable par joueur.** Plus « complète » (reprend tous les comportements d'un coup) mais touche le fichier qui fait tourner tout le solo. Son seul vrai avantage était de réutiliser la machine à états du balai bas ; cet avantage tombe avec la décision ci-dessous.
- **Décision de design : le balai bas (`BroomLowActive`) ne sera pas porté en multi.** À terme il sera probablement remplacé par un état « bouclier » (immunité aux déstabilisations physiques), et le balayage de corpses devrait perdre de l'importance. Côté multi, `BroomLowActive` vaut toujours `false`. Le bouclier, quand il sera défini, s'ajoutera au composant par joueur (état par joueur dès le départ).
- **Compromis assumé** : V1 = course sans combat, donc trois lectures suffisent. Si le combat joueur-joueur arrive (tomates, coups de balai, poussées), le composant devra grossir (attaques, interaction) et pourrait ressembler à un second `PlayerInputManager` : on décidera alors de le faire grossir ou de migrer le solo dessus. Rien n'est fermé.
- **Hors périmètre pour l'instant** : le `IsLocked` du singleton (pause) n'est pas reproduit ; les scripts d'attaque/interaction restent sur le singleton ; les 2 lectures de `SentinelDetector` (exception balai-bas) devront lire l'input du joueur concerné quand le composant existera.

## Split-screen avec les vrais joueurs — `Level_TestCameraMulti` (2026-09-19)

- **Scène** : copie de `Level_Ice` (solo), deux vrais joueurs posés dans la scène (duplicata du prefab Player), inputs indépendants via `PlayerLocalInput`. Choix « joueurs posés + duplicata » plutôt qu'apparition à l'exécution : garde toutes les références de scène du joueur 1 (GameManager, LevelManager, SentinelCycleManager, UI, target groups, ennemis) sans recâblage.
- **Setup caméras** : `Cam_Player2` = duplicata de `MainCamera` (Tag Untagged, Audio Listener retiré, `CinemachineBrain` sur Channel 01 seulement, Viewport Rect droite, `CameraConfiner.player` = joueur 2, `cameraTransform` du joueur 2 pointé dessus). `MainCamera` : Brain limité au canal Default, Viewport Rect gauche. `CM_Player2` = duplicata de `CM_NormalMode` (Output Channel = Channel 01, Tracking Target = joueur 2), **sans target group ni `CameraPanningExtension`** (donc pas de montée de départ, toggle vue basse, masquage d'obstacles côté joueur 2). Les autres vcams (`CM_LowView`, `CM_StartZone`, `CM_FinalZone`) restent sur Default : côté joueur 1 uniquement.
- **✅ Validé** : chaque caméra reste sur son joueur, chaque moitié d'écran suit le bon personnage.
- **À traiter plus tard — mort d'un joueur** : `DeathSequence` (Awake) récupère tous les `CinemachineTargetGroup` de la scène et, à la mort, remplace la cible 0 de chacun par ses propres hanches (`hipBone`). Quand le joueur 2 meurt, la caméra du joueur 1 (target group de `CM_NormalMode`) bascule donc sur le cadavre du joueur 2. Correctif probable, petit : ne remplacer la cible 0 que dans les groupes où elle est ce joueur. Même séquence : `SpawnCoins` + `creditBarUI.CountdownToZero` (la barre de crédits du HUD, propre au joueur 1, se vide), et `LevelManager` n'est abonné qu'à l'`OnDeath` du joueur 1 (la mort du joueur 2 ne déclenche pas la défaite). **Bloqué par une décision de design** : que se passe-t-il quand un joueur meurt en Versus (élimination, respawn, victoire de l'autre) ?

## Victoire et règle de course — décision (2026-09-19)

- **Décision : course de vitesse pure, porte ouverte dès le départ, pas de clé.** Atteindre la porte le plus vite possible. Les autres modes (ramasser des pièces, ramasser la clé en premier, etc.) viendront après, sur cette base : on évite le scope creep.
- **Pourquoi pas de clé pour l'instant** : `KeyCollectible` se déclenche pour tout objet de la couche Human et débloque **une porte unique partagée** : celui qui prend la clé ouvre pour les deux, donc le détour profite à l'adversaire (risque de blocage où personne ne la prend). Piste envisagée pour plus tard : une clé par joueur (drapeau « a sa clé » posé par la clé, vérifié par la porte). Limite du split-screen à garder en tête : la porte est un objet unique filmé par les deux caméras, donc son état visuel ne peut pas différer par joueur. Solutions : deux portes superposées sur deux couches avec un Culling Mask par caméra, ou retour visuel côté joueur (icône dans sa moitié d'écran) avec une porte visuellement neutre. Pas de plan caméra dédié à l'ouverture dans le code (seulement changement de matériau + son).
- **Porte à utiliser : `GoalDoorNew`** (l'ancienne `GoalDoor` est obsolète). Elle détecte déjà n'importe quel joueur par composant et son verrou `hasBeenReached` ne retient que la **première arrivée** : la règle du Versus existait déjà. À l'arrivée elle désactive le mouvement du gagnant, arrête le cycle de la sentinelle (globalement) et remet à zéro le feedback du gagnant.
- **Problème avec `LevelManager`** : `OnPlayerReachedGoal` ignorait qui gagne et déroulait le flux solo (désactive le joueur 1, enregistre progression et collectibles, `PlayerVictoryScale`, `VictoryUI` avec la santé du joueur 1, puis charge le niveau suivant ou la roue).
- **Implémentation** : `LevelManager.multiRaceMode` (défaut `false`, donc solo strictement inchangé) : activé, `OnPlayerReachedGoal` sort en première ligne. Nouveau `MultiRaceManager` (`Scripts/Multi/`) : écoute `GoalDoorNew.OnPlayerReached`, retient le gagnant une seule fois (`Winner`), le logue et déclenche `OnRaceWon`. Comme décidé à l'Étape 3b : pas d'écran de fin, le perdant reste jouable.
- **Setup de scène (`Level_TestCameraMulti`)** : `Is Active` coché sur `GoalDoorNew`, clé retirée, `multiRaceMode` coché sur le `LevelManager`, un `MultiRaceManager` dans la scène (piège rencontré : sans ce GameObject, la porte se déclenche mais personne n'écoute, aucun log de gagnant).
- **✅ Validé (2026-09-19)** : la course de bout en bout fonctionne avec les vrais joueurs : porte ouverte, premier arrivé retenu comme gagnant, flux solo non déclenché, perdant jouable, cycle sentinelle arrêté. Solo (victoire, niveau suivant) inchangé.

## Mort et respawn — décisions et implémentation (2026-09-20)

- **Décision : à la mort, le joueur respawne au point de respawn, sans pénalité.** Revenir au début de la pièce est déjà la sanction (on repart plus loin de l'arrivée). Équivalent du restart solo, mais sans recharger la scène : le point est placé devant le pupitre, orienté vers lui (Gizmo jaune de `PupitreInteraction` pour l'emplacement). Solo inchangé : la mort y recharge toujours la scène.
- **Décision : ragdoll comme en solo, puis remise en place du même joueur** (pas de destruction/recréation pour l'instant). `DeathSequence` attend que le bassin s'immobilise (`velocityThreshold`, minimum 0,3 s, plafond `ragdollTimeout` 5 s, à baisser dans l'Inspector en multi), puis `OnDeath` déclenche le respawn après `respawnDelay`. Destruction/recréation depuis le prefab viendra avec le spawner (vrai flux de join) ; seul le corps de `RespawnRoutine` changera.
- **Implémentation** : `MultiRespawn` (`Scripts/Multi/`) à poser sur chaque joueur, avec un `respawnPoint` (un vide par joueur, légèrement décalés). Il appelle `PlayerRagdoll.Deactivate()` (restaure layers, Rigidbody, collider, Animator + `Rebind`), téléporte, `DeathSequence.Restore()` (cible 0 des target groups + mouvement/mêlée), `PlayerHealth.Revive()`. Pièces et barre de crédits : champs `Coin Prefab` / `Credit Bar UI` de `DeathSequence` laissés vides en multi.
- **Piège : le solo recharge la scène, donc n'a jamais ces problèmes.** Réutiliser l'objet oblige à remettre à zéro ce que la mort n'annule pas. Déjà trouvés : **glace** (`isSlippery` reste vrai, pas de `OnCollisionExit` quand le collider est coupé) → `SetSlippery(false…)` ; **accroupi** (bascule, collider réduit et pose conservés) → `ExitCrouch()`. À surveiller : dash, groggy, balayage, ralentissements d'eau/essaim/Bright Eyes, contact ennemi. **Si cette liste grossit vite, passer à la recréation depuis le prefab.**
- **Sentinelle et respawn** : le suivi du joueur est retiré de `trackedTargets` / `alreadyShot` au respawn (la sentinelle détecte le mouvement par différence de position entre deux scans, une téléportation passerait pour un déplacement ; utilité non prouvée en test, gardé car sans coût).
- **Immunité de respawn** : un joueur qui respawne dans le **même Red Light que celui de son kill** est ignoré de la sentinelle jusqu'à la fin de ce Red Light (`respawnImmune`). Jamais sur un cycle suivant, jamais si tué hors Red Light. `MultiRespawn` retient l'état du cycle à l'instant exact de la mort ; tout changement d'état du cycle efface l'immunité.
- **Vignette rouge de dégâts** (`GameUIManager.damageVignette`) : overlay plein écran, écoute un seul `PlayerHealth` (`FindObjectOfType` si champ vide, donc arbitraire avec deux joueurs : c'était le joueur 2, et `Revive()` la déclenchait). **Décision : désactivée en multi** (GameObject de l'image décoché dans la scène, pas de code). Un feedback de dégâts par joueur reste dans la partie HUD par joueur.
- **✅ Validé (2026-09-20)** : respawn des deux joueurs, ragdoll, caméra, immunité, glace et accroupi. Solo testé sans anomalie.
- **Reste** : la caméra du joueur 1 bascule sur le cadavre du joueur 2 pendant la durée du ragdoll (`DeathSequence` remplace la cible 0 de tous les target groups) ; correctif possible : ne remplacer que dans les groupes où la cible 0 est ce joueur, à vérifier dans la scène. `LevelStatsTracker` et `PlayerHealthUI` (barre de vie, trouvée par `FindObjectOfType`) suivent un seul joueur.

## Pistes pour la prochaine session (état au 2026-09-19)

**État en une phrase** : course de vitesse jouable de bout en bout avec deux vrais joueurs dans `Level_TestCameraMulti` (input indépendant, split-screen, sentinelle, victoire), solo testé sans régression. La liste ci-dessous sert à décider sur quoi travailler ensuite ; ordre suggéré : petits chantiers concrets, puis décisions de design, puis structure, puis cosmétique (en dernier, comme convenu).

### Petits chantiers concrets
- **Spawn points distincts** : les deux joueurs sont posés à la main dans la scène de test, sans vrai spawn. *(Depuis le 2026-09-20 il existe des points de respawn par joueur devant le pupitre, utilisés à la mort ; le placement initial n'en dépend pas encore.)*
- **Test à deux manettes** : un seul pad était disponible (clavier pour l'un, pad pour l'autre). À refaire avec deux pads, et tester la déconnexion d'une manette en cours de partie en contexte multi (testé seulement en solo : ne plante pas).
- **`IsPlayerInSentinelLOS()` et `IsPlayerMoving()`** (`GameManager`) restent sur `gm.player` : la foule (`CrowdReactionManager`) ne réagit qu'au joueur 1.
- **`TestClimbDetection`** utilise `Camera.main` : l'escalade du joueur 2 se calcule par rapport à la caméra du joueur 1.
- **Interactions et attaques par joueur** : le singleton `PlayerInputManager` lit toujours tous les devices. Attaques, escalade, panning caméra, pause et interactions (les pupitres `PupitreInteraction` / `PupitreTuto` / `PupitreShutterOnly` ne marchent qu'avec un joueur) agissent sur le joueur 1 quelle que soit la manette. À traiter en étendant `PlayerLocalInput` (ou en le fusionnant avec le singleton, voir « Input par joueur »). L'état « bouclier » viendra s'y ajouter.

### Décisions de design à trancher (bloquantes pour du code)
- ~~**Mort d'un joueur en Versus**~~ **Tranché le 2026-09-20 : respawn au point de respawn, sans pénalité** (voir « Mort et respawn »). Reste le correctif des target groups (caméra du joueur 1 sur le cadavre du joueur 2).
- **Durée du cycle vert/rouge** (`SentinelCycleManager`, calculée sur un seul `playerTransform`) : moyenne des positions, joueur le plus avancé, le plus en retard ? Laissé de côté volontairement.
- **Autres modes de jeu** : la course pure est retenue. Ramasser des pièces, ramasser la clé en premier, clé par joueur… à venir, attention au scope creep. Limite du split-screen à garder en tête : la porte est un objet unique filmé par les deux caméras (voir « Victoire et règle de course »).
- **Ennemis en multi** : `targetHuman` fixé en scène, raccourcis `FindObjectOfType` dans `EnemyAI_AStar` et `GrabAttack`. Niveaux multi V1 sans ennemis ; à rendre multi-aware si on les réintroduit.
- **Combat joueur-joueur** (pousser, frapper, tomates) : ambition à terme, pas V1.

### Structure et flux de jeu
- **Écran d'assignation manette → joueur** et vrai flux de join : `PlayerLocalInput.SetDevices` est le point d'entrée prévu ; le join par device du `PlayerInputManager` natif est validé (Étape 1a), mais seulement pour des joueurs créés à l'exécution.
- **Menu de sélection de niveau multi** (liste, structure « course ») et **point d'entrée dans le menu** (non tranché). **Débuts de niveau** différents du solo (séquence d'ouverture, porte d'entrée).
- **`GameManager`** : remplacer `player` / `playerHealth` / `playerDetectionFeedback` par une liste de joueurs si on passe à 3-4 joueurs (durée de cycle, condition de victoire).
- **Joueurs posés à la main** : passer à un prefab/variant avec vrai spawn ; noms `Player_1` / `Player_2`.
- **Fin de course** : aujourd'hui un simple log, le perdant reste jouable.

### Cosmétique et polish (en dernier)
- **Caméra du joueur 2** sans `CameraPanningExtension` ni target groups (montée de départ, vue basse, masquage d'obstacles) ; `CM_LowView`, `CM_StartZone`, `CM_FinalZone` côté joueur 1 seulement.
- **HUD par joueur** (moitié d'écran) : barres de vie, crédits, UI liée à `MainCamera`.
- **Audio** : un seul `Audio Listener` (sur `MainCamera`), donc les sons 3D sont entendus depuis le point de vue du joueur 1.
- **Feedback visuel par joueur** (porte, clé) si une règle de clé revient.

### Hygiène (hors multi)
- `KeeponTruckin SDF.asset` (font TMP en atlas **Dynamic**) est réécrit par l'éditeur en permanence : à discarder avant chaque commit, ou à figer en Static une fois les caractères nécessaires générés.

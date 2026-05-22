# Recap session — Opening Sequence (2026-05-22)

## Contexte
Jeu Unity 3D "They Left the Game On (Again!)", branche `Version-2-TV-SHOW`.
Ajout d'une séquence d'ouverture : le joueur se réveille dans une cellule, appuie sur un bouton, une porte monte, il franchit un seuil et entre dans le niveau normal.

---

## Scripts créés / modifiés

### `OpeningSequenceManager.cs` (modifié)
Script central orchestrant toute la séquence.

**Flow :**
1. `Start()` — joueur couché : `playerMovement.enabled = false`, `animator.Play("StandUpOPENING", 0, 0f)`, `animator.speed = 0`
2. Coroutine `AttendreFinLever()` — attend 2s → `CommentPanel.ShowPersistent("Press X")` → attend X → `Hide()`, `speed = 1` → attend fin anim → `playerMovement.enabled = true`, `wakeUpDone = true`
3. `Update()` — détecte X près du bouton → `ActiverBouton()`
4. `ActiverBouton()` — son, transition caméra (camStartRoomVcam → 0, camStartZoneGO actif), lance `MonterPorte()`
5. `OnTriggerEnter` seuil → `FranchiSeuil()` — son grille, désactive cellule, active blockers

**Restart :** détecté dans `Awake()` (pas `Start()`) — `camStartRoomVcam.Priority = 0`, skip séquence.

### `DoorReturnTrigger.cs` (nouveau)
GO séparé avec collider trigger. Quand le joueur entre, la porte redescend à sa position initiale avec un son assignable.

### `PupitreStartRoomFX.cs` (nouveau)
Effet visuel/sonore sur le bouton de la cellule. Pulse émission (material émissif) + son toutes les X secondes. `Stop()` appelé par `ActiverBouton()`.

---

## Architecture caméra

| Caméra | Priorité Inspector | Rôle |
|---|---|---|
| CM_Startroom | 100 | GO désactivé — quads occultants utilisés à la place |
| CM_StartZone | 30 | Active après bouton pressé, spline dolly |
| CM_NormalMode | 10 | Active après pupitre pressé (PupitreInteraction) |
| LowView | 0 | Toggle joueur |

**Règle critique :** `cameraPanningGO` ne doit jamais être désactivé au `Start()`. `CameraPanningExtension.Start()` initialise les priorités caméra — si désactivé/réactivé tardivement, il réécrit les priorités après `PupitreInteraction.InitializeCamerasNextFrame()` et casse tout.

---

## Bugs résolus

### 1. InteractPressed ne déclenchait rien
**Cause :** `boutonTransform` non assigné dans l'Inspector → NullReferenceException silencieuse dans `Update()`, le code n'atteignait jamais la condition.
**Fix :** null-check explicite + assignation Inspector.

### 2. playerMovement.canMove=false inefficace
**Cause :** `PlayerPhysicMovement.cs` ligne 206 auto-reset `canMove = true` dès que le joueur est dans un état normal (grabState == None, etc.).
**Fix :** `playerMovement.enabled = false` pour désactiver le composant entièrement.

### 3. CameraPanningExtension cassait les priorités au restart
**Cause :** `cameraPanningGO` désactivé en `Start()`, réactivé plus tard → `Start()` de `CameraPanningExtension` tournait au mauvais moment et réécrivait `normalCamera.Priority = 20` après que `PupitreInteraction` l'ait posé à 10.
**Fix :** Ne plus jamais désactiver `cameraPanningGO`.

### 4. Restart lisait la clé PlayerPrefs trop tard
**Cause :** `CountdownManager.Start()` supprime la clé `"AutoStartCountdown"`. Si `OpeningSequenceManager` lisait en `Start()`, la clé pouvait déjà être effacée.
**Fix :** Lecture dans `Awake()` — tous les `Awake()` tournent avant tous les `Start()`.

### 5. Suppression d'un champ [SerializeField] → perte de références Inspector
**Cause :** Suppression de `camStartRoomVcam` du milieu du header `[Cameras]` → Unity a perdu les assignations des champs suivants.
**Fix :** Champ restauré. Règle : signaler toujours la suppression d'un champ sérialisé pour que l'utilisateur réassigne en Inspector.

---

## Prochaine session
- Blend spécial entre startroom et CM_StartZone (à définir : Custom Blend CinemachineBrain ou fade écran)
- Animator : brancher `StandUpOPENING` → transition vers `Locomotion` (Has Exit Time = 1.0, Transition Duration = 0)

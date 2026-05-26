# Victory Sequence — Documentation Technique

## Vue d'ensemble

Quand le joueur atteint la GoalDoor, une séquence en plusieurs étapes se déclenche :

```
GoalDoor → VictoryScale → VictoryUI → Wipe → PostVictorySequencer
```

---

## Scripts impliqués

| Script | GO | Rôle |
|---|---|---|
| `LevelManager.cs` | LevelManager | Orchestre toute la séquence |
| `PlayerVictoryScale.cs` | Player | Anime les ghost meshes + fire event OnVictoryScaleComplete |
| `VictoryUI.cs` | Canvas | Overlay victoire (fond + textes) |
| `PostVictorySequencer.cs` | LevelManager | Séquence post-wipe (lights dim, futur TransitionRoom) |
| `LevelStatsTracker.cs` | LevelManager | Suivi des stats (tentatives, kills, détections…) |
| `SentinelCycleManager.cs` | SentinelCycleManager | Cycle 1-2-3 soleil + stop visuel victoire |
| `CycleReactiveRenderer.cs` | Bulbes de la sentinelle | Réagit aux états du cycle (matériaux + pulse) |

---

## Flow détaillé

### 1. GoalDoor atteinte (`LevelManager.OnPlayerReachedGoal`)
- `LevelStatsTracker.OnLevelCompleted()` — enregistre stats + reset compteur tentatives
- `player.enabled = false` — freeze le joueur
- Lance `PlayerVictoryScale.TriggerScale()`

### 2. VictoryScale (`PlayerVictoryScale`)
- Cache le mesh joueur (`SkinnedMeshRenderer.enabled = false`)
- Spawn N ghost meshes (billboards, `VictoryGhostBillboard`) en vagues espacées de `delayBetweenWaves`
- **Skip** : n'importe quelle touche après `skipGracePeriod` (0.3s) spawn tous les ghosts restants immédiatement
- Ghost renderQueue = `3000 + (waveCount - 1 - index)` — s'affichent devant le fond UI
- Quand terminé : fire `OnVictoryScaleComplete` (ne détruit PAS les ghosts, c'est VictoryUI qui s'en charge)

### 3. VictoryUI — Show (`VictoryUI.ShowCoroutine`)

**Séquence :**
1. `SentinelCycleManager.StopCycleForVictory()` — arrête cycle + audio + fire `OnCycleChanged(GreenLight)` → tous les `CycleReactiveRenderer` passent en matériau neutre (plus de pulse)
2. **Fond fade in** (`bgFadeDuration` = 0.4s) — le fond coloré apparaît progressivement sur les ghosts
3. **Kill ghosts** — détruits une fois couverts par le fond
4. **Container visible** (`localScale = 1`) — les textes apparaissent dans le layout
5. **Slides séquentiels** (`textSlideDuration` = 0.35s) :
   - Titre "LEVEL COMPLETE" : slide depuis la gauche → centre + son `titleSlideSound`
   - Quand le titre est arrivé : "Essais : X" slide depuis la droite → centre + son `essaisSlideSound`
6. `timeScale = 0` — pause
7. Attend un input (n'importe quelle touche)

### 4. Wipe (`VictoryUI.WipeCoroutine`)
- `timeScale = 1`
- `CM_Victory.Priority = cm_victoryPriority` — Cinemachine blend vers la caméra victoire
- `slideContainer` slide vers la droite (hors écran) + `colorBackground` fade out simultanément
- Fire `OnWipeComplete`

### 5. Post-victoire (`PostVictorySequencer.StartPostVictorySequence`)
- **Étape 4** : Lights dim via Global Volume weight lerp (`dimDuration` = 1.5s)
- **Étape 5** : TransitionRoom
  1. Téléport player vers `transitionRoomSpawnPoint` (via `rb.position` si Rigidbody)
  2. `PlayerVictoryScale.ShowPlayerMesh()` — re-affiche le mesh caché depuis VictoryScale
  3. Lever `CM_TransitionRoom.Priority` → Cinemachine blend
  4. Attendre `cameraBlendWait` (0.8s)
  5. `DalleVictory.StartDescent(player.transform)` — player parenté, isKinematic=true, descent, unparent, restore
  6. `OnDescentComplete` → `player.enabled = true`
- **Étape 6** : Portes activées via `TransitionRoomDoor.Enable()`

---

## LevelStatsTracker

Singleton sur le GO LevelManager. Statistiques suivies :

| Stat | Déclencheur |
|---|---|
| `AttemptCount` | PlayerPrefs `"Attempts_" + sceneIndex`, incrémenté à Awake, reset à OnLevelCompleted |
| `DeathCount` | `PlayerHealth.OnDeath` |
| `ShotReceivedCount` | `PlayerHealth.TakeSentinelShot()` |
| `DetectionCount` | `PlayerDetectionFeedback.OnDetected()` |
| `KillCount` | `GameManager.OnEnemyKilled()` |
| `ElapsedTime` | Timer interne (pause/resume via `PauseTimer`/`ResumeTimer`) |

---

## Hiérarchie Canvas recommandée

```
Canvas                          ← VictoryUI.cs est ici
├── ColoredBackground           ← colorBackground (Image plein écran, CanvasGroup)
└── VictoryPanel                ← slideContainer (RectTransform)
    ├── LevelCompleteText       ← levelCompleteText (TMP)
    └── AttemptsText            ← attemptsText (TMP)
```

**Règles importantes :**
- `ColoredBackground` est **frère** de `VictoryPanel` (pas enfant) — sinon il slide avec le wipe
- `ColoredBackground` : Image alpha=255 (opaque), CanvasGroup alpha=0 au départ (géré par code)
- `VictoryPanel` : RectTransform centré, les textes aux positions désirées dans le layout

---

## Inspector VictoryUI

| Champ | Valeur conseillée |
|---|---|
| `slideContainer` | VictoryPanel |
| `colorBackground` | CanvasGroup de ColoredBackground |
| `victoryCanvasGroup` | CanvasGroup de VictoryPanel |
| `levelCompleteText` | TMP du titre |
| `attemptsText` | TMP du compteur essais |
| `bgFadeDuration` | 0.4 |
| `textSlideDuration` | 0.35 |
| `titleSlideSound` | Son déclenché au début du slide du titre |
| `essaisSlideSound` | Son déclenché au début du slide de "Essais" |
| `wipeDuration` | 0.6 |
| `cm_victory` | CinemachineCamera CM_Victory (Priority=0 au repos) |
| `cm_victoryPriority` | 20 |

---

## Inspector PlayerVictoryScale

| Champ | Note |
|---|---|
| `victoryMaterial` | Material unlit pour les ghosts (renderQueue sera forcé à 3000+) |
| `waveCount` | Nombre de ghosts (10 par défaut) |
| `delayBetweenWaves` | Délai entre chaque ghost (0.8s) |
| `colors` | Tableau de couleurs par ghost |
| `skipGracePeriod` | 0.3s avant que le skip soit actif |

---

## Système Sentinel — Arrêt au moment de la victoire

`SentinelCycleManager.StopCycleForVictory()` :
1. Appelle `StopCycle()` — arrête le timer, les coroutines, tous les sons
2. Fire `OnCycleChanged(GameState.GreenLight)` — tous les `CycleReactiveRenderer` :
   - Changent vers `materialGreenLight`
   - Stoppent leur coroutine de pulse

**Important** : utiliser `StopCycleForVictory()` et non `StopCycle()` seul — sinon les bulbes restent dans leur état pulsing.

---

## PostVictorySequencer — Setup

- Ajouter sur le GO LevelManager
- Assigner le `Global Volume` de la scène (weight=0 au départ)
- Le profil du volume doit contenir l'ambiance post-victoire (ex: désaturation, teinte bleue)
- Assigner `transitionRoomSpawnPoint`, `dalle`, `cm_transitionRoom`, et les `doors`

---

## DalleVictory — Setup

- Ajouter sur le GO de la dalle physique (plateforme visible dans la scène)
- Créer un GO vide `DalleEndPoint` en bas du puits → assigner à `endPoint`
- `descDuration` : 2.5s conseillé, ajuster selon la hauteur du puits
- La dalle reste à sa position initiale au restart (pas de reset nécessaire, la scène se recharge)

---

## TransitionRoomDoor — Setup

| Champ | Valeur |
|---|---|
| `action` | `NextLevel` ou `Restart` |
| `interactRange` | 2.5 (ajuster selon la géométrie) |
| `interactBubble` | `InteractBubble` sur ce GO ou un enfant |

**Boutons d'interaction** : Submit (gamepad) / E / F

**Règle** : les portes sont inactives jusqu'à `Enable()` → impossible d'interagir pendant la descente.

**Porte Restart** : appelle `LevelManager.RestartLevel()` qui set `AutoStartCountdown=1` → la StartRoom au reload passera en mode porte-fermée (PupitreStartRoomFX, InteractBubble avec hideOnRestart).

---

## Visibilité joueur — Note technique

`PlayerVictoryScale.TriggerScale()` désactive `SkinnedMeshRenderer` (jamais re-activé automatiquement).
Pendant les étapes 2-4, la camera est sur CM_Victory → le joueur invisible est hors-cadre.
`PostVictorySequencer` appelle `ShowPlayerMesh()` avant de lever CM_TransitionRoom → le joueur est visible quand la camera arrive sur la TransitionRoom.

---

## Inspector PostVictorySequencer

| Champ | Valeur conseillée |
|---|---|
| `globalVolume` | Global Volume de la scène |
| `dimTargetWeight` | 1 |
| `dimDuration` | 1.5 |
| `transitionRoomSpawnPoint` | Transform au sommet de la dalle |
| `dalle` | GO avec DalleVictory.cs |
| `cm_transitionRoom` | CinemachineCamera CM_TransitionRoom (Priority=0 au repos) |
| `cm_transitionRoomPriority` | 25 |
| `cameraBlendWait` | 0.8 |
| `doors` | [DoorNextLevel, DoorRestart] |

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
| `PostVictorySequencer.cs` | LevelManager | Séquence post-wipe (vidage décor + TransitionRoom) |
| `LevelStatsTracker.cs` | LevelManager | Suivi des stats (tentatives, kills, détections…) |
| `SentinelCycleManager.cs` | SentinelCycleManager | Cycle 1-2-3 soleil + stop visuel victoire |
| `CycleReactiveRenderer.cs` | Bulbes de la sentinelle | Réagit aux états du cycle (matériaux + pulse) |
| `DecorExitSequencer.cs` | LevelManager (ou dédié) | Expulse les props du décor selon position relative au pivot |
| `EndCurtainRise.cs` | EndCurtain | Lève le rideau de fin une fois le décor vidé |
| `TransitionRoomDoor.cs` | Porte TransitionRoom | Interaction joueur → niveau suivant avec fade |

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

**5a — GoalDoor disparaît**
- Suppression instantanée du GO GoalDoor + son associé
- `PlayerVictoryScale.ShowPlayerMesh()` — mesh joueur restauré
- `player.enabled = true` — reprise du contrôle immédiate

**5b — Lights out en à-coups**
- 3 flashs successifs espacés de 1s
- Chaque flash modifie la couleur/intensité du Global Volume Victory
- Effet saccadé (pas un lerp continu) — simule des lumières qui s'éteignent une par une

**5c — Vidage du décor (`DecorExitSequencer`)**
- Collecte tous les GOs avec Renderer, sauf layer/tag Ground
- Un GO repère `DecorExitPivot` est placé dans la scène — position de référence pour les directions
- Règle d'expulsion par position relative au pivot :
  - X < pivot.X → translate en **-X**
  - X > pivot.X → translate en **+X**
  - Autour du centre (X ≈ pivot.X) et Y > pivot.Y → translate en **+Y**
- Cas spécial : la sentinel (tag `Sentinel`) part en **-Z**
- Tous les props disparaissent en translation (pas de destroy immédiat — ils sortent du champ)

**5d — EndCurtain se lève (`EndCurtainRise`)**
- Déclenché une fois le décor vidé (ou après un délai fixe)
- L'EndCurtain est un prop pur (pas de script actuellement) — script `EndCurtainRise` à créer
- Mouvement : translation vers le haut jusqu'à une position haute définie dans l'Inspector

**6 — TransitionRoom révélée**
- La TransitionRoom était présente dans la scène depuis le début, cachée derrière l'EndCurtain
- Aucun chargement — elle apparaît simplement une fois le rideau levé

**6a — Porte niveau N+1 (`TransitionRoomDoor`)**
- Une seule porte pour l'instant (NextLevel)
- À portée (`interactRange`) : InteractBubble apparaît
- Press X / Submit → porte s'ouvre + affichage UI "Continue"
- Si le joueur recule au-delà de `interactRange` → porte se referme
- Si le joueur franchit le seuil → fade out → loading screen → Level N+1

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

## Visibilité joueur — Note technique

`PlayerVictoryScale.TriggerScale()` désactive `SkinnedMeshRenderer` (jamais re-activé automatiquement).
Pendant les étapes 2-4, la camera est sur CM_Victory → le joueur invisible est hors-cadre.
`PostVictorySequencer` appelle `ShowPlayerMesh()` en 5a → le joueur est visible dès la reprise du contrôle.

---

## PostVictorySequencer — Setup

- Ajouter sur le GO LevelManager
- Assigner le `Global Volume` de la scène (weight=0 au départ)
- Le profil du volume doit contenir l'ambiance post-victoire (teinte, désaturation…)
- Assigner `goalDoor`, `decorExitSequencer`, `endCurtain`, `transitionRoomDoor`

## Inspector PostVictorySequencer

| Champ | Valeur conseillée |
|---|---|
| `globalVolume` | Global Volume de la scène |
| `lightsOutFlashes` | 3 |
| `lightsOutInterval` | 1s |
| `goalDoor` | GO GoalDoor |
| `goalDoorSound` | Son de disparition GoalDoor |
| `decorExitSequencer` | Composant DecorExitSequencer |
| `endCurtain` | Composant EndCurtainRise |
| `transitionRoomDoor` | Composant TransitionRoomDoor |

---

## DecorExitSequencer — Setup

- Ajouter sur n'importe quel GO (LevelManager conseillé)
- Créer un GO vide `DecorExitPivot` et le centrer dans la zone de jeu → assigner à `exitPivot`
- Les props avec Renderer sont collectés automatiquement au runtime (sauf layer/tag Ground)
- La sentinel doit avoir le tag `Sentinel` pour partir en -Z

## Inspector DecorExitSequencer

| Champ | Valeur conseillée |
|---|---|
| `exitPivot` | Transform du GO repère |
| `exitDuration` | 0.6s (durée de la translation de chaque prop) |
| `exitDistance` | 20 (distance de déplacement avant destruction) |
| `staggerDelay` | 0.05s (décalage entre chaque prop pour éviter que tout parte d'un coup) |

---

## EndCurtainRise — Setup

- Ajouter sur le GO EndCurtain (prop pur, metalshutter de fin)
- Créer un GO vide `EndCurtainTopPoint` à la position haute finale → assigner à `riseTarget`
- `riseDuration` : 1.2s conseillé, ajuster selon la hauteur

## Inspector EndCurtainRise

| Champ | Valeur conseillée |
|---|---|
| `riseTarget` | Transform position haute finale |
| `riseDuration` | 1.2 |
| `riseSound` | Son de montée (optionnel) |

---

## TransitionRoomDoor — Setup

- Ajouter sur le GO de la porte dans la TransitionRoom
- La TransitionRoom est présente dans la scène depuis le début, cachée derrière l'EndCurtain
- La porte est inactive jusqu'à `Enable()` (appelé par PostVictorySequencer après EndCurtainRise)

## Inspector TransitionRoomDoor

| Champ | Valeur conseillée |
|---|---|
| `interactRange` | 2.5 |
| `interactBubble` | InteractBubble sur ce GO ou un enfant |
| `doorOpenAnim` | Animation ou translate d'ouverture |
| `continueUI` | Canvas/GO "Continue" à afficher au press X |
| `enterTrigger` | Trigger de franchissement de seuil |
| `fadeDuration` | 0.5s |

**Boutons d'interaction** : Submit (gamepad) / E / F

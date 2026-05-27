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
| `PostVictorySequencer.cs` | LevelManager | Orchestre la séquence post-wipe (5a→5b→5c→5d→6a) |
| `LevelStatsTracker.cs` | LevelManager | Suivi des stats (tentatives, kills, détections…) |
| `SentinelCycleManager.cs` | SentinelCycleManager | Cycle 1-2-3 soleil + stop visuel victoire |
| `CycleReactiveRenderer.cs` | Bulbes de la sentinelle | Réagit aux états du cycle (matériaux + pulse) |
| `DecorExitSequencer.cs` | LevelManager | Expulse les props du décor selon position relative au pivot |
| `EndCurtainRise.cs` | EndCurtain | Lève le rideau de fin de sa propre hauteur |
| `TransitionRoomDoor.cs` | TransitionRoom | Orchestre bouton + porte + chargement niveau suivant |

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
- 3 flashs successifs espacés de 1s sur le `ColorAdjustments.colorFilter` du Global Volume
- Couleurs : `(206,223,255)` → `(156,191,255)` → `(107,160,255)` (teinte finale)
- Effet saccadé (snap, pas de lerp) — simule des lumières qui s'éteignent une par une
- Un son optionnel par palier (`lightsOutSounds[i]`)

**5c — Vidage du décor (`DecorExitSequencer`)**
- Collecte tous les GOs avec Renderer, sauf :
  - layer/tag `Ground` (vérifié sur toute la hiérarchie)
  - tag `Player` ou composant `PlayerPhysicsMovement`
  - composant `EndCurtainRise` (l'EndCurtain monte séparément)
  - composant `Camera` dans la hiérarchie
  - composant `Canvas` dans la hiérarchie
  - composant `LevelManager` à la racine
  - GOs dans `Manual Exclusions` (TransitionRoom, StartRoom, triggers…)
- Un GO repère `DecorExitPivot` définit les directions d'expulsion :
  - X < pivot.X → **-X**
  - X > pivot.X → **+X**
  - autour du centre ET Y > pivot.Y → **+Y**
  - tag `Sentinel` → **-Z**

**5d — EndCurtain se lève (`EndCurtainRise`)**
- L'EndCurtain monte de sa propre hauteur (lue via `Renderer.bounds`)
- Déclenché dès que `OnDecorExitComplete` est reçu

**6 — TransitionRoom révélée**
- La TransitionRoom est présente dans la scène depuis le début, cachée derrière l'EndCurtain
- Elle apparaît sans chargement une fois le rideau levé

**6a — Porte niveau N+1 (`TransitionRoomDoor`)**

Flow :
1. `PostVictorySequencer` appelle `TransitionRoomDoor.Enable()` après EndCurtainRise
2. Joueur s'approche du **Bouton** → `InteractBubble` apparaît
3. Press X → porte monte + `PupitreStartRoomFX.Stop()` + `CommentPanel.ShowPersistent("Continue")`
4. Joueur recule au-delà de `closeDistance` → porte redescend + `CommentPanel.Hide()` + bouton redevient actif
5. Press X à nouveau (porte ouverte) → `LevelManager.LoadNextLevel()`
   *(Phase 2 : animation perso qui entre dans la porte + fade avant le chargement)*

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

## Hiérarchie scène recommandée (nouveaux GOs)

```
Scene
├── LevelManager                ← PostVictorySequencer + DecorExitSequencer ici
├── DecorExitPivot              ← GO vide, centré dans la zone de jeu
├── EndCurtain                  ← prop metalshutter de fin + EndCurtainRise.cs
└── TransitionRoom              ← caché derrière EndCurtain, dans Manual Exclusions
    ├── Bouton                  ← InteractBubble + PupitreStartRoomFX (même esthétique StartRoom)
    ├── Door                    ← mesh porte + TransitionRoomDoor.cs
    └── ...                     ← reste du décor TransitionRoom
```

---

# Guide de Setup — Nouveau Niveau

> Checklist complète pour reproduire la Victory Sequence dans un nouveau niveau.

## Étape 1 — VictoryUI (Canvas)

- [ ] Créer un Canvas avec `VictoryUI.cs`
- [ ] Enfants : `ColoredBackground` (Image + CanvasGroup) + `VictoryPanel` (RectTransform)
  - `VictoryPanel` contient `LevelCompleteText` (TMP) et `AttemptsText` (TMP)
- [ ] Assigner tous les champs dans l'Inspector (voir tableau Inspector VictoryUI ci-dessus)
- [ ] `CM_Victory` : CinemachineCamera dédiée, Priority=0 au repos

## Étape 2 — LevelManager

- [ ] Vérifier que `LevelManager.cs` est présent sur le GO LevelManager
- [ ] Assigner `goalDoor` / `goalDoorNew`, `player`, `victoryUI`
- [ ] Ajouter `LevelStatsTracker.cs` sur le même GO

## Étape 3 — PostVictorySequencer

- [ ] Ajouter `PostVictorySequencer.cs` sur le GO LevelManager
- [ ] Créer un Global Volume dédié `PostVictory Volume` (weight=0 au départ)
  - Profil : `ColorAdjustments` avec `colorFilter` overridé sur `(107,160,255)`
- [ ] Assigner dans l'Inspector :
  - `Global Volume` → le PostVictory Volume
  - `Lights Out Colors` → 3 couleurs (valeurs par défaut conseillées)
  - `Lights Out Sounds` → 3 sons (optionnel)
  - `Goal Door Sound` → son de disparition (optionnel)
  - `Decor Exit` → composant DecorExitSequencer (étape 4)
  - `End Curtain` → composant EndCurtainRise (étape 5)
  - `Transition Room Door` → composant TransitionRoomDoor (étape 6)

## Étape 4 — DecorExitSequencer

- [ ] Ajouter `DecorExitSequencer.cs` sur le GO LevelManager
- [ ] Créer un GO vide `DecorExitPivot`, le placer au centre horizontal + mi-hauteur de la zone de jeu
- [ ] Assigner `Exit Pivot` → DecorExitPivot
- [ ] Tagger le bloc Sentinel avec le tag `Sentinel`
- [ ] Ajouter dans `Manual Exclusions` : `TransitionRoom`, `StartRoom`, triggers, tout GO qui ne doit pas bouger
- [ ] Vérifier que les props de sol/murs fixes ont bien le layer ou tag `Ground`

## Étape 5 — EndCurtain

- [ ] Placer le prop EndCurtain (metalshutter) devant la TransitionRoom
- [ ] Ajouter `EndCurtainRise.cs` sur ce GO
- [ ] Assigner `Rise Sound` (optionnel)
- [ ] Ajuster `Rise Duration` selon la hauteur du rideau (1.2s par défaut)
- [ ] **Note** : la hauteur est calculée automatiquement via `Renderer.bounds` — pas de point cible à assigner

## Étape 6 — TransitionRoom

- [ ] Construire la TransitionRoom derrière l'EndCurtain (cachée au départ)
- [ ] L'ajouter dans `Manual Exclusions` du `DecorExitSequencer`

### Bouton (enfant de TransitionRoom)
- [ ] GO avec mesh bouton + `InteractBubble.cs` + `PupitreStartRoomFX.cs`
  - Même esthétique que le bouton de la StartRoom
- [ ] `InteractBubble` : `detectionRange` = 2.5, `oneTimeOnly` = false

### Porte (enfant de TransitionRoom)
- [ ] GO avec mesh porte + `TransitionRoomDoor.cs`
- [ ] Assigner dans `TransitionRoomDoor` :
  - `Bouton Transform` → le GO Bouton
  - `Door` → le Transform de la porte (mesh)
  - `Interact Range` → 2.5
  - `Close Distance` → 4
  - `Open Duration` → 0.8s
  - `Open Sound` / `Close Sound` (optionnel)
- [ ] La porte est inactive jusqu'à `Enable()` — ne pas activer manuellement

---

## À venir (Phase 2)

- [ ] TransitionRoomCollider : quand le joueur entre → désactiver Global Volume, désactiver Ground GOs, activer `InsideTransitionRoom`
- [ ] Animation perso qui entre dans la porte avant le fade
- [ ] Affichage stats dans la TransitionRoom
- [ ] Portes multiples (Restart, secrets…)

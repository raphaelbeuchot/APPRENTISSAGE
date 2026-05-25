# Système de transitions entre niveaux — État des lieux

> Document de référence. Décrit le code tel qu'il existe, sans plan de modification.  
> Dernière mise à jour : 2026-05-24

---

## Table des matières

1. [Vue d'ensemble des deux flows principaux](#1-vue-densemble)
2. [Flow Victoire — côté visuel](#2-flow-victoire--côté-visuel)
3. [Flow Game Over — côté visuel](#3-flow-game-over--côté-visuel)
4. [Flow Entrée / StartRoom](#4-flow-entrée--startroom)
5. [Système de changement de scène](#5-système-de-changement-de-scène)
6. [Données persistantes et sauvegarde](#6-données-persistantes-et-sauvegarde)
7. [Singletons DontDestroyOnLoad](#7-singletons-dontdestroyonload)
8. [Flags PlayerPrefs existants](#8-flags-playerprefs-existants)
9. [Scripts impliqués — carte de responsabilités](#9-scripts-impliqués--carte-de-responsabilités)

---

## 1. Vue d'ensemble

Il existe deux flows de transition dans un niveau :

| Événement | Déclencheur | Destination |
|---|---|---|
| **Victoire** | Joueur entre dans GoalDoor active | Niveau suivant (ou WheelOfFortune, ou LevelSelect) |
| **Game Over** | PlayerHealth.OnDeath | Même scène rechargée |

Les deux flows passent par `LevelManager`, qui est le chef d'orchestre central de chaque scène de niveau.

---

## 2. Flow Victoire — côté visuel

### Chaîne d'appels

```
GoalDoorNew.OnTriggerEnter(Player)
  └─ ReachGoal()
       ├─ playerMovement.enabled = false          // freeze immédiat, sans timeScale
       ├─ SentinelCycleManager.StopCycle()
       └─ OnPlayerReached.Invoke()                // event C#
            └─ LevelManager.OnPlayerReachedGoal()
                 ├─ CollectibleManager.ConfirmRunCollectibles()
                 ├─ LevelProgressionManager.CompleteLevel(sceneIndex)
                 ├─ CountdownManager.StopAmbient()
                 ├─ player.enabled = false         // redondant mais explicite
                 ├─ PlayerVictoryScale.TriggerScale()
                 └─ [selon skipVictoryUI]
                      ├─ victoryUI.Show()          // chemin normal
                      └─ LoadWheelOrLevelSelect()  // chemin skip
```

### Animation de victoire (PlayerVictoryScale)

- **Durée totale :** ~8 secondes (`waveCount=10` × `delayBetweenWaves=0.8s`)
- **Ce qu'elle fait :**
  1. `GameUIManager.HideGameplayBars()` — masque l'interface gameplay
  2. Bake du mesh du joueur (snapshot de la silhouette)
  3. Désactive le `SkinnedMeshRenderer` du joueur (joueur invisible)
  4. Lance `WaveCoroutine()` : spawn de 10 ghosts en taille exponentielle (×2 à chaque wave), chacun avec une couleur et un renderQueue différents, billboardés via `VictoryGhostBillboard`
- **À la fin de WaveCoroutine :** rien. La coroutine se termine silencieusement. Aucun callback, aucun event.
- **`Time.timeScale` reste à 1** pendant toute l'animation (WaveCoroutine utilise `WaitForSecondsRealtime`)

### VictoryUI (chemin normal)

- S'affiche via `CanvasGroup` (alpha 1, interactable)
- **`Time.timeScale = 0`** à l'affichage
- Attend n'importe quelle touche/bouton (Update loop)
- Au clic : `Time.timeScale = 1f` → appelle `LevelManager.LoadWheelOrLevelSelect()`
- Pas de stats, pas de notation. Juste un écran "press any button"

### Chemin skip (skipVictoryUI = true)

- `LoadWheelOrLevelSelect()` est appelé immédiatement en parallèle de l'animation
- Aucune attente du joueur, aucune attente de fin d'animation

---

## 3. Flow Game Over — côté visuel

### Chaîne d'appels

```
PlayerHealth.OnDeath (event C#)
  └─ LevelManager.OnPlayerDeath()
       ├─ player.enabled = false
       └─ GameOverUI.Show()
            ├─ Time.timeScale = 1f
            ├─ SentinelCycleManager.StopCycle()
            ├─ CanvasGroup : alpha=1, interactable=true
            ├─ Message aléatoire (5 messages possibles) avec animation de scale sur X
            ├─ Son gameOver
            └─ Invoke(AutoRestart, delayBeforeRestart)  // ~1s
                 └─ LevelManager.RestartLevel()
                      ├─ PlayerPrefs.SetInt("AutoStartCountdown", 1)
                      ├─ Time.timeScale = 1f
                      └─ SceneManager.LoadScene(currentScene.name)
```

**Messages game over possibles :**
- "RED MEANS DEAD !"
- "DEAD MAN !"
- "RESTLESS IS DEATH !"
- "THE URGE TO BUDGE !"
- "ONE MOVE TOO FAR !"

Le joueur peut accélérer le restart en appuyant sur Submit/Return avant le délai automatique.

---

## 4. Flow Entrée / StartRoom

La StartRoom est **intégrée dans chaque scène de niveau** (pas une scène séparée). Elle se situe en entrée de scène, avant la zone de jeu. Elle contient :
- Un pupitre avec sphère pulsante (`PupitreStartRoomFX`)
- Une porte qui monte quand le joueur interagit (`OpeningSequenceManager`)
- Une porte qui se referme derrière le joueur (`DoorReturnTrigger`)

### Première entrée (`AutoStartCountdown == 0`)

```
OpeningSequenceManager.Start()
  ├─ playerMovement.enabled = false
  ├─ CamStartRoomVcam priorité haute (vue StartRoom)
  ├─ Écran noir (CanvasGroup alpha=1) pendant 4s
  ├─ Fade out vers 0 en 1s
  ├─ Attente delaiAvantPrompt (2s)
  ├─ CommentPanel.ShowPersistent("Press X")
  ├─ Attend PlayerInputManager.InteractPressed
  ├─ Animation "StandUpOPENING" du joueur
  └─ playerMovement.enabled = true

Joueur s'approche du pupitre + presse Interact
  └─ ActiverBouton()
       ├─ PupitreStartRoomFX.Stop()
       ├─ CamStartRoomVcam priorité 0 (retour caméra normale)
       └─ MonterPorte() : porte monte en 1.2s

Joueur passe le seuil (trigger)
  └─ FranchiSeuil() : celluleGO.SetActive(false) après 0.3s
                       (désactive la géométrie de la cellule d'entrée)
```

**`PupitreStartRoomFX` :** sphère qui pulse toutes les ~3.5s avec un son. Change de matériau après le clic (`materialApresClick`), puis encore quand la porte se referme derrière le joueur (`materialPorteFermee`).

### Restart après mort (`AutoStartCountdown == 1`)

```
OpeningSequenceManager.Awake() : isRestart = true
  └─ Start() :
       ├─ CamStartRoomVcam priorité 0 immédiatement
       ├─ ecranNoir.alpha = 0 immédiatement
       └─ wakeUpDone = true (toute la séquence de réveil skippée)

PupitreStartRoomFX.Awake() : isRestart = true
  └─ Start() : ApplyMaterial(materialPorteFermee)  // pas de pulse
```

Le joueur peut aller directement au pupitre et rejouer.

**Note :** `AutoStartCountdown` n'est **pas remis à 0** dans le code actuel — il persiste via PlayerPrefs jusqu'au prochain restart ou nouvelle entrée.

---

## 5. Système de changement de scène

### Trois chemins possibles

#### Chemin A — Via LoadingScreen (chemin principal)

```
LevelManager.LoadNextLevel()
  ├─ LoadingScreenManager.TargetSceneIndex = nextSceneIndex
  └─ SceneManager.LoadScene(1)           // scène LoadingScreen (index 1)
       └─ LoadingScreenManager.Start()
            └─ LoadSceneAsync(TargetSceneIndex)
                 ├─ allowSceneActivation = false
                 ├─ Durée minimale : 1.5s
                 ├─ Affichage : 10 pips de progression colorés
                 └─ op.allowSceneActivation = true quand prêt
```

Utilisé pour : `LoadNextLevel()`, `LoadLevelSelect()`.

#### Chemin B — Via SceneFader (fondu noir)

```
SceneFader.FadeToScene(sceneIndex)
  └─ FadeToSceneCoroutine()
       ├─ Fade alpha 0→1 (0.4s, unscaledDeltaTime)
       ├─ SceneManager.LoadScene(sceneIndex)   // SYNCHRONE
       ├─ yield null × 2
       └─ Fade alpha 1→0 (0.4s, unscaledDeltaTime)
```

Utilisé pour : `LoadWheelOfFortune()` → WheelOfFortune (index 19).

`SceneFader` est un singleton DontDestroyOnLoad, Canvas ScreenSpaceOverlay sortingOrder **9999**, Image noire plein écran. Utilise `unscaledDeltaTime` (fonctionne même si `timeScale = 0`).

#### Chemin C — LoadScene direct (cas spécial)

```
LevelManager.OnPlayerReachedGoal() :
  si buildIndex == 3 → SceneManager.LoadScene(4)  // tuto hardcodé
```

Et pour les restarts :
```
LevelManager.RestartLevel()
  └─ SceneManager.LoadScene(currentScene.name)    // rechargement de la même scène
```

### Index de scènes connus (depuis le code)

| Index | Rôle |
|---|---|
| 0 | MainMenu |
| 1 | LoadingScreen (intermédiaire) |
| 2 | LevelSelect |
| 3 | Level_TutoNew (→ charge 4 directement en victoire) |
| 4 | Level_TutoNew02 |
| 19 | WheelOfFortune |
| 5–18, 20+ | Niveaux de jeu (26 niveaux actifs) |

---

## 6. Données persistantes et sauvegarde

### Ce qui est sauvegardé (PlayerPrefs)

| Clé PlayerPrefs | Type | Contenu | Géré par |
|---|---|---|---|
| `"CompletedLevels"` | string | Indices de niveaux complétés, séparés par virgules | `LevelProgressionManager` |
| `"UnlockedLevels"` | string | Indices de niveaux déverrouillés, séparés par virgules | `LevelProgressionManager` |
| `"LastUnlockedLevel"` | int | Index du dernier niveau déverrouillé | `LevelProgressionManager` |
| `"PermanentCollectibles"` | string | IDs des collectibles définitivement acquis | `CollectibleManager` |
| `"AutoStartCountdown"` | int (0/1) | Flag restart : 1 = restart après mort, 0 = entrée fraîche | `LevelManager.RestartLevel()` |

### Ce qui n'est PAS sauvegardé

- Temps passé dans un niveau
- Nombre d'essais (tentatives) par niveau
- Nombre de détections par run
- Kills par run
- Toute notion de performance ou de score

### Logique collectibles (deux couches)

```
CollectibleManager
  ├─ collectedThisRun (HashSet<string>)   // ram en mémoire, reset à chaque LoadScene
  └─ permanentlyCollected (HashSet<string>)  // sauvegardé en PlayerPrefs

Collectible ramassé → CollectThisRun(id)          // ajout au run courant
Niveau complété    → ConfirmRunCollectibles()      // run → permanent + Save()
Niveau rechargé    → OnSceneLoaded() : collectedThisRun.Clear()
```

Si le joueur ramasse un collectible puis meurt, il n'est **pas conservé** (pas de `ConfirmRunCollectibles()` sur game over).

### Logique progression niveaux

```
LevelProgressionManager
  ├─ completedLevels  (HashSet<int>) : indices internes (pas sceneIndex)
  └─ unlockedLevels   (HashSet<int>) : indices internes

CompleteLevel(sceneIndex)
  ├─ Marque le niveau comme complété
  ├─ Déverrouille le niveau suivant (idx+1)
  └─ Save() → PlayerPrefs

GetNextLevelSceneIndex(currentSceneIndex)
  └─ Retourne levels[idx+1].sceneIndex, ou -1 si dernier niveau
```

Les niveaux 0 et 1 (indices internes) sont toujours déverrouillés par défaut.

---

## 7. Singletons DontDestroyOnLoad

| Singleton | Script | Rôle principal |
|---|---|---|
| `LevelProgressionManager.Instance` | `LevelProgressionManager.cs` | Progression, unlock, modifier actif, index prochain niveau |
| `SceneFader.Instance` | `SceneFader.cs` | Fondu noir entre scènes (Canvas sortingOrder 9999) |
| `CollectibleManager.Instance` | `CollectibleManager.cs` | État collectibles (run + permanent) |
| `CleaningCreditManager.Instance` | `CleaningCreditManager.cs` | Crédits de nettoyage de cadavres |
| `OptionsManager.Instance` | `OptionsManager.cs` | Paramètres du jeu |
| `UIAudioPlayer` | `UIAudioPlayer.cs` | Audio UI persistant entre scènes |

Tous utilisent le pattern singleton classique (Awake + `_instance != null → Destroy(this)`).  
`LevelProgressionManager` peut s'auto-instancier depuis `Resources/LevelProgressionManager` si absent.

---

## 8. Flags PlayerPrefs existants

### `AutoStartCountdown` (int, 0 ou 1)

Le flag le plus structurant pour le comportement d'entrée dans une scène.

| Valeur | Signifie | Comportement |
|---|---|---|
| `0` | Entrée fraîche (Level Select, ou première fois) | Séquence d'ouverture complète, sphère pulse |
| `1` | Restart après mort | Séquence skippée, sphère montre materialPorteFermee |

**Posé à `1` par :** `LevelManager.RestartLevel()` avant `LoadScene`.  
**Lu par :** `OpeningSequenceManager.Awake()` et `PupitreStartRoomFX.Awake()`.  
**Jamais remis à `0` explicitement dans le code** — reste à `1` jusqu'au prochain restart ou jusqu'à une réinitialisation manuelle.

---

## 9. Scripts impliqués — carte de responsabilités

```
GoalDoorNew.cs
  └─ Détecte l'entrée joueur, vérifie isActive, fire OnPlayerReached

LevelManager.cs
  └─ Chef d'orchestre : abonne OnPlayerReached et OnDeath
       ├─ Victoire : freeze joueur, TriggerScale, VictoryUI ou skip
       ├─ Défaite : freeze joueur, GameOverUI
       └─ Navigation : LoadNextLevel / LoadLevelSelect / LoadWheelOfFortune / RestartLevel

PlayerVictoryScale.cs
  └─ Animation de victoire (ghosts, ~8s). Autonome, pas de callback sortant.

VictoryUI.cs
  └─ Panel "press any button". timeScale=0. Délègue la navigation à LevelManager.

GameOverUI.cs
  └─ Message aléatoire + auto-restart. Délègue RestartLevel à LevelManager.

SceneFader.cs (DontDestroyOnLoad)
  └─ Fondu noir + LoadScene synchrone. Utilisé pour WheelOfFortune.

LoadingScreenManager.cs
  └─ Intermédiaire de chargement. Reçoit TargetSceneIndex (static), LoadSceneAsync.

LevelProgressionManager.cs (DontDestroyOnLoad)
  └─ Source de vérité pour "quel est le prochain niveau" (GetNextLevelSceneIndex).

CollectibleManager.cs (DontDestroyOnLoad)
  └─ Double couche run/permanent. ConfirmRunCollectibles() appelé par LevelManager en victoire.

OpeningSequenceManager.cs
  └─ Séquence d'entrée (écran noir, animation réveil, pupitre, porte). Skippée si AutoStartCountdown=1.

PupitreStartRoomFX.cs
  └─ Feedback visuel du pupitre (pulse sphère). Skippé si AutoStartCountdown=1.

DoorReturnTrigger.cs
  └─ Fait redescendre la porte derrière le joueur quand il quitte la StartRoom.
```

---

*Ce document décrit l'état du code au 2026-05-24. Toute modification du système de transition devra être répercutée ici.*

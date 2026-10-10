# Passage en production

> Doc vivant, ouvert le 2026-10-10.
> Comment passer des scènes « brouillon » (prototypes dupliqués, réglages copiés, différences invisibles) à des niveaux et des systèmes prêts pour les versions finales.
> Ce qui est marqué **suggestion** n'est pas tranché.

---

## Pourquoi maintenant

Le jeu a des systèmes qui marchent, des prefabs pour les éléments importants (`LevelEssentiels`, `VictoryManager`, `TransitionRoom`) et une vingtaine de niveaux solo. Mais beaucoup de scènes sont des brouillons : elles ont été dupliquées les unes des autres, et leurs réglages ont divergé sans qu'on sache si c'était voulu.

Chaque niveau finalisé multiplie le coût d'un changement d'organisation fait ensuite. Le bon moment pour fixer les règles, c'est **avant** de polir les niveaux, pas après.

**Méthode** : pas de grand refactoring. Une consolidation par petites étapes vérifiables, un système pilote d'abord, puis chaque système remis au propre au moment où on finalise un niveau qui l'utilise.

---

## Principes

Discutés le 2026-10-10 à partir du cas Lava Train. À confirmer à l'usage.

### 1. Les réglages vivent dans des assets partagés, pas dans les scènes
Un système utilisé dans plusieurs niveaux (piège, plateforme, ennemi) prend ses réglages (sons, matériaux, vitesses, timings) dans un **ScriptableObject** partagé. Si un niveau doit être différent, il pointe sur un autre asset : la différence devient **visible et voulue**, au lieu d'être un accident de copie.
- Modèle déjà présent : `RotatingPlatform` + `RotatingPlatformSettings`.

### 2. Un système se branche tout seul
Un prefab posé dans une scène doit marcher tout de suite. On ne touche l'Inspector que pour **régler** son comportement, jamais pour le **brancher**.
- Références internes au prefab (ses enfants, ses sons, ses matériaux) : réglées une fois dans le prefab, c'est normal.
- Références vers des objets de la scène (joueur, caméra, sentinelle, managers) : le système les trouve lui-même.
- Modèle déjà présent : `PostVictorySequencer.AutoAssign()`.
- ⚠️ Multi : chercher « le » joueur (`FindObjectOfType<PlayerPhysicsMovement>`) ne renvoie qu'un joueur. **Suggestion** : un registre où chaque joueur s'inscrit en apparaissant, interrogé par les systèmes.

### 3. Ce qu'on voit en édition correspond au départ du jeu
Un système qui se place lui-même au démarrage doit montrer cette position dans la vue Scène pendant l'édition (aperçu dessiné ou placement réel), pour qu'on ne construise pas à l'aveugle.

### À discuter
- **Un seul exemplaire de chaque système** : choisir la bonne version quand plusieurs cohabitent (`EpervierManager` / `EpervierManagerLoop` / `EpervierManagerNEW`, `GoalDoor` / `GoalDoorNew`, `MetalShutter` / `MetalShutterSafe`…) et ranger le reste.
- **Une scène modèle** dont on part pour tout nouveau niveau, plutôt que de dupliquer le dernier niveau en date.
- **Rangement libre de la hiérarchie** : éviter que le code dépende de la forme de la hiérarchie (`transform.root`, « tout ce qui est sous l'objet tagué Sentinel »), préférer des composants ou marqueurs explicites. Détail des points sensibles dans `CE_QUI_RESTE_A_FAIRE.md` (section TECH, « Ranger les hiérarchies avec des parents vides »).

---

## Grille d'audit d'un système

Pour passer un piège ou une plateforme au crible :

- [ ] Des réglages partagés vivent-ils encore dans les scènes (copiés d'un niveau à l'autre) ? Les copies sont-elles identiques ? → les sortir dans un asset partagé (SO).
- [ ] Ce qui reste dans la scène est-il vraiment propre au niveau (forme, nombre, position) ? Un prefab n'est utile que s'il reste des éléments communs à monter à chaque fois — ce n'est pas un but en soi.
- [ ] Y a-t-il des listes ou références à remplir à la main qui pourraient être trouvées automatiquement ?
- [ ] Trouve-t-il le joueur et les managers tout seul ? Fonctionne-t-il avec deux joueurs (si besoin) ?
- [ ] Ce qu'on voit en édition correspond-il au départ du jeu ?
- [ ] D'autres scripts le connaissent-ils par son type, son nom ou son layer (liens cachés) ?
- [ ] Existe-t-il plusieurs versions concurrentes du script ?

---

## Cas d'étude : Lava Train

Audit du 2026-10-10. **Rien n'est modifié.**

**Fonctionnement** : `PlatformTrainManager` fait avancer N wagons (`PlatformTrainCar`, prefab `Prefabs/Lava Train/LavaPlatform`) le long d'une spline ; frein en maintenant X, sons, matériaux et taille du disque selon l'état. Utilisé dans `Level_LavaTrain01`, `02`, `03`.

**Constats** :
1. **Le manager n'est pas un prefab** : il est posé directement dans chaque scène, avec ~15 réglages copiés (vitesse, inerties, 3 sons, 3 matériaux, 4 tailles de disque). Les trois scènes ont été dupliquées l'une de l'autre (la spline a le même identifiant interne dans les trois). Différences **non voulues** (confirmé) :
   | Réglage | LavaTrain01 | LavaTrain02 | LavaTrain03 |
   |---|---|---|---|
   | `stopInertia` | 2 | 3 | 3 |
   | `lavaTrainBasic` (matériau) | `96b34f8b…` | `929a012d…` | `96b34f8b…` |
2. **Liste des wagons remplie à la main** (`platforms`), alors que leur position dans la scène ne compte pas : `Start()` les replace à intervalles égaux sur la spline. Seul leur nombre compte (5, 5, 6).
3. **Wagons « invisibles » en édition** : ils ne sont pas désactivés, mais posés n'importe où (dans LavaTrain01, trois des cinq empilés au même point, tous à y = 0). Ils ne prennent leur place qu'en Play → la spline est dessinée sans voir les wagons.
4. **Solo seulement** : le joueur est trouvé avec `FindFirstObjectByType` (pas d'assignation à la main, mais un seul joueur), l'input est lu via `PlayerInputManager.Instance`.
5. **Liens cachés** : `PlayerPhysicMovement` teste le layer `LavaTrain` ; `SentinelDetector` a un cas particulier pour `PlatformTrainCar` (joueur non considéré en mouvement sur le train).
6. Les trois scènes contiennent aussi un `EpervierManager` → concernées si l'Epervier quitte le jeu.

**Version production (suggestion)** :
- Un prefab `LavaTrain` complet (manager + spline + wagons) : dans la scène, on ne modifie que la forme de la spline et le nombre de wagons.
- Un asset `LavaTrainSettings` partagé par les trois niveaux.
- ~~Les wagons trouvés automatiquement parmi les enfants du manager~~ → remplacé par mieux : le manager **crée** les wagons depuis le prefab (voir étapes ci-dessous).
- Un aperçu des wagons sur la spline en édition, dessiné (gizmos) : il ne modifie rien dans la scène.

Rien de tout ça ne change le comportement en jeu.

**À garder en tête** : le Lava Train servira probablement en multi (voir constat 4). Pas traité maintenant, on y reviendra une fois le pilote solo terminé.

**Workflow visé** (décidé le 2026-10-10) : je dessine la spline → je règle le nombre de wagons dans le manager → l'aperçu les montre en édition → au Start, le manager crée lui-même les wagons depuis le prefab. Plus de wagons posés à la main dans la scène. Le **premier wagon part toujours du point 0 de la spline** (pas de décalage : pour le déplacer, on bouge le premier point de la spline), le train avance dans le sens de la spline.

**Étapes du pilote** (chacune testée dans les trois niveaux avant la suivante) :
1. [x] Aperçu des wagons en édition (gizmos pleins, forme réelle du wagon, premier wagon en jaune) — validé le 2026-10-10
2. [x] **Wagons créés par le manager + asset `LavaTrainSettings`** (ex-étapes 2 et 3 fusionnées) — codé, trois scènes migrées et playtestées le 2026-10-10
   - `Scripts/Scriptable Objects/LavaTrainSettings.cs` : prefab du wagon, vitesse, inerties, message, sons, matériaux, tailles du disque.
   - Asset `Scripts/Scriptable Objects/LavaTrain_01.asset`, valeurs recopiées des scènes. Choix de référence : `stopInertia` = **2** au départ (LavaTrain01, arrêt en ~1,1 s ; LavaTrain02/03 avaient **3**, arrêt plus sec en ~0,75 s), puis ajusté à **1.85** après playtest. ⚠️ Plus la valeur est haute, plus le train s'arrête **vite** (c'est la vitesse à laquelle il rejoint 0, pas une inertie au sens physique) ; matériau « basic » = **`Materials/LavaTrain/Black_01`** (l'autre, `Materials/Black_01`, est visuellement identique mais partagé avec le reste du projet).
   - Dans le manager (par scène) : la spline, le SO, le **nombre de wagons**. Les wagons sont créés au Start en enfants du manager.
   - Vérifié avant : rien d'autre dans les scènes ne pointe vers les wagons ; leurs rotations et échelles étaient celles du prefab ; le manager est à la racine avec une échelle de 1.
   - **Migration par scène** : supprimer les wagons posés à la main, assigner `LavaTrain_01` au manager, régler le nombre (01 : 5, 02 : 5, 03 : 6), tester.
3. ~~Prefab `LavaTrain` complet (manager + spline)~~ → **optionnel** (décidé le 2026-10-10) : avec le SO, il ne reste dans le manager que la spline, le SO et le nombre de wagons, tous propres à chaque niveau → un prefab n'apporterait que des overrides. Seul gain : monter plus vite un nouveau niveau Lava Train. À faire seulement si on en crée beaucoup.
4. [ ] Multi (voir constat 4), plus tard.

---

## Catalogue de niveaux

Même logique que le principe 1 : l'ordre de la campagne dans un asset unique, par référence de scène au lieu des numéros de build. Contexte du problème dans `CE_QUI_RESTE_A_FAIRE.md` (section TECH).

**Étapes** :
1. [x] **Asset `LevelCatalog`** (2026-10-10, validé dans Unity) — `Scripts/Meta Game/LevelCatalog.cs`, `Scripts/Editor/LevelCatalogEditor.cs` (alertes : ligne sans scène, scène hors Build Settings ou désactivée, scène en double, nom affiché en double ; bouton « Rafraîchir les chemins de scène »), `Scripts/Editor/LevelCatalogEntryDrawer.cs` (titre de ligne = nom de la scène). Asset `Project/ScriptableObjects/LevelCatalog.asset` pré-rempli avec les 24 niveaux de MainMenu. **Pas encore utilisé par le jeu.**
   - Une ligne = la scène (référence, éditeur) + son chemin (rempli automatiquement, utilisé en jeu) + nom affiché (optionnel) + collectibles.
   - Le nom affiché ne sert qu'à l'écran LevelSelect, atteint seulement à la fin du dernier niveau, sans `PostVictorySequencer`, ou par un vieux chemin de `LevelManager`. Si vide → nom de la scène (à l'étape 2).
   - Collectibles reproduits tels qu'effectifs dans MainMenu : seulement **Rush Hour III** (`RushHour03`) et **Lava Train III** (`LavaTrain_03`). Les autres listes avaient été vidées (d'anciens identifiants traînent encore, inutilisés, dans le fichier de MainMenu).
   - Alerte attendue : « Bowling II » en double (Level_Epervier01 et Level_Epervier02).
2. [ ] **`LevelProgressionManager` lit le catalogue** ; suppression de la liste de MainMenu et de la liste divergente du prefab `Resources/LevelProgressionManager` (le prefab pointera sur le catalogue). ⚠️ **D'abord** : la sauvegarde (PlayerPrefs `CompletedLevels`, `UnlockedLevels`, `LastUnlockedLevel`) stocke la **position dans la liste** → réordonner la campagne décale les sauvegardes. À régler avant. Puis checklist solo.
3. [ ] **Texte de lancement dans le catalogue** (décidé le 2026-10-10 : centraliser). Voir ci-dessous.
4. [ ] `SceneMenuGenerator` lit le catalogue.
5. [ ] Multi (Race / Coin Race), plus tard.

### Texte de lancement (le texte qui tourne au début du niveau)

**Aujourd'hui** : `CountdownTextConfig` (champ `titleMessage`, plus les réglages d'animation) est sur l'objet `CountdownText` du prefab `UI` (aussi `UI_Multi`, `UI_ShowTime`) ; chaque scène remplace le texte dans son instance. `GameUIManager` le lit au compte à rebours. Sans lien avec le nom affiché du catalogue.

**Branchement proposé (suggestion)** : un champ « texte de lancement » par ligne du catalogue. Au compte à rebours, `GameUIManager` demande le texte de la scène en cours (par son chemin) via `LevelProgressionManager` ; si la scène n'est pas dans le catalogue ou que le texte est vide → texte de `CountdownTextConfig` comme aujourd'hui (scènes multi, test, tutos inchangées). `CountdownTextConfig` garde les réglages d'animation. Dépend de l'étape 2. ⚠️ `GameUIManager` est partagé avec le multi → checklist solo + vérifier une scène multi.

**Textes actuels — erreurs de copie à corriger** (choix des textes : à toi) :
| Scène | Texte de lancement actuel | Nom dans la campagne |
|---|---|---|
| Level_Ice | Go for a Spin ! | Slippery slope |
| Level_TomatoFest_01 | It's Show Time ! | TomatoFest01 |
| Level_Manage the Staff | It's show time ! | Manage the Staff |
| Level_Bowling_001 / Level_Epervier01 | Right up your alley ! (les deux) | Bowling I / Bowling II |

Pas de texte propre (texte par défaut du prefab) : Pit'em All, It's Show Time, PotPourri, tutos, scènes multi.

---

## Ménage noté en passant

- Deux dossiers de ScriptableObjects : `Project/ScriptableObjects/` (grilles de pits, `LevelCatalog`) et `Scripts/Scriptable Objects/` (réglages : `LavaTrain_01`, `RotatingPlatform_*`…). À regrouper.

---

## Chantiers liés
- **Déploiement du `VictoryManager`** : 13 niveaux solo équipés, voir `Assets/Project/VICTORY_SEQUENCE.md`.

---

## Journal

- **2026-10-10** : discussion générale sur l'organisation du projet ; cas d'étude Lava Train ; ouverture de ce doc.
- **2026-10-10 (suite)** : pilote Lava Train, étapes 1 et 2 faites. Le manager ne demande plus que la spline, le SO et le nombre de wagons ; les wagons sont créés au Start et visibles en aperçu en édition. Ressenti : « beaucoup mieux ».

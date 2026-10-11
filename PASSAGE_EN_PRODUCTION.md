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
1. [x] **Asset `LevelCatalog`** (2026-10-10, validé dans Unity) — `Scripts/Meta Game/LevelCatalog.cs`, `Scripts/Editor/LevelCatalogEditor.cs` (alertes : ligne sans scène, scène hors Build Settings ou désactivée, scène en double, nom affiché en double ; bouton « Rafraîchir les chemins de scène »), `Scripts/Editor/LevelCatalogEntryDrawer.cs` (titre de ligne = nom de la scène). Asset `Project/Resources/LevelCatalog.asset` pré-rempli avec les 24 niveaux de MainMenu (déplacé de `Project/ScriptableObjects/` vers `Resources/` à l'étape 3, même identifiant). Utilisé par le jeu depuis l'étape 2a.
   - Une ligne = la scène (référence, éditeur) + son chemin (rempli automatiquement, utilisé en jeu) + nom affiché (optionnel) + collectibles.
   - Le nom affiché ne sert qu'à l'écran LevelSelect, atteint seulement à la fin du dernier niveau, sans `PostVictorySequencer`, ou par un vieux chemin de `LevelManager`. Si vide → nom de la scène (à l'étape 2).
   - Collectibles reproduits tels qu'effectifs dans MainMenu : seulement **Rush Hour III** (`RushHour03`) et **Lava Train III** (`LavaTrain_03`). Les autres listes avaient été vidées (d'anciens identifiants traînent encore, inutilisés, dans le fichier de MainMenu).
   - Alerte « Bowling II » en double (Level_Epervier01 et Level_Epervier02) → corrigée : Epervier02 renommé « Bowling III » (deux niveaux distincts : 6 emplacements / 3 trous contre 12 / 5).
2. **Le jeu lit le catalogue** :
   - [x] **2a — branchement** (validé le 2026-10-10) : `LevelProgressionManager` a un champ `catalog` et construit sa liste `levels` au Awake (numéro de build calculé depuis le chemin, nom de la scène si nom affiché vide ; scène hors build → erreur console, ligne ignorée). `levels` n'est plus sérialisée : les anciennes listes (24 overrides dans MainMenu, 16 lignes dans le prefab `Resources`) sont des données mortes, ignorées. Catalogue branché dans le prefab `Resources/LevelProgressionManager` (MainMenu en hérite). `SceneMenuGenerator` (menu « Scene ») lit aussi le catalogue (ex-étape 4, nécessaire en même temps). Corrige le bug « niveau lancé depuis l'éditeur → LevelSelect ou ancien niveau suivant après la victoire ».
   - [x] **Progression non sauvegardée pour un niveau lancé hors MainMenu** (validé le 2026-10-10) : un niveau lancé directement en éditeur écrasait le « Continuer » de la vraie partie. Le manager auto-instancié depuis `Resources` ne sauvegarde plus (progression en mémoire, message console) ; la réinitialisation explicite reste écrite. `[DefaultExecutionOrder(-100)]` pour que l'instance de MainMenu existe avant tout appel à `Instance`. Sans effet dans un build (démarre toujours sur MainMenu). Limite : en éditeur, revenir à MainMenu dans la même session de Play garde le manager auto-instancié → pas de sauvegarde jusqu'au prochain Play.
   - [ ] **2b — remise à zéro automatique de la progression quand l'ordre du catalogue change** (validé le 2026-10-11) : empreinte (hash FNV-1a des chemins de scène, dans l'ordre) écrite à chaque sauvegarde (PlayerPrefs `CatalogSignature`) ; au chargement, empreinte différente → progression des niveaux remise à zéro + avertissement console. Sauvegarde sans empreinte (antérieure) → on adopte l'ordre actuel sans rien effacer. Ne touche pas aux collectibles ni aux crédits (stockés par identifiant, pas par position). Débloquer des niveaux en jeu ne change pas l'empreinte (elle ne dépend que du catalogue). Décidé le 2026-10-10 : en dev, une remise à zéro est souhaitable ; il s'agit surtout d'éviter un décalage silencieux si on oublie.
   - [ ] **2c — sécurité des chemins de scène** (validé le 2026-10-11) : `Scripts/Editor/LevelCatalogPathSync.cs` met les chemins à jour automatiquement quand une scène est renommée ou déplacée, et avant chaque build ; alerte « chemin périmé » dans l'Inspector du catalogue en dernier recours. Effet de bord accepté : renommer une scène change l'empreinte de la 2b → progression remise à zéro (sans gravité en dev ; réglé par la sauvegarde par identifiant avant distribution).
   - ⏸ **Sauvegarde par identifiant de scène** (au lieu de la position dans la liste) : **à faire avant de distribuer des builds** (testeurs, sortie). Aujourd'hui la sauvegarde (PlayerPrefs `CompletedLevels`, `UnlockedLevels`, `LastUnlockedLevel`) stocke des positions → réordonner décale la progression.
3. [x] **Texte de lancement dans le catalogue** (validé le 2026-10-11). Voir ci-dessous.
4. [x] ~~`SceneMenuGenerator` lit le catalogue~~ → fait avec 2a.
5. [ ] Multi (Race / Coin Race), plus tard.

### Fenêtre Game Catalog (décidée le 2026-10-11)

Vue campagne du catalogue dans une **fenêtre dédiée**, menu du haut **Game Catalog > Ouvrir le catalogue** (pas de raccourci clavier). Un tableau, une ligne par niveau dans l'ordre de jeu, lisible sans rien déplier.
- **Colonne Niveau = l'appellation** : scène (titre), nom affiché, texte de lancement (modifiables sur place). Détail dépliable : référence de scène, chemin, liste des collectibles. Glisser-déposer pour réordonner (Ctrl+Z), alertes partagées avec l'Inspector. Bouton « Ouvrir dans Game Catalog » dans l'Inspector de l'asset.
- **Colonnes** : Collectibles (nombre) ; puis **Balai / Spray** (réglages **pilotés** : le catalogue décide) ; puis **Caisse de tomates / Ennemis** (**observés** : lus dans les scènes par un bouton « Analyser les scènes », résultat gardé avec sa date — la vérité reste le contenu de la scène, pas de double vérité).
- **Étapes** : (1) [x] la fenêtre avec les champs actuels (validée le 2026-10-11) — `Scripts/Editor/GameCatalogWindow.cs` ; (2) [x] loadout (validé le 2026-10-11, import fait) : champs `hasBroom` / `hasSpray` par ligne, colonnes Balai/Spray cochables dans la fenêtre ; `PlayerLoadout.Awake` prend les valeurs du catalogue si la scène y est (cases de la scène ignorées, utiles seulement hors catalogue), forçage DevPanel toujours prioritaire ; bouton « Importer le loadout depuis les scènes » = **migration unique** (ouvre chaque scène en arrière-plan sans la modifier, lit le `PlayerLoadout` actif, avertit si aucun ou plusieurs), à retirer à l'étape 3 ; (3) [x] colonnes observées (validé le 2026-10-11) : **Caisse** (`TomatoCrate`), **Ennemis** (zombies et essaims : `EnemyHealth`, `EnemyAI_AStar`, `EnemyAI_PitMode`, `SwarmController_AStar`, `SwarmSpawner` — sans les BrightEyes), **BrightEyes** (`BrightEyesController`), **Bombes** (`BombSpawner`, système à part) ; présence seulement (le nombre importe peu) ; **présent = l'objet qui porte le composant est actif (lui et ses parents) et le composant est activé** ; en gris, non cliquables ; la bulle au survol d'une coche (et la console) donne l'objet trouvé. Bouton « Analyser les scènes » (remplace l'import du loadout, retiré) ; résultat gardé dans le catalogue (champs éditeur seulement) avec la date affichée — à relancer après avoir modifié des scènes.
- Idée DevPanel liée : « Désactiver les ennemis » (test), plutôt que de piloter les ennemis depuis le catalogue.

### Fenêtre LD Catalog (décidée le 2026-10-11)

**Pur observateur** de la structure des niveaux de la campagne : menu **Game Catalog > Ouvrir le LD catalog**. Mêmes lignes et même ordre que le Game Catalog, rien d'éditable (l'ordre se règle dans le Game Catalog). Un seul bouton « Analyser les scènes » pour les deux fenêtres. Exemple qui a motivé l'outil : on ne voyait pas que la plupart des niveaux avaient encore l'ancienne start zone.

**Cases à trois états** : cochée = présent et correct ; **tiret orange = à compléter** ; vide = absent. Bulle au survol : objet trouvé ou problème. Validé le 2026-10-11.

| Colonne | ✔ | ⚠ |
|---|---|---|
| LevelEssentiels | objet nommé « LevelEssentiels » tagué `Sentinel` | présent mais pas tagué |
| Start Door | `LevelEntranceDoor` (nouvelle start zone `Prefabs/StartZone/StartZone_8`) | **ancienne** start zone (instance de `Prefabs/LevelEssentiels/StartZone_*m`) sans porte |
| GoalDoor | `GoalDoorNew` | — |
| Clé | `KeyCollectible` | — |
| Fin de niveau | `PostVictorySequencer` (nouveau flux de fin) | — |
| Rideau | `EndCurtainRise` | — |
| Transition Room | `TransitionRoomDoor` | — |

Écartés : Pupitre et `SentinelCycleManager` (essentiels, forcément présents), `VictoryUI` (relève de l'UI → peut-être un **3ᵉ catalogue « UI »** plus tard).

**Système de vérifications générique** (`Scripts/Editor/CatalogChecks.cs`) : une colonne = une ligne dans `CatalogChecks.All` (identifiant, étiquette, catalogue, fonction qui lit la scène). Repérage par **composant**, par **instance de prefab** ou par **nom / tag**. Résultats rangés par niveau dans une liste (`LevelCatalogEntry.observed`, éditeur seulement). Les 4 colonnes observées du Game Catalog y sont passées (les anciens champs `observed…` ont disparu → **relancer une analyse**). Ajouter une colonne = une ligne.

**Plus tard** : vérifications croisées (ex. collectibles posés dans la scène ↔ déclarés dans le catalogue).

### Montée de victoire unifiée : rideau + bloc sentinelle (décidé le 2026-10-11)

**Constat (révélé par le LD Catalog)** : trois mécanismes de montée différents à la victoire, ajoutés au fil des besoins :
| Ce qui monte | Comment c'est trouvé | Mouvement |
|---|---|---|
| Rideau | composant `EndCurtainRise` (un seul : `FindObjectOfType`) | sa propre hauteur, 1,2 s, courbe douce |
| Groupe sentinelle | *un* objet tagué `Sentinel` → **sa racine** (`transform.root`) | 12 m, 2 s, linéaire, puis désactivé |
| Cercles de lumière | **tous** les `ConcentricCirclesSpawner` (rustine) | idem sentinelle |
Ex. It's Show Time : `LevelEssentiels` non tagué, mais `CentralLightTest` (enfant) tagué Sentinel → tout le groupe monte « par accident ».

**Écarté** : glisser le bloc sentinelle en enfant du rideau — le rideau est un cube étiré (8 × 10 × 0,2 dans le prefab, largeur 8 à 40 selon les niveaux) → le bloc hériterait de l'échelle (cisaillement de ce qui tourne, étirement si on redimensionne le rideau) ; et l'ancien système prendrait le rideau pour « racine de la sentinelle ».

**Retenu** : un **prefab parent « Rideau + Sentinelle »** (échelle 1) :
- sur le parent : `EndCurtainRise` revu — **hauteur de montée réglable** (0 = calcul actuel, la hauteur du rideau → les rideaux existants marchent sans rien toucher, migration scène par scène), même montée douce qu'aujourd'hui, case **« Désactiver à la fin »** (désactive tout le groupe ; si le rideau « pop » en haut du cadre, décocher pour ce niveau) ;
- enfant 1 : le rideau (cube à la taille du niveau) ;
- enfant 2 : le **bloc sentinelle, prefab imbriqué** (sentinelle, cercles, lumière centrale) → le polish se répercute partout.
- Le reste de `LevelEssentiels` (start zone, volet, pupitre…) **ne monte plus** : sans importance, invisible à la fin du niveau.

**Étapes** : (1) [x] LD Catalog validé et commité ; (2) [x] code (validé le 2026-10-11, ENDWALL de Go For a Spin fonctionne) : `EndCurtainRise` — champs **Rise Height** (0 = hauteur du premier Renderer des enfants, comportement historique) et **Deactivate When Risen** (décoché par défaut : les rideaux existants ne changent pas) ; `PostVictorySequencer` — **rustine des cercles retirée** (`RiseAndHide` et sa boucle) ; (3) [ ] prefabs créés par l'utilisateur, migration d'un premier niveau pour valider, puis des autres (colonne LD Catalog « Rideau + bloc sentinelle » au lieu de « LevelEssentiels tagué ») — **premier essai : ENDWALL dans Level_Go For a Spin** (parent avec `EndCurtainRise`, enfants : `EndCurtain` + bloc sentinelle ; retirer l'`EndCurtainRise` de **l'instance** du rideau, pas du prefab `EndCurtain` utilisé par 15 scènes non migrées : sinon leur rideau ne monte plus ; on le retirera du prefab à la fin de la migration ; `PostVictorySequencer` ne lève qu'un seul `EndCurtainRise`, choisi au hasard s'il y en a plusieurs) ; (4) [ ] suppression du reste de l'ancien système (tag Sentinel, racine) et retrait du tag dans les scènes ; retrait de l'`EndCurtainRise` du prefab `EndCurtain`. ⚠️ Pendant la migration : **pas de tag Sentinel sur le bloc** (sinon l'ancien système le prend pour racine).

**Idée de polish (2026-10-11)** : avant, le rideau et la sentinelle montaient avec un léger décalage (courbes et durées différentes), ce qui plaisait ; avec le parent commun, tout monte d'un bloc. Suggestion : un composant **« Retard de montée »** sur un enfant (ex. bloc sentinelle, 0,15 s) : même courbe que le parent, décalée ; le parent attend le dernier retardataire avant de tout désactiver ; plusieurs retards possibles pour une cascade (sentinelle → cercles → lumière), réglés dans le prefab. Pas encore décidé.

### Suivi des chantiers de reprise (discuté le 2026-10-11)

La montée unifiée ajoute un chantier de reprise des niveaux (après la start zone). Besoin : savoir ce qui est effectivement migré et ce qui reste dans les vieilles versions. Principe : **ne pas tenir de liste de niveaux à la main** (elle se désynchronise).
- **Le doc** (ici) garde le *pourquoi* et le *comment* de chaque chantier : décision, ancien / nouveau système, recette de migration d'un niveau.
- **Le LD Catalog** mesure l'*état réel* dans les scènes : une colonne par chantier (✔ migré / ⚠ ancienne version / — absent). **Suggestion** : un compteur par colonne (« Start Door : 3/24 ») et une colonne « Rideau + bloc sentinelle ».
- **Statut de production par niveau** (brouillon / LD validé / art en cours / final), réglé à la main dans le Game Catalog, pour ce qui ne se détecte pas automatiquement : **voulu, à faire plus tard**.

### Ce qui a sa place dans le catalogue (décidé le 2026-10-10)

But : la **vision globale de la progression** de la campagne, qui manquait au projet. Une info va dans le catalogue si elle répond à **au moins une** de ces questions :
1. Elle concerne **le niveau dans son ensemble**, pas un objet précis dedans ?
2. On veut la **comparer d'un niveau à l'autre** (vue campagne) ?
3. On en a besoin **avant que la scène soit chargée** (menus, chargement, progression) ?

→ Oui : texte de lancement, loadout, musique, collectibles, plus tard chapitre / temps de référence… → Non : ce qui est spatial ou propre à un système (nombre de wagons, position d'un piège, vitesse d'une plateforme) reste dans la scène ou dans le SO du système.

**Règles** :
- Enrichir **au fil des besoins réels**, un champ à la fois, pas de champs « au cas où ».
- **Dans la campagne, le catalogue décide seul** (règle précisée le 2026-10-11). Une scène présente dans le catalogue ne lit **jamais** son réglage local, même si le champ est vide : un champ vide est un oubli, signalé par une alerte dans l'Inspector du catalogue (sinon une vieille erreur de copie réapparaîtrait en silence). Le réglage local ne sert qu'aux **scènes hors catalogue** (multi, tests, tutos). Les réglages locaux restés dans les scènes de la campagne deviennent du ménage.
- Si les lignes deviennent trop épaisses : variante « une fiche de niveau par asset, le catalogue ne fait que les ordonner ». Pas nécessaire aujourd'hui.

**Candidats, dans l'ordre** : (1) texte de lancement — fait ; (2) **loadout** (balai / spray) — aujourd'hui en overrides du joueur dans chaque scène, dispersés et illisibles à l'échelle de la campagne ; (3) musique, si besoin de la piloter niveau par niveau.

**Loadout — objection (2026-10-11)** : en travaillant une scène, c'est pratique de désactiver balai/spray à la volée pour tester. Or, avec « le catalogue décide seul », la case de la scène serait ignorée. Principe retenu : **les réglages de test vivent dans un outil de debug, pas dans les données du niveau** (le désordre actuel du loadout vient sans doute en partie de tests jamais remis en place). → d'abord le **DevPanel** (ci-dessous), puis le loadout dans le catalogue.

### DevPanel (outil de playtest)

Décidé le 2026-10-11. `Scripts/Meta Game/DevPanel.cs` — première version validée le 2026-10-11.
- **F1** en Play ouvre/ferme un panneau de cases à cocher ; **jeu en pause** pendant l'ouverture (vitesse rétablie à la fermeture).
- S'installe tout seul au lancement (`RuntimeInitializeOnLoadMethod`), rien à poser dans les scènes ; **éditeur et builds de développement seulement** (absent d'un build joueur).
- Options **retenues d'un Play à l'autre** (PlayerPrefs `DEV_…`) ; **badge « DEV »** à l'écran tant qu'une option est active. ⚠️ `DevManager.startFromZero` vide toutes les PlayerPrefs, donc aussi ces options.
- Dessiné en `OnGUI` (outil de debug : pas de Canvas ni de prefab). Ajouter une option = un champ + une ligne + sa sauvegarde.
- **Options** : (1) **Forcer le loadout** + balai / spray — appliqué à chaque lancement de niveau par-dessus le loadout du niveau, décoché → loadout du niveau rétabli (`PlayerLoadout.ApplyLoadout` / `RestoreLevelLoadout`). Solo uniquement (rien n'est touché s'il y a plusieurs joueurs).
  - (2) **Invincible** (validé le 2026-10-11) : `DevFlags.Invincible` → `PlayerHealth.TakeDamage` ignore les dégâts (sentinelle, zombies, pits, lave). `TakeHeadshot` non concerné (élimination de la course multi). Le recul des tirs reste. À vérifier : comportement dans la lave (le joueur coule-t-il ou reste-t-il coincé ?).
  - (3) **Zéro détection sentinelle** (validé le 2026-10-11) : `DevFlags.SentinelIgnoresPlayer` → `SentinelDetector` ignore les joueurs, sur la même ligne que les exemptions existantes (balai, sonné, réapparition). Pas de tir, de silhouette ni d'alarme ; zombies et pièges inchangés.
  - (4) **Porte ouverte** (validé le 2026-10-11) : le DevPanel ouvre la `GoalDoorNew` (`SetActive(true)`) à chaque lancement de niveau et quand on coche ; ouvre aussi les portes « tuer tous les ennemis » (`LevelManager` les verrouille au Start, avant le DevPanel). Décocher ne referme pas la porte en cours de niveau.
  - Invincible, zéro détection et porte ouverte agissent **aussi en multi** (tous les joueurs) — voulu (2026-10-11) ; le loadout reste solo. `DevFlags` (`Scripts/Meta Game/DevFlags.cs`) : interrupteurs statiques toujours à faux dans un build joueur.
  - À venir selon les besoins : niveau suivant, ralentir le temps…

### Texte de lancement (le texte qui tourne au début du niveau)

**Aujourd'hui** : `CountdownTextConfig` (champ `titleMessage`, plus les réglages d'animation) est sur l'objet `CountdownText` du prefab `UI` (aussi `UI_Multi`, `UI_ShowTime`) ; chaque scène remplace le texte dans son instance. `GameUIManager` le lit au compte à rebours. Sans lien avec le nom affiché du catalogue.

**Branchement retenu** : champ `launchText` par ligne du catalogue ; catalogue rangé dans `Resources/` et lu par `LevelCatalog.Get()` (aucune référence à brancher). `CountdownTextConfig.GetTitle()` : scène présente dans le catalogue (trouvée par son chemin) → son `launchText`, **même vide** (alerte « pas de texte de lancement » dans l'Inspector du catalogue) ; scène hors catalogue → son `titleMessage` (multi, tests, tutos). `GameUIManager` inchangé. Écarté : passer par `LevelProgressionManager` (le créerait dans les scènes multi, absent au compte à rebours d'un niveau lancé en éditeur) ou une référence glissée (3 endroits à brancher : prefabs `UI`, `UI_ShowTime`, scène MeetTheEnemies). Textes recopiés depuis les scènes le 2026-10-10, les 24 remplis (It's Show Time : « It's Show Time ! », Pit'em All : « Pit 'em all ! », qui venaient des textes par défaut des prefabs `UI_ShowTime` et `UI`). ⚠️ `CountdownTextConfig` est partagé avec le multi → checklist solo + une scène multi.

**Ménage** : les `titleMessage` modifiés dans les scènes de la campagne ne servent plus → à supprimer. Le texte par défaut du prefab `UI` est « Pit 'em all ! » (texte d'un niveau devenu défaut de tous) → remettre un texte neutre (ex. « Super Panopticon! »).

**Textes — erreurs de copie à corriger** (désormais directement dans le catalogue ; choix des textes : à toi) :
| Scène | Texte de lancement actuel | Nom dans la campagne |
|---|---|---|
| ~~Level_Ice~~ | ~~Go for a Spin !~~ → **Caution Wet Floor** (corrigé le 2026-10-10) | Caution floor slippery |
| Level_TomatoFest_01 | It's Show Time ! | TomatoFest01 |
| ~~Level_Manage the Staff~~ | ~~It's show time !~~ → **Riff Raff** (corrigé dans le catalogue le 2026-10-11) | Riff Raff |
| Level_Bowling_001 / Level_Epervier01 | Right up your alley ! (les deux) | Bowling I / Bowling II |

Hors campagne (texte local de la scène ou défaut du prefab) : PotPourri, tutos, scènes multi.

---

## Ménage noté en passant

- Deux dossiers de ScriptableObjects : `Project/ScriptableObjects/` (grilles de pits) et `Scripts/Scriptable Objects/` (réglages : `LavaTrain_01`, `RotatingPlatform_*`…). À regrouper. (`LevelCatalog` est maintenant dans `Resources/`.)
- Level_MeetTheEnemies : objet `CountDownTextSecondaire` mort (un `CountdownTextConfig` « Meet the Staff ! » sans composant texte, rien ne pointe vers lui). Le texte affiché est celui du prefab UI (« Not alone anymore ! »).
- Script `StartZone` (`Scripts/Scripts_LevelDesign/StartZone.cs`) : **code mort**, porté par aucune scène ni aucun prefab (le niveau est lancé par le pupitre).
- **Deux familles de start zones** : la nouvelle `Prefabs/StartZone/StartZone_8` (avec porte d'entrée : `LevelEntranceDoor`, `DoorBasic`, `DoorButtonFX`) et les anciennes `Prefabs/LevelEssentiels/StartZone_8m / _10m / _12m` (décor + trigger, sans script) → déployer la nouvelle, puis retirer les anciennes. Suivi : colonne Start Door du LD Catalog.

---

## Chantiers liés
- **Déploiement du `VictoryManager`** : 13 niveaux solo équipés, voir `Assets/Project/VICTORY_SEQUENCE.md`.

---

## Journal

- **2026-10-10** : discussion générale sur l'organisation du projet ; cas d'étude Lava Train ; ouverture de ce doc.
- **2026-10-10 (suite)** : pilote Lava Train, étapes 1 et 2 faites. Le manager ne demande plus que la spline, le SO et le nombre de wagons ; les wagons sont créés au Start et visibles en aperçu en édition. Ressenti : « beaucoup mieux ».

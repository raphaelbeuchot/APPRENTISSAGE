# Boss final — Rush Hour IV

Ouvert le 2026-11-07, mis à jour le 2026-11-08. Dernier niveau de la démo, dernier gros chantier avant la démo (voir `CE_QUI_RESTE_A_FAIRE.md`, « Boss final »).
Marqué **(décidé)** ce que l'utilisateur a tranché ; tout le reste est piste ou suggestion.

---

## Reprendre ici

**Fait** (tout validé en jeu, scène `Level_TrafficJam_04.unity`) :
- **Caméra** (2026-11-07), sans code. Détail dans « Caméra ».
- **Cycle du boss** (2026-11-08) : `Scripts/Boss/BossCycleManager.cs` **hérite** de `SentinelCycleManager`, pour ne pas toucher à la logique du jeu actuel.
  - Dans le fichier partagé, seulement : `StartGameCycle` virtual ; `GetDistanceFactor`, `GetDynamicGreenLightDuration`, `GetDynamicRedLightDuration` protected virtual ; `sentinelCentralLight`, `playerSpotLight`, `lightsToDisableInRedLight` protected ; calcul de pression regroupé dans `GetPressureFactor()` (protected virtual, formule inchangée).
  - Durées des feux tirées dans la fourchette de la phase, sans raccourcissement. **Pression = celle de la phase, sans distance ni ennemis** : elle règle le pitch des musiques, le pitch et donc la durée de l'alerte (longueur du son ÷ pitch, `maxPitch` du SO), et la jauge ; pas la durée des feux vert / rouge.
  - **Pause** (`PauseCycle`) : coupe tout, quel que soit l'état (**décidé** : le dernier pupitre d'une phase coupe tout, même en plein rouge) ; état remis au vert, sons et visuels coupés, suivis de la sentinelle effacés, lumières remises. **Reprise** (`ResumeCycle`) : relance un feu vert. Clic droit sur le composant pour tester.
  - Règle : ne pas déclarer `OnEnable` ni `Update` dans `BossCycleManager` (Unity n'appellerait plus ceux de la base).
  - Le prefab `SentinelCycleManager` est **unpacké dans cette scène seulement** : les changements futurs du prefab n'y arrivent plus.
- **Pupitres et phases** (2026-11-08) :
  - `BossPupitre` : interaction une seule fois, `InteractBubble` cachée, petit enfoncement, `onActivated` (effets de ce pupitre). Rien de `PupitreInteraction` n'est repris.
  - `BossPhaseManager` : **une seule liste de phases**, en nombre libre. Par phase : nom auto « Phase N », pupitres rangés **n'importe où dans la scène**, pliage « Cycle » (fourchettes, pression, `Resume Delay`), pliage « Effets » (`onPhaseStart` / `onPhaseComplete`). `onAllPhasesComplete` pour la fin du boss.
  - Déroulé : pupitres désactivés au lancement, ceux de la phase en cours activés ; dernier pupitre → pause du cycle → effets de fin → phase suivante (réglages de feux envoyés au cycle) → reprise après `Resume Delay` (3 s par défaut ; 0 = reprise à appeler depuis un événement ou une Timeline). Après la dernière phase, le cycle reste en pause.
  - Clic droit : *Terminer la phase en cours*, *Remplir les feux avec les valeurs de départ*.
  - Dans la scène : `BOSSPHASEMANAGER` + cylindres de test comme pupitres (`PhaseN_CylinderNN`), 4 phases.
  - Choix écarté : phases et pupitres en enfants dans la Hierarchy (impose le rangement de la scène).

- **Cage du joueur** (2026-11-08) : `BossCage`, posée sur l'objet `CAGE` (enfant `Holdpoint`). `Grab` fige le joueur, le passe en cinématique et le colle au point d'accroche chaque frame ; coupe `FallTracker` ; bloque la touche de vue (`CameraPanningExtension.UnlockViewToggle` ajouté, seul changement partagé) ; met la première caméra de transition à 30. `Release` remet tout. `ShowCamera(i)` pour passer d'un plan à l'autre. Bascule CM_NormalMode / vue basse → transition → retour validée. Le trajet de la cage n'est pas codé (Timeline ou animation). **Pas encore branchée sur les phases.**
- **Transition** (2026-11-08) : `BossTransition`, appelée par le `BossPhaseManager` au dernier pupitre, juste après la pause du cycle.
  - Joueur figé sur place (« coffre de BotW », **décidé**) : `isFrozen` + `PlayerInputManager.IsLocked` (crouch, balai, spray, tomates, interaction) ; la gravité continue.
  - Zombies vivants : gel **dédié** et définitif (**décidé** : ils ne reviennent jamais), montée provisoire de 8 m en 3 s, puis suppression. Barres de vie et minuteurs désinscrits. **Cadavres enlevés aussi** (2026-11-09, même gel et même montée : tout le monde est dégagé, comme la barre d'icônes).
  - Clic droit *Rendre le joueur* : fin de transition. **Le cycle reste arrêté toute la transition** ; `Resume Delay` compte à partir du relâchement (0 = reprise immédiate).
  - Limite : rouvrir le menu pause pendant la transition lève `IsLocked`.

Le flow complet du niveau, étape par étape (codé / branché / à faire / piste), est dans **« Flow du niveau »** ci-dessous.

**Flow d'entre-phases imaginé** (2026-11-08, piste) : 3e pupitre → caméra sur la sentinelle, un cercle explose → caméra revient sur le joueur → la cage le prend, monte → le décor se transforme dessous → la cage se déplace vers sa future position, se pose → elle le libère → texte rotatif « Phase 2 » + jingle (afficheur à part, nouveau script) → le cycle reprend à la fin du jingle. Des cages enlèvent les zombies vivants et d'autres apportent ceux de la phase suivante.
- Début de niveau normal, pour comparaison : jingle = `CountdownManager.countdownStartSound`, texte = `GameUIManager.ShowCountdown`, mais le cycle démarre après un délai **fixe** (`PupitreInteraction.gameStartDelay` 4,5 s), pas sur la fin du jingle.

**Suite**, dans l'ordre proposé :
- ~~**b.** Vider la barre d'icônes d'ennemis~~ (2026-11-09) : `EnemyIconsUI.ClearAllIcons` (vivants et cadavres, de la dernière icône à la première, délai réglable, sans compter comme un nettoyage), branchée sur le nouvel événement `BossTransition.onEnemiesRemoved` (fin de la sortie du dernier zombie, vivant ou cadavre, tout de suite s'il n'y en a aucun).
- **c.** Zombies de chaque phase (2026-11-09, **codé, pas encore testé**) : liste `Zombies` par phase dans `BossPhaseManager` (ceux des phases 2+ désactivés dès `Awake`, donc pas recensés au lancement) ; *Faire entrer les zombies de la phase* (`BringInCurrentPhaseZombies`) → descente dans `BossTransition` (`dropHeight` 8 m, `dropDuration` 3 s, inertes pendant la descente, démarrage à l'arrivée) → `onEnemiesArrived` → *Remplir la barre* (`EnemyIconsUI.SpawnIconsForLivingEnemies`, ajout partagé, appelé par le boss seul). Vérifié dans le code : barre de vie, minuteur, détection de la sentinelle et lock-on suivent un zombie activé en cours de partie.
- **d.** Brancher la cage sur la transition (aujourd'hui `BossCage.Release` remet `isFrozen` à faux tout seul : à accorder avec `BossTransition`) ; la cage qui vient au joueur demandera sans doute du code.
- Ensuite : texte « Phase N » + jingle ; caméra sur la sentinelle ; explosion ; descente des pupitres ; cercles abattables ; musique par phase (clips encore `private`). Autres types d'ennemis (essaims, BrightEyes, bombes) non gérés par la transition.

**Ordre de travail d'origine**, pour mémoire : 1. ~~Caméra~~ 2. ~~Cycle~~ 3. ~~Pupitres et phases~~ (reste : abattre un cercle, figer le joueur, mise en place) 4. Cercles abattables 5. Géant (SO dédié, œil et lumière sur sa tête) 6. Fin (lasers, passerelle, pupitre central, sortie).

---

## Flow du niveau (écrit le 2026-11-09, à relire ensemble)

Légende : **[codé]** marche en jeu · **[branché]** codé et relié dans la scène · **[à brancher]** codé, reste à relier dans l'Inspector · **[à faire]** prévu, pas codé · **[piste]** imaginé, rien de décidé.

**Lancement de la scène**
1. `BossPhaseManager` (Awake) : tous les pupitres de boss désactivés ; zombies des phases 2+ désactivés. Ceux de la phase 1 sont là. **[codé]**
2. Phase 1 démarre tout de suite (Start) : ses pupitres activés, ses réglages de feux envoyés au cycle, `onPhaseStart`. Le cycle ne tourne pas encore. **[codé]**

**Début du combat** (le niveau normal, rien de spécifique au boss)
3. Le joueur active le pupitre de départ (`PupitreInteraction`) : compte à rebours, caméra qui monte de 4 m, barre d'icônes remplie avec les zombies de la phase 1, cycle lancé après 4,5 s. **[codé, existant]**

**Pendant une phase**
4. Le cycle tourne avec les fourchettes et la pression de la phase. Le joueur active les pupitres de la phase, se défend, jette les cadavres dans les fosses (« Cleaned Up ! » + icône rayée). **[codé]**

**Dernier pupitre d'une phase** (tout sur la même frame)
5. Cycle en pause (coupe tout, même en plein rouge). **[codé]**
6. `BossTransition.Begin` : joueur figé ; tous les zombies, vivants et cadavres, gelés et qui montent (8 m, 3 s). **[codé]**
7. `onPhaseComplete` de la phase finie. **[codé, rien de branché]**
8. La phase suivante démarre : ses pupitres apparaissent **tout de suite**, ses réglages de feux partent au cycle, `onPhaseStart`. **[codé]**

**Environ 3 s plus tard**
9. Fin de la montée → `onEnemiesRemoved` → *Vider la barre* **[branché]** + *Faire entrer les zombies de la phase* **[à brancher]**.
10. Descente des zombies de la phase (8 m, 3 s), là où ils sont posés dans la scène. **[codé, à tester]**
11. Tous à terre → `onEnemiesArrived` → *Remplir la barre* **[à brancher]**.

**Reprise**
12. Joueur rendu : **à la main pour l'instant** (clic droit *Rendre le joueur*), indépendant des étapes 9 à 11. **[codé]**
13. `Resume Delay` compté depuis le relâchement → le cycle reprend au vert. Retour à l'étape 4. **[codé]**

**Dernière phase**
14. Étapes 5 à 7, puis `onAllPhasesComplete` ; le cycle reste en pause. **[codé, rien de branché]**
15. Fin : passerelle, pupitre central, « pan pan pan », porte, ciel bleu, fondu au blanc, écran de fin de démo. **[à faire]**

**Ce qui doit venir s'insérer** (voir « Flow d'entre-phases imaginé ») : caméra sur la sentinelle et explosion d'un cercle (vers l'étape 7) **[piste]** ; cage qui prend le joueur, transformation du décor, cage qui le repose et le relâche (remplace l'étape 12) **[piste, d. à faire]** ; cages qui enlèvent et apportent les zombies (remplacent les étapes 6 et 10) **[piste]** ; descente des pupitres (remplace l'apparition sèche de l'étape 8) **[piste]** ; texte « Phase N » + jingle, cycle qui reprend à la fin du jingle (remplace l'étape 13) **[à faire]**.

**Points à vérifier ensemble**
- Étape 8 : les pupitres de la phase suivante apparaissent dès le dernier pupitre, en pleine transition. Voulu ?
- Étapes 11 / 12 : le joueur peut être relâché avant que les zombies soient à terre, ou l'inverse. Faut-il un ordre imposé ?
- Étape 14 : après la dernière phase, le joueur reste **figé** (la transition est lancée) ; la fin devra le rendre ou le prendre en charge.
- **`GoalDoor` qui s'ouvrirait en plein combat** : `LevelManager` de la scène a `Require All Enemies Killed` coché et `goalDoor` relié ; `totalEnemies` ne compte que la phase 1 → la porte s'ouvre dès qu'on a tué autant de zombies qu'en phase 1 (toutes phases confondues). Suggestion : décocher, la fin passera par `onAllPhasesComplete`.
- **« Cleaned Up ! » muet à partir de la phase 2** (probable, pas testé) : `CleaningBonusManager` croit avoir fini quand il a nettoyé autant de cadavres qu'il y avait de zombies au lancement ; ensuite il ne l'affiche plus (l'icône est quand même rayée). « Spick and span ! » sortira à chaque phase où tout est nettoyé.

---

## L'arène

- Une immense salle **ronde ou octogonale** (non tranché), entourée de **gradins vides** : le plateau d'une émission sans public.
- Au centre, une **fosse à pics**, avec **la sentinelle** au-dessus.
- On joue sur un **grand anneau (rim)** autour de la fosse : **on tourne autour de la sentinelle**. C'est le point fort du niveau.
- Gradins : un octogone se fait avec 8 SeatingStand droits ; un cercle demanderait de les courber.
- Suggestion : la caméra est derrière le joueur, donc plus loin du centre que l'anneau, au-dessus des gradins. Garder des gradins assez bas pour qu'ils ne masquent pas le jeu.

## Déroulé (décidé, 2026-11-07)

**Vocabulaire** (décidé) : une **phase** = activer **3 pupitres** et **abattre un cercle concentrique** de roaming obstacles. Les **cycles** de la sentinelle (vert, alerte, rouge) tournent normalement pendant toute la phase. Chaque phase terminée **fait exploser un morceau de la sentinelle**, et la pression monte.

- **Phase 1** : un cercle de roaming obstacles + des ennemis. 3 pupitres.
- **Phase 2** : un nouveau cercle de roaming obstacles, d'autres ennemis arrivent en renfort. 3 pupitres.
- **Phase 3** : 3 cercles avec **du vide entre eux**, des **plateformes refuges** régulièrement. 3 pupitres.
- **Entre deux phases** : **le joueur est immobilisé** pendant la **mise en place** des épreuves suivantes et de leurs pupitres (côté plateau TV : on voit la suite se monter devant nous).
- **Phase 4** :
  - Les sphères de la sentinelle s'assemblent à la place de la tête d'un **gros zombie impressionnant qui nous suit en permanence**. Les cycles restent actifs.
  - **Sa tête porte l'œil de la sentinelle** (décidé) : le laser pointé sur le joueur et le tir partent de sa tête.
  - **3 pupitres** ; chacun allume **un laser qui pointe vers l'ennemi et le suit**.
  - Quand les 3 sont activés, **une passerelle se déploie** (décidé) au-dessus de la fosse jusqu'à **un pupitre central** ; on appuie : **pan pan pan, explosion, victoire**.
  - **L'ennemi pendant le feu rouge** : plutôt il **continue de bouger**, à confirmer à l'équilibrage.

Les phases 1 à 3 tiennent sur des pièces déjà existantes ; la phase 4 est la seule qui demande un vrai nouvel ennemi.

## Fin (décidé)

- **Une porte s'ouvre** ; on devine **un ciel bleu** et on entend **des oiseaux** ; on sort ; **fondu au blanc**.
- **Minimum absolu de cinématique** : ni la DA, ni l'esprit du jeu. **Suggérer plutôt que montrer.**
- Après le fondu : l'écran de fin de démo (décidé le 2026-11-07 : écran simple avec « Retour au menu principal »).

---

## Caméra (validée le 2026-11-07, sans code)

Qui décide des caméras dans les niveaux normaux : **`CameraPanningExtension`** (classe enregistrée sous l'ancien nom `CameraPanningController` dans les scènes), posée sur **`CM_NormalMode`** et dans le prefab `GESTION CAMERAS`. Elle :
- donne la priorité à la vue normale ou à la vue basse (touche de vue, seulement après le pupitre, bloquée à la victoire) ;
- fait monter la caméra de **+4 m** (`verticalOffset`) en ~1,5 s après le pupitre (`OnPlayerExitStartZone`, appelé par `PupitreInteraction`) ;
- en vue basse, rend transparent ce qui est entre la caméra et le joueur (layer 3) ;
- gonfle le rayon du premier membre de son Target Group quand le joueur recule **en −Z monde** (dézoom pour les niveaux en ligne droite). `radiusGrowSpeed` n'est lu nulle part.

Autres caméras : `CM_StartZone` (remplacée au pupitre), `CM_FinalZone` (`SimpleLateralPanning`, activée par `CameraZoneSwitcher` → `SetPrimaryCamera`), caméras de victoire / TransitionRoom (`VictoryUI`, `PostVictorySequencer`). `CameraFollow` ne sert que dans les scènes `OLD`.

**Réglages de Rush Hour IV** (scène seulement, les autres niveaux ne sont pas touchés) :
- `CM_NormalMode` : `CinemachineFollow` sur le Player, (0, 3, −1), World Space ; `HardLookAt` sur **`Target Group NormalView` = Player + MainSentinel** ; `GroupFraming` en Dolly Then Zoom (80 %). Hauteur réelle ≈ 3 m + 4 m de montée + le recul du Group Framing (qui monte la caméra quand le groupe s'élargit).
- **Champ Target Group de `CameraPanningExtension` vidé** : son dézoom en −Z faisait sortir le perso du champ au bout +Z de la salle.
- `CM_LowView` : même montage, sans `CameraPanningExtension` (donc sans les +4 m), vise `Target Group LowView` = Player + MainSentinel. **Follow Offset Y passé de 3 à 5** (validé).
- Résultat : après le pupitre, la caméra regarde le long de la droite joueur → sentinelle partout dans la salle.
- Non lu : le `TargetGroupProxy` du prefab `GestionTargetGroups` (plus utilisé par l'utilisateur ; vérifier qu'il n'agit sur aucun groupe si un comportement bizarre apparaît).

## Besoins techniques et état du code (lu le 2026-11-07)

`SentinelCycleManager`, `PupitreInteraction`, `SentinelDetector` sont partagés avec tous les niveaux solo et multi : toute modification passe par la checklist de non-régression solo.

| Besoin | Ce qui existe | Ce qui manque |
|---|---|---|
| **Pause du cycle entre deux phases** | `StopCycle()` (coupe timer et sons), `StopCycleForVictory()` (remet aussi les visuels au vert), `StartGameCycle()` relançable après un `StopCycle`. | Une pause/reprise propre : arrêter au vert, relancer sans recalculer la « distance initiale ». |
| **Pression par phase** | La pression vient de la **distance en Z** à la porte (ou à la sentinelle) et des ennemis tués ; elle règle la durée des feux, le pitch de la musique et de l'alerte. `disableDistancePressure` (Coin Race) la met à plat. | Chaque feu tire un petit random dans une fourchette fixe ; la fourchette va vers des durées plus courtes à chaque phase terminée. Suggestion : une liste de 4 fourchettes (vert / rouge) réglable dans l'Inspector, une par phase. |
| **Pupitre d'épreuve** | `PupitreInteraction` a un `UnityEvent onActivated`, mais il est taillé pour le pupitre de départ (countdown, volet, caméra StartZone, lance les cycles). | Un pupitre léger (interaction + `onActivated`) et un gestionnaire de phase qui compte 3 pupitres et lance la suite. |
| **Cercles de roaming obstacles** | `RoamingObstacle` suit une spline (`SplineContainer`), vitesse et sens réglables. (`ConcentricCirclesSpawner` pose les ampoules de marquee, pas les obstacles.) | Abattre un cercle, en ajouter un, changer la vitesse. Reprendre l'existant ou composant dédié : non tranché. |
| **Ennemi géant** | Hitter = `EnemyAI_AStar` + `EnemyStats` (SO) ; un Hitter big existe. | Un SO dédié (vitesse, portée, PV). |
| **Sensible aux tomates** | Déjà le cas pour tout `EnemyAI_AStar` : dégâts (`bottleDamageTaken`) + étourdissement (`StunAndChase`). | Des PV assez hauts pour que les tomates ne le tuent pas, s'il ne meurt que par les lasers. |
| **Œil et CentralLight sur la tête** | `SentinelCentralLight` (lumière pilotée par le cycle). La détection lit `gm.sentinelEye.position` à chaque frame : un œil qui bouge devrait marcher (non testé). | Re-parenter lumière et `sentinelEye` sur la tête à la phase 4 ; faire partir lasers et tir de la tête. |
| **Lasers de la phase 4** | `SentinelLaserManager` trace des lasers (LineRenderer) de l'œil vers joueur et ennemis, et anime un tir d'un point à un autre (`TriggerShotAnimation`). | 3 lasers allumés par pupitre qui suivent le géant ; le « pan pan pan » final. |

Remarques :
- **La pression actuelle ne marche pas sur un anneau** (distance en Z) : une autre source est nécessaire pour ce niveau.
- Le calcul de pression est **recopié 5 fois** dans `SentinelCycleManager` : suggestion, le regrouper en une seule fonction avant d'ajouter la source « par phase ».
- `SentinelDetector` voit **à 360°** (raycasts sans angle ; seuls les obstacles cachent).

---

## Questions ouvertes

- Rond ou octogone ?
- L'ennemi géant au feu rouge : bouge ou s'arrête (équilibrage) ?
- Qui fait tourner les cycles quand la sentinelle est devenue la tête du géant (à voir dans le code) ?
- Cercles : reprendre `RoamingObstacle` tel quel ou composant dédié au combat ?
- Le contenu exact des « morceaux de sentinelle qui explosent » (visuel seul, ou effet de jeu : voir pistes).

## Pistes non retenues ou en réserve (historique du brainstorming)

- **Autres buts envisagés** : bombes (BombSpawner) qui touchent la sentinelle jusqu'à la vaincre (« très, trop GLaDOS ? ») ; hordes qui entrent par l'extérieur ; nettoyer le sol de ses ennemis. Suggestion : en faire des épreuves d'une phase plutôt que tout le combat.
- **Premier exemple de manche** (avant le rythme de 3 pupitres) : 10 pupitres, chacun fait exploser un roaming obstacle, puis la sentinelle tue les ennemis restants.
- **Matériau d'épreuves** (classement de l'utilisateur) : pièges et terrain (fosses, sol glissant), ennemis statiques (BrightEyes, bombes), vrais ennemis ; outils : tomates, spray, balai. Vu dans le code : la tomate étourdit et touche les `LockableTarget` ; `TomatoCrate`, `BroomPedestal` existent.
- **Idées de manches** : anneau qui se transforme (fosses, sol glissant) ; pupitres-cibles à toucher à la tomate dans les gradins ; hordes par les accès des gradins avec balai donné sur un socle ; « best-of » des mécaniques de la démo.
- **Effets de pupitres variés** : faire exploser un obstacle, refermer une fosse, éteindre un BrightEyes, faire descendre une caisse de tomates.
- **Morceaux de sentinelle qui changent le jeu** (coûteux) : cycle de plus en plus déréglé, ou œil cassé qui crée un angle mort.
- **Phase 4, autres façons d'en finir** : l'attirer dans la fosse (charge esquivée) ; objets des cintres qui lui tombent dessus ; sphères détachées à pousser au balai dans la fosse (« le nettoyeur nettoie la machine ») ; repli sans boss (l'anneau s'effondre, course vers la porte).
- **Lore** : pupitres qui descendent des cintres (machinerie de théâtre) ; le nettoyeur démonte la machine ; texte de la machine à chaque pupitre (polie, gênée, puis inquiète), par exemple « Je vais devoir m'en occuper moi-même. » à l'arrivée du géant.

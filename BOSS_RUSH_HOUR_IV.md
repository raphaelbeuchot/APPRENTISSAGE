# Boss final — Rush Hour IV

Ouvert le 2026-11-07, mis à jour le 2026-11-08. Dernier niveau de la démo, dernier gros chantier avant la démo (voir `CE_QUI_RESTE_A_FAIRE.md`, « Boss final »).
Marqué **(décidé)** ce que l'utilisateur a tranché ; tout le reste est piste ou suggestion.

---

## Reprendre ici

- **Fait** : la caméra marche dans la scène (`Level_TrafficJam_04.unity`), sans code. Détail dans « Caméra ».
- **Rien n'est codé** pour le reste.
- **Ordre de travail proposé** (petites étapes testables) :
  1. ~~Caméra~~ : validée.
  2. **Cycle** : pression par phase + pause propre entre deux phases (`SentinelCycleManager`, partagé : checklist solo).
  3. **Pupitre d'épreuve + gestionnaire de phases** : compter 3 pupitres, abattre un cercle, figer le joueur, mettre en place la phase suivante.
  4. **Cercles abattables** (roaming obstacles sur splines).
  5. **Géant** : SO dédié, œil et lumière de la sentinelle sur sa tête.
  6. **Fin** : lasers, passerelle, pupitre central, sortie.
- Prochaine étape : la 2 (cycle). Exposer l'approche avant de coder.
- **Cycle du boss (décidé le 2026-11-08)** : un `BossCycleManager` qui **hérite** de `SentinelCycleManager`, pour ne pas toucher à la logique du jeu actuel. Le fichier partagé n'a reçu que 7 mots-clés : `StartGameCycle` virtual ; `GetDistanceFactor`, `GetDynamicGreenLightDuration`, `GetDynamicRedLightDuration` protected virtual ; `sentinelCentralLight`, `playerSpotLight`, `lightsToDisableInRedLight` protected. Le boss pourra retarder le démarrage, régler durées et pression par phase, mettre en pause entre deux phases. Règle : ne pas déclarer `OnEnable` ni `Update` dans `BossCycleManager` (Unity n'appellerait plus ceux de la base).
- **La musique changera aussi par phase** : à voir ensuite (les clips `greenLightMusicLoop` / `redLightMusicLoop` sont encore `private`).

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

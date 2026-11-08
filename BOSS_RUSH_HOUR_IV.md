# Boss final — Rush Hour IV

Notes de brainstorming, ouvertes le 2026-11-07. **Rien n'est tranché** sauf mention contraire : on liste des possibilités.

Contexte : dernier niveau de la démo, dernier gros chantier avant la démo (voir `CE_QUI_RESTE_A_FAIRE.md`, « Boss final »).

---

## L'arène (vision de départ)

- Une immense salle **ronde ou octogonale**, entourée de **gradins vides** (le plateau d'une émission sans public).
- Au centre, une **fosse à pics**, avec **la sentinelle** au-dessus.
- On joue sur un **grand anneau (rim)** autour de la fosse : **on tourne autour de la sentinelle**. C'est le point fort du niveau : les caméras changent vraiment par rapport aux autres niveaux.
- Gradins : un octogone se fait avec 8 SeatingStand droits, un cercle demanderait de les courber.

## Caméra

- Piste de l'utilisateur : une **caméra Cinemachine avec un Target Group joueur + sentinelle**, assez classique.
- La caméra reste **derrière le bloc joueur + sentinelle** : elle regarde les deux, donc **elle tourne autour du centre du cercle** quand le joueur avance sur l'anneau.
- **Code à toucher** : il faudra une exception dans le code, à situer. Aujourd'hui `PanningExtension` gère les caméras, et c'est lié au **pupitre** et au **SentinelCycle**. Tout ça risque de changer pour ce niveau.
- Ce que j'ai vu dans le code (2026-11-07) : `SentinelDetector` voit **à 360°** (raycasts sans angle, seuls les obstacles cachent). `CameraFollow` a un « mode sentinelle » (distance et décalage selon la proximité de la sentinelle), dont l'usage actuel n'est pas vérifié.
- **Validé le 2026-11-07 dans Rush Hour IV, sans code** : `CM_NormalMode` garde son montage (`CinemachineFollow` en World Space sur le Player, `HardLookAt` + `GroupFraming`), mais vise directement un **Target Group Player + MainSentinel**. Une fois le pupitre activé, la caméra regarde le long de la droite joueur → sentinelle partout dans la salle.
  - Un bug (le perso sortait du champ au bout +Z de la salle) a été réglé en **vidant le champ Target Group de `CameraPanningExtension`** sur `CM_NormalMode`. Ce champ sert au dézoom quand le joueur recule en −Z monde (rayon du premier membre gonflé, jusqu'à Max Radius), qui n'a pas de sens sur un anneau. Les autres niveaux ne sont pas touchés.
  - Le champ `radiusGrowSpeed` de cette section n'est lu nulle part dans le code.
  - **Vue basse** (`CM_LowView`) : même montage que la vue normale, mais sans `CameraPanningExtension`, donc sans les +4 m. Elle vise `Target Group LowView` (Player + MainSentinel), et la bascule se fait à la touche de vue, après le pupitre. **Follow Offset Y passé de 3 à 5** : validé par l'utilisateur le 2026-11-07. `CM_LowView` est un objet de la scène, pas un prefab : le réglage ne touche que Rush Hour IV.
- Point à garder en tête (suggestion) : si la caméra est derrière le joueur, elle est plus loin du centre que l'anneau, donc **dans les gradins** ou au-dessus. Il faudra des gradins assez bas, ou une caméra assez haute, pour qu'ils ne masquent pas le jeu. On peut aussi en faire une image : la caméra filme depuis les places vides.

## But du niveau : pistes

1. **Bombes sur la sentinelle** : utiliser les BombSpawner pour toucher la sentinelle ; au bout de plusieurs bombes qui font mouche, elle est vaincue. Pas mal, mais très (trop ?) GLaDOS.
2. **Hordes** qui entrent par l'extérieur de la salle, à nettoyer.
3. **Nettoyer le sol de ses ennemis.**
4. **Compilation d'épreuves** (piste creusée) : des séries d'épreuves à enchaîner sous l'œil de la sentinelle.
   - À chaque épreuve réussie, **un pupitre descend** ; on appuie dessus et **il casse visuellement une partie de la sentinelle**.
   - **4 ou 5 fois** ; à la fin, **la sentinelle explose**.
   - Ça fait très plateau TV : on voit se mettre en place devant nous les épreuves suivantes.

Suggestions à discuter (non tranchées) :
- Les épreuves pourraient reprendre les mécaniques de la démo (sol glissant, fosses, ennemis, bombes…) : un **« best-of »** de l'émission en finale. Ça réutilise des systèmes qui existent déjà.
- Les pistes 1 à 3 peuvent devenir **des épreuves** de la compilation plutôt que tout le combat (une manche « bombes », une manche « horde »…). Les bombes passent mieux en une manche parmi d'autres qu'en combat entier.
- Lore : le pupitre qui descend des cintres colle à la machinerie de théâtre déjà en place (décors qui montent et descendent). Le nettoyeur, seul technicien d'un système abandonné, démonte la machine pièce par pièce. Le texte de la machine peut accompagner chaque pupitre (polie, gênée, puis inquiète), sans cinématique.

## Piste retenue : des pupitres à activer (2026-11-07)

L'utilisateur reste sur l'idée de **pupitres à activer, aux effets divers et variés**. Le détail reste à construire.

Exemple de manche, à partir de l'état actuel de la salle :
- La salle est remplie d'**ennemis** et de **roaming obstacles** qui circulent sur **3 cercles concentriques** autour du centre.
- **10 pupitres** à activer : **chaque pupitre fait exploser un roaming obstacle**.
- Quand c'est fait, **la sentinelle tue tous les ennemis qui restent**.
- **Le joueur est immobilisé** pendant la **mise en place des nouvelles épreuves** et de leurs pupitres.
- Suite de l'idée : à compléter (le message s'arrête sur « qui par contre… »).

### Matériau disponible pour les épreuves suivantes

Trois familles (classement de l'utilisateur) :
1. **Pièges et terrain** plus compliqué à traverser : fosses, sol glissant, etc.
2. **Ennemis statiques / pièges** : BrightEyes, bombes (BombSpawner).
3. **Vrais ennemis.**

Et les outils du joueur : **tomates**, **spray** si besoin, **balai**.
Vu dans le code : une tomate **étourdit un ennemi** et peut toucher une **cible lockable** (`LockableTarget.OnTomatoHit`, déjà utilisé pour des panneaux) ; il existe des caisses de tomates (`TomatoCrate`) et un socle à balai (`BroomPedestal`).

### Suggestions de manches (non tranchées)

Une famille par manche, puis un mélange, comme une émission qui monte en difficulté :
- **Manche terrain** : l'anneau se transforme entre deux manches (des fosses s'ouvrent, des sections deviennent glissantes) ; les pupitres sont au bout des passages difficiles.
- **Manche pièges statiques** : des BrightEyes gardent les pupitres, des bombes tombent sur l'anneau. Variante : certains « pupitres » sont des **cibles à toucher à la tomate** (LockableTarget), placées dans les gradins ou au-dessus de la fosse, hors de portée à pied. Une caisse de tomates descend avec la manche.
- **Manche vrais ennemis** : des hordes entrent par les accès des gradins ; balai (et spray) indispensables. Le balai peut être donné en début de manche sur un socle.
- **Manche finale** : tout mélangé.

Effets possibles des pupitres, pour varier : faire exploser un obstacle, refermer une fosse, éteindre un BrightEyes, faire descendre une caisse de tomates, casser un morceau de sentinelle.

Idée plus coûteuse (code) : **chaque morceau cassé change la sentinelle** (cycle de plus en plus déréglé, ou un « œil » cassé qui crée un angle mort, alors qu'aujourd'hui elle voit à 360°). Le joueur sentirait ses progrès en jouant, pas seulement à l'image.

## Déroulé creusé par l'utilisateur (2026-11-07)

Rythme : **3 pupitres par phase**, chaque série **fait exploser un morceau de la sentinelle**. Les **cercles concentriques** de roaming obstacles (3 actuellement dans la scène, je crois) sont **abattus un par phase**.

- **Phase 1** : un cercle de roaming obstacles + des ennemis. 3 pupitres → un morceau de sentinelle explose.
- **Phase 2** : un nouveau cercle de roaming obstacles, d'autres ennemis arrivent en renfort. 3 pupitres → explosion.
- **Phase 3** : 3 cercles avec **du vide entre eux**, des **plateformes refuges** régulièrement. 3 pupitres.
- **Phase 4** (idée, à creuser) : **les sphères de la sentinelle s'assemblent à la place de la tête d'un gros zombie**. Une **poursuite** ? Question ouverte de l'utilisateur : **comment s'en débarrasser, pourquoi, où ?**

### Phase 4 : version retenue par l'utilisateur (2026-11-07)

- **Un ennemi impressionnant qui nous suit en permanence.** Les **cycles restent actifs**.
- **3 pupitres**, chacun active un truc. Exemple : **chaque pupitre allume un laser qui pointe vers l'ennemi et le suit**.
- Quand les 3 sont activés, **un pupitre au centre** apparaît ; on y va, on appuie : **pan pan pan, explosion, victoire**.

Notes et questions (suggestions, non tranchées) :
- **Réutilisable** : `SentinelLaserManager` trace déjà des lasers (LineRenderer) de l'œil de la sentinelle vers le joueur et les ennemis, et a une animation de tir d'un point à un autre (`TriggerShotAnimation`). Base possible pour les 3 lasers qui suivent l'ennemi et pour le « pan pan pan ».
- **Accès au pupitre central** (décidé) : **une passerelle se déploie** au-dessus de la fosse.
- **La tête de l'ennemi porte l'œil de la sentinelle** (décidé) : c'est elle qui porte **le laser pointé sur le joueur**, et **le tir part de sa tête**.
- **SentinelCycleManager** : à voir plus tard, dans le code (qui fait tourner les cycles une fois la sentinelle devenue la tête de l'ennemi).
- **L'ennemi pendant le feu rouge** : plutôt **il continue de bouger**, mais à confirmer, peut-être pas tenable à l'équilibrage. S'il continue, le joueur doit rester figé pendant qu'il approche.

### Phase 4 : pistes pour s'en débarrasser (suggestions, non tranchées)

- **L'attirer dans la fosse à pics** : il charge (comme le windup d'un Hitter), on esquive au bord, il tombe. On peut aussi faire céder sous son poids les plateformes refuges de la phase 3.
- **La machinerie de théâtre** : 3 derniers pupitres, chacun fait tomber un élément des cintres (projecteur, contrepoids, décor) sur lui quand il passe dessous. Même langage que le reste du niveau (pupitres, cintres).
- **Les tomates pour gagner du temps** : elles étourdissent déjà les ennemis ; on l'étourdit pour atteindre le pupitre suivant.
- **Le nettoyeur nettoie la machine** : à chaque coup (chute, objet des cintres), une sphère se détache de sa tête ; on la **pousse au balai dans la fosse**, comme un cadavre. Le dernier geste du boss est un geste de nettoyage, c'est l'identité du jeu.
- **Pourquoi il nous poursuit** : la machine n'a plus de corps de surveillance et prend celui d'un de ses zombies (d'ex-prisonniers, selon le lore). Un seul carton de texte suffit (« Je vais devoir m'en occuper moi-même. »).
- **Coût** : la phase 4 est la seule qui demande un vrai nouvel ennemi. Version de repli moins chère : pas de nouveau boss, l'anneau s'effondre derrière le joueur, et il court jusqu'à la porte de sortie.

## Besoins techniques (liste de l'utilisateur, 2026-11-07) et état du code

Lu dans le code le 2026-11-07. Rien n'est codé. `SentinelCycleManager`, `PupitreInteraction`, `SentinelDetector` sont partagés avec tous les niveaux solo et multi : toute modification passe par la checklist de non-régression solo.

| Besoin | Ce qui existe | Ce qui manque |
|---|---|---|
| **Mettre le cycle en pause** | `StopCycle()` (coupe timer et sons), `StopCycleForVictory()` (remet aussi les visuels au vert), `StartGameCycle()` relançable après un `StopCycle`. | Une vraie pause/reprise propre : arrêter au vert entre deux phases, relancer ensuite sans recalculer la « distance initiale ». |
| **Pupitre qui déclenche des effets** | `PupitreInteraction` a un `UnityEvent onActivated`, mais il est taillé pour le pupitre de départ (countdown, volet, caméra StartZone, lance les cycles). | Un pupitre d'épreuve léger : interaction + `onActivated` + un « gestionnaire de phase » qui compte 3 pupitres et lance la suite. |
| **Cercles de roaming obstacles modifiables à la volée** | Les obstacles suivent une spline (`RoamingObstacle` + `SplineContainer`), vitesse et sens réglables. (`ConcentricCirclesSpawner`, lui, pose les ampoules de marquee, pas les obstacles.) | Abattre un cercle (faire exploser ses obstacles un par un), en ajouter un, changer la vitesse. Reprendre l'existant ou faire un composant dédié au combat : non tranché. |
| **Ennemi géant** | Hitter = `EnemyAI_AStar` + `EnemyStats` (SO) ; un Hitter big existe déjà. | Un SO dédié (vitesse, portée, PV). |
| **Sensible aux tomates** | Déjà le cas pour tout `EnemyAI_AStar` : la tomate fait des dégâts (`bottleDamageTaken`) et étourdit (`StunAndChase`). | Régler ses PV pour que les tomates ne le tuent pas, s'il ne doit mourir que par les lasers. |
| **CentralLight sur la tête du géant** | `SentinelCentralLight` (lumière pilotée par le cycle). La détection lit `gm.sentinelEye.position` à chaque frame : un œil qui bouge devrait marcher (non testé). | Déplacer la lumière et `sentinelEye` sur la tête à la phase 4 (re-parenter), et faire partir les lasers et le tir de la tête. |
| **Pression par phase** | Aujourd'hui la pression vient de la **distance en Z** jusqu'à la porte (ou la sentinelle) et des ennemis tués. Elle règle la durée des feux, le pitch de la musique et celui de l'alerte. `disableDistancePressure` (Coin Race) la met à plat. | Une source de pression « par phase » : chaque feu tire un petit random dans une fourchette fixe, et la fourchette va vers des durées plus courtes à chaque phase terminée. |

Remarques :
- **La pression actuelle ne marche pas sur un anneau** : elle mesure la distance en Z, qui n'a pas de sens quand on tourne autour du centre. Il faut donc de toute façon une autre source de pression pour ce niveau.
- Le calcul de pression est **recopié 5 fois** dans `SentinelCycleManager`. Avant d'ajouter une source de pression, le regrouper en une seule fonction rendrait le changement plus sûr (suggestion).
- **Vocabulaire** (décidé) : une **phase** = activer 3 pupitres et abattre un cercle concentrique. Les **cycles** de la sentinelle (vert, alerte, rouge) tournent normalement pendant toute la phase. La pression monte **à chaque phase terminée**.

## Fin

- **Une porte s'ouvre** ; on devine **un ciel bleu** et on entend **des oiseaux** ; on sort ; **fondu au blanc**.
- **Minimum absolu de cinématique** : ni la DA, ni l'esprit du jeu. **Suggérer plutôt que montrer.**
- Lien : après le fondu, l'écran de fin de démo (décidé le 2026-11-07 : écran simple avec « Retour au menu principal »).

## Questions ouvertes

- Rond ou octogone ?
- Le but du niveau (pistes ci-dessus).
- Contenu et nombre des épreuves (4 ou 5 ?), et comment elles se mettent en place sur l'anneau.
- Où placer l'exception caméra dans le code (`PanningExtension`, pupitre, SentinelCycle).

# Notes de playtest

> Prises en jeu avec F2 (PlaytestNotes). Boite de reception brute : triee dans RAPPORT_PLAYTEST.md (case cochee = triee).

**Tri du 2026-10-28** : les 24 notes du 2026-10-27 sont rangees dans RAPPORT_PLAYTEST.md.

**Tri du 2026-11-13** : les notes dictees du 2026-11-06 / 07 sont rangees dans RAPPORT_PLAYTEST.md (session du 2026-11-13) et CE_QUI_RESTE_A_FAIRE.md (anim climb).

- [x] **Level_It's Show Time** — 2026-10-27 22:36 — pos (-2.9, 0.0, -3.5)
  ajouter du decor ds la salle de depart ? lit de camp, etc. ds le lore, le player rentre "ds sa niche" et ressort pour "l'emission"
- [x] **Level_It's Show Time** — 2026-10-27 22:37 — pos (4.4, 0.0, 14.6)
  quand va ton bake les lights ds le dev ?
- [x] **Level_It's Show Time** — 2026-10-27 22:37 — pos (4.2, 0.0, 29.7)
  faire disapraotre les murs et marquee lights et camera apres victory
- [x] **Level_Go For a Spin** — 2026-10-27 22:39 — pos (1.3, 0.0, -2.6)
  le sol de la cabine de startrooom est un peu surrelevé par rapport a sol de la grande startzone
- [x] **Level_Go For a Spin** — 2026-10-27 22:40 — pos (5.5, 0.0, 10.1)
  epaisseur des murs lateraux a passer a 0.2, et baisseur leur hauterua aussi
- [x] **Level_Go For a Spin** — 2026-10-27 22:40 — pos (2.9, 0.0, 22.6)
  j'y pense : verifier ds  ts le sniveaux de la demo si on a un bien un spotlight qu is'allume sur le player en redlight
- [x] **Level_HandleHurdles** — 2026-10-27 22:41 — pos (1.8, 0.1, -2.8)
  idem, sol cabine startroom surreleve. verifier ds les overrides ?
- [x] **Level_StrangeAttractor** — 2026-10-27 22:43 — pos (1.7, 0.1, -1.8)
  le perso spawn pas ds la cabine mais ds la salle startroom
- [x] **Level_StrangeAttractor** — 2026-10-27 22:43 — pos (1.5, 0.1, 10.1)
  je tetnterais bien sur bright eyes d'activer les cercles rouges concentriques que si on ets détecté. faudra ptretre review cet ennemi d'ailleurs, y aptetre plu ssimple que le ysteme actuel d'animation
- [x] **Level_GoWithTheFlow01** — 2026-10-27 22:46 — pos (2.7, 0.0, 20.0)
  faire diapraitres les deux murets fianux a la victory
- [x] **Level_TrafficJam_01** — 2026-10-27 22:47 — pos (4.4, 0.1, 29.7)
  faire disapraiyres le smurs lateraux a victory. pouruqoi le son final ets plus fort ou differe,t que ds les autres scenes ?
- [x] **Level_WesternFlow** — 2026-10-27 22:47 — pos (2.7, 0.0, 18.6)
  revoir le systeme des lights, elles eclairent bien l'environnemt mais tro pviolemment le player.
- [x] **Level_WesternFlow** — 2026-10-27 22:48 — pos (4.1, 0.7, 32.3)
  creer un shader "paillettes" pour les murs qui sont actuellement slippery ou ice
- [x] **Level_Flower03** — 2026-10-27 22:49 — pos (3.8, 0.0, 28.1)
  le wall de gauche ets mal positionne par rapport au endcurtain
- [x] **Level_Flower03** — 2026-10-27 22:50 — pos (4.2, 0.0, 29.7)
  si on touche goaldoor en redlight, le spot qui ets allumé sur le player reste allumé apres la victory. je crois que c'ets pas normal, a verifier ds le sdocs ?
- [x] **Level_TomatoFest_01** — 2026-10-27 22:51 — pos (6.7, 0.0, -2.0)
  sur la scene d'avnt, j'ai eu a la tte fin, ds le couloir exit, un mesh gris lair visible. a corriger, je pensais l'avir fait ds le prefab transitionroom  bizarre
- [x] **Level_LavaTrain01** — 2026-10-27 22:52 — pos (0.2, 0.2, -2.4)
  tomatofest n'a pas de victorymanager et rtansition room ?
- [x] **Level_LavaTrain01** — 2026-10-27 22:53 — pos (4.4, 0.4, 8.6)
  au restart, la porte de startroom fait son animation, ca devrait pas, en retsart de niveau elle devrait etre down
- [x] **Level_LavaTrain01** — 2026-10-27 22:54 — pos (3.9, 0.3, 29.7)
  les cercles sentinelle sotn ils pas un peu bas sur la porte ?
- [x] **Level_LavaTrain01** — 2026-10-27 22:54 — pos (3.7, 0.3, 29.9)
  ground disparait pas, et transition room trop haute, et marqueelights dispariassent pas non plus
- [x] **Level_LavaTrain01** — 2026-10-27 22:55 — pos (1.6, -1.0, 22.5)
  et pas de postvictorybounds posés
- [x] **Level_LavaTrain02** — 2026-10-27 22:57 — pos (3.9, 0.4, 28.0)
  concentric cires trop bas
- [x] **Level_LavaTrain02** — 2026-10-27 22:57 — pos (3.8, 0.4, 29.7)
  tomatoUI disaprait pas a victory ?
- [x] **Level_TrouNormand** — 2026-10-27 23:01 — pos (23.1, -0.1, 28.5)
  j'ia reussi a echapper de l'ascensuer pdt la montee ? i la ts les colldiers ?
- [x] **Level_TrouNormand** — 2026-10-28 18:54 — pos (11.6, -0.2, -2.1)
  si je restart, j'entends le curtainrise, ce qu ine devrait pas etre le cas ds ce niveau car j'ai pas de curtain rise. faut il juste desacyiver un component qque part ?
- [x] **Level_HandleHurdles** — 2026-11-06 — note dictee (hors F2)
  mettre des colliders pour empecher l'escalade depuis l'autre cote (fait le 2026-11-06)
- [x] **Level_GoWithTheFlow** — 2026-11-06 — note dictee (hors F2)
  saut possible sur les murets ? bizarre, pas normal, a verifier -> laisse tel quel (step climb en l air, muret safe = 0.75 m)
- [x] **Level_WesternFlow** (Death Row I, « Row Deal ») — 2026-11-06 — note dictee (hors F2)
  bruit de chute infini si on tombe au-dessus d'un pit water ? -> corrige : son coupe a l entree du fill (d2333aff)
- [x] **Level_GoWithTheFlow** — 2026-11-06 — note dictee (hors F2)
  UI victory sans textes ?
- [x] **Level_TrouNormand** — 2026-11-06 — note dictee (hors F2)
  polish : jouer des effets d'etincelles, gear qui tourne et autres sur certains pupitres du level
- [x] **Level_TrouNormand** — 2026-11-06 — note dictee (hors F2)
  faire une salle avec des gears qui tournent au niveau du point de vue
- [x] **Level_TrouNormand** — 2026-11-06 — note dictee (hors F2)
  changer la regle d'impact des mannequins (un bool existe pour ca, je crois) + bruit d'impact + TMP BroomAttack
- [x] **General** — 2026-11-06 — note dictee (hors F2)
  les corpses dans un pit font-ils un Cleaned Up ? -> oui, dans les fosses spline : retabli le 2026-10-27 (SplinePitFillController, cas « Hips » du ragdoll), valide dans Flower Power III. Pas dans les anciennes fosses grille (voir migration dans CE_QUI_RESTE_A_FAIRE.md)
- [x] **General** — 2026-11-06 — note dictee (hors F2)
  spot parasite dans TomatoThrow a enlever (corrige le 2026-11-06)
- [x] **Level_WesternFlow** (Death Row I, « Row Deal ») — 2026-11-07 — note dictee (hors F2)
  uniformiser les blocks
- [x] **Level_WesternFlow** (Death Row I, « Row Deal ») — 2026-11-07 — note dictee (hors F2)
  si la poutre tombe, on est bloques dans le niveau. la poutre est un piege voulu (equilibre precaire, tombe si on passe trop lentement) -> il faut en faire reapparaitre une neuve a l'emplacement initial (piste : un switch). ne pas detruire les anciennes : les poutres tombees restent et illustrent nos echecs. au restart, on repart de zero (une seule poutre)
- [x] **General** — 2026-11-06 — note dictee (hors F2)
  polish de l'animation climb : ok vue de dos, mais de face vraiment pas clean du tout
- [x] **Level_TrafficJam_01** — 2026-11-18 22:51 — pos (3.8, 0.2, -1.9)
  rush hour 1 : le spawn du perso ets u peu haut, on a un mvt de camera) -> regle le 2026-11-21
- [x] **Level_Acid_02** — 2026-11-19 18:18 — pos (4.4, -1.0, 38.0)
  probleme sur ce niveau : les enemys sot desactivés (sauf le premier), afin den epas abpoir les lasers de detection des la premiere salle. mais probeme : si on fait ça, les enemys sotn actifs au depart ne sont pas enregistres dans le deomcpte d'enemys (et donc pressur gauge aux fraises, etc) -> regle le 2026-11-22 : case Add Enemies Activated Later du GameManager (ennemis ajoutes au total, a la liste a nettoyer et a la barre quand ils s activent)
- [x] **Level_GoWithTheFlow01** — 2026-11-20 11:39 — pos (4.0, 0.1, 9.9)
  sol tro pbas de la zone ou ets la clé, ca accroche quand on revient sue le convoyeor -> regle le 2026-11-21
- [x] **Level_Race_Lava** — 2026-11-20 17:50 — pos (0.3, 0.0, 6.1)
  enorme bu ici : player 1 dans a vae ais pas mort et capable de bouger -> filet de securite ajoute le 2026-11-25 (SplinePitZone : mort forcee apres 3 s dans la lave ou l acide, warning [SplinePitZone] Filet de securite dans le Player.log si le trigger rate) ; cause exacte inconnue, a surveiller
- [x] **Level_Race_Lava** — 2026-11-20 17:50 — pos (0.3, 0.0, 6.1)
  jeu cassé du coup -> voir la note precedente
- [x] **Level_CoinRace_Lava** — 2026-11-20 18:08 — pos (3.0, 0.0, 5.8)
  il faut desactivzer les enemuys a la victoire -> regle le 2026-11-21 : arret sur image au buzzer (timeScale 0), ennemis figes
- [x] **Level_TrafficJam_04** — 2026-11-23 21:12 — pos (48.7, -0.4, 73.1)
  toutes les portes sauf la finale door doivent etre non ouvrables, et avec le voyant rouge
- [x] **Level_TomatoFest_01** — 2026-11-24 18:48 — pos (5.1, 0.1, 4.5)
  chnager texte "Lancer RB" -> regle le 2026-11-24 (touches selon l appareil)
- [x] **Level_WesternCanyon** — 2026-11-25 11:42 — pos (1.4, 0.0, -1.6)
  changer l'apparence du bloat -> regle le 2026-11-25
- [ ] **Level_Ice** — 2026-11-25 22:04 — pos (1.4, 0.0, -2.6)
  une modification generale : le etxte qu s'affiche a lfin des niveaux a l'activatin de la porte de fin, il faut q'uilr reste jusqu'au fade out
- [ ] **Level_Ice** — 2026-11-25 22:05 — pos (4.9, 0.0, 32.2)
  faire diapraitres les falre a la victory
- [ ] **Level_Go For a Spin** — 2026-11-25 22:07 — pos (4.5, 0.1, -1.5)
  bug a regler : quand on est sur une rotplat, on peut se faire tirer dessus deux fois de suite sans coold won, en cumulant un tir a cause de la rotation et un crouch. i ldoit manquer un cas codé ici
- [ ] **Level_Go For a Spin** — 2026-11-25 22:08 — pos (4.4, 0.0, 31.6)
  changer la 'pparition des textes commentaires, au lieu d'un pop : un fade in fade out
- [ ] **Level_HandleHurdles** — 2026-11-25 22:09 — pos (4.6, 0.3, -1.5)
  titre fancais de handle hurdles :  "Défaire le mur"
- [ ] **Level_HandleHurdles** — 2026-11-25 22:10 — pos (4.9, 0.1, 9.9)
  sur le premier mur : rajouter collider climbamble obkject comme sur le sconde
- [ ] **Level_StrangeAttractor** — 2026-11-25 22:12 — pos (4.3, 0.1, 22.9)
  la platform de finn 'ets pas en obstacle ? elle n'arrete pas le laser, en tt cas pas entierement
- [ ] **Level_LavaTrain01** — 2026-11-25 22:20 — pos (2.0, 0.1, 15.4)
  repositionner commei i lfaut le sparticules au dessus de la lave (voire les enlever carrement)
- [ ] **Level_LavaTrain01** — 2026-11-25 22:22 — pos (3.0, 0.3, 30.7)
  tiens ? la platforme finale reapparait a la vin apres victory. f=verifier son layer
- [ ] **Level_TrouNormand** — 2026-11-25 22:24 — pos (-8.9, -0.2, -2.3)
  verifier hauteru du perso au spawn, petit mvt intempestif de camera
- [ ] **Level_TrouNormand** — 2026-11-25 22:25 — pos (9.0, -0.2, -3.0)
  suppr le dummy ds le couloir
- [ ] **Level_TrouNormand** — 2026-11-25 22:26 — pos (16.7, 0.1, 15.4)
  mes dumy ontt tous bougé ! wtf ??
- [ ] **Level_TrouNormand** — 2026-11-25 22:26 — pos (17.5, 0.1, 14.5)
  quand j'appui esur entree ils bougent tous ???
- [ ] **Level_TrouNormand** — 2026-11-25 22:27 — pos (22.1, 1.0, 26.5)
  projecteur juste avant acenseur a detailler plus car ils otn au premeir plan
- [ ] **Level_TrouNormand** — 2026-11-25 22:29 — pos (50.7, 10.6, 46.6)
  changer imperativement le texte de la porte du fond
- [ ] **Level_Acid_02** — 2026-11-25 22:32 — pos (3.3, 0.2, 44.0)
  faire disapraitre le decor a la fin : les lampes, les murs de l aslalleronde
- [ ] **Level_Acid_01** — 2026-11-25 22:34 — pos (13.0, 5.9, 43.4)
  ah on a le mme bug que ds western cayon il y a qques jour : perso deplacé vers la guache, un collider invisible ?
- [ ] **Level_Pit'em All** — 2026-11-25 22:36 — pos (4.4, 0.3, -1.2)
  placer le coffre a tomates comme il faut

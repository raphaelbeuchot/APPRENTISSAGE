# Notes de playtest

> Prises en jeu avec F2 (PlaytestNotes). Boite de reception brute : triee dans RAPPORT_PLAYTEST.md (case cochee = triee).

**Tri du 2026-10-28** : les 24 notes du 2026-10-27 sont rangees dans RAPPORT_PLAYTEST.md.

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

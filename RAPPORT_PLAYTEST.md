# Rapport de playtest

> Liste d'actions tirée des notes prises en jeu avec F2 (`NOTES_PLAYTEST.md`, boîte de réception brute).
> Une session par section, niveaux dans l'ordre du catalogue. On coche dans Unity au fil des corrections ; une session entièrement cochée peut être archivée ou supprimée.
> Ce qui dépasse un niveau (bug d'un système, idée de design, chantier technique) va dans `CE_QUI_RESTE_A_FAIRE.md`.

---

## Session du 2026-10-27

### It's Show Time
- [x] Murs, marquee lights et caméra ne disparaissent pas après la victoire (2026-10-29 : calques Obstacle + MarqueeLights, 1 objet)

### Go for a Spin
- [x] Sol de la cabine de StartRoom un peu surélevé par rapport à la StartZone (regarder les overrides du prefab)
- [x] Murs latéraux : épaisseur à 0,2 et hauteur à baisser (2026-10-29)

### Handle Hurdles
- [x] Sol de la cabine de StartRoom surélevé, comme Go for a Spin (overrides ?)

### Strange Attractor
- [x] Le joueur apparaît dans la salle StartRoom, pas dans la cabine (2026-10-29)

### Go with the flow (`Level_GoWithTheFlow01`)
- [x] Les deux murets finaux ne disparaissent pas à la victoire (2026-10-29)

### Rush Hour I (`Level_TrafficJam_01`)
- [x] Les murs latéraux ne disparaissent pas à la victoire (2026-10-29)
- [ ] Son final plus fort ou différent des autres scènes : **trouvé** (2026-10-29), override de `Lights Off Sound` sur le VictoryManager (`TransitionRoom_VolumeSwitch_02` au lieu du `_03` du prefab). Reste à faire : clic droit sur le champ → Revert. **Mis de côté pour le moment (2026-10-29).** (It's Show Time a aussi un override, `Interrupteur.wav` : voulu ?)

### Death Row I (`Level_WesternFlow`)
- [ ] Lumières : éclairent bien le décor mais trop violemment le joueur

### Flower Power III (`Level_Flower03`)
- [x] Mur de gauche mal placé par rapport à l'EndCurtain (2026-10-29)
- [x] Mesh gris clair visible tout à la fin dans le couloir de sortie (corrigé dans le prefab TransitionRoom ? → override dans la scène) (2026-10-29)

### TomaThrow (`Level_TomatoFest_01`)
- [x] Pas de VictoryManager ni de TransitionRoom : confirmé et équipé le 2026-10-29 (VictoryManager, TRANSITIONROOM, rideau, bornes)

### Lava Train I
- [x] Cercles concentriques de la sentinelle un peu bas au-dessus de la porte
- [x] Le ground et les marquee lights ne disparaissent pas à la victoire (2026-10-29)
  - 2026-10-29 : le VictoryManager n'a ni `Layers To Hide On Victory` ni `Objects To Hide On Victory` (vides dans le prefab). Reprendre le réglage de TomaThrow (Obstacle, Ground, Lockable, MarqueeLights). À préciser : ground visible sous l'écran de victoire ou à la toute fin ?
- [x] TransitionRoom trop haute (2026-10-29)
- [x] Pas de `PostVictoryBounds` (2026-10-29)

### Lava Train II
- [x] Cercles concentriques trop bas
- [x] La TomatoUI ne disparaît pas à la victoire : `GameUIManager.HideGameplayBars` cache maintenant les TomatoUI, J2 comprise (2026-10-29)

### Trou normand (`Level_TrouNormand`, hors catalogue)
- [x] On peut s'échapper de l'ascenseur pendant la montée : **résolu** (commit ff764b82). Cause : le MeshCollider d'un mesh de décor placé en plein dans le passage de la montée ; retiré de la cage d'ascenseur. (La piste Timeline / Transform hors physique était fausse.)
- [ ] Au restart, on entend le son de montée du rideau (curtain rise) alors que ce niveau n'a pas de rideau : trouver quel composant le joue (note du 2026-10-28)

### Lava Train III
- [x] EndCurtain coché `Static` → ne monte pas : décocher Static (2026-10-29)
- [ ] Level art à faire (noté le 2026-10-29) ; précisé le 2026-11-13 : petits rochers naturels, étudier les plateformes, etc. C'est encore un peu vide.

### Renvoyé dans `CE_QUI_RESTE_A_FAIRE.md`
- Porte de StartRoom qui rejoue son animation au redémarrage (BUGS)
- Spot rouge resté allumé après la victoire + vérifier sa présence dans tous les niveaux de la démo (BUGS, TECH)
- Bake des lumières pendant le dev (TECH, avec le static batching)
- Décor de « niche » de la StartRoom, BrightEyes à revoir (GAME DESIGN) ; shader « paillettes » (POLISH)

---

## Session du 2026-10-30

### Tous niveaux
- [x] Coup de balai à vide : plus aucun son. **Corrigé le 2026-10-31** : son à vide remis en fin de `OnBroomHit`. **Cause trouvée** : ce n'est pas un réglage d'Inspector, le code « AUDIO MISS » (`stats.broomSound` joué si le coup ne touche rien) a été retiré de `BroomAttackSystem.OnBroomHit` le 2026-04-02 (commit 756447ff « bomb »). Le clip `broomSound` est toujours renseigné dans `HammerGirl`, il n'est plus lu nulle part.
- [x] Coup de balai sur les cubes explosables (`PhysicsProp`, Flower Power II) : pas de son. Les props n'ont jamais eu de son au coup de balai : avant avril, c'était sans doute le son « à vide » qu'on entendait (le prop ne compte pas comme une touche). **Corrigé le 2026-10-31** avec le son à vide (un son propre aux props reste possible plus tard).
  - Au passage (2026-10-31) : props composés gérés (Rigidbody + `PhysicsProp` sur le parent, colliders sur les enfants, tous en layer Prop, MeshColliders convexes — ex. prefab `DUMMY`) : balai et bombes cherchent `PhysicsProp` dans les parents, une seule poussée par prop. Nouvelle option `Push At Hit Point` sur `PhysicsProp` (poussée au point touché par le balai, sans `Broom Torque`), décochée par défaut.
- [x] Blinder sans animations (Lava Train III). **Corrigé le 2026-11-07** : le prefab `ZombieBlinder` portait encore `EnemyIdleVariant` (hérité du Chaser), qui au `Start` remplaçait le `BlinderController` posé en override de scène par une variante d'idle du Chaser → les triggers de `ChargeAttack` (`Surprised`, `Run`, `Idle`) n'existaient plus dans le controller actif. Pit'em All marchait parce que l'instance avait retiré le composant. Correctif : `EnemyIdleVariant` supprimé du prefab. Le prefab est aussi passé sur `BlinderController` + avatar `TPose_Blinder` (même jour) : un nouveau Blinder est juste sans override de scène.

### Void I (`Level_Casino`, « Call of the Void »)
- [ ] Ajouter des ennemis

---

## Session du 2026-11-13 (prise de note rough + notes dictées du 2026-11-06 / 07)

### Tous niveaux
- [ ] « Cleaned Up ! » qui n'a pas marché dans un ou deux niveaux (lesquels : oublié). Deux pistes déjà connues, voir `CE_QUI_RESTE_A_FAIRE.md` : champ `Enemy Icons UI` vide sur `CleaningManager` (liste de scènes dans BUGS, dont Flower Power II) ; anciennes fosses grille, qui ne font aucun Cleaned Up (Lava Train III, Western Canyon : migration en fosses spline, TECH).

### Go with the flow (`Level_GoWithTheFlow01`)
- [ ] UI de victoire sans textes ? (note du 2026-11-06)

### Death Row I (`Level_WesternFlow`, « Row Deal »)
- [ ] Uniformiser les blocks (note du 2026-11-07)
- [ ] Poutre piège : si elle tombe, on est bloqué dans le niveau (note du 2026-11-07). Elle est voulue (équilibre précaire, tombe si on passe trop lentement) → en faire réapparaître une neuve à l'emplacement initial (piste : un switch). Ne pas détruire les anciennes : les poutres tombées restent et illustrent nos échecs. Au restart, on repart de zéro (une seule poutre).

### Flower Power II (`Level_Flower02`, celui avec les bombes)
- [x] Il manque une StartRoom (2026-11-13 : StartRoom avec porte d'entrée ; au passage colliders adaptés à `ClimbableObject`, VictoryManager revu, `PostVictoryBounds` ajoutée ; flow complet testé)
- [x] Pas de son au coup de balai sur les props (2026-11-13) : les 44 props sont des cubes posés à la main, pas des prefabs, `Broom Hit Sound` était vide sur tous → clips assignés dans l'Inspector, validé en jeu
- [x] `Enemy Icons UI` vide sur `CleaningManager` : sans objet, le niveau n'a pas de fosse (2026-11-13)

### AcidFloor I (`Level_Acid_01`)
- [ ] Enlever la chaîne

### Trou normand (`Level_TrouNormand`)
- [ ] Polish : effets d'étincelles, gear qui tourne et autres sur certains pupitres (note du 2026-11-06)
- [ ] Une salle avec des gears qui tournent au niveau du point de vue (note du 2026-11-06)

### Renvoyé ailleurs
- Gants de boxe des Hitters, tête du Blinder, Bright Eyes, polish de l'anim climb → `CE_QUI_RESTE_A_FAIRE.md`
- Points du boss (Cleaned Up, icônes des pupitres, textes de victoire, surcharge avant explosion) → `BOSS_RUSH_HOUR_IV.md`
- Version française → `VF_DEMO.md`

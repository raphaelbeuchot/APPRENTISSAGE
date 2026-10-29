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
- [ ] On peut s'échapper de l'ascenseur pendant la montée : vérifier les colliders de la cabine
  - 2026-10-29 : confirmé, il suffit de bouger dans la cabine pendant la montée, alors que **tous les meshes ont des colliders**. Piste (non vérifiée) : la Timeline déplace la cabine par son Transform, hors physique ; ses colliders « sautent » d'une position à l'autre à chaque image, et le Rigidbody du joueur, qui bouge, passe à travers ou se fait éjecter. Pistes de correction à discuter : Rigidbody kinematic sur la cabine (déplacé en physique), joueur rattaché à la cabine pendant la montée, ou murs invisibles plus épais.
- [ ] Au restart, on entend le son de montée du rideau (curtain rise) alors que ce niveau n'a pas de rideau : trouver quel composant le joue (note du 2026-10-28)

### Lava Train III
- [x] EndCurtain coché `Static` → ne monte pas : décocher Static (2026-10-29)
- [ ] Level art à faire (noté le 2026-10-29)

### Renvoyé dans `CE_QUI_RESTE_A_FAIRE.md`
- Porte de StartRoom qui rejoue son animation au redémarrage (BUGS)
- Spot rouge resté allumé après la victoire + vérifier sa présence dans tous les niveaux de la démo (BUGS, TECH)
- Bake des lumières pendant le dev (TECH, avec le static batching)
- Décor de « niche » de la StartRoom, BrightEyes à revoir (GAME DESIGN) ; shader « paillettes » (POLISH)

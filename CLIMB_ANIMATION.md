# Climb : attraper le rebord selon sa hauteur

Recap du 2026-11-06. Methode pour que le perso attrape le haut d'une plateforme, quelle que soit sa hauteur, puis
joue son animation de climb avec les mains bien posees sur le dessus.

## Ce qui ne va pas aujourd'hui

- `TestClimbDetection` deplace le corps lui-meme : montee lineaire de toute la hauteur de l'obstacle, puis glisse
  vers l'avant (SpeedZ 0.5).
- L'anim `ClimbTest` joue en parallele, sur place, sans rien savoir de la hauteur reelle : les mains tombent
  "n'importe ou".
- Sur un cube de 1 m, les epaules finissent ~1 m au-dessus du rebord : aucune main ne peut rester posee.

### Essai abandonne : IK des mains (2026-11-06)

Les mains etaient tirees vers le rebord par IK pendant que le corps montait. Juge moche : bras tendus, paumes qui
basculent, coup de main bref. Le rebord n'etait a portee de bras qu'un court instant pendant la montee. Lecon :
l'IK ne rattrape pas une anim qui ne correspond pas au deplacement. Code annule, rien de commite.

## La methode : root motion + MatchTarget

L'anim fait vraiment monter le perso, et Unity la deforme pour que la main tombe pile sur le rebord.
`Animator.MatchTarget` est integre a Unity, reserve aux rigs Humanoid (c'est le cas du perso).

### 1. Blender : une anim avec root motion

- Le perso **monte et avance vraiment** dans l'anim (pas sur place).
- L'animer sur un **obstacle de reference** (ex. cube de 1 m pose dans la scene Blender), mains posees sur le dessus.
- **Noter le moment ou les mains touchent le rebord**, en pourcentage de l'anim (ex. de 15 % a 30 %).
- Idealement, les mains lachent le rebord avant la fin de la montee (le corps passe au-dessus et pousse).

### 2. Import dans Unity

- Dans les reglages du clip : la montee en Y et l'avancee doivent rester du **root motion** (ne pas les "baker"
  dans la pose), sinon le perso reste sur place.

### 3. Script, au declenchement du climb

1. Trouver le **point du rebord** : rayon vers le bas juste derriere l'arete, centre en face du perso.
2. Passer le Rigidbody en **kinematic** et activer `applyRootMotion` : l'anim deplace le perso, plus le script.
3. Lancer l'anim, puis appeler **une fois** `MatchTarget` : main gauche (par ex.), point du rebord, fenetre ou la
   main touche (15 % -> 30 %). Unity etire / compresse le deplacement pendant cette fenetre pour que la main arrive
   exactement sur le point. Ne pas l'appeler pendant une transition de l'Animator.
4. A la fin de l'anim : remettre la physique (kinematic off, root motion off).

Resultat : la meme anim s'adapte seule a un obstacle de 0.8 m ou 1.2 m.

### 4. Hauteurs tres differentes : une anim par tranche

Une anim tient bien sur environ **+/- 30 %** de sa hauteur de reference. Au-dela, une anim par tranche. Le
ScriptableObject `ClimbType` a deja `minHeight` / `maxHeight` :

| Tranche (exemple) | Anim |
|---|---|
| 0.3 - 0.7 m | vault bas, une main en appui |
| 0.7 - 1.3 m | climb moyen |
| 1.3 m et plus | s'accroche puis se hisse |

Chaque `ClimbType` aurait son clip ; le script choisit selon la hauteur mesuree (il le fait deja en partie).

## Ce que ca changerait dans le code

- `ClimbCoroutine` ne ferait plus la montee lineaire + glisse : l'anim pilote, le script vise.
- Coherent avec "Animator pilote par l'intention" (CLAUDE.md) : on declenche une action, on ne lit pas la velocite.
- `TestClimbDetection.cs` est en Windows-1252 : l'outil Edit le refuse, passer par iconv.
- Fichier partage solo / multi : derouler la checklist solo apres modification.

## Premiere etape

Dans Blender : une anim de climb **en root motion** sur un cube de **1 m**, en notant **l'image ou les mains
touchent le rebord**.

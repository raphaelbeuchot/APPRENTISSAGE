# They Left the Game On (Again!)
Jeu Unity 3D, solo dev. Zombie comédie, gameplay top-down/plateforme.
Langage : C#. Pas d'unicode dans le code.

## Architecture generale
- GameManager.cs : chef d'orchestre central (refactorise recemment)
- PlayerPhysicsMovement.cs : mouvement joueur + detection surfaces mobiles
- ZombieAI.cs : IA ennemis avec states (Idle, Chasing, Attacking, OnIslandPlatform...)
- LockableTarget.cs + LockableTargetManager.cs : systeme de cibles lockables
- TargetLockSystem.cs : lock-on joueur
- TomatoProjectile.cs : projectile avec trajectoire parabolique
- DeadBodyPhysics.cs : ragdoll/corpses
- SlipperyZone.cs, ConveyorBelt, RotatingPlatform, RisingPlatform, PlatformTrain...

## Interfaces importantes
- ILockableTarget : implementee par ennemis et LockableTargets
- IMovingPlatform : implementee par RotatingPlatform, PlatformTrainCar, etc.

## Patterns etablis
- Surfaces mobiles : DetectMovingPlatform() dans PlayerPhysicsMovement, raycast vers le bas,
  parentage temporaire au transform de la surface (layer Ground obligatoire)
- Projectiles multiples sur une cible : toujours GetComponentInParent en complement de
  GetComponent, parentage sur lockable.transform (pas collision.transform)
- Detection sentinelle : basee sur worldSpaceVelocity du Rigidbody. Cas speciaux :
  RotatingPlatform force isMoving si GetCurrentPlatform() != null sauf PlatformTrainCar.
  Island platform : isMoving = (zombieAI.targetHuman != null)
- Animator : piloter par intention (input * vitesse cible), jamais par velocite physique reelle
- Pause : IsLocked = true au Resume, libere frame suivant via UnlockNextFrame()

## Systemes solides (ne pas refacto sans raison)
RotatingPlatform, RisingPlatform, PlatformTrain, Sweep bombes, Spray, TomatoThrow/LockOn,
SplinePitZone, Bright Eyes, SlipperyZone, Corpses ragdoll, KeyCollectible, LockableTargets

## Systemes a surveiller
- ConveyorBelt : migration vers spline prevue, shader pas satisfaisant
- Meta game / progression : LevelSelect supprime, pas de remplacant defini
- BroomLow : pertinence en discussion

## Backlog en cours
- Broom animation from scratch (pas de Mixamo adequat)
- LockableTargets animations de chute
- Tomatoes polish visuel crate
- KeyCollectible particules de ramassage

## Conventions
- ScriptableObjects pour les stats (ex: SentinelSettings, EnemyStats)
- Valeurs de tuning exposees en Inspector
- Coroutines pour les timings (UnlockNextFrame, corpseSlidDelay, etc.)
- Ne pas modifier DeadBodyPhysics.cs sans raison, version originale est la bonne
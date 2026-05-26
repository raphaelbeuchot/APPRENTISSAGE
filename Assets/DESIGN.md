Récap — Boucle de progression globale
Vision et références
Jeu stealth Unity, ~26 niveaux
Référence majeure : The Talos Principle — on peut finir le jeu de base, mais le dépassement débloque des niveaux secrets. Pas un roguelite, la rejouabilité vient de l'envie de complétion
Fiction : le joueur est coincé dans un gigantesque vaisseau spatial, une IA folle le fait jouer un "1 2 3 soleil" dans une ambiance jeu télé années 70
Pas d'animateur qui parle (scope trop lourd) — l'IA s'exprime par d'autres canaux (texte, jingles, lumières, gimmicks visuels)
Vocabulaire des verbes joueur
Kill un ennemi → fait diminuer la pressure gauge (cycles moins stressants) = récompense de confort immédiat
Cleanup un ennemi (lave, eau, aspirateur) → donne des crédits pour la wheel of fortune = capital long terme, plus pénible, plus stealth dans l'esprit
Asymétrie assumée : le joueur "lazy" kill, le joueur "mastery" cleanup
Gamedesign

A quoi ressemble un niveau standard

StartZone, ou se situe le pupitreinteraction la plupart du temps. Une sorte de vestibule, fermé par un grand rideau de scène (metalshutter). NB : le script StartZone ets obsolète, l’ignorer.

Quand on active le pupitre, le rideau s’ouvre, et dévoile une longue pièce. Une porte au fond : GoalDoor. Il faut l’atteindre, en respectant les cycles red light, green light de la sentinelle.

Le premier niveau possède une StartRoom adjacente au vestibules StartZone. C’est une simple cellule, avec une porte qu’on franchit pour aller vers StartZone.

Pour terminer un niveau, il faut ouvrir la porte du fond en ramassant KeyCollectible (GO posé quelque part dans la scene) et atteindre le trigger sur MainGoal.

Le joueur peut s’il le souhaite performer d’avantage :
Jamais détecté
jamais touché par un tir
temps rapide
clean up certains ennemis (lié à Wheel Of fortune, voir plus bas)
clean up tous les ennemis (difficile)
kill tous les ennemis
finir le jeu sans jamais mourir
Ces compétitions ne sont pas toutes codées, et servent pour le moment de réservoir d’idées pour la progression dans le jeu : débloquer des niveaux secrets, des bonus, etc.
Note : tous les objectifs ne s'appliquent pas à tous les niveaux (certains premiers niveaux n'ont pas d'ennemis).
Système existant à requestionner : La Wheel of Fortune
Rôle game design : fun gambling + variance volontaire sur le niveau suivant + mettre de la pression et récompenser
Justification diégétique parfaite : roue de la fortune = élément naturel de jeu télé 70s
Fonctionnement : bonus ET malus pour le niveau suivant — la tension assumée
Distribution :
Imposée à certains paliers de progression (tous les X niveaux)
Relançable avec les crédits du joueur (= il négocie son destin)
Tension à régler : aujourd'hui le cleanup donne des crédits, mais la wheel peut produire un malus → léger souci de dissonance ("je joue bien, je peux être puni"). À retravailler en Phase 3, probablement en imposant la WoF entre certains niveaux, de manière prédéfinie.
Système non existant, objet d’un chantier proche : La TransitionRoom (cellule entre niveaux, non codée)
Principe
À la fin de chaque niveau, l'IA ré-enferme le joueur. La cellule fait écho à la StartRoom-cellule du tout début (cellule de prison). On peut utiliser cette salle comme une sorte de hub de progression.
Justification narrative
La cellule transition n'est pas juste un outil game design — c'est le symbole de l'emprise de l'IA. Le joueur subit ce rituel d'enfermement pendant tout le jeu. 
Rôle structurel (modèle Talos)
Actuellement on n’affiche aucune stat. Mais si on le décide, alors la TransitionRoom peut permettre une navigation plurielle :
Le joueur peut revisiter des niveaux passés pour réussir les objectifs ratés
Les secrets se débloquent au fil de la complétion, pas seulement à la première run
Le level select n'est pas mort — il est juste diégétisé dans la cellule, pas affiché comme une UI plate
Dans ce cas, deux familles de portes peuvent exister dans la cellule TransitionRoom
Portes "système" (méta-jeu) : restart, exit, menu, settings → pas de continuité spatiale, UI au press X
Portes "diégétiques" (in-game) : niveau suivant, niveaux secrets, retour mastery → continuité spatiale (la cellule est juxtaposée à la startzone du niveau suivant, traversée sans coupure)
Flow de fin de niveau — séquence complète
Joueur touche goaldoor → freeze player
VictoryScale joue (extrusion façon corridor 70s), avec une musique
Option pas encore déterminée : afficher stats et UI sur cette séquence
Wipe / inverse VictoryScale (plusieurs possibilités visuelles à tester, on commencera par la plus simple)
Reprise du contrôle du joueur
Temps 1 (1-2s) — Vidage à vue : les lumières s'éteignent, la sentinelle s'éteint, les props se rangent
Temps 2 (1-2s) — Cellule s'installe : ici plusieurs options : soit le sol sous le player est en fait une dalle qui descend dans le sol façon extrusion intérieure, soit des cloisons apparaissent autour du joueur (plafond/sol/coulissement à tester)
Cellule complète, joueur libre de bouger. Une porte, ou plusieurs comme on l’a déhjà mentionné
Joueur s'approche de la porte → InteractBubble, press X (comme StartRoom actuelle)
Porte s'ouvre, joueur la franchit
Continuité spatiale → startzone du niveau N+1
Règles de design qui guident la séquence :
Rien ne se passe hors-champ — tout est visible, lisible, honnête envers le joueur
3-5 secondes max pour les étapes 6-7 (anticipation du 10e, 20e visionnage)
Mécanique de positionnement réutilisable : connaître la position finale d'un GO, le faire partir d'ailleurs pour y aller (vaut pour les props qui se rangent ET pour les murs qui arrivent)
Découpage en 3 chantiers
Chantier 1 — Plomberie invisible (données)
Tracker tous les événements ingame sans s'occuper de l'affichage :
Nombre de morts
Nombre de tirs reçus
Nombre de détections
Kills / cleanups (avec distinction)
Temps écoulé
(autres à définir)
Données stockées proprement, prêtes à être affichées plus tard.
Chantier 2 — MVP du flow de transition
= Minimum Viable Product : version la plus simple possible du flow, qui marche de bout en bout, testable en jeu.
Contenu du MVP :
La séquence complète post-goaldoor (étapes ci-dessus)
Une seule porte diégétique : niveau suivant (continuité spatiale)
Une seule porte système : restart (UI au press X)
Pas de wheel, pas de stats affichées, pas de portes multiples, pas de secrets
Objectif : valider que le rythme, le feel et la cohérence narrative tiennent debout avant d'enrichir.

Chantier 3 — Enrichissement (si Phase 2 validée)
À ajouter par couches successives :
Affichage des stats pendant le victoryscale ou juste après (ou ailleurs dans la cellule)
Wheel intégrée au flow (paliers obligatoires + relances via crédits)
Portes multiples dans la cellule (retour mastery, secrets, options méta)
Signalisation des niveaux secrets (porte qui apparaît dans la cellule quand un secret est débloqué)
Évolution visuelle de la cellule au fil de la progression (graffitis, marques, accessoires)
Skip / accélération de la transition pour speedrunners ou joueurs qui retry beaucoup
Retravail de l'asymétrie wheel (régler la dissonance cleanup → crédits → potentiel malus)
Inconnues techniques à tester en Phase 2
Le wipe (option B) vs l'inverse des meshes VictoryScale (option A) — commencer par B
Animation d'arrivée des murs de la cellule (plafond / sol / coulissement), ou dalle sous joueur qui s’enfonce
Juxtaposition spatiale : faire spawner la cellule de fin du niveau N pile à côté de la startzone du niveau N+1. Si ceci est trop compliqué, on remplacera par un loadingscreen, et une petite animation du player dans le vestibule du niveau n+1 qui sort de la porte, la porte se referme (similaire à ce qui se passe après startroom actuellement). Mais ce serait cool d’avoir une continuité spatiale quand même.
Principe directeur
On part toujours du cas le plus simple, on teste, on valide, puis on complexifie. Pas de système ambitieux construit d'un coup avant validation du feel.

Claude Code : 
RECAP FLOW et chantiers.
Victoryscale se fait
Garder les meshes à l’écran : coder une fin propre pour victoryscale (on affiche tous les meshes et ils restent affichés ?), à laquelle le joueur impatient peut accèder en appuyant sur un bouton pendant victoryscale. Vérifie car on a déja un système qui permet d’accèder à loadingscreen je crois
Ensuite : par dessus les mehses de victoryscale, afficher un UI de victoire simple pour le moment, disons Level Complete”, et “Nombre d’essais : X”. Si c'est facile, tu peux coder le décompte d’essais pour un niveau, et l’afficher ici.
Faire translater UI + meshes vers la droite comme un masque. Le niveau N réapparaît dessous, on retrouve le perso là où il était.
Translation faite : les lumières s’éteignent et des props se rangent. Pour tester le flow, on peut peut-être juste pour le moment toucher au global volume pour faire une ambiance un peu bleutée sombre.
Le sol sous le player s’enfonce. Préciser ici le set-up unity optimal pour réussir cet effet. Sur cette séquence, la caméra va se positionner vers CM_TransitionRoom (qui n’exitse pas encore). Tout ce qui n’est pas dans le GO “TrasitionRoom” disparaît quand la dalle est à -2m (valeur à tweak)
On se retrouve donc avec player dans transitionroom, qui ressemble à playerroom
AU moins une porte qui emmène vers lvl n+1. Interactbuble sur l’interrupteru allumé à côté de la porte, un UI : go to next level ? Yes, no.
Si yes, fade out noir. Ecran noir peut masque le chargement de N+1. 
Fade in : selon difficulté technique : soit on est ds une fausse transitionroom qui est en fait une startrom de n+1, soit c’est bien transitionroom N qu’on a copié juxtaposée à côté du vestibule N+1. La port est ouverte, on la franchit comme pour startroom.

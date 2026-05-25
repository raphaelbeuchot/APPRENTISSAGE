Récap — Boucle de progression globale
Vision et références
Jeu stealth Unity, ~26 niveaux
Référence majeure : The Talos Principle — on peut finir le jeu de base, mais le dépassement débloque des niveaux secrets. Pas un roguelite, la rejouabilité vient de l'envie de complétion
Fiction : le joueur est coincé dans un gigantesque vaisseau spatial, une IA folle le fait jouer un "1 2 3 soleil" dans une ambiance jeu télé années 70
Pas d'animateur qui parle (scope trop lourd) — l'IA s'exprime par d'autres canaux (texte, jingles, lumières, gimmicks visuels)
Vocabulaire des verbes joueur
Kill un ennemi → fait diminuer la pressure gauge (cycles moins stressants) = récompense de confort immédiat
Cleanup un ennemi (lave, eau, aspirateur) → donne des crédits pour la wheel = capital long terme, plus pénible, plus stealth dans l'esprit
Asymétrie assumée : le joueur "lazy" kill, le joueur "mastery" cleanup
Objectifs de mastery
Conditions de victoire : trouver la Key (= condition de base, pas un objectif optionnel).
Objectifs de dépassement organisés en 2 tiers :
Tier 1 — Faisables avec effort (équivalents en difficulté, dépendent du niveau et du temps cible) :
Jamais détecté
Jamais touché par un tir
Temps cible respecté
Cleanup all (le plus "grind")
Tier 2 — Hardcore / exploit pur :
First try sans mourir
Tier 1 → débloque les niveaux secrets standards. Tier 2 → réservé à quelque chose de vraiment spécial (à définir : fin alternative, ultime niveau, achievement).
Note : tous les objectifs ne s'appliquent pas à tous les niveaux (certains premiers niveaux n'ont pas d'ennemis).
La Wheel of Fortune
Rôle game design : fun gambling + variance volontaire sur le niveau suivant + mettre de la pression et récompenser
Justification diégétique parfaite : roue de la fortune = élément naturel de jeu télé 70s
Fonctionnement : bonus ET malus pour le niveau suivant — la tension assumée
Distribution :
Imposée à certains paliers de progression (tous les X niveaux)
Relançable avec les crédits du joueur (= il négocie son destin)
Tension à régler : aujourd'hui le cleanup donne des crédits, mais la wheel peut produire un malus → léger souci de dissonance ("je joue bien, je peux être puni"). À retravailler en Phase 3.
La TransitionRoom (cellule entre niveaux)
Principe
À la fin de chaque niveau, l'IA rechoppe le joueur et l'enferme dans une cellule. La cellule fait écho à la StartRoom-cellule du tout début (cellule de prison). C'est le hub de progression.
Justification narrative
La cellule transition n'est pas juste un outil game design — c'est le symbole de l'emprise de l'IA. Le joueur subit ce rituel d'enfermement pendant tout le jeu. La promesse implicite : à un moment, on casse la boucle (probablement au dernier niveau, où l'on s'échappe par les coulisses → fusée → liberté).
→ Tout ce qui sera conçu dans la cellule transition doit pouvoir être subverti / brisé au dernier niveau.
Rôle structurel (modèle Talos / option B)
Le joueur peut revisiter des niveaux passés pour chasser les objectifs ratés
Les secrets se débloquent au fil de la complétion, pas seulement à la première run
Le level select n'est pas mort — il est juste diégétisé dans la cellule, pas affiché comme une UI plate
Deux familles de portes dans la cellule
Portes "système" (méta-jeu) : restart, exit, menu, settings → pas de continuité spatiale, UI au press X
Portes "diégétiques" (in-game) : niveau suivant, niveaux secrets, retour mastery → continuité spatiale (la cellule est juxtaposée à la startzone du niveau suivant, traversée sans coupure)
Flow de fin de niveau — séquence complète
Joueur touche goaldoor → freeze player
VictoryScale joue (extrusion façon corridor 70s)
Beat de gloire (court, à enrichir Phase 3 avec stats + UI)
Wipe / inverse VictoryScale (révèle le plateau — piste B = masque coulissant privilégiée pour démarrer, plus économique que l'inverse-meshes)
Reprise du contrôle du joueur
Temps 1 (1-2s) — Vidage à vue : lumières s'éteignent, sentinelle s'inerte, props se rangent
Temps 2 (1-2s) — Cellule s'installe : murs apparaissent autour du joueur (plafond/sol/coulissement à tester, jamais TP)
Cellule complète, joueur libre de bouger
Joueur s'approche de la porte → InteractBubble, press X (comme StartRoom actuelle)
Porte s'ouvre, joueur la franchit
Continuité spatiale → startzone du niveau N+1
Règles de design qui guident la séquence :
Rien ne se passe hors-champ — tout est visible, lisible, honnête envers le joueur
3-5 secondes max pour les étapes 6-7 (anticipation du 10e, 20e visionnage)
Mécanique de positionnement réutilisable : connaître la position finale d'un GO, le faire partir d'ailleurs pour y aller (vaut pour les props qui se rangent ET pour les murs qui arrivent)
Découpage en 3 chantiers
Phase 1 — Plomberie invisible (données)
Tracker tous les événements ingame sans s'occuper de l'affichage :
Nombre de morts
Nombre de tirs reçus
Nombre de détections
Kills / cleanups (avec distinction)
Temps écoulé
(autres à définir)
Données stockées proprement, prêtes à être affichées plus tard.
Phase 2 — MVP du flow de transition
= Minimum Viable Product : version la plus simple possible du flow, qui marche de bout en bout, testable en jeu.
Contenu du MVP :
La séquence complète post-goaldoor (étapes 1 à 11 ci-dessus)
Une seule porte diégétique : niveau suivant (continuité spatiale)
Une seule porte système : restart (UI au press X)
Pas de wheel, pas de stats affichées, pas de portes multiples, pas de secrets
Objectif : valider que le rythme, le feel et la cohérence narrative tiennent debout avant d'enrichir.
Niveau 1 et dernier niveau :
Niveau 1 : sortie via cellule transition comme les autres
Dernier niveau : à concevoir plus tard (probablement rupture du rituel)
Phase 3 — Enrichissement (si Phase 2 validée)
À ajouter par couches successives :
Affichage des stats pendant le beat de gloire (ou ailleurs dans la cellule)
Wheel intégrée au flow (paliers obligatoires + relances via crédits)
Portes multiples dans la cellule (retour mastery, secrets, options méta)
Signalisation des niveaux secrets (porte qui apparaît dans la cellule quand un secret est débloqué)
Évolution visuelle de la cellule au fil de la progression (graffitis, marques, accessoires)
Skip / accélération de la transition pour speedrunners ou joueurs qui retry beaucoup
Retravail de l'asymétrie wheel (régler la dissonance cleanup → crédits → potentiel malus)
Inconnues techniques à tester en Phase 2
Le wipe (option B) vs l'inverse des meshes VictoryScale (option A) — commencer par B
Animation d'arrivée des murs de la cellule (plafond / sol / coulissement)
Juxtaposition spatiale : faire spawner la cellule de fin du niveau N pile à côté de la startzone du niveau N+1
Décisions encore ouvertes (à trancher quand on y arrivera)
Durée exacte du beat de gloire
À quel moment exact reprend-on le contrôle player (pendant le vidage ? après ?)
Vidage du plateau : option A (complet) ou B (partiel/lumières) — penche vers B
À quoi ressemble visuellement la cellule transition (identique à StartRoom originelle ou variation ?)
Comment évaluer les temps cibles pour l'objectif "temps" (pro / medium / easy)
Niveau d'agentivité du joueur dans la cellule pour la wheel (la lance lui-même ou subit ?)
Principe directeur
On part toujours du cas le plus simple, on teste, on valide, puis on complexifie. Pas de système ambitieux construit d'un coup avant validation du feel.

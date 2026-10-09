# Lore et design — fil narratif du jeu

*Ouvert le 2026-10-09. Document de référence, rien n'est codé. Le lore et le game design vont ensemble ici : chaque élément de fiction doit servir une mécanique, et inversement.*

*À ne pas confondre avec `Assets/DESIGN.md` (transitions entre niveaux, séquence de victoire) ni avec `MULTIJOUEUR_LOCAL.md` (chantier multi).*

---

## Le problème de départ

Hésitation entre deux jeux :
- **arcade** : on enchaîne des niveaux rigolos, sans lore ni mise en scène ;
- **narratif façon Portal / Talos** : une surcouche d'histoire.

**Contrainte ferme : dev solo.** Pas de voix, pas de cinématiques coûteuses. Le jeu ne pourra pas être un Portal dans sa narration.

**Résolution** : ce n'est pas l'un *ou* l'autre. Portal est structurellement une suite de salles de test courtes ; la narration passe par le format et les transitions, pas par des interruptions du jeu. On vise donc **un cœur arcade avec un fil narratif léger et diégétique**, porté par des moyens sobres.

## Références

- **Superhot** — la plus proche : niveaux arcade très courts, et entre eux du texte brut qui devient inquiétant. Aucune voix. **Le côté brut et hyper sobre est un style en soi.**
- **Ape Out** (Devolver) — inspiration nette, découverte pendant le dev : un captif qui s'échappe, aucun texte en jeu, la structure (faces d'un album) porte le récit, style graphique et sonore très affirmé.
- **Le Prisonnier** (série, 1967) — village-prison absurde et coloré, surveillance permanente, tentatives d'évasion. Esthétique et ton 70s.
- **Portal** — humour noir, une machine qui te fait passer des tests, la révélation par le décor.
- Le jeu télévisé dont le prix est la liberté : *Running Man*, *Squid Game*, *The Truman Show*.

## Le concept

**Panopticon** — nom d'origine du jeu, choisi parce qu'il sonnait 70s et technologie 70s. Mais le panoptique est d'abord un **dispositif pénitentiaire** (Bentham) : une tour centrale d'où un gardien peut observer chaque détenu sans que celui-ci sache s'il est regardé. Le détenu finit par se surveiller lui-même (Foucault).

**La mécanique centrale est déjà le thème** : le « 1, 2, 3 soleil » avec la sentinelle, c'est un œil central, et le joueur doit se figer quand il regarde.

**En une phrase (brouillon)** : *on est un détenu dans un jeu télévisé pénitentiaire ; l'évasion est possible, si on y survit — et c'est mis en scène.*

## Le monde

- **Un monde vide.** Le décor est complètement noir en dehors de la salle de jeu. On ne voit pas les spectateurs ; si on les voyait, on ne verrait que des **sièges vides**.
- **Une machine (une IA) tourne en boucle sur ce système.** Plus personne aux commandes.
- **Le titre le dit déjà : « They Left the Game On (Again!) »** — le show a été laissé allumé.
- **La foule** (`CrowdReactionManager`, réactions audio) : à recadrer comme des **rires enregistrés** — une foule en conserve qui réagit pour personne.

## Le joueur

- **Un détenu.** Stylisé d'abord comme un janitor, il peut être perçu comme un prisonnier.
- **Il doit s'échapper.** Le lieu permet l'évasion, à condition d'y survivre.

### Progression : la promotion

- **On commence sans le balai.**
- Au fil de la progression, **le panoptique décide qu'on a mérité de monter en grade** : on devient **nettoyeur**.
- Logique pénitentiaire réelle : le détenu modèle obtient un statut, un travail, des privilèges en échange de sa docilité. Le système récompense la conformité.
- Côté design : **porte de progression naturelle** (le balai arrive quand la base est maîtrisée) et un vrai moment de mise en scène au déblocage. Cohérent avec la décision déjà prise : le balai est un **outil défensif**, pas une arme (dégâts balai à 0).

### Le nettoyage : qui, quoi ?

**Non tranché** — principe : on revient dans des salles déjà traversées pour y nettoyer des cadavres. Trois pistes pour *quels* cadavres :

1. **Les ennemis morts dans la salle** (première idée). Lien direct avec le premier passage : on nettoie les conséquences de sa propre traversée. La plus proche de l'existant (les ennemis morts deviennent déjà des cadavres physiques, le système de crédits de ménage tourne autour d'eux). Lore : on ramasse des ex-détenus costumés ; un costume qui glisse peut montrer ce qu'il y a dessous.
2. **Le personnage du joueur, aux positions exactes où il est mort** lors du premier passage. Les échecs du joueur deviennent le contenu ; très panoptique (*le système n'oublie rien*). La plus forte en mystère : ouvre vers des théories folles (« ils ont cloné Tyrone » : clonage, détenus en série, mémoire effacée…). La plus coûteuse techniquement.
3. **D'autres participants.** La plus simple et lisible (« d'autres sont passés avant toi »), mais la plus générique.

- **Direction envisagée par l'utilisateur (2026-10-09) — la confiance de la machine comme progression** :
  - **Au début** : on repasse par les salles déjà traversées, où on a laissé des **cadavres d'ennemis** (piste 1).
  - **Au bout de quelques niveaux**, la machine est contente de nous : on travaille bien, elle peut nous faire confiance. Elle nous fait alors nettoyer des salles aux **cadavres plus dérangeants** : des prisonniers, **des gens qui nous ressemblent** (vers la piste 2 et la théorie du clone). Moment de bascule possible : le premier corps qui porte la tenue et le visage du joueur.
  - Ces salles peuvent être **de nouvelles salles**, **bloquées** parce que des participants y sont morts. Objectif double : **récupérer la clé et nettoyer les cadavres**. Synthèse des deux modes (premier passage + revisite), bon contenu de fin de progression.
  - **La machine est un peu ennuyée de nous demander ça**, mais « ça lui rendrait service » si on allait débloquer ces salles. Personnalité de la machine, portée uniquement par le texte (poli, gêné, glaçant).
  - **Pourquoi une salle se bloque** : un participant n'a pas réussi à la passer, et on ne peut pas envoyer un nouveau participant dans une pièce avec un cadavre. **Pour la machine, un cadavre dans une salle est un bug de son jeu.** Plus d'humains pour la maintenance (*They Left the Game On*) : elle doit demander à un détenu de corriger ses propres bugs — d'où sa gêne, elle avoue un dysfonctionnement. Le janitor devient de fait le technicien de maintenance d'un système abandonné : prisonnier de la machine et seul capable de la faire tourner.
- **Cas à prévoir : la salle revisitée est vide** (aucun ennemi tué au premier passage, ou tous déjà nettoyés — le nettoyage existe dès le premier passage). **Non tranché**, pistes :
  1. La machine n'envoie nettoyer que les salles sales (et félicite le joueur zélé). S'adapte seul, mais un joueur très propre ne verrait presque jamais la revisite.
  2. **La salle a continué de tourner en notre absence** (*They Left the Game On*) : la sentinelle a continué ses cycles, des humanoïdes costumés ont bougé au mauvais moment et se sont fait abattre → il y a toujours des cadavres au retour, placés dans les zones couvertes par la sentinelle. Règle le problème par la fiction ; malaise : on n'était pas là, et des gens sont quand même morts.
  3. La toute première revisite (celle qui présente le mécanisme) est scénarisée, cadavres placés à la main.
  - *Suggestion* : 2 comme règle générale + 3 pour la première revisite.
- **Cas à prévoir** si la piste 2 est retenue : un joueur qui n'est jamais mort dans un niveau n'y a laissé aucun cadavre → compléter avec la piste 1 ou 3 pour que la salle ne soit pas vide.
- **Conditions pour que la revisite ne soit pas du remplissage** : un autre objectif (nettoyer, pas traverser), des cadavres qui racontent quelque chose, quelques changements de décor ou de règles, et plus court que la première visite.
- **Coût technique** (plus tard, pistes 1 et 2) : enregistrer les positions de mort (ennemis et/ou joueur) par niveau et par partie, les persister, puis faire apparaître des cadavres à ces positions. Briques existantes à regarder : `LevelStatsTracker`, `CorpseRagdoll` / `DeadBodyPhysics`, `CleaningCreditManager` / `CleaningBonusManager`.

### La revisite : carte d'accès et porte latérale (2026-10-09)

- **Promotion = carte d'accès.** Premier passage (mode solo de base) : détenu, il faut trouver la **clé** pour ouvrir la porte de sortie. Revisite : membre du personnel, la **carte d'accès** ouvre les portes, plus besoin de clé. Le changement de statut se lit dans la façon de traverser la salle.
- **Déroulé** : on entre dans une salle déjà vaincue, **elle tourne toujours** (*They Left the Game On*), on nettoie les cadavres, et on ressort par une **porte latérale** quand c'est fait.
- **Gameplay** : si le cycle de la sentinelle tourne encore, **on nettoie sous surveillance** (se figer au Red Light). Même mécanique, autre objectif → la revisite n'est pas du remplissage. *(Ennemis présents ou non pendant la revisite : à décider.)*
- **Contrainte de LD à anticiper** : la porte latérale **existe dès le premier passage, fermée** (« Personnel uniquement »). C'est du teasing façon metroidvania et de la narration gratuite. Elle doit être accessible et lisible, mais hors du chemin principal pour ne pas embrouiller le premier passage. Son emplacement définit le trajet de la revisite (traverser la salle, passer par les zones de mort, revenir).
- **Périmètre conseillé** : la porte latérale seulement dans les niveaux qu'on revisitera, pas dans tous, pour garder le chantier petit.

## Les ennemis

Deux familles :

| | Robots | Humanoïdes costumés |
|---|---|---|
| Ce que c'est | machines rudimentaires, outils du système | créatures sous des costumes comiques (selon les biomes), **sans doute d'ex-prisonniers** |
| La sentinelle peut les abattre | **non** | **oui**, comme le joueur |
| Statut | à créer (souhaité) | proches des zombies actuels |

- La règle se lit visuellement et sert la fiction : les humanoïdes sont des détenus sous le même regard, les robots appartiennent au système.
- Elle ouvre du level design : attirer un humanoïde dans le regard de la sentinelle, ce qui ne marche pas avec un robot.
- Ton : costumes comiques en surface, malaise par petites touches (« qu'y a-t-il sous le costume ? »).
- **Code à prévoir si on va dans cette direction** : une immunité aux tirs de la sentinelle pour les robots ; classer les ennemis existants (Chaser, Hitter, Grabber, Bloat, Blinder, Swarm, Bright Eyes…) dans l'une ou l'autre famille.

## La machine : seule voix du jeu, en texte

- Pas de voix : la machine s'exprime **uniquement par du texte**, style Superhot : cartons dans la cellule, règlement du show, titres d'épisodes, message de promotion, commentaires (`CommentPanel`).
- Ton : poli, bureaucratique, un peu détraqué. Il porte à la fois l'humour et le malaise.

## Règle de budget narratif

**L'histoire ne vit qu'entre les niveaux et dans le décor, jamais dans le gameplay.**
- Lieux narratifs : la **cellule** (séquence d'ouverture), la **salle de transition**, l'**écran de victoire**.
- Par niveau : un texte, un détail de décor. Budget fixe, qui monte en charge sans exploser.
- Les niveaux restent de l'arcade pure, rejouables.

### Moyens de raconter, du moins cher au plus cher

1. **Format TV** : épisodes, numéros de détenu, titres façon programmes (l'équivalent des faces d'album d'Ape Out).
2. **Texte de la machine** (cartons, règlement, commentaires).
3. **Décor qui se dégrade** au fil des niveaux : écrans qui figent, sentinelle qui a des « ratés », foule enregistrée qui se répète.
4. **La fin** (seule vraie mise en scène à produire).

## Piste de fin (non tranchée)

Dans le panoptique, la tour n'a pas besoin d'être occupée : la possibilité d'être vu suffit. Le joueur atteint la tour et la trouve **vide** : le show tourne en automatique, la foule est enregistrée, les gardiens sont partis depuis longtemps. On s'est plié aux règles pour un regard qui n'existait plus.

## Multi

Fiction gratuite : **deux détenus, une seule place de libération par épisode**. La course Versus a un sens.

## Ton

Registre *Le Prisonnier* / Portal : **absurde, coloré, drôle en surface**, malaise par petites touches. Le contraste entre zombie comique et thème pénitentiaire fait le charme ; éviter de basculer dans le sinistre.

## Déjà en place dans le projet et qui sert ce fil

- Séquence d'ouverture : réveil dans une **cellule**, bouton, porte (`OpeningSequenceManager`).
- **Salle de transition** entre niveaux, séquence de victoire.
- Titre de compte à rebours « Super Panopticon! », `WheelOfFortune`, foule, `CommentPanel`.
- Système de nettoyage : `CleaningCreditManager`, `CleaningBonusManager`, cadavres ragdoll.
- Balai déjà conçu comme outil défensif ; `PlayerLoadout` permet de commencer sans balai.

## Questions ouvertes

- Le concept en une phrase : à affiner.
- Que rapportent les crédits de nettoyage dans la fiction ? (rachat de sa sortie ? illusion de progrès ?)
- **Quels cadavres nettoie-t-on** : ennemis morts dans la salle, le joueur lui-même, d'autres participants, ou une progression qui combine ? (voir « Le nettoyage »)
- Si on nettoie ses propres cadavres : le joueur est-il le même détenu à chaque essai, ou un nouveau ? (respawn = clone, nouveau détenu, même détenu ? « ils ont cloné Tyrone »)
- La fin : tour vide confirmée ? que devient le joueur ensuite ?
- Combien de niveaux revisités en mode nettoyage, et lesquels ? (chacun demande une porte latérale prévue au LD)
- **En quoi consiste le nettoyage, concrètement ?** Pousser les corps au balai vers une trappe ou un incinérateur ? Passer dessus ? Lien avec les crédits et le bonus de ménage déjà codés. Définit tout le gameplay de la revisite.
- Pendant la revisite : ennemis présents, absents, différents ?
- Classement des ennemis existants en robots / humanoïdes.

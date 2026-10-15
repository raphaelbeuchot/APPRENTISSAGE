# Structure de la campagne — progression et agencement des niveaux

*Ouvert le 2026-10-15. Notes de discussion, rien n'est codé ni réordonné. Les pistes marquées « suggestion » ne sont pas tranchées.*

*Liens : `LORE_ET_DESIGN.md` (promotion, carte d'accès), `PASSAGE_EN_PRODUCTION.md` (section « Catalogue de niveaux » : l'ordre réel vit dans `Project/Resources/LevelCatalog.asset`), `Assets/DESIGN.md` (boucle de progression, objectifs, Talos).*

---

## La question de départ

Le sentiment de progression du joueur : aujourd'hui deux upgrades (tomates, balai). Est-ce suffisant ?

- Le nombre d'upgrades n'est pas le seul levier. Sources de progression : puissance (outils), nouveauté (pièges, ennemis), maîtrise, statut et récit (promotion, carte d'accès), méta (objectifs, secrets).
- Les références (Superhot, Portal, Ape Out) reposent surtout sur la nouveauté et la maîtrise, très peu sur les upgrades.
- Vigilance : un outil qui aide à contourner le « 1, 2, 3 soleil » affaiblit le cœur du jeu. Un bon outil change *ce qu'on fait* sous la contrainte.

## Agencer les niveaux : blocs, mélange ou biomes ?

Règle commune : **présenter une mécanique seule, sans danger, puis la combiner**. Dans un même niveau, soit la mécanique est nouvelle, soit le défi est dur, pas les deux.

Trois modèles discutés :
1. **Blocs par piège** : profondeur, mais lassitude et peu de combinaisons.
2. **Mélange complet** (proche de l'actuel) : variété, mais on oublie entre deux apparitions et la campagne n'a pas d'arc.
3. **Biomes** de 3 ou 4 niveaux, avec montée de difficulté et variation des pièges.

**Constat de l'utilisateur** : l'erreur a peut-être été d'associer biome et piège.

**Nuance** : un biome peut garder **une mécanique maison** et accueillir **des invités** (pièges ou outils venus d'ailleurs). C'est le plus réaliste en dev solo, car les décors sont construits autour de leur mécanique.

**Critère de tempo** (suggestion) : certaines mécaniques **tiennent un arc** (assez de variations pour s'enchaîner) ; d'autres sont **lentes et cycliques** et lassent si on les enchaîne. Ces dernières peuvent servir de **niveaux de respiration** entre deux arcs. À passer au crible pour chaque famille de pièges.

**Convention du jeu vidéo** (Super Meat Boy, Isaac, Celeste, Portal 2) : **un biome forme un bloc continu**, on ne le quitte pas pour y revenir plus tard dans la campagne principale. Dans le bloc, c'est la mécanique qui varie (Mario, et Talos où les mécaniques traversent les trois mondes). Revenir dans un biome existe, mais **comme un mode à part** (Dark World de Super Meat Boy, faces B et C de Celeste, secrets de Talos) : c'est l'équivalent de **la revisite en nettoyeur** de `LORE_ET_DESIGN.md`.

Conséquence : les outils ne se règlent pas en découpant un biome mais en le **plaçant** dans la campagne (un biome qui demande les tomates et le balai vient après leur présentation).

**Format TV** (suggestion) : un arc = une saison, son dernier niveau = la finale, moment naturel pour une promotion (balai, carte d'accès).

## Biome Flower (plateformes rotatives) — un arc qui fonctionne

| Niveau | Rôle |
|---|---|
| Go for a Spin | présentation : aller dans le sens de la plateforme pour aller plus vite |
| Flower Power I | invités : les ennemis (comment ils réagissent sur les plateformes) |
| Flower Power II | invités : les bombes |
| Flower Power III | examen : plateformes au-dessus d'une fosse d'acide, maîtrise vitale |

- **Écart actuel** : Go for a Spin est le niveau 2 du catalogue, Flower I le 19.
- `Level_Flower03.unity` existe mais **n'est pas encore dans le `LevelCatalog`**.
- À faire au passage : ajouter **l'acide** au menu déroulant du composant `SplinePitZone` (son `FillType` ne propose que None, Water, Lava).

## Biome Lava (train) — exemple pris en notes

| Niveau | Rôle |
|---|---|
| Lava Train I | présentation : le train seul, on apprend à le maîtriser |
| Lava Train II | mécanique en plus : ouvrir le `LockableTargetManager`, sinon le train est bloqué et on tombe dans la lave |
| Lava Train III | train + ennemis |

- Mécanique lente, soumise aux cycles et au rythme des trains : **risque de lassitude si les niveaux s'enchaînent**.
- **Prérequis de Lava Train II** : il faut maîtriser le lancer de tomates sur des cibles qui débloquent des portes. Deux options :
  1. **Ordre** : Lava Train II vient juste après un niveau qui présente ce système (aujourd'hui TomaThrow, `Level_TomatoFest_01`). C'est déjà le cas dans le catalogue actuel (TomaThrow 13, Lava Train II 14).
  2. **Biome autonome** : une partie de niveau (ou un niveau) qui « révise » les tomates avant d'en avoir besoin. Suggestion : une zone d'ouverture sans danger dans Lava Train II (une cible qui ouvre une porte avant le premier train).
- Notion générale qui en sort : chaque niveau a des **prérequis** (outils, mécaniques déjà vues). Si on réorganise par biomes, l'ordre doit les respecter.

## Le balai et les tomates dans une campagne en biomes

**Le problème** : chaque biome suit un gabarit proche (découverte, niveau plus dur, niveau avec ennemis, niveau ultime). Or les niveaux **avec ennemis mais sans balai ni tomates** sont très bons et durs, ce qui paraît paradoxal si on commence sans balai et qu'on l'obtient ensuite.

**Les tomates sont déjà la bonne façon** : des caisses (`TomatoCrate`) posées dans le niveau. Ce sont des accessoires de l'épisode, pas un équipement possédé, et on voit tout de suite comment jouer le niveau.

**Direction envisagée par l'utilisateur (2026-10-15) — le balai comme accessoire de l'épisode** :
- Un **socle avec un balai** à disposition dans les niveaux qui en ont besoin. L'équipement devient une variable du niveau, comme les pièges invités : plus de paradoxe d'un niveau à l'autre.
- Un **socle de dépôt** en sortie de niveau si besoin : *« Pour continuer, merci de déposer votre balai. »* Logique pénitentiaire réelle : les outils de travail sont comptés, pris et rendus à chaque service.
- L'obtention du balai dans le lore (la promotion) est mise de côté pour l'instant.
- Texte de la machine pour un niveau sans balai (piste) : *« Vos privilèges de nettoyage sont suspendus pour la durée de cet épisode. Nous vous remercions de votre compréhension. »*

Décidé par l'utilisateur (2026-10-15) :
- **Placement du socle de prise : selon le niveau**, comme les tomates (parfois dès le début, parfois plus loin). Un niveau difficile au début devient jouissif quand on récupère le balai au milieu.
- **Dépôt : hyper rapide et satisfaisant, très arcade.**
- **Multi : pas de socle, on a toujours le balai.**

Points ouverts :
- **La promotion et les portes fermées** (carte d'accès, porte latérale « Personnel uniquement » de `LORE_ET_DESIGN.md`) : à discuter, maintenant que le balai ne porte plus la promotion.
- Revisite en nettoyeur : prendre le balai au socle en entrant pour faire son service.
- Code : `CE_QUI_RESTE_A_FAIRE.md` prévoit déjà le ramassage (`InteractBubble` + `EquipBroom()` à ajouter dans `PlayerLoadout`) ; le dépôt en serait l'inverse.

## Biomes

Direction artistique rappelée : **thématiques ciné et TV des années 70**.

**Existants** : plateau TV de base, Lava, Flower Power, Western, Rush Hour (pas très beau, mais l'idée plaît), plus Bowling et la glace (*Caution floor slippery*), qui peuvent devenir des biomes ou servir d'invités.

**Idées (2026-10-15)**, en réutilisant des mécaniques existantes :
- **Centre commercial** (*Zombie*, Romero 1978) — **plaît beaucoup** pour les tapis roulants (escalators). Invités possibles : rayons en labyrinthe, chariots en obstacles mobiles.
- **Station spatiale** (*Cosmos 1999*, *2001*, *Moonraker*) — idée d'**endgame** de l'utilisateur : *on est dans une vraie station spatiale* (rejoint la fiction du vaisseau de `Assets/DESIGN.md`). Pas sûr de la garder.
- **Plage** (*Les Dents de la mer*) — fosses d'eau et nage ; lien avec l'idée d'ennemi dans les fosses d'eau (`CE_QUI_RESTE_A_FAIRE.md`, backlog).
- **Discothèque / roller disco** (*Saturday Night Fever*) — dalles lumineuses rythmées, sol glissant réutilisé.
- **Émission de cuisine** — plateau TV dans le plateau, foyer naturel des tomates.
- **Film catastrophe** (*L'Aventure du Poséidon*, *La Tour infernale*) — spectaculaire mais coûteux.
- **Chalet de ski** (James Bond de l'époque) — la glace en biome complet.
- ~~Kung-fu / salle des miroirs~~ — **vigilance** : risque de caricature raciste, à éviter sous cette forme.

## Questions ouvertes

- Blocs, mélange ou biomes : non tranché.
- Quelles familles de pièges tiennent un arc, lesquelles sont des respirations ?
- Où placer la présentation des tomates et du balai dans une campagne par biomes ?

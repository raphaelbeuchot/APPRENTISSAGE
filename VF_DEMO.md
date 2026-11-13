# Chantier VF : version française de la démo

> Récap de la séance du 2026-11-13 sur le PC du bureau, recopié ici le même jour (la mémoire de Claude du bureau ne suit pas d'une machine à l'autre).
> Tableau complet (texte actuel, proposition VF, emplacement, source) : Google Sheet « inventaire_VF_demo ».
> Rien de ce qui est marqué « envisagé » n'est tranché.

## Contexte

Objectif : avoir une version française de la démo de *They Left the Game On (Again!)*.

Inventaire fait en lecture seule sur le PC du bureau, branche `Version-2-TV-SHOW`, commit `9e515b8f`. Rien n'a été modifié dans le projet. Depuis, deux commits boss (`fe67d673`, `704e0d7f`) : d'éventuels textes de fin du boss ne sont pas dans l'inventaire.

## Règle de travail sur deux machines

Le travail fait sur une machine n'est peut-être pas tout commité ni sauvegardé. Avant de toucher aux scènes ou aux prefabs : File > Save dans Unity, commit + push, puis pull sur l'autre poste. Avant ça, on se limite à créer de nouveaux fichiers (scripts, CSV), qui ne peuvent pas créer de conflit.

## Périmètre : la démo uniquement

- Les 22 niveaux cochés `inDemo` dans le `LevelCatalog` : It's Show Time, Ice, Go For a Spin, HandleHurdles, StrangeAttractor, GoWithTheFlow01, TrafficJam_01 et 04, WesternFlow, Flower01/02/03, TomatoFest_01, LavaTrain01/02/03, TrouNormand, Acid_01/02, Pit'em All, WesternCanyon, Casino.
- MainMenu, LoadingScreen, LevelSelect.
- Le multi : MultiStart, Level_CoinRace_Lava, Race_Lava, Race_Slippery, Race_Void et `UI_Multi.prefab`.
- Hors périmètre : la roue de la fortune, les scènes Tuto, les scènes OLD et les niveaux non cochés `inDemo`.

## Volume : environ 105 textes

| Bloc | Textes | Où ça se modifie |
|---|---|---|
| Noms + titres de lancement des niveaux | ~35 | Tout dans `Resources/LevelCatalog.asset` (`levelName`, `launchText`) |
| Menus, pause, options, chargement | ~18 | MainMenu, `UI.prefab`, `OptionsPanel.prefab`, `OptionsPanelUI.cs`, `LoadingScreenManager.cs`, `LevelSelectUI.cs` |
| Tutos en texte dans les niveaux | ~12 | Directement dans les scènes (`TMP_Climb`, `TMP_Crouch`, « Press A to Lundge »…) |
| Messages (pupitre, victoire, porte, train) | ~8 | Inspector : Pupitre, VictoryUI, TRANSITIONROOM, BROOMPEDESTAL, `LavaTrain_01.asset` |
| Game Over | 9 | `GameOverUI.cs` + assets `DeathCause` (Lava, Acid, OutOfBounds) |
| Multi | ~15 | MultiStart, MultiLevelStart, `MultiRaceManager.cs`, `MultiCoinRaceManager.cs`, `MultiStartUI.cs`, `UI_Multi.prefab` |

## Constats importants

- **La police n'a aucun accent.** `Fonts/KeeponTruckin.ttf` ne contient ni é, è, à, ç, ni œ, alors qu'environ 130 textes TMP l'utilisent. Il faut une version de la police avec les accents, ou une police de secours TMP (fallback). Voir aussi « KeeponTruckin SDF.asset, toujours dirty » dans `CE_QUI_RESTE_A_FAIRE.md` (TECH).
- **Le jeu mélange EN et FR.** Une quinzaine de textes sont déjà en français : « Quitter », « Essais : », « A pour quitter », « EX AEQUO », « RETOUR », « GRIMPER : … », « Nettoyage ! », le message du pupitre (écrit sans accent), « Appuyer sur L2 pour frapper au balai ». L'anglais sera la langue de base, et ces textes ont besoin d'une version EN.
- **Ancien système de tuto en images (`PupitreTuto`)** : dans la démo, il ne reste que sur un pupitre de TrouNormand, avec 4 images (Tuto_Camera, Climb, Crouch, Dash « _FR »). Leur texte est en anglais malgré le suffixe. Il faut soit remplacer ce pupitre par le système actuel, soit refaire les 4 images.
- **Des phrases sont assemblées dans le code**, à passer en texte avec emplacement (`{0}`) :
  - « Timer display » + On/Off
  - « Difficulty » + Easy/Normal
  - « Stress » + n + « % »
  - « < First to n > »
  - « LEFT/RIGHT » + « WINS THE MATCH » : à remplacer par deux phrases complètes
- **Beaucoup de doublons** : « Press A to Lundge » se trouve dans 8 scènes, le message du pupitre dans 24, et `UI_Multi.prefab` est une copie de `UI.prefab`. Avec un système de clés, chaque texte n'est écrit qu'une fois.
- **Fautes d'anglais à corriger** : « Lundge » → « Lunge », « Spick and span » → « Spic and span ».
- **À vérifier en jeu** :
  - Est-ce que « Health: 100% / Status: OK » (`StatsText`) est visible, ou est-ce du debug ?
  - Est-ce que `MultiDeviceAssignment` (« clavier », « manette (en attente) ») sert encore ? Il n'est posé dans aucune scène.

## Décision envisagée : un système maison léger

Le package Unity Localization paraît trop lourd pour environ 105 textes et deux langues. Le système prévu :
- un CSV `clé ; EN ; FR`, construit à partir de la Google Sheet ;
- `Loc.Get("clé")` dans le code ;
- un composant `LocalizedText` à poser sur les textes TMP des scènes et prefabs ;
- un choix de langue dans les Options.

## Prochaines étapes

1. Sauvegarder, commit et push tout ce qui est en cours sur la machine de la maison.
2. Régler le problème de police (accents).
3. Créer `Loc.cs`, le CSV et `LocalizedText` (nouveaux fichiers, sans risque de conflit).
4. Brancher les textes, bloc par bloc : `LevelCatalog`, puis `UI.prefab` et `UI_Multi.prefab`, puis le code, puis les scènes.
5. Traduire les jeux de mots des titres de niveaux : Handle Hurdles, Row Deal, Purple Pain, Pit 'em all, Heart goes Broom.
6. Décider du sort du pupitre tuto de TrouNormand.

## Liens avec les autres chantiers (2026-11-13)

- Les nouveaux textes du boss (Cleaned Up, victoire, fin de démo, « Phase N ») peuvent naître directement en EN + FR si le système est en place avant : voir `BOSS_RUSH_HOUR_IV.md`.
- Des retouches de scènes sont en attente (Flower Power II, Lava Train III, AcidFloor I…, voir `RAPPORT_PLAYTEST.md`, session du 2026-11-13) : mieux vaut ne pas brancher la traduction dans une scène en même temps qu'on la retouche.

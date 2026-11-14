# Chantier VF : version française de la démo

> Récap de la séance du 2026-11-13 sur le PC du bureau, recopié ici le même jour (la mémoire de Claude du bureau ne suit pas d'une machine à l'autre).
> Tableau des textes : Google Sheet « inventaire_VF_demo » (Drive de raphaelbeuchot@gmail.com), créée le 2026-11-14 sur le PC maison. C'est la source : le CSV que lira le jeu en sera exporté, jamais retouché à la main.
> Rien de ce qui est marqué « envisagé » n'est tranché.

## Contexte

Objectif : avoir une version française de la démo de *They Left the Game On (Again!)*.

Inventaire fait en lecture seule sur le PC du bureau, branche `Version-2-TV-SHOW`, commit `9e515b8f`. Rien n'a été modifié dans le projet. Depuis, deux commits boss (`fe67d673`, `704e0d7f`) : d'éventuels textes de fin du boss ne sont pas dans l'inventaire.

## Inventaire refait le 2026-11-14 (PC maison, commit `c881aa7f`)

- La Sheet annoncée le 13/11 n'avait jamais été créée : inventaire refait en lecture seule (catalogue, scripts, assets, scènes et prefabs via `unityscan.pl texts`).
- 104 lignes, une par texte unique, doublons regroupés. Colonnes : `bloc | clé | EN | FR | contexte | source | note`. Le jeu ne lira que clé, EN, FR.
- Clés en `bloc.nom` (`level.`, `menu.`, `options.`, `hud.`, `tuto.`, `gameover.`, `victory.`, `multi.`…). `{0}` = valeur insérée par le code, `\n` = retour à la ligne.
- FR = propositions, rien n'est validé. Jeux de mots laissés vides (« à trouver »). Propositions écrites au vouvoiement : tu ou vous reste à trancher.
- Questions relevées dans la colonne note : objets inactifs (sert encore ?), `StatsText` (debug ?), les 5 messages Game Over du code qui ne s'affichent pas, boutons PlayStation (tomates) et Xbox (ailleurs) mélangés.
- Les textes du boss vus dans le code : seulement « Cleaned Up ! » (les « Phase N » ne sortent que dans la Console).

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

## Système en place (2026-11-14, validé en jeu)

Système maison (le package Unity Localization était trop lourd pour ~100 textes et deux langues).

- **`Loc.cs`** (`Scripts/Localization`) lit `Resources/Localization/Loc.csv`.
  - `Loc.Get("clé")` ou `Loc.Get("clé", valeur)` pour les `{0}`.
  - `Loc.Text(texte)` pour un texte saisi dans l'Inspector ou un asset (catalogue, pupitre, cause de mort…) : il le retrouve dans la Sheet (EN ou FR, sans tenir compte des accents, majuscules, ponctuation) et le traduit, sinon il le renvoie tel quel.
  - FR vide → EN. Clé inconnue → la clé s'affiche à l'écran.
  - Langue sauvegardée dans les PlayerPrefs ; au premier lancement, langue de Windows.
- **`LocalizedText`** : composant sur un texte TMP fixe, avec sa clé. 57 posés (UI, UI_Multi, UI_ShowTime, tutos des niveaux, MainMenu, MultiStart, scènes multi).
- **Outil `Game Catalog > Localisation`** (`Editor/LocBranchement.cs`) : « Analyser » (rapport dans `Logs/LocBranchement.txt`, ne modifie rien) puis « Brancher ». À relancer pour un nouveau texte TMP fixe ajouté à la Sheet.
- **Code branché** : titre animé et noms de niveaux, CommentPanel (tous les messages), Game Over, Victoire, Options, Chargement, jauge de stress, multi.
- **DevPanel (F1)** : changer de langue, relire le CSV.

### Circuit pour les textes

1. Modifier la Sheet « inventaire_VF_demo ».
2. Demander à Claude « exporte » : il télécharge la Sheet en CSV dans `Resources/Localization/Loc.csv` et commite. Ne jamais retoucher le CSV à la main.
3. Attention : si l'EN d'un texte rangé dans le catalogue, un asset ou l'Inspector change (colonne « source »), l'ancien texte n'est plus reconnu : mettre la source à jour en même temps (Claude vérifie à l'export).

## Prochaines étapes

1. Régler le problème de police (accents) : sans ça, les é, è, à, ç s'affichent en carrés.
2. Ajouter le choix de langue dans les Options (`OptionsPanelUI.cs` + une 4e ligne dans `OptionsPanel.prefab`).
3. Remplir la Sheet : relire les propositions FR, trouver les jeux de mots, trancher tu ou vous, puis exporter.
4. Décider du sort du pupitre tuto de TrouNormand (4 images de texte anglais).
5. Restes hors système : « Nettoyage ! » du CommentManager (texte de départ), `StatsText` (debug ?), « Super Panopticon! » de secours dans GameUIManager.

## Liens avec les autres chantiers (2026-11-13)

- Les nouveaux textes du boss (Cleaned Up, victoire, fin de démo, « Phase N ») peuvent naître directement en EN + FR si le système est en place avant : voir `BOSS_RUSH_HOUR_IV.md`.
- Des retouches de scènes sont en attente (Flower Power II, Lava Train III, AcidFloor I…, voir `RAPPORT_PLAYTEST.md`, session du 2026-11-13) : mieux vaut ne pas brancher la traduction dans une scène en même temps qu'on la retouche.

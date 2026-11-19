# Refonte de l'UI

> Chantier ouvert le 2026-11-19. Reprise : « on attaque la refonte UI ».
> Problème de départ : poser une UI, c'est « j'entre une valeur, je lance le jeu pour voir si c'est au bon endroit ». Écrans imbriqués (cadre TV), barres construites au lancement.

## Fait

- **Cadre TV recentré** (`UI_Images/Vignette_002.png`, 2a0a8ee0) : le trou de l'écran est maintenant pile au centre de l'image 1920×1080 (x 375-1544, y 93-986 → **1170 × 894 px**, centre 960 × 540). Colonnes et lignes uniformes ajoutées / retirées, rien de redessiné. Format quasi 4:3 ; zone utile sûre ≈ **1170 × 880 centrée** (coins arrondis et bord bombé). Tout ce qui était calé à la main sur l'ancien trou (décalé d'environ 57 px à gauche, 3 px en bas) est à recaler.
- **Trou rendu symétrique haut/bas et gauche/droite** (2026-11-19) : le quart bas-gauche du rectangle noir recopié en miroir sur les trois autres (les coins du haut étaient plus arrondis, 49-58 px contre ~12 en bas). Bois, panneaux, bouton intacts ; même fichier, même GUID.
- **`ScreenArea`** posée dans le prefab `UI` (2026-11-19) : enfant de `CadreTV`, ancre centre, pos 0,0, **1100 × 890**. Enfant `Guide` (Image magenta translucide, étirée, Raycast Target décoché) pour la voir ; à couper avant un build.
- **Aperçu en éditeur** (2026-11-19) : `PlayerHealthUI`, `StaminaBarFollower`, `CyclePressureGaugeUI`, `EnemyIconsUI` en `[ExecuteAlways]` ; hors jeu, les vrais pips / icônes sont construits en `HideFlags.DontSave` (jamais sauvegardés, seuls eux sont détruits au rafraîchissement). Champs « Preview… » (vie 100, stamina 100, 15 icônes). Barres : le RectTransform prend la taille du dessin, pips depuis le bord gauche, pivot (0, 0.5). Gauge : carré du cercle au plus large (repos ou pression max). `TomatoUI` : rien à faire (rien de construit).
- Éléments de jeu rattachés à `ScreenArea` et reposés.
- **Aperçu sans override** : hors jeu, une instance de prefab dans une scène ne se redimensionne pas (`PrefabUtility.IsPartOfPrefabInstance`), la taille est tenue par le prefab (45c60d31).
- **Étape 2 faite** (2026-11-19) : `CadreTV` en ancre centre, taille fixe 1920 × 1080 (il était étiré, 1200 de haut dans une Game view en 16:10) ; 4 bandes noires autour (`CadreHaut/Bas` 300 px, `CadreGauche/Droit` 500 px) pour les formats ≠ 16:9. `Vignette_002` en Compression None (couleurs exactes). Reste : recaler `ScreenArea` sur le trou non déformé. **Poser l'UI en Game view 1920×1080**, le 16:10 ne sert qu'à vérifier.
- Bruit de commit connu : `Level_Go For a Spin` reçoit 18 overrides `m_Mesh` vides à chaque sauvegarde (MeshFilter des BallStack portés par des zombies, mesh reconstruit au chargement) ; sans effet, pas lié à l'UI.
- Utilisé par : prefab `UI`, `UI_ShowTime`, scènes MainMenu, LoadingScreen, MultiStart, Level_GoWithTheFlow01, Level_LavaTrain02. `Vignette_Multi_001` = UI_Multi seul ; `VignetteTest` = vieilles scènes (LevelSelect, TutoChoice, WheelOfFortune) et `TVFrame_StencilWrite.mat` ; `Vignette_001` = inutilisée.

## Relevé du prefab UI (solo), 2026-11-19

- **Canvas** : Screen Space Overlay, Canvas Scaler 1920×1080 en mode **Expand**. En 16:9 = la référence. En 16:10 (portables), le Canvas grandit en hauteur et **`CadreTV`, étiré sur tout le Canvas (ancres 0,0 → 1,1), se déforme** : le trou perd forme et centrage.
- **Éléments de jeu enfants de `CadreTV` mais ancrés aux coins du Canvas**, positions à la main :
  - `PlayerHealthBar` (`PlayerHealthUI`), `StaminaBar` (`StaminaBarFollower`), `SprayAmmoPanel`, `PressureGauge` (`CyclePressureGaugeUI`), `BonusBarContainer` (inactif) : ancre haut gauche (ex. pos 373.536, -202.559).
  - `TomatoUI` : bas gauche. `CreditBar` : bas droite.
  - Aucune boîte « écran de la TV » comme repère.
- **Menus** (`PausePanel`, `VictoryContiner`, `GameOverPanel`) : centrés sur le Canvas, lignes posées une à une (0, -40, -80, -120). Pas de Layout Group (sauf icônes de spray).
- **Prefabs imbriqués** : CommentManager, OptionsPanel (sous Canvas), EnemyIconsContainer (sous CadreTV).
- **Construit / déplacé par le code au lancement** :
  - `PlayerHealthUI`, `StaminaBarFollower` : pips et contours créés un par un, positions calculées. En éditeur : une boîte vide de 150×10. **Source principale du « je lance pour voir ».**
  - `CyclePressureGaugeUI` : pips disposés en cercle par le code.
  - `SprayAmmoUI` : icônes instanciées, rangées par un Horizontal Layout Group (sain).
  - `VictoryUI` : anime des glissements, part de la position de l'éditeur et y revient (pas gênant).
  - `OptionsPanelUI`, `SoloPanelUI` : décalage des lignes en code (ajouté le 2026-11-19 pour la difficulté) ; un Layout Group le rendrait inutile.
- **Scènes** : 38 utilisent UI ; **6 déplacent des éléments par override** (positions, tailles, ancres) : Level_Flower02 (10), Level_WesternCanyon (5), Level_GoWithTheFlow01 (2), Level_LavaTrain03 (1), OLD/Level_EpervierLoop01 (7), OLD/Level_Tuto01 (10). À regarder et sans doute retirer pour que le prefab décide seul.

## Techniques (discutées le 2026-11-19)

- **`ScreenArea`** : un RectTransform vide calé sur le trou de la TV (≈ 1170 × 880, centré) ; tout ce qui s'affiche « dans la TV » en devient enfant et s'ancre dessus. Pour les écrans imbriqués : chaque niveau = une boîte, son contenu s'ancre dedans.
- **Éditeur = jeu** : Game view en résolution fixe 1920×1080 (pas Free Aspect) ; Scene view en 2D avec l'outil Rect (T) ; panneaux laissés actifs dans la scène, cachés par le code au lancement (déjà le cas de `DemoEndPanel`, `OptionsPanelUI`), œil de la Hierarchy pour les masquer pendant l'édition.
- **Rectangles guides** : Image semi-transparente colorée sur chaque boîte (Raycast Target décoché), une couleur par niveau d'imbrication, sous un objet `Debug_Guides` à désactiver avant le build.
- **Layout Groups** plutôt que positions à la main : ils ignorent les enfants désactivés.
- **Construit au lancement** : le code remplit un conteneur, il ne décide pas où il est. Conteneur posé et dimensionné dans l'éditeur ; éventuellement une commande « Prévisualiser » qui construit en éditeur.
- **Décor du mode Prefab** : Project Settings > Editor > *UI Environment* = une scène avec Canvas + cadre TV → un prefab d'UI s'édite dans la TV.

## Ordre d'attaque proposé (non tranché)

1. `ScreenArea` calée sur le trou ; éléments de jeu reparentés et ancrés dessus ; reposés une fois dans l'éditeur.
2. Cadre TV qui garde ses proportions en 16:10 (réglage de l'image ou Aspect Ratio Fitter) au lieu de s'étirer.
3. Aperçu en éditeur des barres à pips et de la gauge.
4. Layout Groups dans les menus (pause, victoire, options, Solo).
5. Overrides des 6 scènes : relire, retirer.
6. Multi (`UI_Multi`, copie indépendante) avec la même méthode.

## Bug lié

- **Barre de vie à 2 pips au lancement** (noté le 2026-11-19, aussi dans `CE_QUI_RESTE_A_FAIRE.md`) : vie à 100, 25 pips par barre, mais 2 pips affichés. Override sur UI / `PlayerHealthUI` ? Pas encore enquêté.

## Idées liées (pour plus tard)

- Loading screen : cercles concentriques qui s'effacent un par un depuis l'extérieur (`CE_QUI_RESTE_A_FAIRE.md`, Polish).
- Écran de fin « fiche participant » (`DIFFICULTE_ET_PARTIE.md`).

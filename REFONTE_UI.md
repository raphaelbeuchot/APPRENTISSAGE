# Refonte de l'UI

> Chantier ouvert le 2026-11-19. Reprise : « on attaque la refonte UI ».
> Problème de départ : poser une UI, c'est « j'entre une valeur, je lance le jeu pour voir si c'est au bon endroit ». Écrans imbriqués (cadre TV), barres construites au lancement.

## Fait

- **Cadre TV recentré** (`UI_Images/Vignette_002.png`, 2a0a8ee0) : le trou de l'écran est maintenant pile au centre de l'image 1920×1080 (x 375-1544, y 93-986 → **1170 × 894 px**, centre 960 × 540). Colonnes et lignes uniformes ajoutées / retirées, rien de redessiné. Format quasi 4:3 ; zone utile sûre ≈ **1170 × 880 centrée** (coins arrondis et bord bombé). Tout ce qui était calé à la main sur l'ancien trou (décalé d'environ 57 px à gauche, 3 px en bas) est à recaler.
- **Trou rendu symétrique haut/bas et gauche/droite** (2026-11-19) : le quart bas-gauche du rectangle noir recopié en miroir sur les trois autres (les coins du haut étaient plus arrondis, 49-58 px contre ~12 en bas). Bois, panneaux, bouton intacts ; même fichier, même GUID.
- **`ScreenArea`** posée dans le prefab `UI` (2026-11-19) : enfant de `CadreTV`, ancre centre, pos 0,0, **1100 × 830** (recalée après l'étape 2). Enfant `Guide` (Image magenta translucide, étirée, Raycast Target décoché) pour la voir ; à couper avant un build.
- **Aperçu en éditeur** (2026-11-19) : `PlayerHealthUI`, `StaminaBarFollower`, `CyclePressureGaugeUI`, `EnemyIconsUI` en `[ExecuteAlways]` ; hors jeu, les vrais pips / icônes sont construits en `HideFlags.DontSave` (jamais sauvegardés, seuls eux sont détruits au rafraîchissement). Champs « Preview… » (vie 100, stamina 100, 15 icônes). Barres : le RectTransform prend la taille du dessin, pips depuis le bord gauche, pivot (0, 0.5). Gauge : carré du cercle au plus large (repos ou pression max). `TomatoUI` : rien à faire (rien de construit).
- Éléments de jeu rattachés à `ScreenArea` et reposés.
- **Aperçu sans override** : hors jeu, une instance de prefab dans une scène ne se redimensionne pas (`PrefabUtility.IsPartOfPrefabInstance`), la taille est tenue par le prefab (45c60d31).
- **Étape 2 faite** (2026-11-19) : `CadreTV` en ancre centre, taille fixe 1920 × 1080 (il était étiré, 1200 de haut dans une Game view en 16:10) ; 4 bandes noires autour (`CadreHaut/Bas` 300 px, `CadreGauche/Droit` 500 px) pour les formats ≠ 16:9. `Vignette_002` en Compression None (couleurs exactes). `ScreenArea` recalée (1100 × 830) et éléments reposés. **Poser l'UI en Game view 1920×1080**, le 16:10 ne sert qu'à vérifier.
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

## Ordre d'attaque (validé le 2026-11-19)

1. `ScreenArea` calée sur le trou ; éléments de jeu reparentés et ancrés dessus ; reposés une fois dans l'éditeur. **Fait.**
2. Cadre TV qui garde ses proportions en 16:10 (réglage de l'image ou Aspect Ratio Fitter) au lieu de s'étirer. **Fait.**
3. Aperçu en éditeur des barres à pips et de la gauge. **Fait** (+ icônes d'ennemis).
4. Layout Groups dans les menus (pause, victoire, options, Solo). **Fait le 2026-11-20** pour Pause, Options, Solo : conteneur `Lignes` (Vertical Layout Group Middle Center + Content Size Fitter Preferred Size) ; difficulté du Solo dans `Difficulte` (Horizontal, Control Child Size Width). Le code de décalage des lignes est retiré (OptionsPanelUI, SoloPanelUI). Game Over : une seule ligne, rien à faire. **Victoire reportée** (écran à revoir). Leçon : un Layout Group ne centre que si le contenu tient dans le conteneur, d'où le Content Size Fitter.
5. Overrides des 6 scènes : relire, retirer. **Fait le 2026-11-20** (scènes OLD ignorées) : GoWithTheFlow01 (cadre écrasé à hauteur 0, barre de vie à 50 PV par pip = bug des 2 pips), Flower02 (matériau stencil, icône dollar, positions crédit), WesternCanyon (positions/tailles des barres, icônes, IconContainer, texte crédit, CommentManager en double ; Bubble Container gardé, sert au BubbleManager), LavaTrain03 (position CreditBar, CommentManager en double). Overrides des Layout Groups : **le bruit existe, mais seulement si le panneau est affiché à la sauvegarde** (vu le 2026-11-20 dans `UI_Multi` avec `PausePanel` activé : les overrides d'ancres / positions de l'`OptionsPanel` imbriqué passent de 100 / −220, 200×245 à 0, recalculés par le layout). Sauvegarder avec les menus cachés ne le déclenche pas (LavaTrain03, rien). Sans effet en jeu. Règle pratique : recacher Pause / Options avant de sauvegarder.
6. Multi (`UI_Multi`, copie indépendante) avec la même méthode.
   - **Vignette recentrée** (2026-11-20) : `Vignette_Multi_001` était décalée (marges 68/60 en largeur, 68/72 en hauteur, quelques pixels transparents en haut à droite). Refaite : noir de **64 px partout en largeur** (bords et milieu), 70 px en haut et en bas, trous **864 × 940** (x 64-927 et 992-1855, y 70-1009), coins d'origine. Compression None. Même GUID.
   - Écart accepté : les caméras filment chacune une moitié d'écran (centre x 480 pour J1), le centre du trou est à 496, soit 16 px. Invisible en jeu. Correction possible par le Viewport Rect des caméras (X 0.0333 / 0.5167, W 0.45), écartée pour l'instant : lie les caméras au dessin de la vignette.
   - **Étape 2 faite** (2026-11-20, f0b04540) : `CadreTV` de `UI_Multi` fixe 1920 × 1080 au centre, 4 bandes noires copiées du solo.
   - **ScreenArea_J1 / J2** (2026-11-20, 48e99404) : posées à la main sous `CadreTV` (X ∓464, 840 × 910), UI de chaque joueur rangée dedans (vie, stamina, gauge, tomates, score).
   - **Barre de vie du J2 dans le prefab** : avant, ajoutée en scène (Race_Lava et CoinRace_Lava seulement). `PlayerHealthUI` a un champ **Side** (Aucun / Left / Right) : Player Health vide → joueur du bon côté via `PlayerScreenSide` ; Aucun = solo inchangé. J1 = Left, J2 = Right.
   - Reste : la stamina et les tomates du J2 trouvent leur joueur par un override de scène (même principe que Side possible sur `StaminaBarFollower` / `TomatoUI`) ; overrides `playerHealth` de la barre J1 dans les 4 scènes, inutiles maintenant ; 4 overrides orphelins dans Race_Lava ; Pause du multi sans Layout Group.
   - Relevé : 4 scènes (CoinRace_Lava, Race_Lava, Race_Slippery, Race_Void). `CadreTV` étiré sur le Canvas, pas de `ScreenArea`, éléments sous `CadreTV` posés à la main ; une seule barre de vie (stamina, gauge, tomates en double J1/J2) ; Pause sans Layout Group.

## Bug lié

- **L'aperçu en éditeur efface les enfants posés à la main** (vu le 2026-11-20) : `RefreshPreview` de `PlayerHealthUI` / `StaminaBarFollower` marque *tous* les enfants de la barre en DontSave, pas seulement les pips qu'il construit (le commentaire dit le contraire). Tout objet posé à la main sous une barre disparaît à la sauvegarde. Ça a retiré les anciens enfants (HealthBarFill, StaminaFill, Background…) du solo (f525163d) puis du multi (f0b04540), sans effet en jeu. Dans le multi, `PausePanel.backgroundOverlay` pointait sur le `Background` de la StaminaBar (champ inutilisé dans `PauseMenuUI`), il est maintenant vide. À corriger : ne marquer que les pips construits.
- **Barre de vie à 2 pips au lancement** : **réglé** à l'étape 5 (override 50 PV par pip dans GoWithTheFlow01, 035f801e).

## Idées liées (pour plus tard)

- Loading screen : cercles concentriques qui s'effacent un par un depuis l'extérieur (`CE_QUI_RESTE_A_FAIRE.md`, Polish).
- Écran de fin « fiche participant » (`DIFFICULTE_ET_PARTIE.md`).

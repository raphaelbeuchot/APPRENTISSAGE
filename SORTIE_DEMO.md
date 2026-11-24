# Sortie de la démo de playtest fermé

> Tout ce qu'il faut faire **hors du développement du jeu** pour que la démo arrive bien chez les testeurs et que leurs retours reviennent : build, version, envoi, consignes, collecte des retours, licences.
> Ouvert le 2026-11-16. Les points marqués *suggestion* ne sont pas tranchés.

Rappels :
- Décision du 2026-10-25 : démo de **playtest fermé**, multi complet + 22 niveaux solo cochés « In Demo » dans le catalogue. Contexte et testeurs pressentis : `CE_QUI_RESTE_A_FAIRE.md` (section démo).
- Le contenu du jeu (boss, niveaux, VF) se suit dans ses propres docs : `BOSS_RUSH_HOUR_IV.md`, `VF_DEMO.md`, `MULTIJOUEUR_LOCAL.md`.

---

## 1. Calendrier

- [ ] Finir le boss (Rush Hour IV), éventuellement un niveau multi de plus.
- [ ] **Gel des nouveautés** (*suggestion*) : après ça, on ne fait plus que corriger.
- [ ] Date d'envoi visée : *à fixer*.
- [ ] Durée du playtest et date limite des retours : *à fixer*.

## 2. Build

- [x] Profil `Assets/Settings/Build Profiles/Demo Playtest` : Windows, symbole `DEMO_BUILD`, liste de scènes globale (vérifiée le 2026-11-16 : 42 scènes, tout le catalogue et les 4 courses multi présents).
- [x] Premier build complet (2026-11-16) : démo finie sans bug bloquant. Constats :
  - [x] « ??? » après le Trou normand : corrigé, testé le 2026-11-24 (voir `RAPPORT_PLAYTEST.md`).
  - [x] `NullReferenceException` dans `OpeningSequenceManager.FranchiSeuil` au début de la partie (`audioSource` ou `celluleGO` vide), sans effet visible. Lu dans `Player.log` (`%USERPROFILE%\AppData\LocalLow\DefaultCompany\APPRENTISSAGE\`). **Réglé le 2026-11-24** : `celluleGO` vide (obsolète), test de null ajouté.
  - [ ] **Performances** : le jeu rame en plein écran, chute de framerate à l'alerte. Le portable du test n'a qu'une puce intégrée (Intel Iris Xe) et un écran 2880×1800 ; en fenêtré 1280×720 c'est mieux. Dans l'éditeur (Game view plus petite) : autour de 60 FPS, moins de 15 ms par image. Pistes non tranchées : limiter la résolution de rendu, VSync, mesures (affichage FPS dans le jeu, Profiler sur Development Build).
    - 2026-11-17, mesures `PerfRecorder` (F3, CSV dans le dossier de `Player.log` ; F5-F8 coupent ombres / post-process / lumières / ampoules) : **le test d'avant était sur batterie**. Sur secteur, éditeur, 1080p, Iris Xe, Level_HandleHurdles : ~97 FPS (10,3 ms), alerte comprise ; tout couper ne gagne que 6 %. Le jeu est sain ; causes probables : batterie + résolution native 2880×1800. Colonne GPU non fiable en éditeur.
    - [x] Build en plein écran sur secteur (2026-11-17) : les alertes n'ont plus d'incidence, mais ~40 FPS dans la start zone et partout. Development Build mesuré (2880×1659, Level_Ice) : GPU bloqué à ~22 ms, ombres et Volumes sans effet → **le coût est par pixel**.
    - Éditeur en Game view 2880×1800 (concorde avec le build : 45 FPS contre 42), HandleHurdles, coupures cumulées : rien 22,1 ms → SSAO coupée 19,8 (−2,3) → **TAA coupée 13,7 (−6,1)** → Render Scale 0,75 : 10,0 (plafond éditeur). Ordre des coûts : TAA, puis résolution, puis SSAO (renderer feature de `PC_Renderer`).
    - Suggestion non tranchée : Max = TAA + SSAO + scale 1 ; Haute = SMAA/FXAA + SSAO + scale 1 ; Basse = FXAA, sans SSAO, scale 0,75. Question ouverte : garder la TAA par défaut ?
    - F4 (temps GPU par étape, 2880×1800, HandleHurdles), 2026-11-17. Avant : start zone 23 ms (opaques 5,0 dont le rideau qui remplit l'écran, SSAO 4,8), jeu 18 ms. **SSAO coupée** (intensité 0,1, invisible) et rideau allégé (ad9dcb9c : relief Normal From Height débranché, ombres portées coupées ; il était déjà rendu sur une seule face, le message du commit se trompe) → start zone 17,5 ms, jeu 14-15 ms. Restent en jeu : TAA 2,75 (+ vecteurs de mouvement 0,5), **profondeur de champ Bokeh 2,5** (Global Volume `SampleSceneProfile` ; Gaussian bien moins cher), opaques 1,6, uber post 0,8, copie finale 0,7, bloom 0,6, UI 0,55. TAA et DoF : choix visuels de l'utilisateur. **DoF du profil partagé coupée** (mode Off, invisible) : jeu à 10 ms GPU en natif. Le Trou normand garde son flou via `CM_PointdeVue`.
    - Anticrénelage comparé (F4, 2880×1800, HandleHurdles, 2026-11-17) : **TAA ~10,3 ms** GPU, **SMAA (High) 7,1 ms**, **FXAA 7,4 ms**. SMAA et FXAA coûtent pareil, la TAA ~3 ms de plus (passe TAA + vecteurs de mouvement). Sans anticrénelage, c'est trop moche (utilisateur). **Décision : la TAA reste par défaut** : c'est la seule qui stabilise les tranches fines des murs vues de loin (sous le pixel, elles scintillent en SMAA / FXAA). Les LOD n'y changeraient rien. *Suggestion* pour l'option Qualité : SMAA en Basse, en acceptant le scintillement.
    - **Bilan Development Build du 2026-11-17 soir** (2880×1659, Go For a Spin, sons préchargés + shaders préchargés) : **72 FPS de moyenne** (41 dans le build d'avant), GPU 10-13 ms, **aucun à-coup aux trois alertes** (pire image 18 ms en partie, 42 ms une fois au démarrage du niveau). Préchargement des shaders au lancement du jeu : 434 variantes en 0,6 s ; seulement 8 préparées en cours de partie (Ice2, VictoryGhost, Lit), ~2 ms au total.
    - Outils de formes (meshes reconstruits au chargement) validés en build : démo finie sans défaut visuel, mémoire stable ~210 Mo d'un niveau à l'autre (`Player.log`, 4 chargements). Non mesuré : temps de reconstruction (caché par le chargement) ; longue session à vérifier dans les logs des testeurs.
    - Trou de 4 s sans image dans le CSV du Development Build (vers 78 s) : probablement un changement de fenêtre. À surveiller si ça se reproduit.
    - [ ] Option Qualité dans le menu (Basse / Haute / Max), voulue par l'utilisateur : utile pour les machines modestes ou sur batterie. Contenu à définir.
    - [ ] Petits à-coups : première alerte (préchauffage shader / matériau probable), ~20 Ko alloués par image (GC), à vérifier en Development Build.
- [x] **Product Name propre à la démo** dans le profil, sinon la démo partage sa sauvegarde avec le jeu complet. **Fait le 2026-11-24** (override des Player Settings dans le profil, 347ba30b) :
  - Company `deBrume` (nom de société envisagé, pas encore créée ; rien trouvé sous ce nom exact sur le web, voisin : Brume Studio, Belgique ; marques INPI / EUIPO pas vérifiées), Product `Grand Panoptique Demo`, Version `0.1.0`. Pas de numéro dans le Product Name : il changerait l'adresse de la sauvegarde à chaque version.
  - Logs, notes F2 et relevés de perf du build démo : `%USERPROFILE%\AppData\LocalLow\deBrume\Grand Panoptique Demo\`.
  - L'override copie **tous** les Player Settings : icône, splash, résolution… se règlent désormais dans le profil pour la démo.
  - Ne plus changer Company / Product une fois le premier build envoyé (les testeurs perdraient leur sauvegarde).
- [ ] **Sauvegarde par identifiant de scène** avant toute distribution (voir `PASSAGE_EN_PRODUCTION.md`, ⏸).
- [ ] **Liste des shaders à jour** juste avant le build : Project Settings > Graphics > onglet Built-In > Shader Loading → **Clear**, jouer chaque niveau de la démo (alerte, tir, mort, victoire, multi, boss), puis **Save to asset** sur `Assets/Settings/ShaderVariants_Demo` (déjà branché dans Preload Shaders, préparé au lancement du jeu). Save to asset remplace le fichier, on ne peut pas le compléter : tout rejouer d'un coup.
- [x] Sons (2026-11-17, 0870d6c1) : bruitages en Preload Audio Data (chargés avec la scène), musiques d'ambiance en Streaming. Un nouveau son importé doit avoir Preload Audio Data coché.
- [ ] Development Build : **décoché** pour les testeurs (DevPanel et notes F2 disparaissent alors du build).
- [ ] Partie complète jouée **sur une autre machine** que celle du build, du menu au boss, puis une course multi.
- [ ] Test avec les manettes que les testeurs auront probablement (Xbox, PlayStation), et au clavier si c'est prévu.
- [x] **Écran titre** (fait le 2026-11-24, 0ef6920c) : panneau `TitleScreen` dans MainMenu, « Grand Panoptique » + « Demo v0.1.0 », une fois par lancement, un appui ouvre le menu (pas de fondu, pas de texte « appuyez »).
- [x] ~~**Écran des commandes**~~ (noté le 2026-11-24) : remplacé le jour même par des textes de tuto qui affichent la touche du dernier appareil utilisé (manette Xbox / clavier) : `InputPrompts`, balises `[Action]` dans la Sheet VF (515ffe30, 887bc974). Restent : repositionner les textes dans les scènes (surtout TomatoFest), tester le Trou normand (GoWithTheFlow01 ok), FR de `input.leftShift`.
- [ ] **Splash screen** (Player Settings du profil > Splash Image) : à regarder, garder ou couper le logo Unity, ajouter le sien.

## 3. Version

- [x] **Numéro de version visible** : sur l'écran titre, « Demo v » + version des Player Settings (`VersionLabel`), jamais faux (0ef6920c).
- [ ] Noter ici, pour chaque build envoyé : version, date, commit. Usage : `majeur.mineur.correctif`, 0.x avant la sortie ; correction de bugs → 0.1.1, vraie passe de retouches → 0.2.0.

| Version | Date | Commit | Envoyé à | Remarques |
|---|---|---|---|---|
| | | | | |

## 4. Envoi

- [ ] Canal d'envoi : *à choisir* (zip sur un Drive / page itch.io privée ou en accès restreint / autre).
- [ ] **Avertissement Windows SmartScreen** : un .exe non signé déclenche « Windows a protégé votre ordinateur ». Prévenir les testeurs (« Informations complémentaires » → « Exécuter quand même »).
- [ ] Antivirus : un zip d'exe inconnu peut être bloqué. À vérifier sur une machine tierce.
- [ ] Taille du zip, et configuration minimale constatée (machine la plus faible où ça tourne).
- [ ] Confidentialité : demander de ne pas diffuser (simple message, ou plus formel ?) *à décider*.

## 5. Consignes aux testeurs

À donner dans le message d'envoi, dans un fichier joint ou dans un écran du jeu (*à décider*).

- [ ] Ce que c'est : une démo, pas finie, ce qu'on attend d'eux.
- [ ] Les commandes (manette / clavier), le multi (2 manettes ?).
- [ ] Bugs déjà connus, pour ne pas les recevoir dix fois.
- [ ] Comment et où envoyer les retours, avec le numéro de version.
- [ ] Langue : EN / FR selon l'état de la VF.

## 6. Collecte des retours

- [ ] Format : *à choisir* (formulaire en ligne, message libre, appel, session filmée…).
- [ ] Questions à poser : *à écrire* (ressenti général, niveaux préférés / détestés, moments de blocage, durée, bugs).
- [ ] Où on range les retours reçus : `RAPPORT_PLAYTEST.md` pour ce qui touche un niveau, `CE_QUI_RESTE_A_FAIRE.md` pour le reste.

## 7. Licences et crédits

À vérifier même pour un playtest privé, et indispensable pour une démo publique.

- [ ] Police KeeponTruckin : licence d'intégration dans un jeu (voir `VF_DEMO.md`, Font Bros contacté le 2026-11-14). **Décidé le 2026-11-23** : version avec accents trouvée par l’utilisateur, utilisée pour la démo de playtest fermé sans licence achetée ; achat (licence jeu) ensuite selon les retours.
- [ ] Musiques et sons : origine et licence de chacun.
- [ ] Assets achetés ou gratuits (modèles, textures, packages) : licences.
- [ ] Écran ou fichier de crédits : *à décider*.

## 8. Après le playtest

- [ ] Remercier les testeurs.
- [ ] Trier les retours, décider de la suite (démo publique, éditeur, autoédition : voir la décision du 2026-10-25 dans `CE_QUI_RESTE_A_FAIRE.md`).

## Sauvegarde et plateforme (question ouverte, 2026-11-19, non tranchée)

- Aujourd'hui toute la progression est en `PlayerPrefs` : sous Windows, dans le registre de la session → déjà liée au compte Windows de l'utilisateur. Suffit pour la démo.
- Sortie Steam : Steam Cloud synchronise des fichiers, pas le registre → passer la sauvegarde dans un fichier (`Application.persistentDataPath`). Peu de scripts concernés (`LevelProgressionManager`, `OptionsManager`, `CollectibleManager`, `LevelStatsTracker`…).
- « Effacer la sauvegarde » façon reset d'Isaac : option à ajouter si voulue ; effacerait aussi la trace « jeu terminé en X » (`DIFFICULTE_ET_PARTIE.md`).
- Les marques de fin de partie pourraient aussi devenir des succès Steam.

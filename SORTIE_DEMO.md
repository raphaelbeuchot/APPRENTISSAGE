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
  - [ ] « ??? » après le Trou normand : corrigé, à tester (voir `RAPPORT_PLAYTEST.md`).
  - [ ] `NullReferenceException` dans `OpeningSequenceManager.FranchiSeuil` au début de la partie (`audioSource` ou `celluleGO` vide), sans effet visible. Lu dans `Player.log` (`%USERPROFILE%\AppData\LocalLow\DefaultCompany\APPRENTISSAGE\`).
  - [ ] **Performances** : le jeu rame en plein écran, chute de framerate à l'alerte. Le portable du test n'a qu'une puce intégrée (Intel Iris Xe) et un écran 2880×1800 ; en fenêtré 1280×720 c'est mieux. Dans l'éditeur (Game view plus petite) : autour de 60 FPS, moins de 15 ms par image. Pistes non tranchées : limiter la résolution de rendu, VSync, mesures (affichage FPS dans le jeu, Profiler sur Development Build).
    - 2026-11-17, mesures `PerfRecorder` (F3, CSV dans le dossier de `Player.log` ; F5-F8 coupent ombres / post-process / lumières / ampoules) : **le test d'avant était sur batterie**. Sur secteur, éditeur, 1080p, Iris Xe, Level_HandleHurdles : ~97 FPS (10,3 ms), alerte comprise ; tout couper ne gagne que 6 %. Le jeu est sain ; causes probables : batterie + résolution native 2880×1800. Colonne GPU non fiable en éditeur.
    - [ ] Rejouer le build en plein écran **sur secteur** avec la Game Bar pour confirmer.
    - [ ] Option Qualité dans le menu (Basse / Haute / Max), voulue par l'utilisateur : utile pour les machines modestes ou sur batterie. Contenu à définir.
    - [ ] Petits à-coups : première alerte (préchauffage shader / matériau probable), ~20 Ko alloués par image (GC), à vérifier en Development Build.
- [ ] **Product Name propre à la démo** dans le profil, sinon la démo partage sa sauvegarde avec le jeu complet (voir `CE_QUI_RESTE_A_FAIRE.md`).
- [ ] **Sauvegarde par identifiant de scène** avant toute distribution (voir `PASSAGE_EN_PRODUCTION.md`, ⏸).
- [ ] Development Build : **décoché** pour les testeurs (DevPanel et notes F2 disparaissent alors du build).
- [ ] Partie complète jouée **sur une autre machine** que celle du build, du menu au boss, puis une course multi.
- [ ] Test avec les manettes que les testeurs auront probablement (Xbox, PlayStation), et au clavier si c'est prévu.

## 3. Version

- [ ] **Numéro de version visible dans le menu** (ex. « Démo playtest v0.1 »), lu automatiquement dans les Player Settings pour ne jamais être faux.
- [ ] Noter ici, pour chaque build envoyé : version, date, commit.

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

- [ ] Police KeeponTruckin : licence d'intégration dans un jeu (voir `VF_DEMO.md`, Font Bros contacté le 2026-11-14).
- [ ] Musiques et sons : origine et licence de chacun.
- [ ] Assets achetés ou gratuits (modèles, textures, packages) : licences.
- [ ] Écran ou fichier de crédits : *à décider*.

## 8. Après le playtest

- [ ] Remercier les testeurs.
- [ ] Trier les retours, décider de la suite (démo publique, éditeur, autoédition : voir la décision du 2026-10-25 dans `CE_QUI_RESTE_A_FAIRE.md`).

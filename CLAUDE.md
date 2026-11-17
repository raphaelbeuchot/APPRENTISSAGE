# Mise en route de session

Lu à chaque démarrage. L'architecture du jeu est dans `Assets/Project/CLAUDE.md`.

## Outils à connaître
- Skill **unity-scenes** : lire les `.unity` / `.prefab` (guid vers asset, qui utilise un prefab ou un script, overrides d'une instance, fileID vers nom de GameObject). Toute enquête dans une scène ou un prefab passe par lui, **jamais de grep à la main dans les `.unity`**.
- Avant de fouiller une scène : si la réponse se voit en deux secondes dans l'éditeur (« c'est un prefab ? », « le champ est rempli ? »), **demander à l'utilisateur**.

## Où sont les docs de suivi
- `NOTES_PLAYTEST.md` : boîte de réception brute (F2 en jeu, notes dictées).
- `RAPPORT_PLAYTEST.md` : actions propres à un niveau, une session par section.
- `CE_QUI_RESTE_A_FAIRE.md` : bugs, design, tech, polish qui dépassent un niveau.
- Un doc par gros chantier : `BOSS_RUSH_HOUR_IV.md`, `VF_DEMO.md`, `MULTIJOUEUR_LOCAL.md`, `PASSAGE_EN_PRODUCTION.md`, `OUTILS_FORMES.md`, `SORTIE_DEMO.md` (le monde réel : build, version, envoi, testeurs, licences)…
- Une prise de note de l'utilisateur hors de ces docs : la ranger dedans (par niveau → RAPPORT, sinon → CE_QUI_RESTE ou le doc du chantier).

## Deux machines (maison, bureau)
- La mémoire de Claude ne suit pas d'une machine à l'autre : ce qui doit suivre va dans un doc du repo.
- Au démarrage : `git status` et dernier commit. Avant de toucher une scène ou un prefab, s'assurer que l'autre poste a sauvegardé, commité, poussé, et qu'on a pullé.
- `Assets/Project/Fonts/KeeponTruckin SDF.asset` est toujours modifié (police TMP en mode Dynamic) : ne pas le commiter avec le reste.

## Reprise de chantier
Résumer l'état en quelques lignes, demander l'intention du jour, ne pas partir lire le code tout de suite.

## Méthode (audit du 2026-11-16)
- Mise en scène à plusieurs déclencheurs (événements, triggers, délais, fondus) : d'abord un **tableau du déroulé** (temps, événement, ce qui part, état obtenu), validé avec l'utilisateur, puis le code.
- Avant de proposer un branchement : **lire ce qui est déjà branché** dans la scène (UnityEvents, délais, durées, priorités, objets actifs).
- Rythme testé → commité : pas plus de deux ou trois morceaux non testés à la fois.
- Textes du jeu en français : c'est l'utilisateur qui les écrit. Pour une ligne de la Sheet VF, fournir clé, EN, contexte, source, et laisser FR vide.

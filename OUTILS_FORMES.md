# Outils de formes (blocking / level art)

Composants éditeur qui génèrent des formes de décor paramétriques sans passer par un logiciel 3D. Scripts dans `Assets/Project/Scripts/Scripts_LevelDesign/`.

Principes communs :
- se posent sur un Cube (ou tout GameObject avec MeshFilter) ; le BoxCollider est remplacé par un MeshCollider qui épouse la forme ;
- mise à jour en direct ; le mesh n'est **pas sauvegardé**, il est reconstruit au chargement → marche tel quel dans un prefab ;
- **UV0 en mètres** (densité constante quelle que soit la taille, le Tiling du matériau règle la répétition), **UV2 (TEXCOORD2) en 0-1** par face pour les shaders maison (dégradés, liserés) ;
- option **Matériaux séparés** (faces / tranches), usage phare : shader guirlande lumineuse sur les tranches ;
- limite acceptée : un Scale de Transform ≠ 1 étire texture et arrondis (régler la taille dans le composant, pas avec le Scale).

---

## RoundedBlock — pavé aux coins arrondis

`RoundedBlock.cs`. Un rectangle aux coins arrondis (profil) extrudé. Point fort vs Blender : on redimensionne sans déformer les rayons.

- **Profil** : face qui porte les coins (Dessus / Avant / Côté), rayons liés ou par coin (A-D affichés dans la scène).
- **Arrondi du pourtour** : quart de rond sur tout le bord haut et/ou bas.
- **Évasement** (2026-10-22) : *Échelle du haut* (1 = droit, < 1 se resserre, > 1 s'évase ; tout le profil suit, trou compris). En Dessus réduction autour du centre, en Avant/Côté autour du milieu de la base (le bloc reste au sol).
  - *Galbe* (-1 creusé : volcan, trompette / +1 bombé : dôme, tulipe), *Ventre* (+ vase / − diabolo) et sa position, *Segments en hauteur*.
  - Une AnimationCurve a été essayée puis abandonnée : « une courbe qui n'a rien à voir avec la forme ».
  - Usage vu : cratères de Lava Train (évasement + trou + matériau lave sur la tranche du trou).
- **Trou** : perce de part en part, marges liées ou G/D/B/H, coins déduits ou libres.

## CurvedWall — mur courbe

`CurvedWall.cs` (2026-10-22). Une section (épaisseur × hauteur) tournée autour de l'axe Y. Composant séparé de RoundedBlock : plier un RoundedBlock aurait demandé de remailler ses faces.

- **Pivot = centre du cercle, au sol.** L'arc part de +X et tourne vers +Z. Deux murs de même rayon au même pivot se raccordent exactement en donnant au second une rotation Y = angle du premier.
- **Arc** : angle (boutons 90 / 180 / 270 / 360 ; 360 = anneau fermé), rayon intérieur (l'épaisseur s'ajoute vers l'extérieur), facettes par quart de tour.
- **Section** : coins arrondis liés ou par coin — A bas intérieur, B bas extérieur, C haut extérieur, D haut intérieur (lettres sur le bout de départ).
- **Arêtes des bouts** : DC (haut du bout) et AB (bas du bout), en biseau ou arrondi, sur les deux bouts / départ / arrivée. Taille mesurée au milieu de l'épaisseur, bornée automatiquement.
- **Matériaux séparés** : 0 = faces intérieure et extérieure, 1 = dessus, dessous, arrondis et bouts.
- Limite connue : texture en miroir horizontal sur la face intérieure (invisible en aplats).

---

## BallStack — pile de boules grossières (codé et validé le 2026-10-25)

`BallStack.cs` (+ bouton Relancer dans `Editor/BallStackEditor.cs`). Se pose sur un GameObject vide (MeshFilter + MeshRenderer ajoutés), matériau sur le MeshRenderer. Besoin : formes bizarres et rudimentaires (ceps de vigne, concrétions, rochers empilés, cactus) sans logiciel 3D. Pistes écartées : Tree Editor d'Unity (ancien, mal supporté en URP, fait pour des arbres feuillus réalistes), Spline Extrude + Spline Instantiate (échelle aléatoire **ajoutée** et tirée axe par axe → objets déformés ; matériau non modifiable sur un modèle importé ; instances recréées en permanence).

- **Forme** : des boules empilées en Y, **un seul mesh** fusionné (comme RoundedBlock), reconstruit au chargement.
- **Réglages** : nombre de boules (1 à 50), taille de base, variation d'échelle par boule, **effilement** (les boules rétrécissent en montant), **chevauchement** (de -1 = boules espacées en chapelet à 0,9 = soudées), **décalage horizontal** aléatoire d'une boule à l'autre (casse l'alignement), rotation aléatoire autour de Y (par boule et/ou globale), **graine** + bouton Relancer (même résultat tant qu'on ne relance pas), matériau.
- **Boules grossières** : low-poly facettées générées dans le code, réglage de finesse (peu de faces = très facetté) + léger bosselage (chaque boule cabossée, unique).
- **Collider** (décidé) : les piles sont **dans l'aire de jeu**, sur le layer **Obstacle** (vu par la grille A* des ennemis). Choix dans le composant : Aucun / **Sphères** (une par boule, sur le même objet ; défaut, le moins coûteux et le plus fidèle) / Capsule englobante. Exception au principe commun (pas de MeshCollider : inutilement précis et plus lourd). Pavés plus tard si un usage demande un dessus plat (sur des sphères, le joueur glisse).
- **Pousse** (2026-10-25, sliders seulement : l'utilisateur préfère l'Inspector aux poignées dans la scène) : inclinaison 0-180° (180 = crosse qui retombe), direction, courbure (0 = droite penchée dès la base, 1 = tige qui ploie), ondulation (amplitude + nombre). Nombre de boules fixe, la hauteur en découle. Remplace l'idée de pile le long d'une spline (splines jugées pénibles à manipuler).
- **Pointe** (2026-10-25) : la dernière boule en goutte (longueur 0-3 rayons, finesse de bulbe à aiguë), orientée selon la pousse. Non couverte par la sphère de collision. Piste non testée : petites boules finales.
- Finesse utilisée en pratique : 0 ou 1 (choix visuel).

## En réflexion (pause du 2026-10-22)

**Raccorder deux murs proprement** — l'équivalent du snap V (sommets), mais pour des faces. Aujourd'hui : par le pivot (cf. ci-dessus), ou V sur un coin si les deux bouts sont déjà orientés pareil. Piste proposée, **non tranchée** : boutons « Mur suivant » dans l'inspecteur de CurvedWall (même sens / sens inverse = virage en S), qui dupliquent et placent le mur à la suite. Question ouverte : couper automatiquement les arêtes de bout au raccord, ou laisser la main. Plus tard éventuellement : raccord RoundedBlock droit ↔ mur courbe.

## PerforatedWall — mur droit à ouvertures (étapes 1 à 3 validées le 2026-10-28)

**Étape 1 faite** (`PerforatedWall.cs`) : mur droit (coins arrondis liés ou A-D), une ouverture (position, taille, coins liés ou A-D), Bord = Aucun / Bas (porte) / Haut / Gauche / Droite, niche (profondeur, face avant ou arrière), matériaux séparés (0 faces, 1 tranche extérieure, 2 intérieur de l'ouverture), MeshCollider. Ouverture qui déborde ou tombe dans un arrondi du mur : non percée, message dans l'Inspector. Arche = porte avec coins C et D à la demi-largeur.
- Faces triangulées par oreilles (trou relié au contour par un pont) : topologie en longs triangles fins, **sans effet** sur l'éclairage ni les textures (normales et UV projetés). À revoir seulement si on veut un jour déformer le mur ou peindre des couleurs par sommet.
- **Étape 2 faite (2026-10-28) : ébrasement** — après essai d'un biseau d'arête (pas ce que voulait l'utilisateur), les faces intérieures de l'ouverture sont inclinées sur toute l'épaisseur (ou toute la profondeur d'une niche) : taille de base au fond, élargie côté face, comme une meurtrière. Lié ou par côté (haut, bas, gauche, droite) ; le côté d'un bord mangé reste droit. L'ouverture élargie doit tenir dans le mur.
- Revues de l'utilisateur intégrées : **face avant = -Z** (celle que voit une caméra tournée vers +Z) ; taille du mur en un seul champ **Size X, Y, Z** comme RoundedBlock.
- Idées en attente, non tranchées : emplacement de matériau à part pour l'ébrasement (aujourd’hui avec la tranche de l’ouverture, élément 2 ; le fond de niche a son propre élément 3 depuis le 2026-10-28) ; **vitre** (panneau qui bouche l'ouverture, position dans l'épaisseur, matériau propre, bloque ou non le joueur).
- Inspector entièrement en français (2026-10-28) ; noms des champs restés en anglais dans le code.
- **Étape 3 faite (2026-10-28) : plusieurs ouvertures** — liste repliable (Ajouter, Dupliquer décalé à côté, Supprimer), chaque ouverture avec tous ses réglages (bord mangé, niche avant/arrière, ébrasement) ; plusieurs portes sur un même bord, niche qui mange un bord. Chevauchement (rectangles englobants à moins de 1 cm) refusé, message avec le numéro. Ancienne ouverture unique reprise automatiquement en « Ouverture 1 ». Triangulation : ponts trou → contour par rayon horizontal vers la droite (l’ancienne recherche du point visible le plus proche cassait avec des trous alignés verticalement).
- **Suite** : étape 4 répétition en ligne / trame (réglage « Répéter » par ouverture, à discuter).

Notes de conception (discussion du 2026-10-28) :

Portes, fenêtres et niches dans des murs, sans ProBuilder (« détestable sur le nouveau Unity »). Sur nos propres blocs seulement.

- **Composant à part** (nom provisoire `PerforatedWall`), comme CurvedWall : le trou actuel de RoundedBlock relie contour et trou point à point (un seul trou intérieur), ce qui ne permet ni plusieurs trous, ni une porte qui mange le bord, ni une niche. RoundedBlock n'est pas touché.
- **Mur** : largeur, hauteur, épaisseur ; **mêmes options que les autres blocs**, surtout les coins arrondis.
- **Ouvertures** : position, largeur, hauteur, coins arrondis ; traversante ou **niche** (profondeur) ; une ouverture qui touche un bord le **mange** (porte = hauteur 0) ; plus tard **plusieurs**, puis **répétées** en ligne ou en trame N × M.
- **Bord des ouvertures** : **biseau droit**, taille **par arête (haut, bas, gauche, droite)**.
- Matériau séparé pour la tranche des ouvertures.
- Contexte perf : voir `CE_QUI_RESTE_A_FAIRE.md` (pas de bake, mesh reconstruit au chargement comme les autres outils).
- Non tranché : arche (haut en demi-cercle).

## Autres idées d'outils (non tranchées)

- **Tour** : profil révolu autour d'un axe (colonnes, balustres, vases), angle partiel. L'utilisateur voudrait **dessiner le profil** (silhouette éditable dans la scène) plutôt que régler une courbe ; ce profil dessiné pourrait aussi servir à RoundedBlock.
- **Balayage le long d'une spline** : murs sinueux, bordures de piste ; avec une section ronde, des tubes (néons, rampes, câbles). Pièges : virages plus serrés que la demi-épaisseur, torsion.
- **Façades à trous** : en attendant, RoundedBlock troué case par case (fastidieux mais ça marche) ; piste d'une option « répéter N × M ».
- **Rampes** (idée du 2026-10-28) : construire des rampes rapidement.
- **Percer des trous dans un mesh sans casser sa topologie** (idée du 2026-10-28, vue passer par l'utilisateur, « me rendrait énormément service ») : portes et fenêtres dans les murs. À regarder de près le moment venu : ce n'est pas l'outil booléen général écarté ci-dessous, mais un perçage ciblé ; à comparer avec le trou de RoundedBlock et la piste « Façades à trous ».
- **Gradins / podiums empilés**, **arches**, **plateformes bombées**.
- À éviter : un outil booléen général (gouffre, et on perdrait le redimensionnement sans déformation).

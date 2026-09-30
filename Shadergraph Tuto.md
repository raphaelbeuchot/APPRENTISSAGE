# Shader Graph : dissolution, glow interne et effet fantôme

Notes d'apprentissage (Unity Shader Graph). Objectif : comprendre chaque node, pas seulement recopier le graph.

---

## 1. Les bases à retenir

### Vector3 ≠ vecteur mathématique

Un **Vector3**, ce sont juste **trois nombres** (x, y, z). Selon le contexte, ils représentent :

| Usage | Exemple | Sens |
|---|---|---|
| **Position** (point) | Object Position, Camera Position | un endroit dans le monde |
| **Flèche** (vecteur maths) | Point B − Point A | une direction + une longueur |
| **Direction** | View Direction normalisée | une flèche de longueur 1 |
| **Couleur** | (R, V, B) | pas de sens géométrique |

Shader Graph ne fait **aucune différence** entre ces usages : brancher une position à la place d'une direction ne donne pas d'erreur, juste un résultat faux. **C'est à moi de savoir ce que représente chaque fil.**

### Règles de calcul

| Opération | Résultat |
|---|---|
| Point − Point | Flèche (du 2ᵉ vers le 1ᵉʳ) : *B − A = flèche de A vers B* |
| Point + Flèche | Point (on se déplace) |
| Length(Flèche) | Nombre : sa longueur |
| Normalize(Flèche) | Direction : même sens, longueur 1 |
| Direction × Nombre | Flèche de cette longueur |

**Règle d'or :** on normalise ce qui représente une **direction**, on garde intact ce qui porte une **distance**.

### Nodes mathématiques simples

| Node | Calcul | Usage typique |
|---|---|---|
| **Negate** | −x | retourner une flèche |
| **One Minus** | 1 − x | inverser un masque 0→1 en 1→0 (en restant entre 0 et 1) |
| **Saturate** | bloque entre 0 et 1 | supprimer les valeurs négatives ou > 1 |
| **Power** | xⁿ | resserrer un dégradé (n grand = plus concentré, plus contrasté) |
| **Smoothstep** | transition douce entre Edge1 et Edge2 | contrôler précisément un seuil |
| **Lerp** | mélange A→B selon T | dégradé entre deux couleurs |

⚠️ **Negate ≠ One Minus** : sur un masque 0→1, Negate donne 0→−1 (tout est noir), One Minus donne 1→0.
⚠️ Toujours **Saturate** avant un **Power** : une puissance sur un nombre négatif peut donner des **NaN** (pixels noirs, carrés noirs avec le Bloom).

### Add vs Multiply (couleurs)

Les deux travaillent canal par canal (R avec R, V avec V, B avec B).

- **Multiply = filtre** (vitre teintée) : ne peut qu'assombrir. Blanc = neutre, noir = efface. Rouge × Vert = noir.
- **Add = lumière** (deux projecteurs) : ne peut qu'éclaircir. Noir = neutre. Rouge + Vert = jaune. Peut dépasser 1 (HDR).

**Repère :** un **masque** (0 à 1) → Multiply. Deux **effets lumineux** à combiner → Add.
**Méthode standard :** chaque masque × sa couleur, **puis** Add des résultats.

### Emission, HDR, Glow

1. **Emission** : la surface produit sa propre lumière, indépendamment de l'éclairage. Pas de halo.
2. **HDR** : une couleur peut dépasser 1 (« plus blanc que blanc »). Invisible directement à l'écran, mais l'information est conservée.
3. **Bloom** (post-process) : fait « baver » les pixels au-dessus d'un seuil (≈ 1). **C'est lui qui crée le glow.**

| Setup | Résultat |
|---|---|
| Emission ≤ 1 | surface auto-éclairée, pas de halo |
| Emission HDR, sans Bloom | pareil, pas de halo |
| Emission HDR + Bloom | **glow** |

Rien de tout ça n'éclaire les autres objets : il faut une vraie **Point Light** (ou du GI baké).

---

## 2. Sphère au bord qui se dissout

**Réglages :** Surface Type = **Transparent** (Blend = Alpha).

```
Fresnel Effect (Power 2–4) → One Minus ─┐
                                        Subtract → Smoothstep (0 / 0.3) → Saturate → Alpha
Noise × intensité ──────────────────────┘
```

- **Fresnel** : 0 au centre, 1 sur les bords (vus depuis la caméra).
- **One Minus** : centre opaque, bords transparents.
- **Noise** (Gradient / Simple, Scale 10–30) : le bruit « mange » surtout les bords, déjà faibles.
- **Animer le noise** : Time × vitesse, Add à l'UV ou à la Position (Object space, sans couture).
- **Smoothstep** : douceur de la transition.

**Variantes :**
- Bord rongé net : **Alpha Clipping** + Alpha Clip Threshold (peut rester Opaque).
- Liseré lumineux : deux Smoothstep décalés, soustraits → × couleur HDR → Emission.
- Dissolution globale : propriété Float `Dissolve` soustraite avant le Smoothstep.

---

## 3. Glow interne : le faux volume

Un shader ne dessine que la **surface**. Pour simuler un cœur lumineux à l'intérieur, on calcule pour chaque pixel **à quelle distance le rayon de vue passe du pivot** de l'objet.

```
Caméra ●────────────P──────→ rayon (D)
         ╲          ┆
           ╲        ┆  distance cherchée
          V  ╲      ┆
               ╲    ┆
                 ● Pivot (Object Position)
```

C'est une distance **point ↔ ligne**, pas point ↔ point. D'où le besoin d'un calcul particulier.

### Version A : Cross Product (raccourci)

| Étape | Node | Ce que c'est |
|---|---|---|
| 1 | Object → Position | **point** pivot |
| 2 | Camera → Position | **point** caméra |
| 3 | Subtract (Object − Camera) = **V** | **flèche** caméra → pivot (sa longueur compte, **on ne la normalise pas**) |
| 4 | View Direction (World) → Normalize → Negate = **D** | **direction** du rayon (Shader Graph donne surface → caméra, on la retourne) |
| 5 | Cross Product (V, D) → Length | **distance** rayon ↔ pivot |

**Pourquoi ça marche :** |V × D| = |V| × |D| × sin(angle).
Avec |D| = 1 → |V| × sin(angle) = hypoténuse × sin = **côté opposé** = la distance cherchée.

- Si D n'était **pas normalisé**, la taille du glow varierait avec la distance de la caméra.
- Si V était **normalisé**, on n'aurait plus qu'un sin(angle) : glow de taille fixe **à l'écran**, qui grossit par rapport au perso quand on s'éloigne.

### Version B : calcul explicite du point P (même résultat)

```
Dot(V, D)            → distance caméra → P le long du rayon (nombre)
× D                  → flèche caméra → P
+ Camera.Position    → point P
Object.Position − P  → flèche P → pivot
Length               → distance
```

Plus long, mais plus intuitif : c'est bien « la norme de la différence entre deux points », une fois P connu. D doit aussi être normalisé ici.

### Suite du graph

```
distance → Divide(GlowRadius) → One Minus → Saturate → Power(2–6) → × Color HDR → Emission
```

- **Divide** : 0 au centre, 1 au rayon du glow. `GlowRadius` = taille.
- **One Minus** : 1 au centre.
- **Saturate** : supprime les négatifs hors du rayon.
- **Power** : netteté du cœur.
- **Color HDR** + **Bloom** obligatoire pour le halo.

### Astuces

- Objet mis à l'échelle : `GlowRadius` × Object **Scale**.x.
- Perso dont le pivot est aux pieds : décaler le centre, `Object Position + (0, 1, 0)`.
- Scintillement : Time → Sine → remap 0.9–1.1 → × intensité.
- Alternative simple : **deux meshes** (enveloppe transparente + petite sphère émissive HDR) + Bloom + Point Light.

---

## 4. Combiner les effets (effet fantôme)

Deux masques issus de la même distance :
- **sans** One Minus → brille en périphérie ;
- **avec** One Minus → brille au cœur.

**Combinaison :**
```
masque cœur × couleur cœur ──┐
                             Add → Emission
masque bord × couleur bord ──┘
```

- ❌ **Multiply** des deux masques → 1×0 = 0 et 0×1 = 0 : il ne reste qu'un anneau faible.
- ✅ **Lerp**(couleur cœur, couleur bord, masque bord) si c'est juste un dégradé de couleur.
- Trop brillant au chevauchement → baisser l'intensité HDR d'une couleur, pas toucher aux masques.

**Alpha :**
- Une même sortie peut alimenter plusieurs entrées : pas besoin de dupliquer les nodes.
- Se brancher **après le Saturate** (ou le Power).
- `Alpha = Maximum(masque cœur, masque bord)` pour garder les deux effets visibles.
- Alternative : **Blend = Additive**, où le noir est invisible (l'objet ne peut qu'éclaircir).

---

## 5. Lignes qui rayonnent depuis le pivot

Sur la surface, une ligne partant du pivot n'apparaît que comme un **point**. Pour voir des **lignes**, on travaille dans le disque projeté face à la caméra (même principe que le glow).

### Calculer l'angle autour du pivot

```
Cross Product (flèche, avant Length)
→ Transform (World → View, Direction)
→ Split (R = x, G = y)
→ Arctangent2 (A = G, B = R)       // angle entre −π et π
→ Divide 6.283 → Add 0.5           // angle entre 0 et 1
```

### Lignes immobiles (noise sur l'angle seul)

```
Angle (0–1) → × N → Vector2 (X, Y = 0) → Gradient Noise (Scale 1) → Power (≈ 6) → masque de lignes
```

- Chaque direction reçoit une valeur aléatoire, constante en s'éloignant du centre → une **ligne**.
- **N** = densité, **Power / Smoothstep** = finesse et quantité.
- Changer le **Y** du Vector2 = autre tirage aléatoire.
- Puis : × masque de cœur → × Color HDR → Add à l'Emission.

### Lignes animées (étape suivante)

- **U** = angle × 20–50 (beaucoup de variation → beaucoup de lignes)
- **V** = distance × 0.5–1 − Time × vitesse (peu de variation → blobs étirés en lignes)
- **− Time** : vers l'extérieur ; **+ Time** : vers le centre.

### Couture

L'angle saute de 1 à 0 à un endroit → ligne de coupure avec Gradient/Simple Noise (non tileables).
- Solution : **texture de noise tileable** (Sample Texture 2D, Wrap = Repeat) avec un N **entier**.
- Rayons réguliers : Sine(angle × N × 2π), sans couture.

### Variante « énergie qui coule sur le corps »

V = Length(Position **Object space**) − Time : le motif s'écoule le long du perso vers les extrémités (tête, mains, pieds sont à des distances différentes du pivot).

---

## 6. Bonnes habitudes

- **Nommer / grouper** les nodes : « point pivot », « flèche V », « direction D ».
- Se demander pour chaque fil : **point, flèche, direction, nombre ou couleur ?**
- Normaliser toute direction utilisée dans Cross, Dot, angles.
- Saturate avant Power et avant Alpha.

## 7. Pistes à explorer

- [ ] Traiter la couture de l'angle (texture tileable)
- [ ] Animer les lignes radiales
- [ ] Revoir le Fresnel et le Smoothstep en détail
- [ ] Vrai volume de brume par raymarching (Custom Function node)

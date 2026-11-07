# Spécification — Belmont Practice

Ce document a été reconstitué à partir de l'historique de conception (23/10 → 03/11/2026, versions 0.1 → 0.65)
et du code. Il décrit **ce que le mod doit faire** et les **règles décidées en cours de route**.
Le détail des décisions se trouve aussi dans l'historique git (un commit par version, message en français).

Référence de départ : le DebugMod de Silksong (BepInEx) avec les savestates en haut au centre
et le panneau latéral à droite.

---

## 0. Règles transverses

| # | Règle |
|---|---|
| R1 | **Utilisable seulement en jeu** : les fenêtres ne s'ouvrent que si le joueur contrôle le personnage ou un menu en jeu (les ouvrir plus tôt faisait planter le jeu). |
| R2 | **Interface en anglais**, sauf les noms de savestates saisis par l'utilisateur. |
| R3 | **Les clics dans l'interface du mod n'agissent jamais sur le jeu.** Le clavier n'est retiré au jeu que pendant le **renommage** d'une savestate. |
| R4 | **Style du jeu** : polices, couleurs et assets du jeu, échelle 85 %, fonds semi-transparents, bandeaux rouges unis, cadre « carte d'arcane » détouré, séparateurs en losange. On garde la disposition, on habille avec les assets. |
| R5 | **Sécurité des sauvegardes** : copie automatique des saves à chaque lancement (`<jeu>\BelmontPractice\SauvegardesJeu`, 10 dernières) ; toute manipulation manuelle d'une save se fait jeu fermé et après sauvegarde. |
| R6 | **Savestate ≠ Fight** : charger une savestate rend **exactement** l'état capturé (mémoire comprise) ; Fight sert à refaire un boss **vite** (raccourcis autorisés). |
| R7 | **F1** ouvre / ferme toutes les fenêtres avec un **fondu** uniforme (boutons et contours compris, même opacité pour tout le calque). Les fenêtres sont construites d'avance pour une première ouverture immédiate. |
| R8 | Après le chargement d'une savestate ou d'un boss, les fenêtres du mod se **ferment**. |

---

## 1. Savestates

### 1.1 Chargement
- **Aucun écran de chargement visible** quand c'est possible (chargement rapide dans la même salle / le même niveau) ; sinon, un **fondu au noir complet et solide**, dans tous les cas : pas de flash bleu, gris ou vert, et le personnage ne bouge pas pendant la transition.
- L'état restauré comprend : position, salle et son cycle, objets mobiles (scies…), PV, mana, potions (y compris les potions bonus), mémoire du monde.
- Les temps de chargement ne doivent pas s'allonger au fil des rechargements.
- Option **Full reload** (case dans la fenêtre des savestates) : force un rechargement complet.
- Possibilité de charger pendant une cinématique ou une absorption : les états de cinématique sont retirés, la fenêtre d'arcane est fermée et un chargement complet est forcé.

### 1.2 Collection (fenêtre du haut)
- Pages de **10 emplacements** ; bouton **Add page** / suppression de page (à droite) ; indicateur « Page 1/X » à gauche.
- Emplacements **nommables** (crayon) et supprimables (poubelle) ; double-clic sur un nom pour charger ; confirmation avant d'écraser.
- Renommage : répétition des touches maintenues (Retour arrière), **Ctrl+A**, flèches, sélection visible, sélection à la souris.
- **Quickslot** : emplacement rapide en mémoire seulement, **vidé en quittant le jeu ou en revenant au menu principal** (le point de réapparition aussi).
- Raccourcis : sauver / charger le quickslot, sauver dans un emplacement, charger un emplacement, emplacement → quickslot, quickslot → emplacement, page précédente / suivante. Affichés comme des **touches de clavier** (keycaps), pas dans des boutons.
- Stockage : `<jeu>\BelmontPractice\Savestates`.

---

## 2. Panneau latéral (F1)

Pleine hauteur, barre de défilement visible collée à droite, **zone d'aide fixe en bas** qui explique l'élément survolé.
Onglets : **General · Skills · Items · Bosses · Keybinds**.

### 2.1 General
- **Utilities** (cases à cocher style jeu, sans sous-texte) : Infinite HP, Infinite mana, Infinite potions, Noclip, Infinite jump, **Lock HP**.
  - Noclip : fonctionne au stick, à la croix et au clavier, fluide, suit les transitions de salle verticales et met à jour le point de réapparition (dégâts de pièges) à chaque changement de salle.
  - Lock HP : fige les PV **à leur valeur actuelle** au moment où on coche (pas au maximum). Les soins et les dégâts s'appliquent, puis les PV reviennent à la valeur figée ; les potions restent buvables.
- **Santé / mana / potions** : boutons ±10 / ±50, soin, contrôle du nombre de potions (3 de base + bonus, potions bonus ajoutables et retirables). Les barres du HUD se mettent à jour.
- **Upgrades** : Attack+ / Defense+ (livres, valeur saisissable), Life hearts / Mana hearts (6 × 4 fragments), avec les icônes du jeu et les maximums prévus par le jeu.
- **Level** 1–99, saisie exacte dans un champ texte.
- **Stats** : attaque et défense cachées, en lecture seule.
- **Respawn point** Set / Clear : après une mort, recharge l'état capturé à l'endroit exact.

### 2.2 Skills
- **Movement** : les 5 capacités de déplacement, activables ou désactivables, avec leurs icônes.
- **Arcanas** (sorts) : une ligne par arcane, le nom à gauche puis l'icône, suivie de la chaîne de ses **3 améliorations** en losanges alignés en colonnes. Clic : débloquer / retirer ; clic droit : équiper / déséquiper. Retirer une arcane la déséquipe.
- Ligne spéciale **Cursed Flame / Absolution** en première position : un bouton pour basculer de l'une à l'autre, jamais les deux à la fois.
- Toutes les arcanes sont visibles même sur une partie neuve.
- Survol : halo doré animé pour l'élément équipé, pas de liseré au simple survol.

### 2.3 Items
- **Weapons** : les 28 armes, 4 par ligne, 4 tiers (aide : « Fists of Talos - Tier 3 »). Clic : débloquer / retirer ; clic droit : équiper.
- **Relics** et **équipements** (3 équipables parmi N), même présentation.
- Code couleur : **doré = équipé**, rouge au survol.

### 2.4 Bosses
- Tous les boss affichés **tout le temps** (pas de « ??? »), **dans l'ordre du jeu** : Leon, Joan, Medusa, Death I, Isaac I, Carmilla, Talos, Death II, les autres boss, Isaac II, Dracula, Dracula Beast. Brauner et le Juge (Saint Louis) sont retirés : ce sont des cinématiques.
- Boutons d'état par boss : **Alive / Defeated**, **TP** (devant l'arène) et **Fight** (Alive + TP : on n'a plus qu'à entrer).
- Fight pose **tous les drapeaux nécessaires dès le clic**. Aucun combat « d'apprentissage » ne doit être nécessaire. Les conditions de chaque boss sont stockées dans `scenarios_boss.json` et les entrées d'arène dans `arenes.json`, livrés avec le mod.
- Téléportation **toujours en dehors de l'arène**.
- Isaac et Death ont deux rencontres chacun (I / II). Death I : entrée scellée par l'histoire, combat perdu scénarisé suivi de la scène avec Trevor. Dracula n'a pas de drapeau de défaite. Dracula Beast est la phase 2 : une mort en phase 2 ramène à la porte de la phase 1.
- Options Boss : **Boss infinite HP**, Lock HP du boss.
- Cas particuliers : Doppelganger (interaction pour entrer dans l'arène), Mammon, Carmilla (entité présente pendant l'intro).

### 2.5 Keybinds
- Options On / Off (cases à cocher), par exemple l'accélération des cinématiques ou l'affichage du nom de la salle.
- Tous les raccourcis sont redéfinissables : clic puis touche ; Retour arrière = aucune touche ; Échap = annuler ; clic droit = valeur par défaut. Enregistrement immédiat dans `BepInEx\config\fr.vasseur.belmontpractice.cfg`.

---

## 3. Téléportation
- Vers **n'importe quel miroir**.
- **Depuis la carte** : au point visé, entre zones et niveaux. Confirmation en deux appuis (touche **T** ou **Start** à la manette), avec un message près du curseur qui utilise les glyphes de touches du jeu. L'aide reste toujours au-dessus du curseur.
- Atterrissage au sol si le sol est à moins de **12 unités** (comme au miroir).
- Pas de plantage sur les zones sans carte (« ??? »).

## 4. Cinématiques
- **Accélération** des cinématiques de combat : F7 / bouton **Y** (pas A, qui sert à passer certaines cinématiques), avec un indicateur à l'écran.
- On distingue :
  - les **cinématiques du moteur** (dialogues et acteurs du jeu) : accélérées ×10 / ×20 ;
  - les **vidéos** : passées avec le bouton « passer » prévu par le jeu (appui détecté et simulé).
- Les **absorptions** (maintenir RB) et les révélations de carte s'enchaînent automatiquement. Les boîtes de dialogue ne restent pas à l'écran.

## 5. Affichages permanents
- **Boîte de version** (jeu + mod), cadre carte : visible dans les menus et pendant les chargements longs, masquée en jeu et quand les panneaux F1 sont ouverts.
- **Nom de la salle** (en bas à droite, activable, identifiant raccourci) : masqué seulement pendant les chargements longs, réapparaît en fondu 0,3 s après le retour du contrôle.
- **Notifications** en bas à gauche, petit cadre carte : apparition instantanée, disparition en fondu.

---

## 6. Distribution
- Les collègues installent **BepInEx 6.0.0-be.791 IL2CPP** eux-mêmes ; le zip (`package.ps1`) ne contient que le mod, le LISEZMOI et les données des boss.
- Dépôt GitHub **privé** : `icons/` et `docs/maquettes/assets/` contiennent des images extraites du jeu.

## 7. Reste à faire / pistes
- **Hot reload** : décidé mais pas commencé, voir [notes/hot-reload.md](notes/hot-reload.md).
- Boss à confirmer en jeu : Isaac II, Death II et les boss de fin.
- Pistes laissées de côté :
  - réutiliser la transition « chauves-souris » du jeu (abandonné) ;
  - supprimer totalement l'écran de chargement pour les sauts entre niveaux.
- Question ouverte (03/11) : pertes de son dans le casque, lien avec le mod non établi.

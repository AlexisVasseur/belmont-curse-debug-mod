# interface Specification

## Purpose
Règles transverses de l'interface du mod : quand elle est disponible, comment elle s'ouvre, son style et son isolation vis-à-vis du jeu.

## Requirements
### Requirement: Disponibilité en jeu uniquement
Les fenêtres du mod MUST ne s'ouvrir que lorsque le joueur contrôle le personnage ou un menu en jeu (les ouvrir plus tôt faisait planter le jeu).

#### Scenario: F1 avant d'être en jeu
- **WHEN** le joueur appuie sur F1 sur l'écran titre ou pendant un chargement
- **THEN** aucune fenêtre du mod ne s'ouvre

#### Scenario: F1 en jeu
- **WHEN** le joueur contrôle le personnage et appuie sur F1
- **THEN** toutes les fenêtres du mod s'ouvrent

### Requirement: Ouverture et fermeture par F1 avec fondu
F1 MUST ouvrir et fermer toutes les fenêtres ensemble avec un fondu uniforme : boutons et contours compris, même opacité pour tout le calque. Les fenêtres MUST être construites d'avance pour que la première ouverture soit immédiate.

#### Scenario: Première ouverture
- **WHEN** le joueur appuie sur F1 pour la première fois de la session
- **THEN** les fenêtres apparaissent en fondu sans délai de construction

### Requirement: Panneau latéral
Le panneau latéral MUST occuper toute la hauteur de l'écran, avec une barre de défilement visible collée à droite et une zone d'aide fixe en bas qui explique l'élément survolé. Ses onglets MUST être, dans l'ordre : General, Skills, Items, Bosses, Keybinds.

#### Scenario: Survol d'une option
- **WHEN** le joueur survole une option du panneau
- **THEN** la zone d'aide en bas affiche l'explication de cette option

### Requirement: Interface en anglais
Tous les textes de l'interface MUST être en anglais, sauf les noms de savestates saisis par l'utilisateur.

#### Scenario: Nom saisi en français
- **WHEN** l'utilisateur nomme une savestate « Avant Méduse »
- **THEN** ce nom est affiché tel quel, le reste de l'interface restant en anglais

### Requirement: Isolation des entrées
Les clics dans l'interface du mod MUST ne jamais agir sur le jeu. Le clavier MUST n'être retiré au jeu que pendant le renommage d'une savestate.

#### Scenario: Clic sur un bouton du mod
- **WHEN** le joueur clique sur un bouton du panneau
- **THEN** le personnage n'attaque pas et le jeu ne reçoit pas le clic

#### Scenario: Renommage
- **WHEN** le joueur renomme une savestate
- **THEN** les touches tapées ne déplacent pas le personnage

### Requirement: Style du jeu
L'interface MUST reprendre les polices, couleurs et assets du jeu : échelle 85 %, fonds semi-transparents, bandeaux rouges unis, cadre « carte d'arcane » détouré, séparateurs en losange. La disposition reste celle des maquettes (`docs/maquettes/`) ; seul l'habillage vient du jeu.

#### Scenario: Affichage du panneau
- **WHEN** le panneau est affiché
- **THEN** il utilise les polices et cadres du jeu, pas les contrôles Unity par défaut

### Requirement: Fermeture après chargement
Les fenêtres du mod MUST se fermer après le chargement d'une savestate ou d'un boss.

#### Scenario: Chargement d'une savestate
- **WHEN** le joueur charge une savestate depuis la fenêtre
- **THEN** les fenêtres du mod se ferment et le joueur reprend la main

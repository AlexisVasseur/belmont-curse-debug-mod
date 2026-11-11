# overlays Specification

## Purpose
Affichages permanents hors panneau : version, nom de la salle, input viewer et notifications.

## Requirements
### Requirement: Boîte de version
Une boîte (cadre carte) MUST afficher la version du jeu et du mod dans les menus et pendant les chargements longs, et MUST être masquée en jeu et quand les panneaux F1 sont ouverts.

#### Scenario: Menu principal
- **WHEN** le joueur est sur le menu principal
- **THEN** la boîte de version est visible

#### Scenario: En jeu
- **WHEN** le joueur contrôle le personnage
- **THEN** la boîte de version est masquée

### Requirement: Nom de la salle
Le nom de la salle (identifiant raccourci) MUST être affiché en bas à droite si l'option est active. Il MUST être masqué seulement pendant les chargements longs et réapparaître en fondu 0,3 s après le retour du contrôle.

#### Scenario: Fin de chargement
- **WHEN** un chargement long se termine et le joueur reprend le contrôle
- **THEN** le nom de la salle réapparaît en fondu en 0,3 s

### Requirement: Notifications
Les notifications MUST s'afficher en bas à gauche dans un petit cadre carte, apparaître instantanément et disparaître en fondu.

#### Scenario: Sauvegarde du quickslot
- **WHEN** le joueur sauve dans le quickslot
- **THEN** une notification apparaît instantanément en bas à gauche puis disparaît en fondu

### Requirement: Input viewer
Si l'option **Input viewer** est cochée, un petit encadré MUST afficher en bas à droite de l'écran, en jeu, les entrées du joueur, selon la disposition choisie par l'option **Viewer layout** :
- **Keyboard / mouse** : à gauche la direction reçue par le jeu (8 directions), à droite une case par action du personnage, étiquetée avec sa touche clavier / souris et allumée tant qu'elle est appuyée ;
- **Gamepad** : une manette façon Xbox avec boutons ronds, LT / RT remplies selon la pression, LB / RB, stick gauche en haut à gauche, A B X Y en losange en haut à droite (couleurs Xbox), croix et stick droit en bas, View / Menu au centre ; chaque élément s'allume quand il est utilisé et les sticks montrent leur position ;
- **Auto** (par défaut) : la disposition du dernier périphérique utilisé, qui MUST changer dès que le joueur utilise l'autre.

L'encadré MUST se placer au-dessus de l'encadré du nom de la salle quand celui-ci est affiché, se décaler à gauche du panneau F1 quand il est ouvert et être masqué pendant les chargements longs. L'option Input viewer est désactivée par défaut.

#### Scenario: Bascule automatique vers la manette
- **WHEN** la disposition est Auto, le viewer montre le clavier et le joueur appuie sur A à la manette
- **THEN** le viewer passe à la disposition manette et le bouton A rond, vert, s'allume

#### Scenario: Retour au clavier
- **WHEN** la disposition est Auto et le joueur appuie ensuite sur une touche du clavier
- **THEN** le viewer repasse à la disposition clavier

#### Scenario: Disposition forcée
- **WHEN** la disposition est Keyboard / mouse et le joueur joue à la manette
- **THEN** le viewer reste en disposition clavier

#### Scenario: Option décochée
- **WHEN** l'option Input viewer est décochée
- **THEN** l'encadré n'est pas affiché

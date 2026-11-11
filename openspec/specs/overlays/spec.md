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
Si l'option **Input viewer** est cochée, un petit encadré MUST afficher en bas à droite de l'écran, en jeu, les entrées reçues par le jeu : à gauche la direction (8 directions), à droite une case par action du personnage, étiquetée avec sa touche sur le périphérique utilisé en dernier (clavier / souris ou manette) et allumée tant qu'elle est appuyée. Il MUST se placer au-dessus de l'encadré du nom de la salle quand celui-ci est affiché, se décaler à gauche du panneau F1 quand il est ouvert et être masqué pendant les chargements longs. L'option est désactivée par défaut.

#### Scenario: Saut à la manette
- **WHEN** l'option est cochée et le joueur appuie sur le bouton de saut de la manette en tenant la direction diagonale haut-droite
- **THEN** la case de l'action de saut, étiquetée « A », s'allume et la case haut-droite de la croix s'allume

#### Scenario: Retour au clavier
- **WHEN** le joueur appuie ensuite sur une touche du clavier liée à une action
- **THEN** les étiquettes des cases passent aux touches du clavier

#### Scenario: Option décochée
- **WHEN** l'option Input viewer est décochée
- **THEN** l'encadré n'est pas affiché

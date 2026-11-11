## ADDED Requirements
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

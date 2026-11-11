## MODIFIED Requirements
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

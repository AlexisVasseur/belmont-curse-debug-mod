# teleport Specification

## Purpose
Téléportation vers les miroirs et vers n'importe quel point de la carte.

## Requirements
### Requirement: Téléportation vers un miroir
Le mod MUST permettre de se téléporter vers n'importe quel miroir.

#### Scenario: Miroir non découvert
- **WHEN** le joueur choisit un miroir qu'il n'a pas encore découvert
- **THEN** il est téléporté devant ce miroir

### Requirement: Téléportation depuis la carte
Depuis la carte, le mod MUST téléporter au point visé, y compris entre zones et niveaux, avec une confirmation en deux appuis (touche **T** ou **Start** à la manette). Un message près du curseur MUST utiliser les glyphes de touches du jeu et rester au-dessus du curseur.

#### Scenario: Confirmation en deux appuis
- **WHEN** le joueur appuie une fois sur T sur la carte
- **THEN** un message de confirmation apparaît au-dessus du curseur, et la téléportation n'a lieu qu'au second appui

### Requirement: Atterrissage au sol
Si le sol est à moins de 12 unités sous le point visé, le joueur MUST atterrir au sol (comme au miroir).

#### Scenario: Point en l'air
- **WHEN** le point visé est à 5 unités au-dessus du sol
- **THEN** le joueur apparaît posé au sol

### Requirement: Zones sans carte
Viser une zone sans carte (« ??? ») MUST ne pas faire planter le jeu.

#### Scenario: Zone inconnue
- **WHEN** le joueur vise une zone « ??? » et confirme
- **THEN** le jeu ne plante pas

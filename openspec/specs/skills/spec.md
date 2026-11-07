# skills Specification

## Purpose
Onglet **Skills** : capacités de déplacement et arcanes (sorts) avec leurs améliorations.

## Requirements
### Requirement: Capacités de déplacement
L'onglet MUST afficher les 5 capacités de déplacement avec leurs icônes, chacune activable ou désactivable.

#### Scenario: Désactivation
- **WHEN** le joueur désactive une capacité de déplacement
- **THEN** le personnage ne peut plus l'utiliser

### Requirement: Arcanes
Chaque arcane MUST occuper une ligne : le nom à gauche, puis l'icône, puis la chaîne de ses 3 améliorations en losanges alignés en colonnes. Toutes les arcanes MUST être visibles, même sur une partie neuve. Clic : débloquer / retirer ; clic droit : équiper / déséquiper. Retirer une arcane MUST la déséquiper.

#### Scenario: Retrait d'une arcane équipée
- **WHEN** le joueur retire une arcane équipée
- **THEN** l'arcane est retirée et déséquipée

#### Scenario: Partie neuve
- **WHEN** le joueur ouvre l'onglet sur une partie neuve
- **THEN** toutes les arcanes sont listées

### Requirement: Cursed Flame / Absolution
La première ligne MUST être Cursed Flame / Absolution, avec un bouton pour basculer de l'une à l'autre ; le joueur MUST ne jamais posséder les deux à la fois.

#### Scenario: Bascule
- **WHEN** le joueur possède Cursed Flame et clique sur le bouton de bascule
- **THEN** il possède Absolution et plus Cursed Flame

### Requirement: Retour visuel
L'élément équipé MUST avoir un halo doré animé ; le simple survol MUST ne pas ajouter de liseré.

#### Scenario: Survol
- **WHEN** le joueur survole une arcane non équipée
- **THEN** aucun liseré n'apparaît

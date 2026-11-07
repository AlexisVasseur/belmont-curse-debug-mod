# items Specification

## Purpose
Onglet **Items** : armes, reliques et équipements.

## Requirements
### Requirement: Armes
L'onglet MUST afficher les 28 armes, 4 par ligne, réparties en 4 tiers, l'aide indiquant le nom et le tier (ex. « Fists of Talos - Tier 3 »). Clic : débloquer / retirer ; clic droit : équiper.

#### Scenario: Équiper une arme
- **WHEN** le joueur fait un clic droit sur une arme débloquée
- **THEN** l'arme est équipée et affichée en doré

### Requirement: Reliques et équipements
Les reliques et les équipements (3 équipables parmi N) MUST suivre la même présentation et les mêmes interactions que les armes.

#### Scenario: Quatrième équipement
- **WHEN** 3 équipements sont équipés et le joueur en équipe un autre
- **THEN** le jeu n'en garde jamais plus de 3 équipés

### Requirement: Code couleur
Un élément équipé MUST être affiché en doré ; le survol MUST être signalé en rouge.

#### Scenario: Survol d'une relique
- **WHEN** le joueur survole une relique
- **THEN** elle est mise en évidence en rouge

# overlays Specification

## Purpose
Affichages permanents hors panneau : version, nom de la salle et notifications.

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

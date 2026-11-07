# keybinds Specification

## Purpose
Onglet **Keybinds** : options On / Off et redéfinition des raccourcis.

## Requirements
### Requirement: Options On / Off
L'onglet MUST proposer des options en cases à cocher, par exemple l'accélération des cinématiques ou l'affichage du nom de la salle.

#### Scenario: Masquer le nom de la salle
- **WHEN** le joueur décoche l'affichage du nom de la salle
- **THEN** le nom de la salle n'est plus affiché

### Requirement: Raccourcis redéfinissables
Tous les raccourcis MUST être redéfinissables : clic puis touche ; Retour arrière = aucune touche ; Échap = annuler ; clic droit = valeur par défaut.

#### Scenario: Réinitialisation
- **WHEN** le joueur fait un clic droit sur un raccourci
- **THEN** le raccourci reprend sa valeur par défaut

#### Scenario: Annulation
- **WHEN** le joueur clique sur un raccourci puis appuie sur Échap
- **THEN** le raccourci garde sa valeur précédente

### Requirement: Enregistrement immédiat
Chaque modification MUST être enregistrée immédiatement dans `BepInEx\config\fr.vasseur.belmontpractice.cfg`.

#### Scenario: Relance du jeu
- **WHEN** le joueur modifie un raccourci puis relance le jeu
- **THEN** le nouveau raccourci est conservé

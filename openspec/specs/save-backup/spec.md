# save-backup Specification

## Purpose
Protéger les sauvegardes du jeu contre toute perte liée au mod.

## Requirements
### Requirement: Copie automatique au lancement
À chaque lancement du jeu, le mod MUST copier les sauvegardes du jeu dans `<jeu>\BelmontPractice\SauvegardesJeu` et MUST ne conserver que les 10 copies les plus récentes.

#### Scenario: Onzième lancement
- **WHEN** le jeu est lancé alors que 10 copies existent déjà
- **THEN** une nouvelle copie est créée et la plus ancienne est supprimée

### Requirement: Manipulation manuelle jeu fermé
Toute manipulation manuelle d'une sauvegarde du jeu (`%USERPROFILE%\AppData\LocalLow\KONAMI\Castlevania Belmont's Curse`) MUST se faire jeu fermé et après une copie de sauvegarde.

#### Scenario: Remplacement d'une save
- **WHEN** un développeur doit remplacer un fichier de save
- **THEN** il ferme le jeu, copie le dossier de saves, puis remplace le fichier

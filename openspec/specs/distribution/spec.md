# distribution Specification

## Purpose
Livrer le mod aux collègues : prérequis, contenu du paquet, contrôle de version de BepInEx et dépôt.

## Requirements
### Requirement: Prérequis BepInEx
Les utilisateurs MUST installer eux-mêmes BepInEx 6.0.0-be.793 Unity IL2CPP win-x64 ; le paquet MUST ne pas contenir BepInEx.

#### Scenario: Installation
- **WHEN** un collègue suit le README
- **THEN** il installe BepInEx be.793 depuis le lien du README avant de décompresser le mod

### Requirement: Builds BepInEx testées
Au démarrage, le mod MUST vérifier la build de BepInEx et signaler toute build hors de la liste testée (be.791 à be.793).

#### Scenario: Build non testée
- **WHEN** le jeu tourne avec une build de BepInEx hors de la liste
- **THEN** le mod l'indique dans le journal BepInEx

### Requirement: Contenu du paquet
`package.ps1` MUST produire `dist\BelmontPractice-<version>.zip` contenant uniquement la DLL et les icônes (`BepInEx\plugins\BelmontPractice`), `LISEZMOI_BelmontPractice.txt` et, sauf `-SansDonneesBoss`, `BelmontPractice\scenarios_boss.json` et `arenes.json`.

#### Scenario: Paquet sans données de boss
- **WHEN** le développeur lance `.\package.ps1 -SansDonneesBoss`
- **THEN** le zip ne contient pas le dossier `BelmontPractice`

### Requirement: Version du jeu
Le mod MUST cibler la version 1.1.3 du jeu, téléchargée par `download_depot 4231820 4231821 8796979530797619124`.

#### Scenario: Copie séparée
- **WHEN** un collègue suit l'installation
- **THEN** il obtient une copie 1.1.3 séparée de son installation Steam

### Requirement: Dépôt privé
Le dépôt GitHub MUST rester privé : `icons/` et `docs/maquettes/assets/` contiennent des images extraites du jeu.

#### Scenario: Nouveau collègue
- **WHEN** un collègue doit accéder au code
- **THEN** il est invité comme collaborateur, le dépôt n'est pas rendu public

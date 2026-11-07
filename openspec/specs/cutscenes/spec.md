# cutscenes Specification

## Purpose
Réduire le temps passé dans les cinématiques pendant l'entraînement.

## Requirements
### Requirement: Accélération à la demande
F7 au clavier et **Y** à la manette MUST activer l'accélération des cinématiques de combat (pas A, qui sert à passer certaines cinématiques), avec un indicateur à l'écran.

#### Scenario: Activation à la manette
- **WHEN** le joueur appuie sur Y
- **THEN** l'accélération est activée et un indicateur s'affiche

### Requirement: Traitement selon le type de cinématique
Les cinématiques du moteur (dialogues et acteurs du jeu) MUST être accélérées ×10 / ×20. Les vidéos MUST être passées avec le bouton « passer » prévu par le jeu (appui détecté et simulé).

#### Scenario: Vidéo
- **WHEN** une vidéo démarre avec l'accélération active
- **THEN** elle est passée par le mécanisme de saut du jeu

#### Scenario: Cinématique du moteur
- **WHEN** une cinématique du moteur démarre avec l'accélération active
- **THEN** elle est jouée accélérée

### Requirement: Enchaînements automatiques
Les absorptions (maintenir RB) et les révélations de carte MUST s'enchaîner automatiquement ; les boîtes de dialogue MUST ne pas rester à l'écran.

#### Scenario: Absorption
- **WHEN** une absorption commence avec l'accélération active
- **THEN** elle se termine sans que le joueur maintienne RB

# bosses Specification

## Purpose
Onglet **Bosses** : refaire n'importe quel boss rapidement (remise en vie, téléportation devant l'arène, Fight).

## Requirements
### Requirement: Liste des boss
Tous les boss MUST être affichés en permanence (pas de « ??? »), dans l'ordre du jeu : Leon, Joan, Medusa, Death I, Isaac I, Carmilla, Talos, Death II, les autres boss, Isaac II, Dracula, Dracula Beast. Brauner et le Juge (Saint Louis) MUST ne pas apparaître : ce sont des cinématiques.

#### Scenario: Partie neuve
- **WHEN** le joueur ouvre l'onglet sur une partie neuve
- **THEN** tous les boss sont listés par leur nom, dans l'ordre du jeu

### Requirement: Actions par boss
Chaque boss MUST proposer les boutons **Alive / Defeated**, **TP** (devant l'arène) et **Fight** (Alive + TP : il ne reste qu'à entrer).

#### Scenario: Fight
- **WHEN** le joueur clique sur Fight pour Medusa
- **THEN** Medusa est remise en vie et le joueur est placé devant l'arène

### Requirement: Fight sans apprentissage
Fight MUST poser tous les drapeaux nécessaires dès le clic ; aucun combat « d'apprentissage » MUST être nécessaire. Les conditions de chaque boss MUST être stockées dans `scenarios_boss.json` et les entrées d'arène dans `arenes.json`, livrés avec le mod. Contrairement aux savestates, Fight peut prendre des raccourcis d'état pour aller vite.

#### Scenario: Boss jamais combattu
- **WHEN** le joueur clique sur Fight pour un boss qu'il n'a jamais affronté
- **THEN** le combat se déclenche en entrant dans l'arène

### Requirement: Téléportation hors de l'arène
La téléportation TP / Fight MUST toujours placer le joueur en dehors de l'arène.

#### Scenario: TP
- **WHEN** le joueur clique sur TP
- **THEN** il apparaît devant l'entrée de l'arène, pas à l'intérieur

### Requirement: Rencontres multiples et cas particuliers
Isaac et Death MUST avoir deux entrées chacun (I / II). Death I : entrée scellée par l'histoire, combat perdu scénarisé suivi de la scène avec Trevor. Dracula n'a pas de drapeau de défaite. Dracula Beast est la phase 2 : une mort en phase 2 MUST ramener à la porte de la phase 1. Les cas particuliers MUST être gérés : Doppelganger (interaction pour entrer), Mammon, Carmilla (entité présente pendant l'intro).

#### Scenario: Mort contre Dracula Beast
- **WHEN** le joueur meurt en phase 2
- **THEN** il revient devant la porte de la phase 1

### Requirement: Options de boss
L'onglet MUST proposer **Boss infinite HP** et un Lock HP du boss.

#### Scenario: Boss infinite HP
- **WHEN** Boss infinite HP est cochée et le joueur frappe le boss
- **THEN** les PV du boss ne diminuent pas

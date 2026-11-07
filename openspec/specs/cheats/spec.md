# cheats Specification

## Purpose
Onglet **General** du panneau latéral : utilitaires (triches), santé / mana / potions, améliorations, niveau, statistiques et point de réapparition.

## Requirements
### Requirement: Utilitaires
L'onglet General MUST proposer, en cases à cocher au style du jeu et sans sous-texte : Infinite HP, Infinite mana, Infinite potions, Noclip, Infinite jump, Lock HP.

#### Scenario: Infinite mana
- **WHEN** Infinite mana est cochée et le joueur lance des arcanes
- **THEN** la mana ne diminue pas

### Requirement: Noclip
Le noclip MUST fonctionner au stick, à la croix et au clavier, de façon fluide, suivre les transitions de salle verticales et mettre à jour le point de réapparition des pièges à chaque changement de salle.

#### Scenario: Changement de salle vertical
- **WHEN** le joueur en noclip traverse le plafond vers la salle du dessus
- **THEN** la transition de salle a lieu et le point de réapparition des pièges est mis à jour

### Requirement: Lock HP
Lock HP MUST figer les PV à leur valeur au moment où la case est cochée (pas au maximum). Soins et dégâts s'appliquent puis les PV reviennent à la valeur figée ; les potions restent buvables.

#### Scenario: Dégâts avec Lock HP
- **WHEN** Lock HP est cochée à 120 PV et le joueur prend 30 dégâts
- **THEN** les PV reviennent à 120

### Requirement: Santé, mana et potions
L'onglet MUST proposer des boutons ±10 / ±50 pour les PV et la mana, un soin, et le contrôle du nombre de potions (3 de base plus des potions bonus ajoutables et retirables). Les barres du HUD MUST se mettre à jour immédiatement.

#### Scenario: Ajout d'une potion bonus
- **WHEN** le joueur ajoute une potion bonus
- **THEN** le HUD affiche une potion de plus

### Requirement: Améliorations
L'onglet MUST permettre de régler Attack+ et Defense+ (livres, valeur saisissable), Life hearts et Mana hearts (6 × 4 fragments), avec les icônes du jeu et sans dépasser les maximums prévus par le jeu.

#### Scenario: Valeur au-delà du maximum
- **WHEN** le joueur saisit une valeur supérieure au maximum du jeu
- **THEN** la valeur est ramenée au maximum

### Requirement: Niveau et statistiques
Le niveau MUST être réglable de 1 à 99 par saisie exacte dans un champ texte. L'attaque et la défense cachées MUST être affichées en lecture seule.

#### Scenario: Saisie du niveau
- **WHEN** le joueur saisit 42 dans le champ Level
- **THEN** le personnage passe au niveau 42

### Requirement: Point de réapparition
Les boutons **Set** / **Clear** MUST définir ou effacer un point de réapparition. Après une mort, le mod MUST recharger l'état capturé à l'endroit exact.

#### Scenario: Mort après Set
- **WHEN** un point de réapparition est défini et le joueur meurt
- **THEN** l'état capturé est rechargé à l'endroit exact du Set

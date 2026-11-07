# savestates Specification

## Purpose
Capturer et restaurer exactement un état de jeu, rapidement, et gérer une collection de savestates nommées.

## Requirements
### Requirement: Restauration exacte
Charger une savestate MUST rendre exactement l'état capturé : position, salle et son cycle, objets mobiles (scies…), PV, mana, potions (bonus comprises) et mémoire du monde. Contrairement à Fight (voir `bosses`), aucun raccourci d'état n'est autorisé.

#### Scenario: Piège en mouvement
- **WHEN** le joueur charge une savestate capturée pendant le passage d'une scie
- **THEN** la scie est à la même position de son cycle qu'à la capture

#### Scenario: Potions bonus
- **WHEN** la savestate a été capturée avec 2 potions bonus
- **THEN** le chargement rend exactement ces 2 potions bonus

### Requirement: Chargement sans écran de chargement
Le chargement MUST se faire sans écran de chargement visible lorsque c'est possible (même salle ou même niveau). Sinon, il MUST passer par un fondu au noir complet et solide : pas de flash bleu, gris ou vert, et le personnage ne bouge pas pendant la transition.

#### Scenario: Même niveau
- **WHEN** la savestate est dans le niveau courant
- **THEN** l'état est restauré sans écran de chargement

#### Scenario: Autre niveau
- **WHEN** la savestate est dans un autre niveau
- **THEN** l'écran passe au noir complet, sans flash de couleur, puis l'état apparaît

### Requirement: Temps de chargement stables
Les temps de chargement MUST ne pas s'allonger au fil des rechargements.

#### Scenario: Rechargements répétés
- **WHEN** le joueur recharge la même savestate de nombreuses fois
- **THEN** le temps de chargement reste constant

### Requirement: Full reload
Une case **Full reload** dans la fenêtre des savestates MUST forcer un rechargement complet.

#### Scenario: Case cochée
- **WHEN** Full reload est cochée et le joueur charge une savestate de la même salle
- **THEN** un rechargement complet est effectué

### Requirement: Chargement pendant une cinématique
Le chargement MUST être possible pendant une cinématique ou une absorption : les états de cinématique sont retirés, la fenêtre d'arcane est fermée et un chargement complet est forcé.

#### Scenario: Pendant une absorption
- **WHEN** le joueur charge une savestate pendant une absorption d'arcane
- **THEN** la fenêtre d'arcane est fermée et l'état capturé est restauré sans cinématique résiduelle

### Requirement: Collection paginée
La fenêtre du haut MUST présenter les savestates par pages de 10 emplacements, avec l'indicateur « Page 1/X » à gauche et les boutons **Add page** et suppression de page à droite.

#### Scenario: Ajout de page
- **WHEN** le joueur clique sur Add page
- **THEN** une page de 10 emplacements vides est ajoutée et l'indicateur se met à jour

### Requirement: Emplacements nommables
Chaque emplacement MUST pouvoir être renommé (crayon) et supprimé (poubelle). Un double-clic sur le nom charge l'emplacement. Écraser un emplacement occupé MUST demander confirmation. Le renommage gère la répétition des touches maintenues (Retour arrière), Ctrl+A, les flèches, la sélection visible et la sélection à la souris.

#### Scenario: Écrasement
- **WHEN** le joueur sauve dans un emplacement occupé
- **THEN** une confirmation est demandée avant d'écraser

#### Scenario: Chargement par double-clic
- **WHEN** le joueur double-clique sur le nom d'un emplacement
- **THEN** la savestate de cet emplacement est chargée

### Requirement: Quickslot
Un emplacement rapide (quickslot) MUST exister en mémoire uniquement. Il MUST être vidé, ainsi que le point de réapparition, en quittant le jeu ou en revenant au menu principal.

#### Scenario: Retour au menu
- **WHEN** le joueur revient au menu principal
- **THEN** le quickslot et le point de réapparition sont vides

### Requirement: Raccourcis de savestates
Le mod MUST proposer des raccourcis pour : sauver / charger le quickslot, sauver dans un emplacement, charger un emplacement, emplacement → quickslot, quickslot → emplacement, page précédente / suivante. Ils MUST être affichés comme des touches de clavier (keycaps), pas dans des boutons.

#### Scenario: Sauvegarde rapide
- **WHEN** le joueur appuie sur le raccourci de sauvegarde du quickslot
- **THEN** l'état courant est capturé dans le quickslot

### Requirement: Stockage
Les savestates nommées MUST être stockées dans `<jeu>\BelmontPractice\Savestates`.

#### Scenario: Persistance
- **WHEN** le joueur relance le jeu
- **THEN** les savestates nommées sont toujours présentes

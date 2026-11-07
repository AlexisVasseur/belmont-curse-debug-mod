# Belmont Practice

## Purpose
Mod d'entraînement (debug mod) pour **Castlevania: Belmont's Curse** (Steam, Windows, jeu en 1.1.3) :
savestates, remise en vie des boss, téléportation, triches et accélération des cinématiques.
Inspiration : le DebugMod de Silksong (savestates en haut au centre, panneau latéral à droite).

## Tech Stack
- BepInEx 6.0.0-be.793 Unity IL2CPP (builds 791 à 793 testées), Il2CppInterop 1.5.3
- C# / net6.0, interop générées par BepInEx dans `<jeu>\BepInEx\interop`
- Unity 2022.3 (moteur du jeu)

## Conventions
- Les specs sont en français. Le mot-clé normatif reste **MUST** (exigé par le validateur OpenSpec).
- Incrémenter `Plugin.Version` à chaque changement ; un commit par version, message en français.
- Interface du mod en anglais.
- Toute évolution passe par `openspec/changes/<nom>/` (proposal, tasks, deltas de specs), puis est archivée.

## Capacités
| Spec | Contenu |
|---|---|
| `interface` | règles transverses de l'UI (disponibilité, F1, style, clics) |
| `save-backup` | protection des sauvegardes du jeu |
| `savestates` | capture, chargement, collection, quickslot |
| `cheats` | onglet General (utilitaires, santé, upgrades, niveau, respawn) |
| `skills` | onglet Skills (déplacements, arcanes) |
| `items` | onglet Items (armes, reliques, équipements) |
| `bosses` | onglet Bosses (Alive/Defeated, TP, Fight) |
| `keybinds` | onglet Keybinds et configuration |
| `teleport` | téléportation miroirs et carte |
| `cutscenes` | accélération et passage des cinématiques |
| `overlays` | affichages permanents (version, salle, notifications) |
| `distribution` | paquet, prérequis, dépôt |

## Backlog
- **Hot reload** : décidé, pas commencé (voir `docs/notes/hot-reload.md`) ; à ouvrir en change `add-hot-reload`.
- Boss à confirmer en jeu : Isaac II, Death II, boss de fin.
- Pistes abandonnées : transition « chauves-souris » du jeu ; suppression totale de l'écran de chargement entre niveaux.
- Question ouverte (03/11/2026) : pertes de son dans le casque, lien avec le mod non établi.

## Historique
Specs reconstituées le 07/11/2026 à partir de l'historique de conception (23/10 → 03/11/2026, versions 0.1 → 0.65)
et du code ; il n'y avait pas de spec écrite auparavant.

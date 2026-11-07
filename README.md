# Belmont Practice

Mod d'entraînement pour **Castlevania: Belmont's Curse** (Steam, Windows), jeu en version **1.1.3**.

## Installation

1. **Sauvegarde** `%USERPROFILE%\AppData\LocalLow\KONAMI\Castlevania Belmont's Curse` : ce dossier est partagé par toutes les installations du jeu.
2. **Télécharge le jeu en 1.1.3** :
   - ouvre `steam://open/console` ;
   - dans la console Steam, lance :
     ```
     download_depot 4231820 4231821 8796979530797619124
     ```
   - attends « Depot download complete », puis déplace `Steam\steamapps\content\app_4231820\depot_4231821` où tu veux : ce sera ton dossier du jeu.
3. **Crée `steam_appid.txt`** à côté de l'exe, avec pour seul contenu `4231820`.
4. **Installe BepInEx** : décompresse dans le dossier du jeu
   [BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.791](https://builds.bepinex.dev/projects/bepinex_be/791/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.791%2Bb782da4.zip).
   Lance l'exe une fois jusqu'au menu principal, puis quitte.
5. **Installe le mod** : décompresse le zip de la [dernière release](../../releases/latest) dans le dossier du jeu, en fusionnant les dossiers.

Pour jouer, lance `Castlevania Belmont's Curse.exe` avec Steam ouvert. En jeu, **F1** ouvre le mod.

| Emplacement | Contenu |
|---|---|
| `<jeu>\BelmontPractice\Savestates` | savestates |
| `<jeu>\BepInEx\config\fr.vasseur.belmontpractice.cfg` | réglages et raccourcis |
| `<jeu>\BepInEx\LogOutput.log` | journal (à envoyer en cas de bug) |

## Développement

Prérequis : [.NET SDK 6+](https://dotnet.microsoft.com/download) et un dossier du jeu installé comme ci-dessus (lancé une fois, pour générer `BepInEx\interop`).

```powershell
Copy-Item local.props.example local.props   # indique ton dossier du jeu (GameDir)
dotnet build -c Release                     # compile et copie le mod dans le jeu (jeu fermé)
.\package.ps1                               # crée dist\BelmontPractice-<version>.zip
```

Conventions :
- incrémenter `Plugin.Version` à chaque changement ;
- interface en anglais ;
- jamais toucher aux saves avec le jeu ouvert.

Documentation :
- spécification : [docs/SPEC.md](docs/SPEC.md) ;
- notes techniques : [docs/notes](docs/notes).

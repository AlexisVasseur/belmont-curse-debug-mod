# Belmont Practice

Mod d'entraînement (« debug mod ») pour **Castlevania: Belmont's Curse** (Steam, Windows), inspiré du DebugMod de Silksong.
Il tourne sur **BepInEx 6.0.0-be.791 (Unity IL2CPP)**.

Fonctionnalités principales : savestates sans écran de chargement (pages de 10, renommables, raccourcis),
retour sur n'importe quel boss (bouton **Fight**), téléportation depuis la carte ou vers un miroir,
accélération des cinématiques, PV / mana / potions infinis, lock HP, noclip, déblocage des armes, reliques,
arcanes et améliorations, raccourcis configurables.

La spécification complète se trouve dans [docs/SPEC.md](docs/SPEC.md).

---

## Juste jouer avec le mod

Pas besoin de compiler : récupère le zip `BelmontPractice-<version>.zip` dans les
[Releases](../../releases) du dépôt, puis suis le fichier `LISEZMOI_BelmontPractice.txt` qu'il contient.
En résumé :

1. **Sauvegarde tes parties** : `%USERPROFILE%\AppData\LocalLow\KONAMI\Castlevania Belmont's Curse`.
2. Installe **BepInEx 6.0.0-be.791, variante Unity IL2CPP, win-x64** depuis
   <https://builds.bepinex.dev/projects/bepinex_be> (build #791). Une autre version risque de ne pas charger le mod.
3. Lance le jeu une fois jusqu'au menu principal (BepInEx génère ses fichiers), puis quitte-le.
4. Décompresse le zip du mod dans le dossier du jeu.
5. En jeu : **F1** ouvre les fenêtres du mod.

---

## Développer

### Prérequis

- Windows, le jeu installé (Steam) ;
- BepInEx 6.0.0-be.791 IL2CPP installé dans le dossier du jeu, **et le jeu lancé une fois** :
  c'est ce premier lancement qui génère `BepInEx\interop\*.dll`, nécessaires à la compilation ;
- [.NET SDK](https://dotnet.microsoft.com/download) 6 ou plus récent (le mod cible `net6.0`).

Les DLL du jeu et de BepInEx ne sont **pas** dans le dépôt : chacun compile avec celles de sa propre installation.

### Indiquer le dossier du jeu

Par défaut : `C:\Games\Castlevania Belmont's Curse (Modded)`. Pour un autre emplacement :

```powershell
Copy-Item local.props.example local.props   # puis modifie GameDir dans local.props (fichier non versionné)
```

Autre solution, pour une seule compilation : `dotnet build -p:GameDir="D:\SteamLibrary\steamapps\common\Castlevania Belmont's Curse"`.

> Conseil : mods sur une **copie** du jeu, à côté de l'installation Steam, pour garder un jeu propre.

### Compiler et installer

```powershell
dotnet build -c Release
```

La compilation copie automatiquement `BelmontPractice.dll` et `icons\` dans
`<jeu>\BepInEx\plugins\BelmontPractice\`. **Le jeu doit être fermé** (il verrouille la DLL).
Pour compiler sans copier : `-p:SkipGameCopy=true`.

### Fabriquer le zip pour les joueurs

```powershell
.\package.ps1                  # dist\BelmontPractice-<version>.zip
.\package.ps1 -SansDonneesBoss # sans scenarios_boss.json / arenes.json
```

Le zip contient la DLL, les icônes, le LISEZMOI et, par défaut, les données des boss déjà apprises
(`<jeu>\BelmontPractice\scenarios_boss.json`, `arenes.json`), pour que les joueurs n'aient pas à refaire cette découverte.
Publie-le ensuite dans une Release GitHub.

### Conventions

- Incrémente `Plugin.Version` (Plugin.cs) à chaque changement ; le message de commit se termine par ` - <version>`.
- Textes de l'interface **en anglais** (seuls les noms de savestates donnés par l'utilisateur sont libres) ;
  journaux, commentaires et commits en français.
- **Sécurité des sauvegardes** : copie de sauvegarde avant toute manipulation de save, et seulement jeu fermé.
- Pas de Harmony : le mod injecte un seul `MonoBehaviour` (`PracticeBehaviour`) et lit / écrit l'état du jeu via Il2CppInterop.

### Outil `tools/inspect`

Petit utilitaire pour explorer les types IL2CPP du jeu (les assemblies interop) :

```powershell
dotnet run --project tools\inspect -- Assembly-CSharp "Regex" m   # m : membres des types dont le nom correspond
dotnet run --project tools\inspect -- Assembly-CSharp "Health"    # propriétés dont le nom correspond
```

Dossier du jeu : variable d'environnement `BELMONT_GAMEDIR`, sinon le chemin par défaut.

---

## Structure

| Chemin | Contenu |
|---|---|
| `Plugin.cs` | point d'entrée BepInEx, version, configuration |
| `PracticeBehaviour.cs` | boucle `Update` unique du mod |
| `SaveStates*.cs`, `SlotStore.cs`, `LoadSequence.cs` | savestates, emplacements, chargement rapide |
| `Boss*.cs`, `Arenas.cs` | retour sur les boss, bouton Fight, arènes |
| `MapTeleport*.cs` | téléportation depuis la carte et vers les miroirs |
| `FastForward.cs`, `CutsceneHint.cs` | accélération / passage des cinématiques |
| `Cheats.cs`, `PlayerStats.cs`, `Upgrades.cs`, `Weapons.cs`, `Relics.cs`, `Arcana.cs`, `Abilities.cs` | onglets General / Skills / Items |
| `SidePanelView*.cs`, `Ui.cs`, `Skin.cs`, `CardFrame.cs`, … | interface (uGUI + TextMeshPro, assets du jeu) |
| `Keybinds.cs` | raccourcis configurables |
| `icons/` | images de l'interface (extraites du jeu, propriété de Konami, d'où le dépôt **privé**) |
| `docs/` | spécification, notes techniques, maquettes |
| `tools/inspect/` | outil d'exploration des types du jeu |

Fichiers créés en jeu (hors dépôt) : `<jeu>\BelmontPractice\` (savestates, sauvegardes automatiques, données des boss)
et `<jeu>\BepInEx\config\fr.vasseur.belmontpractice.cfg` (réglages et raccourcis).

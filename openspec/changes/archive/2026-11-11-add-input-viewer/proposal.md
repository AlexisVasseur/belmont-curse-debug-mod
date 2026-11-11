## Why
Pour s'entraîner (sauts précis, combos, enregistrements), il faut voir les entrées réellement reçues par le jeu, au clavier comme à la manette.

## What Changes
- Nouvel affichage permanent **Input viewer**, petit, en bas à droite de l'écran, au-dessus du nom de la salle.
- Nouvelle option On / Off « Input viewer » dans l'onglet Keybinds (désactivée par défaut, config `Interface/AfficheurTouches`).

## Impact
- Specs : `overlays` (exigence ajoutée).
- Code : `InputViewer.cs`, `Plugin.cs`, `PracticeBehaviour.cs`, `SidePanelView.Keybinds.cs`.

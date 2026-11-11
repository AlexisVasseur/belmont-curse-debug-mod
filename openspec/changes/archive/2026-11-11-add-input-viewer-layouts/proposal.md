## Why
L'input viewer n'a qu'une disposition « clavier » (cases rectangulaires). À la manette, on veut voir une vraie manette : boutons ronds, disposition Xbox.

## What Changes
- Deux dispositions : **Keyboard / mouse** (l'actuelle) et **Gamepad** (manette façon Xbox : LT / RT avec remplissage, LB / RB, deux sticks, croix, A B X Y ronds et colorés, View / Menu).
- Nouvelle ligne « Viewer layout » dans l'onglet Keybinds : **Auto** (par défaut, suit le dernier périphérique utilisé), Keyboard / mouse ou Gamepad. Config `Interface/AfficheurTouchesDisposition`.

## Impact
- Specs : `overlays` (exigence Input viewer modifiée).
- Code : `InputViewer.cs`, `Plugin.cs`, `SidePanelView.Keybinds.cs`.

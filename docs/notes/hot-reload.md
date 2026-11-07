# Hot reload — conception retenue (non commencé)


User asked on 2026-10-28 to keep "hot reload" on the to-do list, to do later (not started).

Agreed design:
- Tiny fixed BepInEx loader plugin owning the only injected MonoBehaviour; it forwards Update to the module.
- Module (current BelmontPractice code) loaded from bytes in a collectible AssemblyLoadContext, so the DLL is not locked: new build applies in game within seconds (file watch or a reload key).
- Mod has a single injected type (PracticeBehaviour) and a single IL2CPP delegate hook (Keyboard.onTextInput in SaveStatesView), so the split is easy.
- Stop() must undo: fast-forward timeScale, KeyboardBlock/MouseBlock overrides, BossRespawn forced conditions (Revert), UI root, onTextInput hook.
- Limits told to the user: old versions stay in memory (Il2CppInterop prevents real unload); in-progress state lost (e.g. armed Fight); changing the loader still needs a restart.

**Why:** avoid waiting for the game to close to install each build (DLL locked by the game).
**How to apply:** when the user brings it up again ("on verra plus tard"), resume from this design; related: [boss-respawn.md](boss-respawn.md).

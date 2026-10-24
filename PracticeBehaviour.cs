using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BelmontPractice;

/// <summary>Raccourcis clavier, panneau des savestates et déroulé des chargements.</summary>
public class PracticeBehaviour : MonoBehaviour
{
    public PracticeBehaviour(IntPtr ptr) : base(ptr) { }

    readonly LoadSequence sequence = new();
    readonly SaveStatesPanel panel = new();
    SaveStatesView view;
    SidePanelView side;

    void Start()
    {
        view = new SaveStatesView(panel);
        side = new SidePanelView { Notify = Notify };
        sequence.Notify = Notify;
        panel.Notify = Notify;
        panel.Busy = () => sequence.Active;
        panel.CaptureState = Capture;
        panel.LoadState = Load;
        panel.Init();
    }

    void Update()
    {
        try { DevExport.Update(); } catch (Exception ex) { Plugin.Logger.LogError("[Export] " + ex); }
        try { SaveGuard.Update(); } catch (Exception ex) { Plugin.Logger.LogError("[Protection] " + ex); }
        try { GameBridge.UpdateRoomReload(); }
        catch (Exception ex) { Plugin.Logger.LogError("[Salle] " + ex); }
        try { sequence.Update(); }
        catch (Exception ex) { Plugin.Logger.LogError("[Chargement] " + ex); }

        // Le mod n'est utilisable qu'en jeu (personnage chargé, hors chargement) :
        // l'ouvrir au menu principal ou pendant un chargement pouvait faire planter le jeu.
        bool inGame = sequence.Active || IsInGame();
        VersionLabel.Update(inGame);
        if (!inGame)
        {
            panel.Visible = false;
            panel.CancelSelectState();
            panel.CancelRename();
        }

        // Charge en arrière-plan tous les objets du jeu (armes et sorts pas encore rencontrés)
        if (inGame && !sequence.Active)
        {
            try { Cheats.Update(); Cheats.ApplyNoclip(); } catch (Exception ex) { Plugin.Logger.LogError("[Utilitaires] " + ex); }
        }
        if (inGame) { try { ItemLibrary.Update(); } catch (Exception ex) { Plugin.Logger.LogError("[Objets] " + ex); } }

        var kb = Keyboard.current;
        if (kb != null && inGame)
        {
            try { HandleKeys(kb); }
            catch (Exception ex) { Plugin.Logger.LogError("[Raccourcis] " + ex); }
        }

        try { view.Update(); }
        catch (Exception ex) { Plugin.Logger.LogError("[Interface] " + ex); }
        // F1 ouvre tout ; le choix d'un emplacement (Pavé 9/3/2) n'ouvre que la fenêtre des savestates.
        try { side.Update(panel.Visible); }
        catch (Exception ex) { Plugin.Logger.LogError("[Panneau latéral] " + ex); }

        // Clics et molette sur les fenêtres du mod : rien n'est transmis au jeu
        MouseBlock.Update(inGame && panel.ShouldBeVisible && MouseBlock.PointerOverModUi());

        if (panel.ShouldBeVisible)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    static bool IsInGame()
    {
        try
        {
            if (MotherBase.ApplicationFlowManager.ApplicationFlowManager.IsLoading) return false;
            return GameBridge.InGame(out _);
        }
        catch { return false; }
    }

    void HandleKeys(Keyboard kb)
    {
        if (Keybinds.Capturing != null) return; // nouvelle touche en cours de saisie dans l'onglet Keybinds
        if (panel.HandleKeyboard(kb)) return; // saisie d'un nom ou choix d'emplacement

        if (Keybinds.TogglePanel.Pressed(kb))
        {
            if (panel.InSelectState) panel.CancelSelectState();
            else panel.Visible = !panel.Visible;
        }
        if (Keybinds.NextPage.Pressed(kb)) panel.NextPage();
        if (Keybinds.PrevPage.Pressed(kb)) panel.PrevPage();
        if (Keybinds.RefreshFiles.Pressed(kb)) panel.Refresh();

        // Utilitaires
        void Cheat(Bind bind, string name, ref bool flag)
        {
            if (!bind.Pressed(kb)) return;
            flag = !flag;
            Notify($"{name}: {(flag ? "enabled" : "disabled")}");
        }
        Cheat(Keybinds.ToggleInfiniteHealth, "Infinite HP", ref Cheats.InfiniteHealth);
        Cheat(Keybinds.ToggleInfiniteMana, "Infinite mana", ref Cheats.InfiniteMana);
        Cheat(Keybinds.ToggleInfinitePotions, "Infinite potions", ref Cheats.InfinitePotions);
        Cheat(Keybinds.ToggleInfiniteJump, "Infinite jump", ref Cheats.InfiniteJump);
        Cheat(Keybinds.ToggleNoclip, "Noclip", ref Cheats.Noclip);

        if (sequence.Active) return; // pas de sauvegarde / chargement pendant un chargement
        if (Keybinds.QuickSave.Pressed(kb)) panel.QuickSave();
        else if (Keybinds.QuickLoad.Pressed(kb)) panel.QuickLoad();
        else if (Keybinds.SaveToFile.Pressed(kb)) panel.EnterSelectState(SelectOperation.SaveToFile);
        else if (Keybinds.LoadFromFile.Pressed(kb)) panel.EnterSelectState(SelectOperation.LoadFromFile);
        else if (Keybinds.FileToQuickslot.Pressed(kb)) panel.EnterSelectState(SelectOperation.FileToQuickslot);
        else if (Keybinds.QuickslotToFile.Pressed(kb)) panel.EnterSelectState(SelectOperation.QuickslotToFile);
    }

    SaveState Capture()
    {
        try
        {
            if (!GameBridge.InGame(out string why)) { Notify(why, true); return null; }
            return GameBridge.Capture(null);
        }
        catch (Exception ex) { Notify("Save failed: " + ex.Message, true); Plugin.Logger.LogError(ex); return null; }
    }

    void Load(SaveState state)
    {
        if (state == null || sequence.Active) return;
        if (!GameBridge.InGame(out string why)) { Notify(why, true); return; }
        sequence.Start(state);
    }

    void Notify(string msg, bool error = false)
    {
        if (error) Plugin.Logger.LogError(msg); else Plugin.Logger.LogInfo(msg);
        try { view?.ShowToast(msg, error); }
        catch (Exception ex) { Plugin.Logger.LogError("[Interface] " + ex.Message); }
    }
}

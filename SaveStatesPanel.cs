using System;
using UnityEngine.InputSystem;

namespace BelmontPractice;

/// <summary>Opération en attente du choix d'un emplacement (0-9), comme DebugMod.</summary>
public enum SelectOperation { None, QuickslotToFile, FileToQuickslot, SaveToFile, LoadFromFile }

public enum ConfirmKind { None, Overwrite, DeleteState, RemovePage }

/// <summary>
/// Logique du panneau des savestates (reprend le SaveStatesPanel de DebugMod) :
/// quickslot, pages de 10 emplacements, Read / Write, renommage et suppression (crayon / poubelle), choix d'emplacement au clavier.
/// L'affichage est fait par SaveStatesView.
/// </summary>
public class SaveStatesPanel
{
    // Actions fournies par le MonoBehaviour
    public Func<SaveState> CaptureState;
    public Action<SaveState> LoadState;
    public Action<string, bool> Notify;
    public Func<bool> Busy; // un chargement est en cours

    public SaveState Quickslot { get; private set; }
    public bool Visible;

    public int CurrentPage { get; private set; }
    public SelectOperation SelectOp { get; private set; } = SelectOperation.None;

    public int RenameIndex { get; private set; } = -1;
    public string RenameText = "";

    public ConfirmKind Confirm { get; private set; } = ConfirmKind.None;
    public int ConfirmIndex { get; private set; } = -1;
    Action confirmAction;

    public bool InSelectState => SelectOp != SelectOperation.None;
    public bool ShouldBeVisible => Visible || InSelectState;
    public bool IsTyping => RenameIndex >= 0;

    public void Init()
    {
        SlotStore.LoadAll();
        Quickslot = SlotStore.LoadQuickslot();
    }

    void SetQuickslot(SaveState s)
    {
        if (s == null) return;
        Quickslot = s.Clone();
        SlotStore.SaveQuickslot(Quickslot);
    }

    // ---------- Quickslot ----------

    public void QuickSave()
    {
        if (Busy()) { Notify("Can't save while loading", true); return; }
        var s = CaptureState();
        if (s == null) return;
        SetQuickslot(s);
        Notify($"Saved to quickslot: {s.Name}", false);
    }

    public void QuickLoad()
    {
        if (Quickslot == null) { Notify("Quickslot is empty", true); return; }
        LoadState(Quickslot);
    }

    // ---------- Choix d'un emplacement ----------

    public void EnterSelectState(SelectOperation op)
    {
        CancelConfirm();
        RenameIndex = -1;
        SelectOp = SelectOp == op ? SelectOperation.None : op;
    }

    public void CancelSelectState(bool leavePanelOpen = false)
    {
        if (InSelectState && leavePanelOpen) Visible = true;
        SelectOp = SelectOperation.None;
    }

    void DoSelectOperation(int index)
    {
        var op = SelectOp;
        CancelSelectState();
        switch (op)
        {
            case SelectOperation.QuickslotToFile:
                if (Quickslot == null) { Notify("Quickslot is empty", true); return; }
                SlotStore.Set(CurrentPage, index, Quickslot);
                Notify($"Quickslot copied to slot {index}", false);
                break;
            case SelectOperation.FileToQuickslot:
                var f = SlotStore.Get(CurrentPage, index);
                if (f == null) { Notify($"Slot {index} is empty", true); return; }
                SetQuickslot(f);
                Notify($"Slot {index} copied to quickslot: {f.Name}", false);
                break;
            case SelectOperation.SaveToFile:
                if (Busy()) { Notify("Can't save while loading", true); return; }
                var s = CaptureState();
                if (s == null) return;
                SlotStore.Set(CurrentPage, index, s);
                Notify($"Saved to slot {index}: {s.Name}", false);
                break;
            case SelectOperation.LoadFromFile:
                var l = SlotStore.Get(CurrentPage, index);
                if (l == null) { Notify($"Slot {index} is empty", true); return; }
                LoadState(l);
                break;
        }
    }

    // ---------- Boutons des emplacements ----------

    public bool IsReadOperation => SelectOp is SelectOperation.FileToQuickslot or SelectOperation.LoadFromFile;
    public bool IsWriteOperation => SelectOp is SelectOperation.QuickslotToFile or SelectOperation.SaveToFile;

    public void ClickRead(int index)
    {
        if (IsReadOperation) { DoSelectOperation(index); return; }
        if (InSelectState) { CancelSelectState(true); return; }
        var s = SlotStore.Get(CurrentPage, index);
        if (s == null) return;
        SetQuickslot(s);
        Notify($"Slot {index} copied to quickslot: {s.Name}", false);
    }

    public void ClickWrite(int index)
    {
        if (InSelectState && !IsWriteOperation) { CancelSelectState(true); return; }
        if (SelectOp != SelectOperation.SaveToFile && Quickslot == null) { Notify("Save a state to the quickslot first", true); return; }
        Action action = InSelectState
            ? () => DoSelectOperation(index)
            : () => { SlotStore.Set(CurrentPage, index, Quickslot); Notify($"Quickslot copied to slot {index}", false); };
        if (SlotStore.Get(CurrentPage, index) != null) AskConfirm(ConfirmKind.Overwrite, index, action);
        else action();
    }

    /// <summary>Double-clic sur le nom : charge directement l'emplacement.</summary>
    public void LoadSlot(int index)
    {
        var s = SlotStore.Get(CurrentPage, index);
        if (s == null || Busy()) return;
        CancelSelectState(true);
        LoadState(s);
    }

    // ---------- Renommer / supprimer (crayon / poubelle) ----------

    public void StartRename(int index)
    {
        var s = SlotStore.Get(CurrentPage, index);
        if (s == null) return;
        CancelSelectState(true);
        CancelConfirm();
        RenameIndex = index;
        RenameText = s.Name;
    }

    public void CommitRename()
    {
        if (RenameIndex < 0) return;
        if (SlotStore.Rename(CurrentPage, RenameIndex, RenameText)) Notify($"Renamed: {RenameText.Trim()}", false);
        else Notify("Invalid name", true);
        RenameIndex = -1;
    }

    public void CancelRename() => RenameIndex = -1;

    public void ClickDelete(int index)
    {
        if (SlotStore.Get(CurrentPage, index) == null) return;
        AskConfirm(ConfirmKind.DeleteState, index, () => { SlotStore.Delete(CurrentPage, index); Notify($"Slot {index} deleted", false); });
    }

    public void AddPage() { SlotStore.AddPage(CurrentPage + 1); CurrentPage++; }

    public void RemovePage()
    {
        int p = CurrentPage;
        Action remove = () => { SlotStore.RemovePage(p); CurrentPage = Math.Min(CurrentPage, SlotStore.PageCount - 1); };
        if (SlotStore.PageHasStates(p)) AskConfirm(ConfirmKind.RemovePage, -1, remove);
        else remove();
    }

    // ---------- Pages ----------

    public void NextPage() { if (ShouldBeVisible) CurrentPage = Wrap(CurrentPage + 1); }
    public void PrevPage() { if (ShouldBeVisible) CurrentPage = Wrap(CurrentPage - 1); }
    int Wrap(int p) => ((p % SlotStore.PageCount) + SlotStore.PageCount) % SlotStore.PageCount;

    public void Refresh()
    {
        SlotStore.LoadAll();
        CurrentPage = Math.Min(CurrentPage, SlotStore.PageCount - 1);
        Notify("Savestates reloaded from disk", false);
    }

    // ---------- Confirmations ----------

    void AskConfirm(ConfirmKind kind, int index, Action action)
    {
        Confirm = kind;
        ConfirmIndex = index;
        confirmAction = action;
    }

    public void ConfirmYes() { var a = confirmAction; CancelConfirm(); a?.Invoke(); }
    public void ConfirmNo() { CancelConfirm(); CancelSelectState(true); }
    void CancelConfirm() { Confirm = ConfirmKind.None; confirmAction = null; ConfirmIndex = -1; }

    // ---------- Clavier (appelé depuis Update) ----------

    /// <summary>Retourne true si la frame a été consommée (choix d'emplacement / saisie d'un nom).</summary>
    public bool HandleKeyboard(Keyboard kb)
    {
        if (IsTyping)
        {
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) CommitRename();
            else if (kb.escapeKey.wasPressedThisFrame) CancelRename();
            else if (BackspaceRepeat(kb) && RenameText.Length > 0) RenameText = RenameText.Substring(0, RenameText.Length - 1);
            return true; // aucun raccourci pendant la saisie
        }
        if (InSelectState)
        {
            int digit = Keybinds.DigitPressed(kb, out _);
            if (digit >= 0) { DoSelectOperation(digit); return true; }
            if (kb.escapeKey.wasPressedThisFrame) { CancelSelectState(); return true; }
        }
        return false;
    }

    // Répétition de Retour arrière maintenu, comme dans un champ texte (les caractères, eux, sont déjà répétés par onTextInput)
    const float RepeatDelay = 0.45f, RepeatRate = 0.035f;
    float nextBackspace;

    bool BackspaceRepeat(Keyboard kb)
    {
        var k = kb.backspaceKey;
        if (k.wasPressedThisFrame) { nextBackspace = UnityEngine.Time.unscaledTime + RepeatDelay; return true; }
        if (!k.isPressed || UnityEngine.Time.unscaledTime < nextBackspace) return false;
        nextBackspace = UnityEngine.Time.unscaledTime + RepeatRate;
        return true;
    }

    /// <summary>Caractère tapé (Keyboard.onTextInput) pendant un renommage.</summary>
    public void OnTextInput(char c)
    {
        if (!IsTyping || char.IsControl(c) || RenameText.Length >= 60) return;
        RenameText += c;
    }
}

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
    /// <summary>Position du curseur de texte dans RenameText (0 = avant le premier caractère).</summary>
    public int RenameCursor { get; private set; }
    /// <summary>Autre bout de la sélection (égal au curseur : pas de sélection). Ctrl+A, Maj+flèches, glisser à la souris.</summary>
    public int RenameAnchor { get; private set; }
    public bool HasSelection => RenameAnchor != RenameCursor;
    public int SelectionStart => Math.Min(RenameAnchor, RenameCursor);
    public int SelectionEnd => Math.Max(RenameAnchor, RenameCursor);

    /// <summary>Place le curseur (souris, flèches) ; extend : garde l'ancre pour étendre la sélection.</summary>
    public void SetCaret(int index, bool extend)
    {
        RenameCursor = Math.Clamp(index, 0, RenameText.Length);
        if (!extend) RenameAnchor = RenameCursor;
    }

    public void SelectAll() { RenameAnchor = 0; RenameCursor = RenameText.Length; }

    /// <summary>Retire le texte sélectionné (curseur à sa place). Retourne false s'il n'y avait pas de sélection.</summary>
    bool DeleteSelection()
    {
        if (!HasSelection) return false;
        int s = SelectionStart;
        RenameText = RenameText.Remove(s, SelectionEnd - s);
        RenameCursor = RenameAnchor = s;
        return true;
    }

    public ConfirmKind Confirm { get; private set; } = ConfirmKind.None;
    public int ConfirmIndex { get; private set; } = -1;
    Action confirmAction;

    public bool InSelectState => SelectOp != SelectOperation.None;
    public bool ShouldBeVisible => Visible || InSelectState;
    public bool IsTyping => RenameIndex >= 0;

    public void Init()
    {
        SlotStore.LoadAll();
        SlotStore.DeleteOldQuickslot();
    }

    void SetQuickslot(SaveState s)
    {
        if (s == null) return;
        Quickslot = s.Clone();
    }

    // ---------- Quickslot ----------

    /// <summary>Retour au menu principal : le quickslot appartenait à la partie quittée.</summary>
    public void ClearQuickslot() => Quickslot = null;

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
        RenameCursor = RenameAnchor = RenameText.Length;
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
            bool ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
            bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            RenameCursor = Math.Clamp(RenameCursor, 0, RenameText.Length);
            RenameAnchor = Math.Clamp(RenameAnchor, 0, RenameText.Length);
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) CommitRename();
            else if (kb.escapeKey.wasPressedThisFrame) CancelRename();
            else if (ctrl && LetterA(kb).wasPressedThisFrame) SelectAll();
            else if (Repeat(kb.leftArrowKey, ref nextLeft))
            {
                // Sans Maj, une sélection se referme sur son début ; avec Maj, elle s'étend
                if (!shift && HasSelection) SetCaret(SelectionStart, false);
                else SetCaret(RenameCursor - 1, shift);
            }
            else if (Repeat(kb.rightArrowKey, ref nextRight))
            {
                if (!shift && HasSelection) SetCaret(SelectionEnd, false);
                else SetCaret(RenameCursor + 1, shift);
            }
            else if (kb.homeKey.wasPressedThisFrame) SetCaret(0, shift);
            else if (kb.endKey.wasPressedThisFrame) SetCaret(RenameText.Length, shift);
            else if (Repeat(kb.backspaceKey, ref nextBackspace))
            {
                if (!DeleteSelection() && RenameCursor > 0) { RenameText = RenameText.Remove(RenameCursor - 1, 1); SetCaret(RenameCursor - 1, false); }
            }
            else if (Repeat(kb.deleteKey, ref nextDelete))
            {
                if (!DeleteSelection() && RenameCursor < RenameText.Length) RenameText = RenameText.Remove(RenameCursor, 1);
            }
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

    // Répétition des touches maintenues (flèches, Retour arrière, Suppr), comme dans un champ texte
    // (les caractères, eux, sont déjà répétés par onTextInput)
    const float RepeatDelay = 0.45f, RepeatRate = 0.035f;
    float nextBackspace, nextDelete, nextLeft, nextRight;

    static bool Repeat(UnityEngine.InputSystem.Controls.KeyControl k, ref float next)
    {
        if (k.wasPressedThisFrame) { next = UnityEngine.Time.unscaledTime + RepeatDelay; return true; }
        if (!k.isPressed || UnityEngine.Time.unscaledTime < next) return false;
        next = UnityEngine.Time.unscaledTime + RepeatRate;
        return true;
    }

    static UnityEngine.InputSystem.Controls.KeyControl letterA;
    static string letterALayout;

    /// <summary>
    /// Touche qui porte la lettre A dans la disposition actuelle du clavier. Les touches de l'Input System sont nommées
    /// d'après leur place sur un clavier américain : en AZERTY, aKey est la touche marquée Q, et Ctrl+A ne marchait pas.
    /// </summary>
    static UnityEngine.InputSystem.Controls.KeyControl LetterA(Keyboard kb)
    {
        try
        {
            if (letterA == null || letterALayout != kb.keyboardLayout)
            {
                letterALayout = kb.keyboardLayout;
                letterA = kb.FindKeyOnCurrentKeyboardLayout("a") ?? kb.aKey;
            }
            return letterA;
        }
        catch { return kb.aKey; }
    }

    /// <summary>Caractère tapé (Keyboard.onTextInput) pendant un renommage : inséré au curseur, à la place de la sélection.</summary>
    public void OnTextInput(char c)
    {
        if (!IsTyping) return;
        if (c == '\u0001') { SelectAll(); return; } // Ctrl+A envoyé comme caractère de contrôle, quelle que soit la disposition
        if (char.IsControl(c)) return;
        DeleteSelection();
        if (RenameText.Length >= 60) return;
        RenameCursor = Math.Clamp(RenameCursor, 0, RenameText.Length);
        RenameText = RenameText.Insert(RenameCursor, c.ToString());
        SetCaret(RenameCursor + 1, false);
    }
}

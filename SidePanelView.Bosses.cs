using System;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

namespace BelmontPractice;

/// <summary>
/// Onglet Bosses : un boss par ligne. Fight : le boss revient et on est téléporté devant son arène (combat remis
/// à zéro). TP : devant l'arène. Alive / Defeated : état du boss (l'état actuel en rouge).
/// </summary>
public partial class SidePanelView
{
    class BossRowUi
    {
        public Bosses.Boss boss;
        public string key; // rencontre (arène) : null tant qu'aucune arène n'est connue
        public string label; // « Isaac II »
        public Image highlight;
        public TextMeshProUGUI name;
        public UiButton fight, tp, alive, defeated;
        public float y;
    }
    readonly List<BossRowUi> bossRows = new();
    readonly List<(UiButton btn, float x, float y, float w, float h)> bossButtons = new();
    const float BossRowH = 46f, BossIcon = 36f, BossFightW = 56f, BossTpW = 40f, BossStateW = 74f, BossBtnH = 26f, BossGap = 4f;
    int bossesBuiltFor = -1;
    CheatUi bossHpRow, bossLockRow;
    const string BossLockHelp = "Boss health is frozen at its current value: hits deal no damage and the bar never moves (the boss stays in its current phase). Overrides Boss infinite HP. Click the row to turn it on or off.";
    const string BossHpHelp = "Bosses can't die: their health drops as usual (phases still change) and is refilled when it reaches the bottom. Practice a fight as long as you want. Click the row to turn it on or off.";
    // La liste n'est affichée qu'une fois complète ; reconstruite quand une arène est apprise (une ligne par rencontre)
    static int BossesShown => Bosses.Done ? Bosses.All.Count * 1000 + Arenas.Version : -2;

    // Abscisses des boutons d'une ligne (de droite à gauche : Defeated, Alive, TP, Fight)
    static float BossDefeatedX => W - 1 - Pad - 4 - BossStateW;
    static float BossAliveX => BossDefeatedX - BossGap - BossStateW;
    static float BossTpX => BossAliveX - 10 - BossTpW;
    static float BossFightX => BossTpX - BossGap - BossFightW;
    static float BossBtnY(float rowY) => rowY + (BossRowH - BossBtnH) / 2f;

    float BuildBosses(float y)
    {
        bossRows.Clear();
        bossButtons.Clear();
        bossesBuiltFor = BossesShown;
        float iw = W - 1;

        y = SectionTitle("Bosses", y);
        bossHpRow = CheatRow("Boss infinite HP", BossHpHelp, () => BossHealth.Infinite, v => BossHealth.Infinite = v, y);
        y += CheatRowH;
        bossLockRow = CheatRow("Lock boss HP", BossLockHelp, () => BossHealth.Lock, v => BossHealth.Lock = v, y);
        y += CheatRowH + 6;
        if (!Bosses.Done || Bosses.All.Count == 0)
        {
            var t = Ui.Text(fill, "BossesLoading", Pad + 4, y, iw - Pad * 2, BossRowH, GameFonts.Body, 17f, Theme.TextDim);
            t.text = Bosses.Done ? "No boss found." : "Loading…";
            return y + BossRowH;
        }

        var lines = BossLines();
        for (int i = 0; i < lines.Count; i++)
        {
            var (b, key, label) = lines[i];
            var r = new BossRowUi { boss = b, key = key, label = label, y = y };
            r.highlight = Ui.Image(fill, "BossHl" + i, 12, y, iw - 24, BossRowH - 2, Theme.Hex(0x5e1418, 0.55f));
            Ui.Image(fill, "BossLine" + i, 12, y + BossRowH - 1, iw - 24, 1, Theme.Line);
            float nx = Pad + 4;
            if (b.Icon != null)
            {
                var icon = Skin.Sprite(fill, "BossIcon" + i, nx, y + (BossRowH - BossIcon) / 2f - 1, BossIcon, BossIcon, b.Icon, UnityEngine.Color.white);
                icon.preserveAspect = true;
                nx += BossIcon + 10;
            }
            r.name = Ui.Text(fill, "BossName" + i, nx, y, BossFightX - nx - 6, BossRowH, GameFonts.Title, 18f, Theme.Text);
            r.name.text = label;
            var boss = b;
            float by = BossBtnY(y);
            r.fight = UiButton.CreateSkinned(fill, "BossFight" + i, BossFightX, by, BossFightW, BossBtnH, "Fight", GameFonts.Title, 15f,
                () => Notify?.Invoke(Bosses.Fight(boss, key), Arenas.Get(key) == null));
            r.tp = UiButton.CreateSkinned(fill, "BossTp" + i, BossTpX, by, BossTpW, BossBtnH, "TP", GameFonts.Title, 15f,
                () => Notify?.Invoke(Arenas.RequestTeleport(boss, key), Arenas.Get(key) == null));
            r.alive = UiButton.CreateSkinned(fill, "BossAlive" + i, BossAliveX, by, BossStateW, BossBtnH, "Alive", GameFonts.Title, 15f,
                () => Notify?.Invoke(Bosses.SetDefeated(boss, false), false));
            r.defeated = UiButton.CreateSkinned(fill, "BossDefeated" + i, BossDefeatedX, by, BossStateW, BossBtnH, "Defeated", GameFonts.Title, 15f,
                () => Notify?.Invoke(Bosses.SetDefeated(boss, true), false));
            bossRows.Add(r);
            y += BossRowH;
        }

        // Sous la liste : la place ne manque pas, et la liste commence directement sous son titre
        y += 12;
        var all = UiButton.CreateSkinned(fill, "BtnRespawnAll", Pad, y, iw - Pad * 2, BarH, "Respawn all bosses", GameFonts.Title, 16f,
            () => Notify?.Invoke(Bosses.RespawnAll(), false));
        bossButtons.Add((all, Pad, y, iw - Pad * 2, BarH));
        return y + BarH + 8;
    }

    // Boss combattus deux fois : numéro de niveau de chaque rencontre, dans l'ordre (I, II)
    static readonly Dictionary<string, int[]> TwoFights = new()
    {
        ["CharacterID_Isaac"] = new[] { 6, 9 }, // Main Hall, Clock Tower
        ["CharacterID_Death"] = new[] { 3, 5 }, // Catacombs of Paris, Notre Dame
    };
    static readonly string[] Roman = { "I", "II", "III", "IV" };

    /// <summary>
    /// Lignes de l'onglet : une par rencontre. Boss combattus deux fois : « Isaac I » et « Isaac II », toujours affichés
    /// (arène inconnue : ligne sans destination). Autres boss : une ligne par arène connue, une seule si aucune.
    /// </summary>
    static List<(Bosses.Boss boss, string key, string label)> BossLines()
    {
        var lines = new List<(Bosses.Boss b, string key, string label, int order)>();
        foreach (var b in Bosses.All)
        {
            var enc = Arenas.Encounters(b);
            string id = b.Id?.name ?? "";
            if (TwoFights.TryGetValue(id, out var levels))
            {
                var used = new HashSet<string>();
                for (int n = 0; n < levels.Length; n++)
                {
                    var match = enc.FirstOrDefault(x => Arenas.LevelNumber(x.entry.Level) == levels[n] && !used.Contains(x.key));
                    if (match.key != null) used.Add(match.key);
                    lines.Add((b, match.key, $"{b.Name} {Roman[n]}", Order(id, n)));
                }
                foreach (var (k, _) in enc.Where(x => !used.Contains(x.key))) lines.Add((b, k, $"{b.Name} ?", Order(id, levels.Length)));
                continue;
            }
            if (enc.Count == 0) lines.Add((b, null, b.Name, Order(id, 0)));
            for (int n = 0; n < enc.Count; n++)
                lines.Add((b, enc[n].key, enc.Count > 1 ? $"{b.Name} {Roman[Math.Min(n, Roman.Length - 1)]}" : b.Name, Order(id, 0) + n));
        }
        // Tri stable : ordre du jeu, puis ordre d'origine (boss absent de la liste : à la fin)
        return lines.Select((l, i) => (l, i)).OrderBy(x => x.l.order).ThenBy(x => x.i).Select(x => (x.l.b, x.l.key, x.l.label)).ToList();
    }

    /// <summary>
    /// Ordre des rencontres dans le jeu (une entrée par combat : Death et Isaac en ont deux).
    /// Arthur, Doppelganger, Mammon et Succubus : ordre entre eux non précisé, alphabétique en attendant.
    /// </summary>
    static readonly (string id, int fight)[] GameOrder =
    {
        ("CharacterID_Leon", 0), ("CharacterID_Joan", 0), ("CharacterID_Medusa", 0), ("CharacterID_Death", 0),
        ("CharacterID_Isaac", 0), ("CharacterID_Carmilla", 0), ("CharacterID_Talos", 0),
        ("CharacterID_Death", 1),
        ("CharacterID_Arthur", 0), ("CharacterID_Doppelganger", 0), ("CharacterID_Mammon", 0),
        ("CharacterID_Succubus", 0),
        ("CharacterID_Isaac", 1), ("CharacterID_Dracula", 0), ("CharacterID_DraculaBeast", 0),
    };

    /// <summary>Rang d'une rencontre (×10 : place pour les arènes supplémentaires d'un même boss).</summary>
    static int Order(string id, int fight)
    {
        int i = Array.FindIndex(GameOrder, g => g.id == id && g.fight == fight);
        if (i < 0) i = Array.FindIndex(GameOrder, g => g.id == id); // rencontre en plus : avec la première du boss
        return (i < 0 ? GameOrder.Length : i) * 10;
    }

    /// <summary>Bouton d'état : rouge et volutes quand c'est l'état actuel, sombre sinon.</summary>
    static void StyleStateButton(UiButton b, bool current, bool hover, bool enabled)
    {
        b.Selected = current;
        b.BaseColor = current ? Theme.Crimson : b.Skinned ? Skin.TabIdle : Theme.BgButton;
        b.LabelColor = !enabled ? Theme.TextDim : current ? Theme.GoldText : Theme.GoldLabel;
        b.SetHover(hover && enabled && !current);
    }

    void UpdateBosses(bool hasMouse, float mx, float my, bool click, bool rightClick, ref bool handled)
    {
        string title = "Bosses", text = "Fight: brings the boss back and teleports you in front of its arena, then walk in. TP: just teleports you there. Alive / Defeated: sets the boss state.";

        foreach (var (btn, x, y, w, h) in bossButtons)
        {
            bool hover = hasMouse && mx >= x && mx < x + w && my >= y && my < y + h;
            btn.SetHover(hover);
            if (hover) { title = "Respawn all bosses"; text = "Marks every defeated boss Alive. To fight one, use its Fight button."; }
            if (click && hover && !handled) { handled = true; btn.OnClick?.Invoke(); }
        }

        if (bossHpRow != null && UpdateCheatRow(bossHpRow, hasMouse, mx, my, click, ref handled)) { title = bossHpRow.name; text = BossHpHelp; }
        if (bossLockRow != null && UpdateCheatRow(bossLockRow, hasMouse, mx, my, click, ref handled)) { title = bossLockRow.name; text = BossLockHelp; }

        bool Over(float x, float w, float rowY) { float by = BossBtnY(rowY); return hasMouse && mx >= x && mx < x + w && my >= by && my < by + BossBtnH; }

        foreach (var r in bossRows)
        {
            bool hover = hasMouse && my >= r.y && my < r.y + BossRowH && mx >= 12 && mx < W - 14;
            Ui.SetActive(r.highlight, hover);
            bool? defeated = Bosses.IsDefeated(r.boss);
            bool known = Arenas.Get(r.key) != null;
            bool overFight = Over(BossFightX, BossFightW, r.y), overTp = Over(BossTpX, BossTpW, r.y), overAlive = Over(BossAliveX, BossStateW, r.y), overDefeated = Over(BossDefeatedX, BossStateW, r.y);

            if (hover)
            {
                title = r.label;
                text = Bosses.IsSecondPhase(r.boss) ? "Second phase of Dracula, in its own room. Fight: goes straight into it, skipping the first phase."
                    : !r.boss.HasDefeatFlag ? "Final boss: the game never marks it as defeated. Fight: go to its arena with its fight reset."
                    : defeated == true ? "Defeated. Fight: bring it back and go to its arena."
                    : defeated == false ? "Alive. Fight: go to its arena with its fight reset. Defeated: mark it as beaten."
                    : "State unknown (load a game first).";
            }
            if (overFight)
            {
                title = "Fight: " + r.label;
                text = known ? "Brings the boss back and teleports you in front of its arena door. The level is reloaded so the fight is reset (like resting at a mirror: enemies come back). Then walk into the arena. Right-click: forget this entrance (wrong door), the next one you walk through is kept."
                    : "Unknown yet: enter this arena once (walking in through its door) and the mod will remember where the door is.";
            }
            if (overTp)
            {
                title = "Teleport: " + r.label;
                text = known ? "Teleports you in front of the arena door, in the room before it. If the boss is Alive, the level is reloaded and its fight is reset (like resting at a mirror: enemies come back). Right-click: forget this entrance (wrong door), the next one you walk through is kept."
                    : "Unknown yet: enter this arena once (walking in through its door) and the mod will remember where the door is.";
            }

            if (rightClick && !handled && (overFight || overTp) && known)
            {
                handled = true;
                Notify?.Invoke(Arenas.Forget(r.boss, r.key), false);
            }
            if (click && !handled)
            {
                if (overFight) { handled = true; r.fight.OnClick?.Invoke(); defeated = Bosses.IsDefeated(r.boss); }
                else if (overTp) { handled = true; r.tp.OnClick?.Invoke(); }
                else if (overAlive && defeated != null) { handled = true; r.alive.OnClick?.Invoke(); defeated = Bosses.IsDefeated(r.boss); }
                else if (overDefeated && defeated != null) { handled = true; r.defeated.OnClick?.Invoke(); defeated = Bosses.IsDefeated(r.boss); }
            }

            r.fight.SetHover(overFight && known);
            r.fight.LabelColor = known ? Theme.GoldText : Theme.TextDim;
            r.tp.SetHover(overTp && known);
            r.tp.LabelColor = known ? Theme.GoldText : Theme.TextDim;
            // Boss sans état « vaincu » (Dracula) : pas de boutons Alive / Defeated
            Ui.SetActive(r.alive.Rt, r.boss.HasDefeatFlag);
            Ui.SetActive(r.defeated.Rt, r.boss.HasDefeatFlag);
            StyleStateButton(r.alive, defeated == false, overAlive, defeated != null);
            StyleStateButton(r.defeated, defeated == true, overDefeated, defeated != null);
            r.name.color = defeated == true ? Theme.TextDim : hover ? Theme.GoldText : Theme.Text;
        }

        Ui.SetText(detailTitle, title);
        Ui.SetText(detailText, text);
    }
}

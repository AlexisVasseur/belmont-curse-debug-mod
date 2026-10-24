using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

namespace BelmontPractice;

/// <summary>
/// Onglet Bosses : un boss par ligne, état vaincu / vivant. Clic : le fait réapparaître s'il est vaincu
/// (sinon le marque vaincu). Le combat revient à la prochaine entrée dans l'arène.
/// </summary>
public partial class SidePanelView
{
    class BossRowUi
    {
        public Bosses.Boss boss;
        public Image highlight, chipFill;
        public TextMeshProUGUI name, chip;
        public UiButton tp;
        public float y, tpX;
    }
    readonly List<BossRowUi> bossRows = new();
    readonly List<(UiButton btn, float x, float y, float w, float h)> bossButtons = new();
    const float BossRowH = 46f, BossChipW = 110f, BossIcon = 36f, BossTpW = 52f;
    int bossesBuiltFor = -1;
    static int BossesShown => Bosses.Done ? Bosses.All.Count : -2; // la liste n'est affichée qu'une fois complète

    float BuildBosses(float y)
    {
        bossRows.Clear();
        bossButtons.Clear();
        bossesBuiltFor = BossesShown;
        float iw = W - 1;

        var all = UiButton.CreateSkinned(fill, "BtnRespawnAll", Pad, y, iw - Pad * 2, BarH, "Respawn all bosses", GameFonts.Title, 16f,
            () => Notify?.Invoke(Bosses.RespawnAll(), false));
        bossButtons.Add((all, Pad, y, iw - Pad * 2, BarH));
        y += BarH + 10;

        y = SectionTitle("Bosses", y);
        if (!Bosses.Done || Bosses.All.Count == 0)
        {
            var t = Ui.Text(fill, "BossesLoading", Pad + 4, y, iw - Pad * 2, BossRowH, GameFonts.Body, 17f, Theme.TextDim);
            t.text = Bosses.Done ? "No boss found." : "Loading…";
            return y + BossRowH;
        }

        for (int i = 0; i < Bosses.All.Count; i++)
        {
            var b = Bosses.All[i];
            var r = new BossRowUi { boss = b, y = y };
            r.highlight = Ui.Image(fill, "BossHl" + i, 12, y, iw - 24, BossRowH - 2, Theme.Hex(0x5e1418, 0.55f));
            Ui.Image(fill, "BossLine" + i, 12, y + BossRowH - 1, iw - 24, 1, Theme.Line);
            float nx = Pad + 4;
            if (b.Icon != null)
            {
                var icon = Skin.Sprite(fill, "BossIcon" + i, nx, y + (BossRowH - BossIcon) / 2f - 1, BossIcon, BossIcon, b.Icon, UnityEngine.Color.white);
                icon.preserveAspect = true;
                nx += BossIcon + 10;
            }
            r.name = Ui.Text(fill, "BossName" + i, nx, y, iw - nx - Pad - BossChipW - BossTpW - 18, BossRowH, GameFonts.Title, 18f, Theme.Text);
            r.name.text = b.Name;
            r.chipFill = Ui.Frame(fill, "BossChip" + i, iw - Pad - BossChipW - 4, y + (BossRowH - 28) / 2f, BossChipW, 26, Theme.Gold, Theme.BgButton);
            r.chip = Ui.Label(r.chipFill.transform, "Text", 0, 0, BossChipW - 2, 26, GameFonts.Title, 15f, Theme.KeyText);
            r.tpX = iw - Pad - BossChipW - BossTpW - 10;
            var boss = b;
            r.tp = UiButton.CreateSkinned(fill, "BossTp" + i, r.tpX, y + (BossRowH - 28) / 2f, BossTpW, 26, "TP", GameFonts.Title, 15f,
                () => Notify?.Invoke(Arenas.RequestTeleport(boss), Arenas.Get(boss) == null));
            bossRows.Add(r);
            y += BossRowH;
        }
        return y;
    }

    void UpdateBosses(bool hasMouse, float mx, float my, bool click, ref bool handled)
    {
        string title = "Bosses", text = "Click a defeated boss to bring it back: the fight starts again the next time you enter its arena. Click a boss still alive to mark it as defeated.";

        foreach (var (btn, x, y, w, h) in bossButtons)
        {
            bool hover = hasMouse && mx >= x && mx < x + w && my >= y && my < y + h;
            btn.SetHover(hover);
            if (hover) { title = "Respawn all bosses"; text = "Brings back every defeated boss. Their fights start again the next time you enter their arenas."; }
            if (click && hover && !handled) { handled = true; btn.OnClick?.Invoke(); }
        }

        foreach (var r in bossRows)
        {
            // Bouton TP : devant l'arène (connue une fois qu'on y est entré)
            bool known = Arenas.Get(r.boss) != null;
            float ty = r.y + (BossRowH - 28) / 2f;
            bool overTp = hasMouse && mx >= r.tpX && mx < r.tpX + BossTpW && my >= ty && my < ty + 26;
            r.tp.SetHover(overTp && known);
            r.tp.LabelColor = known ? Theme.GoldText : Theme.TextDim;
            if (overTp)
            {
                title = "Teleport: " + r.boss.Name;
                text = known ? "Teleports you in front of the arena door, in the room before it."
                    : "Unknown yet: enter this arena once (walking in through its door) and the mod will remember where the door is.";
            }
            if (overTp && click && !handled) { handled = true; r.tp.OnClick?.Invoke(); }

            bool hover = hasMouse && my >= r.y && my < r.y + BossRowH && mx >= 12 && mx < W - 14;
            Ui.SetActive(r.highlight, hover);
            bool? defeated = Bosses.IsDefeated(r.boss);
            if (hover && !overTp)
            {
                title = r.boss.Name;
                text = defeated == true ? "Defeated. Click: bring it back (the fight starts again the next time you enter its arena)."
                    : defeated == false ? "Not defeated yet. Click: mark it as defeated."
                    : "State unknown (load a game first).";
            }
            if (hover && click && !handled && defeated != null)
            {
                handled = true;
                Notify?.Invoke(Bosses.Toggle(r.boss), false);
                defeated = Bosses.IsDefeated(r.boss);
            }
            Ui.SetText(r.chip, defeated == true ? "Defeated" : defeated == false ? "Alive" : "—");
            r.chip.color = defeated == true ? Theme.TextDim : Theme.GoldText;
            r.chipFill.color = defeated == false ? Theme.Crimson : Theme.BgButton;
            Ui.SetBorder(r.chipFill, defeated == false ? Theme.GoldBright : Theme.Gold);
            r.name.color = defeated == true ? Theme.TextDim : hover ? Theme.GoldText : Theme.Text;
        }

        Ui.SetText(detailTitle, title);
        Ui.SetText(detailText, text);
    }
}

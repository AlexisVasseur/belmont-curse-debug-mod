using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace BelmontPractice;

/// <summary>
/// Section Stats de l'onglet General : toutes les stats d'attaque et de défense du personnage, en lecture seule
/// (le menu du jeu n'affiche que ATK et DEF), relues deux fois par seconde.
/// </summary>
public partial class SidePanelView
{
    const float StatRowH = 26f;
    readonly List<(PlayerStats.Entry entry, TextMeshProUGUI value)> statRows = new();
    float nextStatsRead;

    float BuildStats(float y)
    {
        statRows.Clear();
        float iw = W - 1, right = iw - Pad;
        y = SectionTitle("Stats", y);
        foreach (var (group, entries) in PlayerStats.Groups)
        {
            Ui.Text(fill, "StatGroup" + group, Pad + 4, y, 200, StatRowH + 4, GameFonts.Title, 18f, Theme.GoldLabel).text = group;
            y += StatRowH + 4;
            foreach (var en in entries)
            {
                Ui.Text(fill, "StatName" + en.Label, Pad + 16, y, 260, StatRowH, GameFonts.Body, 16f, Theme.Text).text = en.Label;
                var v = Ui.Text(fill, "StatVal" + en.Label, right - 120, y, 120, StatRowH, GameFonts.Numbers, 18f, Theme.GoldText, TextAlignmentOptions.MidlineRight);
                Ui.Image(fill, "StatLine" + en.Label, Pad + 12, y + StatRowH - 1, iw - Pad * 2 - 12, 1, Theme.Line);
                statRows.Add((en, v));
                Help(y, StatRowH, en.Label, en.Help);
                y += StatRowH;
            }
            y += 6;
        }
        // Formule de dégâts du jeu : affichée au survol
        Ui.Text(fill, "StatFormula", Pad + 4, y, 260, StatRowH + 4, GameFonts.Title, 18f, Theme.GoldLabel).text = "Damage formula";
        Ui.Text(fill, "StatFormulaHint", right - 160, y, 160, StatRowH + 4, GameFonts.Body, 15f, Theme.TextDim, TextAlignmentOptions.MidlineRight).text = "hover to read";
        Help(y, StatRowH + 4, "Damage formula", PlayerStats.FormulaText());
        return y + StatRowH + 4;
    }

    void UpdateStats()
    {
        if (statRows.Count == 0 || Time.unscaledTime < nextStatsRead) return;
        nextStatsRead = Time.unscaledTime + 0.5f;
        foreach (var (entry, value) in statRows) Ui.SetText(value, PlayerStats.Value(entry));
    }
}

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BelmontPractice;

/// <summary>
/// Panneau latéral pleine hauteur, collé à droite, disposé comme celui de DebugMod :
/// rangée d'onglets, puis des sections à grand titre (« Movement », « Spells »), chacune avec
/// une barre « All … » et une grille de tuiles (icône + nom), éclairées quand la compétence est active.
/// </summary>
public partial class SidePanelView
{
    const float W = 470f, Pad = 14f, TabsH = 38f, TitleH = 44f, BarH = 30f;
    const int Cols = 5;
    const float Gap = 6f, TileH = 124f, IconSize = 58f;

    public Action<string, bool> Notify;

    GameObject panel;
    Transform fill;
    bool builtWithGameFonts;

    // Boutons des onglets General et Skills, avec l'onglet qui les porte (les deux pages partagent les coordonnées)
    readonly List<(UiButton btn, float x, float y, float w, float h, Tab page)> buttons = new();
    Tab buildPage;

    class TileUi
    {
        public Abilities.Move move;
        public Image bg, icon;
        public GameObject fallback;
        public TextMeshProUGUI name;
        public string shownName;
        public float x, y, w, h;
    }
    readonly List<TileUi> tiles = new();

    UiButton allMovement;
    Image allIndicator;

    static void Stretch(RectTransform rt, float left, float top, float right, float bottom)
    {
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    float TileW => (W - 1 - Pad * 2 - Gap * (Cols - 1)) / Cols;

    void Build()
    {
        if (panel != null) UnityEngine.Object.Destroy(panel);
        tiles.Clear();
        buttons.Clear();
        builtWithGameFonts = GameFonts.Resolved;
        var t = GameFonts.Title;
        var b = GameFonts.Body;

        var container = Ui.Rect(UiCanvas.Root.transform, "SidePanel", 0, 0, W, 0);
        container.anchorMin = new Vector2(1f, 0f);
        container.anchorMax = new Vector2(1f, 1f);
        container.pivot = new Vector2(1f, 1f);
        container.sizeDelta = new Vector2(W, 0f);
        container.anchoredPosition = new Vector2(0f, 0f);
        panel = container.gameObject;

        var edgeOuter = Ui.Image(container, "EdgeOuter", 0, 0, 0, 0, Theme.OuterLine); Stretch(edgeOuter.rectTransform, -6, 0, 0, 0); edgeOuter.rectTransform.anchorMax = new Vector2(0f, 1f); edgeOuter.rectTransform.offsetMax = new Vector2(-5f, 0f);
        
        var edgeGold = Ui.Image(container, "EdgeGold", 0, 0, 0, 0, Theme.Gold); Stretch(edgeGold.rectTransform, 0, 0, 0, 0); edgeGold.rectTransform.anchorMax = new Vector2(0f, 1f); edgeGold.rectTransform.offsetMax = new Vector2(1f, 0f);
        var bg = Ui.Image(container, "Fill", 0, 0, 0, 0, Theme.Bg);
        Stretch(bg.rectTransform, 1, 0, 0, 0);
        fill = bg.transform;
        float iw = W - 1;

        // --- Onglets ---
        float y = 8;
        for (int i = 0; i < TabNames.Length; i++)
            tabButtons[i] = UiButton.CreateSkinned(fill, "Tab" + TabNames[i], TabX(i), y, TabW, TabsH, TabNames[i], t, 18f, null);

        Ui.Image(fill, "TabsLine", Pad, y + TabsH + 4, iw - Pad * 2, 1, Theme.LineGold);
        y += TabsH + 14;

        // Zone défilante sous les onglets : masque + une page par onglet (mêmes coordonnées que le fond)
        var root = fill;
        viewportTop = y - 6;
        var viewport = Ui.Rect(root, "Viewport", 0, 0, iw, 10);
        Stretch(viewport, 0, viewportTop, 0, DetailAreaH);
        viewport.gameObject.AddComponent<RectMask2D>();

        // Zone de détail fixe en bas du panneau (commune aux onglets) : l'élément survolé a toujours son explication
        var box = Ui.Frame(root, "Detail", 0, 0, iw - Pad * 2, DetailH, Theme.Gold, Theme.TileOff);
        var boxRt = box.transform.parent.Cast<RectTransform>();
        boxRt.anchorMin = new Vector2(0f, 0f);
        boxRt.anchorMax = new Vector2(0f, 0f);
        boxRt.pivot = new Vector2(0f, 0f);
        boxRt.anchoredPosition = new Vector2(Pad, Pad);
        detailTitle = Ui.Text(box.transform, "Title", 10, 6, iw - Pad * 2 - 22, 30, GameFonts.Title, 20f, Theme.GoldText);
        detailText = Ui.Text(box.transform, "Text", 10, 38, iw - Pad * 2 - 22, DetailH - 46, GameFonts.Body, 17f, Theme.Text, TextAlignmentOptions.TopLeft);
        detailText.enableWordWrapping = true;
        detailText.overflowMode = TextOverflowModes.Truncate;
        weaponDetailTitle = detailTitle;
        weaponDetailText = detailText;

        // Barre de défilement (à droite de la zone défilante)
        scrollTrack = Ui.Image(root, "ScrollTrack", 0, 0, ScrollbarW, 10, Theme.Line);
        scrollThumb = Ui.Image(root, "ScrollThumb", 0, 0, ScrollbarW, 10, Theme.GoldBright);
        pages[(int)Tab.Keybinds] = Ui.Rect(viewport, "KeybindsPage", 0, 0, iw, 10).gameObject;
        fill = pages[(int)Tab.Keybinds].transform;
        contentH[(int)Tab.Keybinds] = BuildKeybinds(y) + 8;
        pages[(int)Tab.Weapons] = Ui.Rect(viewport, "WeaponsPage", 0, 0, iw, 10).gameObject;
        fill = pages[(int)Tab.Weapons].transform;
        weaponsBuiltFor = Weapons.Families.Count;
        BuildWeapons(y);
        float pageTop = y;

        // --- Onglet General : Utilities, puis Upgrades (Attack+, Defense+, cœurs de vie et de mana) ---
        pages[(int)Tab.General] = Ui.Rect(viewport, "GeneralPage", 0, 0, iw, 10).gameObject;
        fill = pages[(int)Tab.General].transform;
        buildPage = Tab.General;
        y = BuildUtilities(y) + 10;
        y = BuildUpgrades(y) + 10;
        contentH[(int)Tab.General] = y + 8;

        // --- Onglet Skills : Movement, puis Spells ---
        pages[(int)Tab.Skills] = Ui.Rect(viewport, "SkillsPage", 0, 0, iw, 10).gameObject;
        fill = pages[(int)Tab.Skills].transform;
        buildPage = Tab.Skills;
        y = pageTop;

        // --- Section Movement ---
        y = SectionTitle("Movement", y);
        allMovement = Button(Pad, y, iw - Pad * 2, BarH, "All movement", t, 16f, ToggleAllMovement);
        allIndicator = Ui.Image(allMovement.Fill.transform, "Indicator", 0, 0, 12, 12, Theme.Crimson);
        allIndicator.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        allIndicator.rectTransform.anchoredPosition = new Vector2(iw - Pad * 2 - 20, -(BarH - 2) / 2f);
        allIndicator.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
        y += BarH + 8;

        for (int i = 0; i < Abilities.Traversal.Count; i++)
        {
            int col = i % Cols, row = i / Cols;
            float tx = Pad + col * (TileW + Gap), ty = y + row * (TileH + Gap);
            tiles.Add(BuildTile(Abilities.Traversal[i], tx, ty, b));
        }
        int rowsCount = (Abilities.Traversal.Count + Cols - 1) / Cols;
        y += rowsCount * (TileH + Gap) + 16;

        // --- Section Spells ---
        y = SectionTitle("Spells", y);
        spellsTop = y;
        spellsBuiltFor = Arcana.Spells.Count;
        BuildSpells(y);
        fill = root;

        Plugin.Logger.LogInfo("[Interface] panneau latéral construit");
    }

    // ---------- Onglets ----------

    const float DetailH = 190f, ScrollbarW = 5f;
    Image scrollTrack, scrollThumb;
    bool draggingScrollbar;
    static float DetailAreaH => DetailH + Pad * 2;
    float viewportTop;

    enum Tab { General, Skills, Weapons, Keybinds }
    static readonly string[] TabNames = { "General", "Skills", "Weapons", "Keybinds" };
    Tab tab = Tab.General;
    static float TabW => (W - 1 - Pad * 2 - 4 * (TabNames.Length - 1)) / TabNames.Length; // les onglets occupent toute la largeur
    float TabX(int i) => Pad + i * (TabW + 4);
    readonly UiButton[] tabButtons = new UiButton[TabNames.Length];
    // Par onglet : page, hauteur du contenu, position de défilement
    readonly GameObject[] pages = new GameObject[TabNames.Length];
    readonly float[] contentH = new float[TabNames.Length], scrollPos = new float[TabNames.Length];

    void UpdateTabs(bool hasMouse, float mx, float my, bool click, ref bool handled)
    {
        if (click && !handled && my >= 8 && my < 8 + TabsH)
            for (int i = 0; i < TabNames.Length; i++)
                if (mx >= TabX(i) && mx < TabX(i) + TabW) { tab = (Tab)i; handled = true; }
        for (int i = 0; i < TabNames.Length; i++) StyleTab(tabButtons[i], pages[i], (int)tab == i);
        if (tab != Tab.Keybinds) Keybinds.Capturing = null; // onglet quitté pendant la saisie d'une touche
    }

    /// <summary>Onglet actif : fond bordeaux et volutes dorées ; les autres : fond sombre, sans volutes.</summary>
    static void StyleTab(UiButton b, GameObject page, bool active)
    {
        b.Selected = active;
        b.BaseColor = active ? Theme.Crimson : b.Skinned ? Skin.TabIdle : Theme.BgButton;
        b.LabelColor = active ? Theme.GoldText : Theme.GoldLabel;
        b.SetHover(false);
        if (!b.Skinned) Ui.SetBorder(b.Fill, active ? Theme.GoldBright : Theme.Gold);
        if (page.activeSelf != active) page.SetActive(active);
    }

    // ---------- Utilitaires ----------

    class CheatUi
    {
        public string name, desc;
        public Func<bool> get;
        public Action<bool> set;
        public Image highlight;
        public UiButton pill;
        public TextMeshProUGUI label;
        public float y;
    }
    readonly List<CheatUi> cheatRows = new();
    class GaugeUi { public TextMeshProUGUI value; public Image fill; public float x, y, w, h; }
    GaugeUi healthGauge, manaGauge;
    TextMeshProUGUI potionsValue;
    readonly List<(Image border, Image fill, float cx, float cy)> potionPips = new();
    const float PotionPitch = 30f, PotionSepW = 16f;
    float potionsX, potionsY;
    Image potionSep;

    static string RefillPotions() =>
        Cheats.PotionValues(out int cur, out int max, out int bonus, out _) ? Cheats.SetPotions(Math.Max(max, cur + bonus)) : "Unavailable (load a game first)";

    void UpdatePotions(bool hasMouse, float mx, float my, bool click, ref bool handled)
    {
        bool ok = Cheats.PotionValues(out int cur, out int max, out int bonus, out int bonusMax);
        Ui.SetText(potionsValue, !ok ? "—" : bonus > 0 ? $"{cur} / {max}  + {bonus} bonus" : $"{cur} / {max}");
        int total = cur + bonus, limit = max + bonusMax;
        bool vials = potionPips.Count > 0 && potionPips[0].border == null;
        if (potionSep != null)
        {
            Ui.SetActive(potionSep, ok && bonusMax > 0 && max < potionPips.Count);
            Ui.Place(potionSep.rectTransform, potionsX + max * PotionPitch + 1, potionsY - 12, 13, 24);
        }
        for (int i = 0; i < potionPips.Count; i++)
        {
            var (border, f, cx, cy) = potionPips[i];
            if (vials)
            {
                // Bonus décalés après l'ornement séparateur
                float vx = potionsX + i * PotionPitch + (i >= max ? PotionSepW : 0f);
                cx = vx + 13f;
                bool shown = ok && i < limit;
                Ui.SetActive(f, shown);
                if (!shown) continue;
                Ui.Place(f.rectTransform, vx, cy - 14, 26, 27);
                bool full = i < total;
                var sp = !full ? Skin.PotionEmpty ?? Skin.Potion : i < max ? Skin.Potion : Skin.PotionBonus ?? Skin.Potion;
                if (f.sprite != sp) f.sprite = sp;
                bool over = hasMouse && Mathf.Abs(mx - cx) <= PotionPitch / 2f && Mathf.Abs(my - cy) <= 15f;
                f.color = over ? Theme.Hex(0xffc890) : full ? Color.white : new Color(1f, 1f, 1f, 0.45f);
                if (over && click && !handled)
                {
                    handled = true;
                    int target = i + 1 == total ? i : i + 1;
                    Notify?.Invoke(Cheats.SetPotions(target), false);
                }
                continue;
            }
            bool exists = ok && i < limit;
            Ui.SetActive(border, exists);
            Ui.SetActive(f, exists);
            if (!exists) continue;
            bool hover = hasMouse && Mathf.Abs(mx - cx) <= PotionPitch / 2f && Mathf.Abs(my - cy) <= 14f;
            bool filled = i < total;
            f.color = !filled ? Theme.BgSolid : i < max ? Theme.Hex(0xb3262c) : Theme.GoldText;
            border.color = hover ? Theme.Red : i < max ? Theme.GoldBright : Theme.Gold;
            if (hover && click && !handled)
            {
                handled = true;
                // Clic sur la dernière potion : on la retire ; sinon on remplit jusqu'à ce losange
                int target = i + 1 == total ? i : i + 1;
                Notify?.Invoke(Cheats.SetPotions(target), false);
            }
        }
    }
    const float GaugeRowH = 72f;

    static void UpdateGauge(GaugeUi g, bool ok, float cur, float max)
    {
        if (g == null) return;
        Ui.SetText(g.value, ok ? $"{cur:0} / {max:0}" : "—");
        float ratio = ok && max > 0f ? Mathf.Clamp01(cur / max) : 0f;
        Ui.Place(g.fill.rectTransform, g.x, g.y, Mathf.Max(0f, g.w * ratio), g.h);
        Ui.SetActive(g.fill, ratio > 0f);
    }
    const float CheatRowH = 42f;

    float BuildUtilities(float y)
    {
        cheatRows.Clear();
        potionPips.Clear();
        float iw = W - 1;
        y = SectionTitle("Utilities", y);
        void Row(string name, string desc, Func<bool> get, Action<bool> set)
        {
            var r = new CheatUi { name = name, desc = desc, get = get, set = set, y = y };
            r.highlight = Ui.Image(fill, "CheatHl" + name, 12, y, iw - 24, CheatRowH - 2, Theme.Hex(0x5e1418, 0.55f));
            Ui.Image(fill, "CheatLine" + name, 12, y + CheatRowH - 1, iw - 24, 1, Theme.Line);
            r.label = Ui.Text(fill, "CheatName" + name, Pad + 4, y, iw - Pad * 2 - 90, CheatRowH, GameFonts.Title, 19f, Theme.Text);
            r.label.text = name;
            r.pill = UiButton.CreateSkinned(fill, "CheatPill" + name, iw - Pad - 76, y + (CheatRowH - 30) / 2f, 70, 30, "", GameFonts.Title, 15f, null);
            cheatRows.Add(r);
            y += CheatRowH;
        }
        Row("Infinite HP", "Health never drops: you can't die", () => Cheats.InfiniteHealth, v => Cheats.InfiniteHealth = v);
        Row("Infinite mana", "Mana is always full", () => Cheats.InfiniteMana, v => Cheats.InfiniteMana = v);
        Row("Infinite potions", "Healing potions are always refilled", () => Cheats.InfinitePotions, v => Cheats.InfinitePotions = v);
        Row("Infinite jump", "Jump again in mid-air, as many times as you want", () => Cheats.InfiniteJump, v => Cheats.InfiniteJump = v);
        Row("Noclip", "Fly through walls: WASD / arrows, Shift = faster", () => Cheats.Noclip, v => Cheats.Noclip = v);

        y += 6;
        // Vie et mana : nom, valeur et remplissage complet en haut ; jauge encadrée des boutons -50 -10 / +10 +50 en dessous
        GaugeUi GaugeRow(string name, string fullLabel, Func<int?, string> change, Color color)
        {
            var g = new GaugeUi();
            float right = iw - Pad;
            Ui.Text(fill, name + "Name", Pad + 4, y, 160, 30, GameFonts.Title, 19f, Theme.Text).text = name;
            Button(right - 70, y + 2, 70, 26, fullLabel, GameFonts.Title, 14f, () => Notify?.Invoke(change(null), false), name);
            g.value = Ui.Text(fill, name + "Val", right - 70 - 150, y, 140, 30, GameFonts.Numbers, 20f, Theme.GoldText, TextAlignmentOptions.Right);
            float by = y + 34, bw = 46, bh = 28, gap = 5;
            float x = Pad + 4;
            Button(x, by, bw, bh, "-50", GameFonts.Title, 13f, () => Notify?.Invoke(change(-50), false), name); x += bw + gap;
            Button(x, by, bw, bh, "-10", GameFonts.Title, 13f, () => Notify?.Invoke(change(-10), false), name); x += bw + gap + 4;
            float gw = right - x - 2 * bw - 2 * gap - 4;
            g.x = x + 1; g.w = gw - 2; g.y = by + 7; g.h = bh - 14;
            Ui.Frame(fill, name + "Gauge", x, by + 6, gw, bh - 12, Theme.Gold, Theme.BgSolid);
            g.fill = Ui.Image(fill, name + "GaugeFill", g.x, g.y, g.w, g.h, color);
            x += gw + 4;
            Button(x, by, bw, bh, "+10", GameFonts.Title, 13f, () => Notify?.Invoke(change(+10), false), name); x += bw + gap;
            Button(x, by, bw, bh, "+50", GameFonts.Title, 13f, () => Notify?.Invoke(change(+50), false), name);
            Ui.Image(fill, name + "Line", 12, y + GaugeRowH - 1, iw - 24, 1, Theme.Line);
            y += GaugeRowH;
            return g;
        }
        healthGauge = GaugeRow("Health", "Heal", Cheats.ChangeHealth, Theme.Hex(0xa3262b));
        manaGauge = GaugeRow("Mana", "Refill", Cheats.ChangeMana, Theme.Hex(0x2f72b8));

        // Potions : « -  ◆◆◆◇◇◇  + » (rouge = normales, or = bonus, sombre = vide) ; clic sur un losange = ce nombre
        {
            float right = iw - Pad;
            Ui.Text(fill, "PotionsName", Pad + 4, y, 160, 30, GameFonts.Title, 19f, Theme.Text).text = "Potions";
            Button(right - 70, y + 2, 70, 26, "Refill", GameFonts.Title, 14f, () => Notify?.Invoke(RefillPotions(), false), "Potions");
            potionsValue = Ui.Text(fill, "PotionsVal", right - 70 - 200, y, 190, 30, GameFonts.Numbers, 20f, Theme.GoldText, TextAlignmentOptions.Right);
            float ly = y + 34;
            Button(Pad + 4, ly, 34, 30, "-", GameFonts.Title, 18f, () => Notify?.Invoke(Cheats.ChangePotions(-1), false), "Potions");
            potionsX = Pad + 4 + 34 + 12;
            potionsY = ly + 15;
            int slots = (int)((right - 34 - 12 - potionsX - PotionSepW) / PotionPitch);
            if (Skin.Potion != null)
            {
                // Fioles du HUD du jeu ; l'ornement séparateur est placé entre normales et bonus à chaque frame
                if (Skin.PotionSeparator != null) { potionSep = Skin.Sprite(fill, "PotionSep", 0, ly + 3, 13, 24, Skin.PotionSeparator, Color.white); potionSep.preserveAspect = true; }
                for (int i = 0; i < slots; i++)
                {
                    var img = Skin.Sprite(fill, $"Potion{i}", potionsX + i * PotionPitch, ly + 1, 26, 27, Skin.Potion, Color.white);
                    img.preserveAspect = true;
                    potionPips.Add((null, img, 0f, ly + 15));
                }
            }
            else
                for (int i = 0; i < slots; i++)
                {
                    float cx = potionsX + i * PotionPitch + PotionPitch / 2f;
                    var d = Ui.Diamond(fill, $"Potion{i}", cx, ly + 15, 18, GameFonts.Numbers, 10f);
                    d.label.text = "";
                    potionPips.Add((d.border, d.fill, cx, ly + 15));
                }
            Button(right - 34, ly, 34, 30, "+", GameFonts.Title, 18f, () => Notify?.Invoke(Cheats.ChangePotions(+1), false), "Potions");
            Ui.Image(fill, "PotionsLine", 12, y + GaugeRowH - 1, iw - 24, 1, Theme.Line);
            y += GaugeRowH;
        }
        return y;
    }

    void UpdateUtilities(bool hasMouse, float mx, float my, bool click, ref bool handled)
    {
        UpdateGauge(healthGauge, Cheats.HealthValues(out float hc, out float hm), hc, hm);
        UpdateGauge(manaGauge, Cheats.ManaValues(out float mc, out float mm), mc, mm);
        UpdatePotions(hasMouse, mx, my, click, ref handled);
        foreach (var r in cheatRows)
        {
            bool on = r.get();
            bool hover = hasMouse && my >= r.y && my < r.y + CheatRowH && mx >= 12 && mx < W - 14;
            Ui.SetActive(r.highlight, hover);
            Ui.SetText(r.pill.Label, on ? "On" : "Off");
            r.pill.BaseColor = on ? Theme.Crimson : r.pill.Skinned ? Skin.ButtonIdle : Theme.BgButton;
            r.pill.LabelColor = on ? Theme.GoldText : Theme.TextDim;
            r.pill.SetHover(false);
            if (!r.pill.Skinned) Ui.SetBorder(r.pill.Fill, on ? Theme.GoldBright : Theme.Gold);
            r.label.color = on ? Theme.GoldText : Theme.Text;
            if (click && hover && !handled)
            {
                handled = true;
                r.set(!on);
                Notify?.Invoke($"{r.name}: {(!on ? "enabled" : "disabled")}", false);
            }
        }
    }

    // ---------- Améliorations ----------

    class UpgradeUi
    {
        public Upgrades.Kind kind;
        public TextMeshProUGUI value, sub;
        public Image icon;
        public List<(Image img, float x, float y)> hearts = new();
        // Couches de chaque cœur (fond, 4 pièces, cadre) : le cœur en cours n'affiche que ses pièces trouvées
        public List<Image[]> layers = new();
    }
    readonly List<UpgradeUi> upgradeRows = new();
    bool wasVisible;
    const float BookRowH = 46f, HeartRowH = 74f, HeartSize = 30f, HeartGap = 8f;

    static Sprite UpgradeIcon(Upgrades.Kind kind) => kind switch
    {
        Upgrades.Kind.Attack => GameSprites.Icon(GameSprites.AttackBook),
        Upgrades.Kind.Defense => GameSprites.Icon(GameSprites.DefenseBook),
        Upgrades.Kind.LifeFragments => GameSprites.Icon(GameSprites.LifeHeart),
        _ => GameSprites.Icon(GameSprites.ManaHeart),
    };

    float BuildUpgrades(float y)
    {
        upgradeRows.Clear();
        float iw = W - 1, right = iw - Pad;
        y = SectionTitle("Upgrades", y);

        // Livres : icône, nom, puis « −  valeur  + » à droite
        void BookRow(Upgrades.Kind kind)
        {
            var r = new UpgradeUi { kind = kind };
            r.icon = Ui.Image(fill, "UpgIcon" + kind, Pad + 2, y + 6, 34, 34, new Color(1f, 1f, 1f, 1f));
            r.icon.preserveAspect = true;
            Ui.Text(fill, "UpgName" + kind, Pad + 46, y, 180, BookRowH, GameFonts.Title, 19f, Theme.Text).text = Upgrades.Label(kind);
            float by = y + (BookRowH - 28) / 2f;
            Button(right - 34, by, 34, 28, "+", GameFonts.Title, 18f, () => ChangeUpgrade(kind, +1), kind.ToString());
            r.value = Ui.Label(fill, "UpgVal" + kind, right - 34 - 6 - 56, by, 56, 28, GameFonts.Numbers, 22f, Theme.GoldText);
            Button(right - 34 - 6 - 56 - 6 - 34, by, 34, 28, "-", GameFonts.Title, 18f, () => ChangeUpgrade(kind, -1), kind.ToString());
            Ui.Image(fill, "UpgLine" + kind, 12, y + BookRowH - 1, iw - 24, 1, Theme.Line);
            upgradeRows.Add(r);
            y += BookRowH;
        }

        // Cœurs : icône, nom et valeur en haut ; « −  ♥♥♥♥♥♡  + » en dessous (clic sur un cœur = ce nombre de cœurs)
        void HeartRow(Upgrades.Kind kind)
        {
            var r = new UpgradeUi { kind = kind };
            r.icon = Ui.Image(fill, "UpgIcon" + kind, Pad + 2, y + 2, 30, 30, new Color(1f, 1f, 1f, 1f));
            r.icon.preserveAspect = true;
            Ui.Text(fill, "UpgName" + kind, Pad + 46, y, 180, 34, GameFonts.Title, 19f, Theme.Text).text = Upgrades.Label(kind);
            r.value = Ui.Text(fill, "UpgVal" + kind, right - 200, y, 200, 34, GameFonts.Numbers, 20f, Theme.GoldText, TextAlignmentOptions.Right);

            float ly = y + 36;
            Button(Pad + 4, ly, 34, 30, "-", GameFonts.Title, 18f, () => ChangeUpgrade(kind, -1), kind.ToString());
            float hx = Pad + 4 + 34 + 14;
            for (int i = 0; i < Upgrades.MaxHearts; i++)
            {
                float x = hx + i * (HeartSize + HeartGap);
                var img = Ui.Image(fill, $"UpgHeart{kind}{i}", x, ly, HeartSize, HeartSize, new Color(1f, 1f, 1f, 1f));
                img.preserveAspect = true;
                r.hearts.Add((img, x, ly));
                bool mana = kind == Upgrades.Kind.ManaFragments;
                if (Skin.HeartBg != null && Skin.HeartFrame != null && Skin.HeartPiece(mana, 4) != null)
                {
                    img.gameObject.SetActive(false);
                    var layers = new Image[6];
                    layers[0] = Skin.Sprite(fill, $"UpgHeart{kind}{i}Bg", x, ly, HeartSize, HeartSize, Skin.HeartBg, Color.white);
                    for (int k = 1; k <= 4; k++) layers[k] = Skin.Sprite(fill, $"UpgHeart{kind}{i}P{k}", x, ly, HeartSize, HeartSize, Skin.HeartPiece(mana, k), Color.white);
                    layers[5] = Skin.Sprite(fill, $"UpgHeart{kind}{i}Frame", x, ly, HeartSize, HeartSize, Skin.HeartFrame, Color.white);
                    r.layers.Add(layers);
                }
            }
            float after = hx + Upgrades.MaxHearts * (HeartSize + HeartGap) + 6;
            Button(after, ly, 34, 30, "+", GameFonts.Title, 18f, () => ChangeUpgrade(kind, +1), kind.ToString());
            r.sub = Ui.Text(fill, "UpgSub" + kind, after + 42, ly, right - after - 42, 30, GameFonts.Body, 15f, Theme.GoldLabel, TextAlignmentOptions.Right);
            Ui.Image(fill, "UpgLine" + kind, 12, y + HeartRowH - 1, iw - 24, 1, Theme.Line);
            upgradeRows.Add(r);
            y += HeartRowH;
        }

        BookRow(Upgrades.Kind.Attack);
        BookRow(Upgrades.Kind.Defense);
        HeartRow(Upgrades.Kind.LifeFragments);
        HeartRow(Upgrades.Kind.ManaFragments);
        return y;
    }

    void ChangeUpgrade(Upgrades.Kind kind, int delta) => Notify?.Invoke(Upgrades.Change(kind, delta), false);

    void UpdateUpgrades(bool hasMouse, float mx, float my, bool click, ref bool handled)
    {
        foreach (var r in upgradeRows)
        {
            if (r.icon.sprite == null) r.icon.sprite = UpgradeIcon(r.kind);
            Ui.SetActive(r.icon, r.icon.sprite != null);
            if (r.hearts.Count == 0) { Ui.SetText(r.value, Upgrades.Display(r.kind)); continue; }

            int v = Upgrades.Get(r.kind);
            int full = v / Upgrades.FragmentsPerHeart, frags = v % Upgrades.FragmentsPerHeart;
            Ui.SetText(r.value, Upgrades.Known ? $"{full} / {Upgrades.MaxHearts}" : "—");
            Ui.SetText(r.sub, Upgrades.Known && frags > 0 ? $"+{frags}/{Upgrades.FragmentsPerHeart} pieces" : "");
            for (int i = 0; i < r.hearts.Count; i++)
            {
                var (img, x, y) = r.hearts[i];
                if (img.sprite == null) img.sprite = r.icon.sprite;
                bool hover = hasMouse && mx >= x - HeartGap / 2f && mx < x + HeartSize + HeartGap / 2f && my >= y && my < y + HeartSize;
                if (i < r.layers.Count)
                {
                    // Pièces affichées : 4 pour un cœur complet, celles trouvées pour le cœur en cours, aucune sinon
                    var lay = r.layers[i];
                    int pieces = i < full ? 4 : i == full ? frags : 0;
                    float shell = pieces > 0 || hover ? 1f : 0.45f;
                    lay[0].color = new Color(1f, 1f, 1f, shell);
                    for (int k = 1; k <= 4; k++) Ui.SetActive(lay[k], k <= pieces);
                    lay[5].color = hover ? Theme.Hex(0xffc890) : new Color(1f, 1f, 1f, shell);
                    if (hover && click && !handled)
                    {
                        handled = true;
                        int tgt = (i + 1) * Upgrades.FragmentsPerHeart;
                        if (v == tgt) tgt = i * Upgrades.FragmentsPerHeart;
                        ChangeUpgrade(r.kind, tgt - v);
                    }
                    continue;
                }
                // Plein, partiel (morceaux déjà trouvés) ou vide
                float a = i < full ? 1f : i == full && frags > 0 ? 0.3f + 0.15f * frags : 0.18f;
                float tint = i < full || hover ? 1f : 0.55f;
                img.color = new Color(tint, tint, tint, hover ? Mathf.Max(a, 0.75f) : a);
                if (hover && click && !handled)
                {
                    handled = true;
                    // Clic sur le dernier cœur plein : on le retire ; sinon on remplit jusqu'à ce cœur
                    int target = (i + 1) * Upgrades.FragmentsPerHeart;
                    if (v == target) target = i * Upgrades.FragmentsPerHeart;
                    ChangeUpgrade(r.kind, target - v);
                }
            }
        }
    }
    // ---------- Armes ----------

    const float WeaponTile = 60f, WeaponGap = 8f, WeaponRowGap = 8f;
    // Armurerie : médaillon de famille, 4 armes espacées, lignes aérées ; reliques espacées
    // Cases élargies pour occuper toute la largeur du panneau (jusqu'à la barre de défilement)
    const float ArmTile = 74f, ArmGap = 21f, ArmRowGap = 20f, Medallion = 56f, RelicGap = 21f;
    const int GridCols = 4; // 4 par ligne, comme l'Armurerie
    int weaponsBuiltFor = -1, relicsBuiltFor = -1;
    class RelicUi { public Relics.Relic relic; public ItemTile tile; }
    readonly List<RelicUi> relicTiles = new();
    TextMeshProUGUI relicsCount;

    class WeaponUi { public Weapons.Weapon weapon; public ItemTile tile; }
    readonly List<WeaponUi> weaponTiles = new();
    TextMeshProUGUI weaponDetailTitle, weaponDetailText;

    void BuildWeapons(float y)
    {
        weaponTiles.Clear();
        float iw = W - 1;
        y = SectionTitle("Weapons", y);
        if (Weapons.Families.Count == 0)
        {
            Ui.Text(fill, "WeaponsWaiting", Pad, y, iw - Pad * 2, 30, GameFonts.Body, 19f, Theme.TextDim, TextAlignmentOptions.Center).text = "Load a game to see the weapons";
            y += 40;
        }
        relicTiles.Clear();
        y += 6;

        // Une ligne par famille, comme l'Armurerie : médaillon rond à gauche, filet qui traverse, 4 armes
        float tilesX = Pad + Medallion + 16;
        for (int r = 0; r < Weapons.Families.Count; r++)
        {
            var fam = Weapons.Families[r];
            float ry = y + r * (ArmTile + ArmRowGap), mid = ry + ArmTile / 2f;
            if (Skin.RowSupport != null) Skin.Sliced(fill, $"WRow{r}Line", Pad + Medallion / 2f, mid - 12, iw - Pad * 2 - Medallion / 2f, 24, Skin.RowSupport, Theme.Hex(0xb8a070), 8f);
            else Ui.Image(fill, $"WRow{r}Line", Pad, mid, iw - Pad * 2, 1, Theme.LineGold);

            float mcx = Pad + Medallion / 2f;
            Ui.Image(fill, $"WFam{r}Bg", mcx - Medallion / 2f + 4, mid - Medallion / 2f + 4, Medallion - 8, Medallion - 8, Theme.Hex(0x0b0706)).sprite = Skin.Disc;
            var famIcon = Skin.Family(fam.Key);
            if (famIcon != null) { var fi = Skin.Sprite(fill, $"WFam{r}Icon", mcx - 18, mid - 18, 36, 36, famIcon, Theme.GoldText); fi.preserveAspect = true; }
            if (Skin.Circle != null) Skin.Sprite(fill, $"WFam{r}Ring", mcx - Medallion / 2f, mid - Medallion / 2f, Medallion, Medallion, Skin.Circle, Theme.GoldBright);

            for (int c = 0; c < fam.Weapons.Count && c < GridCols; c++)
            {
                float tx = tilesX + c * (ArmTile + ArmGap);
                var tile = ItemTile.Build(fill, $"W{r}_{c}", tx, ry, ArmTile);
                tile.frameTint = Skin.TierTint(fam.Weapons[c].Tier);
                weaponTiles.Add(new WeaponUi { weapon = fam.Weapons[c], tile = tile });
            }
        }
        y += Weapons.Families.Count * (ArmTile + ArmRowGap) + 4;
        float x0 = tilesX; // reliques alignées sur les colonnes d'armes

        // --- Reliques : 3 emplacements équipables, toutes affichées, 4 par ligne ---
        relicsBuiltFor = Relics.All.Count;
        y = SectionTitle("Relics", y);
        relicsCount = Ui.Text(fill, "RelicsCount", Pad, y - 8, iw - Pad * 2, 22, GameFonts.Body, 16f, Theme.GoldLabel, TextAlignmentOptions.Center);
        y += 16;
        for (int i = 0; i < Relics.All.Count; i++)
        {
            float tx = x0 + (i % GridCols) * (ArmTile + RelicGap), ty = y + (i / GridCols) * (ArmTile + RelicGap);
            relicTiles.Add(new RelicUi { relic = Relics.All[i], tile = ItemTile.Build(fill, $"R{i}", tx, ty, ArmTile) });
        }
        if (Relics.All.Count == 0)
            Ui.Text(fill, "RelicsWaiting", Pad, y, iw - Pad * 2, 30, GameFonts.Body, 19f, Theme.TextDim, TextAlignmentOptions.Center).text = "Load a game to see the relics";
        y += Math.Max(1, (Relics.All.Count + GridCols - 1) / GridCols) * (ArmTile + RelicGap) + 8;

        contentH[(int)Tab.Weapons] = y + 8;
    }

    void UpdateWeapons(bool hasMouse, float mx, float my, bool click, bool rightClick, ref bool handled)
    {
        string title = "Weapons", text = "Hover a weapon to see what it does. Click: unlock or remove it. Right-click: equip it.";
        foreach (var t in weaponTiles)
        {
            bool? owned = Weapons.IsOwned(t.weapon);
            bool equipped = owned == true && Weapons.IsEquipped(t.weapon);
            bool hover = hasMouse && t.tile.Contains(mx, my);

            var sprite = ItemIcons.Get(t.weapon.Item);
            if (sprite != null && t.tile.icon.sprite == null) t.tile.icon.sprite = sprite;
            Ui.SetActive(t.tile.icon, t.tile.icon.sprite != null);
            t.tile.Apply(hover, owned == true, equipped);

            if (hover)
            {
                title = $"{t.weapon.Name} · tier {t.weapon.Tier}";
                text = (owned == true ? (equipped ? "Owned · equipped. " : "Owned. ") : "Not owned. ") + t.weapon.Description;
                string special = t.weapon.Special;
                if (!string.IsNullOrEmpty(special)) text += "  " + special;
                if (click && !handled) { handled = true; Notify?.Invoke(Weapons.Toggle(t.weapon), false); }
                if (rightClick && !handled) { handled = true; Notify?.Invoke(Weapons.Equip(t.weapon), false); }
            }
        }
        foreach (var t in relicTiles)
        {
            bool? owned = Relics.IsOwned(t.relic);
            bool equipped = owned == true && Relics.IsEquipped(t.relic);
            bool hover = hasMouse && t.tile.Contains(mx, my);

            var sprite = ItemIcons.Get(t.relic.Item);
            if (sprite != null && t.tile.icon.sprite == null) t.tile.icon.sprite = sprite;
            Ui.SetActive(t.tile.icon, t.tile.icon.sprite != null);
            t.tile.Apply(hover, owned == true, equipped);

            if (hover)
            {
                title = t.relic.Name;
                text = (owned == true ? (equipped ? "Owned · equipped. " : "Owned. ") : "Not owned. ") + t.relic.Description
                    + "  Click: unlock or remove. Right-click: equip or unequip.";
                if (click && !handled) { handled = true; Notify?.Invoke(Relics.Toggle(t.relic), false); }
                if (rightClick && !handled) { handled = true; Notify?.Invoke(Relics.ToggleEquip(t.relic), false); }
            }
        }
        if (relicsCount != null) Ui.SetText(relicsCount, $"Equipped {Relics.EquippedCount()} / {Relics.SlotsCount()}");

        Ui.SetText(weaponDetailTitle, title);
        Ui.SetText(weaponDetailText, text);
    }

    // ---------- Sorts ----------

    const float SpellCellH = 60f, SpellIcon = 48f, TalentSize = 20f;
    float spellsTop;
    int spellsBuiltFor = -1;

    class TalentUi { public Arcana.Talent talent; public Image border, fill, gem, gemHover; public float cx, cy; }
    class SpellUi
    {
        public Arcana.Spell spell;
        public Image highlight, icon, line, equipped, frame, glow, spikes;
        public GameObject iconRoot;
        public SpellItemData shownItem;
        public GameObject fallback;
        public List<TalentUi> talents = new();
        public float x, y, w;
    }
    readonly List<SpellUi> spellCells = new();
    TextMeshProUGUI detailTitle, detailText;

    /// <summary>
    /// Losange doré contenant une illustration : carré tourné de 45° avec un Mask uGUI,
    /// l'image à l'intérieur est contre-tournée pour rester droite. Retourne l'image de l'illustration.
    /// </summary>
    static Image DiamondArt(Transform parent, string name, float cx, float cy, float side, out GameObject root)
    {
        var holder = Ui.Rect(parent, name, cx - side / 2f, cy - side / 2f, side, side);
        root = holder.gameObject;

        var border = Ui.Image(holder, "Border", 0, 0, side, side, Theme.GoldBright);
        border.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        border.rectTransform.anchoredPosition = new Vector2(side / 2f, -side / 2f);
        border.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);

        var maskImg = Ui.Image(border.transform, "Mask", 2f, 2f, side - 4f, side - 4f, new Color(1f, 1f, 1f, 1f));
        var mask = maskImg.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        // Illustration droite, assez grande pour couvrir tout le losange
        float artW = (side - 4f) * 1.45f;
        var art = Ui.Image(maskImg.transform, "Art", 0, 0, artW, artW * 1.6f, new Color(1f, 1f, 1f, 1f));
        art.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        art.rectTransform.anchoredPosition = new Vector2((side - 4f) / 2f, -(side - 4f) / 2f);
        art.rectTransform.localEulerAngles = new Vector3(0f, 0f, -45f);
        art.preserveAspect = true;
        return art;
    }

    void BuildSpells(float y)
    {
        spellCells.Clear();
        float iw = W - 1;
        float cellW = (iw - Pad * 2 - Gap) / 2f;
        var spells = Arcana.Spells;
        if (spells.Count == 0)
        {
            Ui.Text(fill, "SpellsWaiting", Pad, y, iw - Pad * 2, 30, GameFonts.Body, 19f, Theme.TextDim, TextAlignmentOptions.Center).text = "Load a game to see the spells";
            y += 40;
        }
        for (int i = 0; i < spells.Count; i++)
        {
            float cx = Pad + (i % 2) * (cellW + Gap), cy = y + (i / 2) * SpellCellH;
            var c = new SpellUi { spell = spells[i], x = cx, y = cy, w = cellW };
            c.highlight = Ui.Image(fill, $"Spell{i}Hl", cx, cy, cellW, SpellCellH - 4, Theme.Hex(0x5e1418, 0.55f));
            c.highlight.gameObject.SetActive(false);

            // Icône du sort (les icônes d'arcanes du jeu sont déjà en losange)
            float iconX = cx + 4, iconY = cy + (SpellCellH - 4 - SpellIcon) / 2f;
            float icx = iconX + SpellIcon / 2f, icy = iconY + SpellIcon / 2f;
            // Losange du jeu derrière l'illustration (lueur orangée dessous au survol)
            if (Skin.Diamond != null)
            {
                if (Skin.GlowDiamond != null) c.glow = Skin.ShapeGlow(fill, $"Spell{i}Glow", icx, icy, 54, Skin.GlowDiamond);
                c.frame = Skin.Sprite(fill, $"Spell{i}Frame", icx - 27, icy - 27, 54, 54, Skin.Diamond, Color.white);
            }
            var d = Ui.Diamond(fill, $"Spell{i}Fallback", iconX + SpellIcon / 2f, iconY + SpellIcon / 2f, 32, GameFonts.Numbers, 16f);
            d.label.text = "";
            c.fallback = d.border.transform.parent.gameObject;
            // Losange comme dans le menu Arcanes : l'illustration de la carte cadrée dans un masque en losange
            c.icon = DiamondArt(fill, $"Spell{i}Icon", iconX + SpellIcon / 2f, iconY + SpellIcon / 2f, SpellIcon * 0.72f, out c.iconRoot);
            c.iconRoot.SetActive(false);
            if (c.frame != null)
            {
                // Le losange du jeu sert de cadre : on masque le liseré doré de DiamondArt
                var artBorder = c.iconRoot.transform.GetChild(0).GetComponent<Image>();
                if (artBorder != null) artBorder.color = new Color(0f, 0f, 0f, 0f);
            }
            // Marque « équipé » (sans les assets) : petit losange doré sous l'icône
            c.equipped = Ui.Image(fill, $"Spell{i}Eq", 0, 0, 7, 7, Theme.GoldText);
            c.equipped.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            c.equipped.rectTransform.anchoredPosition = new Vector2(iconX + SpellIcon / 2f, -(iconY + SpellIcon + 1));
            c.equipped.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);

            // Ligne horizontale portant les 3 améliorations
            float lineX = iconX + SpellIcon + 6, lineW = cx + cellW - 10 - lineX, midY = cy + (SpellCellH - 4) / 2f;
            c.line = Ui.Image(fill, $"Spell{i}Line", lineX, midY, lineW, 1, Theme.Gold);
            int n = c.spell.Talents.Length;
            for (int k = 0; k < n; k++)
            {
                float tcx = lineX + lineW * (k + 1) / (n + 1f);
                if (Skin.GemLocked != null && Skin.GemUnlocked != null)
                {
                    // Gemme du jeu : emplacement sombre (verrouillée) ou gemme (débloquée), halo doré au survol
                    var gem = Skin.Sprite(fill, $"Spell{i}T{k}", tcx - 14, midY - 14, 28, 28, Skin.GemLocked, Color.white);
                    Image halo = null;
                    if (Skin.GemHover != null) { halo = Skin.Sprite(fill, $"Spell{i}T{k}Hover", tcx - 20, midY - 20, 40, 40, Skin.GemHover, Color.white); halo.gameObject.SetActive(false); }
                    c.talents.Add(new TalentUi { talent = c.spell.Talents[k], gem = gem, gemHover = halo, cx = tcx, cy = midY });
                    continue;
                }
                var td = Ui.Diamond(fill, $"Spell{i}T{k}", tcx, midY, TalentSize, GameFonts.Numbers, 12f);
                td.label.text = "";
                c.talents.Add(new TalentUi { talent = c.spell.Talents[k], border = td.border, fill = td.fill, cx = tcx, cy = midY });
            }
            spellCells.Add(c);
        }
        y += (spells.Count + 1) / 2 * SpellCellH + 10;

        // Zone de détail : nom et effet de l'élément survolé (comme le panneau de droite des arcanes du jeu)
        contentH[(int)Tab.Skills] = y + 8;
    }

    void UpdateSpells(bool hasMouse, float mx, float my, bool click, ref bool handled)
    {
        bool rightClick = hasMouse && UiCanvas.RightClicked;
        string title = "Skills", text = "Hover a spell or an upgrade to see what it does. Click: unlock or remove. Right-click a spell row: switch variant.";
        foreach (var c in spellCells)
        {
            bool? owned = Arcana.IsOwned(c.spell);
            bool inCell = hasMouse && mx >= c.x && mx < c.x + c.w && my >= c.y && my < c.y + SpellCellH - 4;
            // Toute la ligne vaut pour le sort, sauf au-dessus d'une amélioration
            bool overTalent = false;
            foreach (var t in c.talents)
                if (inCell && Mathf.Abs(mx - t.cx) <= TalentSize * 0.75f && Mathf.Abs(my - t.cy) <= TalentSize * 0.75f) overTalent = true;
            bool overIcon = inCell && !overTalent;

            // Illustration de la variante affichée (change avec Cursed Flame / Absolution)
            if (c.shownItem != c.spell.Item) { c.shownItem = c.spell.Item; c.icon.sprite = null; }
            var sprite = ItemIcons.Get(c.spell.Item) ?? c.spell.DiamondIcon();
            if (sprite != null && c.icon.sprite == null) c.icon.sprite = sprite;
            bool hasIcon = c.icon.sprite != null;
            if (c.iconRoot.activeSelf != hasIcon) c.iconRoot.SetActive(hasIcon);
            if (c.fallback.activeSelf == hasIcon) c.fallback.SetActive(!hasIcon);
            c.icon.color = owned == true ? new Color(1f, 1f, 1f, 1f) : new Color(0.3f, 0.27f, 0.25f, 0.75f);
            bool spellEquipped = owned == true && Arcana.IsEquipped(c.spell);
            Ui.SetActive(c.equipped, spellEquipped && c.frame == null);
            if (c.frame != null)
            {
                var fs = overIcon && Skin.DiamondHover != null ? Skin.DiamondHover : Skin.Diamond;
                if (c.frame.sprite != fs) c.frame.sprite = fs;
                c.frame.color = owned == true || overIcon ? Color.white : new Color(0.55f, 0.5f, 0.45f, 1f);
                Skin.ApplyGlow(c.glow, overIcon, spellEquipped);
            }
            c.line.color = owned == true ? Theme.GoldBright : Theme.Line;
            Ui.SetActive(c.highlight, overIcon);

            if (overIcon)
            {
                title = c.spell.Name;
                text = (owned == true ? (Arcana.IsEquipped(c.spell) ? "Owned · equipped. " : "Owned. ") : "Not owned. ") + c.spell.Description;
                var next = c.spell.NextVariant();
                if (next != null) text += $"  Right-click: switch to {Loc.Get(next.itemDisplayName, next.name)}.";
                if (click && !handled) { handled = true; Notify?.Invoke(Arcana.ToggleSpell(c.spell), false); }
                if (rightClick && !handled && next != null) { handled = true; Notify?.Invoke(Arcana.SwitchVariant(c.spell), false); }
            }

            foreach (var t in c.talents)
            {
                bool? on = Arcana.IsUnlocked(t.talent);
                bool hover = inCell && Mathf.Abs(mx - t.cx) <= TalentSize * 0.75f && Mathf.Abs(my - t.cy) <= TalentSize * 0.75f;
                if (t.gem != null)
                {
                    var gs = on == true ? Skin.GemUnlocked : Skin.GemLocked;
                    if (t.gem.sprite != gs) t.gem.sprite = gs;
                    Ui.SetActive(t.gemHover, hover);
                }
                else
                {
                    t.fill.color = on == true ? Theme.GoldText : Theme.BgSolid;
                    t.border.color = hover ? Theme.Red : on == true ? Theme.GoldText : Theme.Gold;
                }
                if (hover)
                {
                    title = $"{c.spell.Name} · upgrade {t.talent.Index + 1}";
                    text = (on == true ? "Unlocked. " : "Locked. ") + t.talent.Description;
                    if (click && !handled) { handled = true; Notify?.Invoke(Arcana.ToggleTalent(t.talent), false); }
                }
            }
        }
        if (detailTitle != null) { Ui.SetText(detailTitle, title); Ui.SetText(detailText, text); }
    }

    float SectionTitle(string text, float y)
    {
        float iw = W - 1;
        // Ruban bordeaux du jeu ; à défaut, titre doré entre deux filets
        if (Skin.BannerSprite != null) return Skin.Banner(fill, text, Pad, y + 6, iw - Pad * 2, 50, GameFonts.Title) + 14;
        var title = Ui.Text(fill, "Section" + text, Pad, y, iw - Pad * 2, TitleH, GameFonts.Title, 32f, Theme.GoldText, TextAlignmentOptions.Center);
        title.text = text;
        float tw = Mathf.Min(iw - Pad * 2 - 40, title.preferredWidth + 24);
        float side = (iw - Pad * 2 - tw) / 2f - 8;
        if (side > 10)
        {
            Ui.Image(fill, "LineL" + text, Pad, y + TitleH / 2f, side, 1, Theme.LineGold);
            Ui.Image(fill, "LineR" + text, iw - Pad - side, y + TitleH / 2f, side, 1, Theme.LineGold);
        }
        return y + TitleH + 6;
    }

    TileUi BuildTile(Abilities.Move move, float x, float y, TMP_FontAsset font)
    {
        float w = TileW;
        var bgImg = Ui.Frame(fill, "Tile" + move.Key, x, y, w, TileH, Theme.Gold, Theme.TileOff);
        var tile = new TileUi { move = move, bg = bgImg, x = x, y = y, w = w, h = TileH };

        float iconY = 8;
        var d = Ui.Diamond(bgImg.transform, "Fallback", (w - 2) / 2f, iconY + IconSize / 2f, 28, GameFonts.Numbers, 16f);
        d.label.text = "";
        tile.fallback = d.border.transform.parent.gameObject;
        tile.icon = Ui.Image(bgImg.transform, "Icon", (w - 2 - IconSize) / 2f, iconY, IconSize, IconSize, new Color(1f, 1f, 1f, 1f));
        tile.icon.preserveAspect = true;
        tile.icon.gameObject.SetActive(false);

        tile.name = Ui.Text(bgImg.transform, "Name", 2, iconY + IconSize + 2, w - 6, TileH - IconSize - iconY - 6, font, 15f, Theme.Text, TextAlignmentOptions.Top);
        tile.name.enableWordWrapping = true;
        tile.name.overflowMode = TextOverflowModes.Overflow;
        tile.name.text = move.DisplayName;
        tile.shownName = move.DisplayName;
        return tile;
    }

    UiButton Button(float x, float y, float w, float h, string text, TMP_FontAsset font, float size, Action onClick, string id = "")
    {
        var btn = UiButton.CreateSkinned(fill, "Btn" + id + text, x, y, w, h, text, font, size, onClick);
        buttons.Add((btn, x, y, w, h, buildPage));
        return btn;
    }

    void ToggleAllMovement()
    {
        bool allOn = true;
        foreach (var m in Abilities.Traversal) if (Abilities.IsUnlocked(m) == false) allOn = false;
        foreach (var m in Abilities.Traversal)
        {
            var on = Abilities.IsUnlocked(m);
            if (on == null || on.Value != allOn) continue;
            Abilities.Toggle(m, out _);
        }
        Notify?.Invoke(allOn ? "All movement disabled" : "All movement enabled", false);
    }

    public void Update(bool visible)
    {
        if (!visible) { if (panel != null && panel.activeSelf) panel.SetActive(false); wasVisible = false; Keybinds.Capturing = null; return; }
        // À l'ouverture : relit les améliorations (Attack+, cœurs…)
        if (!wasVisible) { wasVisible = true; Upgrades.Refresh(true); }

        UiCanvas.Ensure();
        if (!GameFonts.Resolved) GameFonts.Resolve();
        Abilities.Resolve();
        Arcana.Resolve();
        Weapons.Resolve();
        Relics.Resolve();
        // Reconstruit quand les polices ou les listes (sorts, armes) deviennent disponibles
        if (panel == null || (!builtWithGameFonts && GameFonts.Resolved) || spellsBuiltFor != Arcana.Spells.Count
            || weaponsBuiltFor != Weapons.Families.Count || relicsBuiltFor != Relics.All.Count) Build();
        if (!panel.activeSelf) panel.SetActive(true);

        bool hasMouse = UiCanvas.Mouse(out float mx, out float my);
        mx -= UiCanvas.Width - W + 1f;
        bool click = hasMouse && UiCanvas.Clicked;
        bool handled = false;

        UpdateTabs(hasMouse, mx, my, click, ref handled);

        // Défilement à la molette (au-dessus du panneau)
        int ti = (int)tab;
        float viewH = UiCanvas.Height - DetailAreaH;
        float maxScroll = Mathf.Max(0f, contentH[ti] - viewH);
        float scroll = scrollPos[ti];
        if (hasMouse && mx >= 0f && Mouse.current != null)
        {
            float wheel = Mouse.current.scroll.ReadValue().y;
            if (wheel > 0.01f) scroll -= 90f; else if (wheel < -0.01f) scroll += 90f;
        }
        // Barre de défilement : glisser le curseur (ou cliquer sur la piste)
        float trackTop = viewportTop, trackH = UiCanvas.Height - DetailAreaH - viewportTop - 4;
        float thumbH = maxScroll > 0f ? Mathf.Max(30f, trackH * trackH / (trackH + maxScroll)) : trackH;
        bool overTrack = hasMouse && mx >= W - 1 - ScrollbarW - 6 && my >= trackTop && my < trackTop + trackH;
        if (overTrack && click && maxScroll > 0f) { draggingScrollbar = true; handled = true; }
        if (draggingScrollbar)
        {
            if (Mouse.current == null || !Mouse.current.leftButton.isPressed) draggingScrollbar = false;
            else if (trackH > thumbH) scroll = (my - trackTop - thumbH / 2f) / (trackH - thumbH) * maxScroll;
        }
        scroll = Mathf.Clamp(scroll, 0f, maxScroll);
        scrollPos[ti] = scroll;
        bool showBar = maxScroll > 0f;
        Ui.SetActive(scrollTrack, showBar);
        Ui.SetActive(scrollThumb, showBar);
        if (showBar)
        {
            Ui.Place(scrollTrack.rectTransform, W - 1 - ScrollbarW - 3, trackTop, ScrollbarW, trackH);
            float thumbY = trackTop + (trackH - thumbH) * (scroll / maxScroll);
            Ui.Place(scrollThumb.rectTransform, W - 1 - ScrollbarW - 3, thumbY, ScrollbarW, thumbH);
            scrollThumb.color = draggingScrollbar || overTrack ? Theme.GoldText : Theme.GoldBright;
        }
        var page = pages[ti].GetComponent<RectTransform>();
        page.anchoredPosition = new Vector2(0f, viewportTop + scroll);
        // Coordonnées dans le contenu ; rien n'est survolé au-dessus de la zone défilante
        // Rien n'est survolé hors de la zone défilante (onglets en haut, zone de détail en bas)
        my = my < viewportTop || my >= UiCanvas.Height - DetailAreaH ? -10000f : my + scroll;
        if (tab == Tab.Weapons)
        {
            UpdateWeapons(hasMouse, mx, my, click, hasMouse && UiCanvas.RightClicked, ref handled);
            return;
        }
        if (tab == Tab.Keybinds)
        {
            UpdateKeybinds(hasMouse, mx, my, click, hasMouse && UiCanvas.RightClicked, ref handled);
            return;
        }

        foreach (var (btn, x, y, w, h, onPage) in buttons)
        {
            if (onPage != tab) continue;
            bool hover = hasMouse && mx >= x && mx < x + w && my >= y && my < y + h;
            btn.SetHover(hover);
            if (click && hover && !handled) { handled = true; btn.OnClick?.Invoke(); }
        }
        if (tab == Tab.General)
        {
            UpdateUtilities(hasMouse, mx, my, click, ref handled);
            UpdateUpgrades(hasMouse, mx, my, click, ref handled);
            Ui.SetText(detailTitle, "General");
            Ui.SetText(detailText, "Click a utility to turn it on or off. Health, mana and potions: use the buttons or click a diamond. Hearts: click a heart to set how many you have.");
            return;
        }

        bool allOn = true;
        foreach (var tile in tiles)
        {
            bool? on = Abilities.IsUnlocked(tile.move);
            if (on != true) allOn = false;
            bool hover = hasMouse && mx >= tile.x && mx < tile.x + tile.w && my >= tile.y && my < tile.y + tile.h;

            var sprite = ItemIcons.Get(tile.move.Item);
            if (sprite != null && tile.icon.sprite == null) tile.icon.sprite = sprite;
            bool hasIcon = tile.icon.sprite != null;
            if (tile.icon.gameObject.activeSelf != hasIcon) tile.icon.gameObject.SetActive(hasIcon);
            if (tile.fallback.activeSelf == hasIcon) tile.fallback.SetActive(!hasIcon);

            tile.bg.color = hover ? Theme.Crimson : on == true ? Theme.TileOn : Theme.TileOff;
            Ui.SetBorder(tile.bg, on == true ? Theme.GoldBright : Theme.Line);
            tile.icon.color = on == true ? new Color(1f, 1f, 1f, 1f) : new Color(0.35f, 0.31f, 0.28f, 0.8f);
            tile.name.color = on == true ? Theme.Text : Theme.TextDim;
            // Le nom officiel arrive quand l'objet du jeu est chargé
            string dn = tile.move.DisplayName;
            if (dn != tile.shownName) { tile.shownName = dn; tile.name.text = dn; }

            if (click && hover && !handled)
            {
                handled = true;
                bool ok = Abilities.Toggle(tile.move, out string msg);
                if (msg != null) Notify?.Invoke(msg, !ok);
            }
        }
        allIndicator.color = allOn ? Theme.GoldText : Theme.Line;
        UpdateSpells(hasMouse, mx, my, click, ref handled);
    }
}

using System;
using System.Globalization;
using Il2CppInterop.Runtime;
using MotherBase.EntityFramework;
using UnityEngine;

namespace BelmontPractice;

/// <summary>
/// Stats du personnage, en lecture seule (onglet General) : le menu du jeu n'affiche que ATK et DEF, mais la formule
/// de dégâts (ADamagesFormula) applique aussi des multiplicateurs (arme, coup de dos, dégâts reçus…).
/// </summary>
public static class PlayerStats
{
    public class Entry
    {
        public string Label, Help;
        public Il2CppSystem.Type Type;          // stat lue dans le StatsStateComponent
        public Func<Entity, StatsStateComponent, float?> Custom; // valeur calculée (ATK / DEF du menu)
        public bool Multiplier;
    }

    static Entry S<T>(string label, string help) where T : Il2CppSystem.Object =>
        new() { Label = label, Help = help, Type = Il2CppType.Of<T>(), Multiplier = typeof(T).Name.Contains("Multiplier") };

    static (string group, Entry[] entries)[] groups;

    public static (string group, Entry[] entries)[] Groups => groups ??= new[]
    {
        ("Attack", new[]
        {
            new Entry { Label = "ATK (menu)", Help = "Attack as shown in the game's menu, computed by the game's damage formula.",
                Custom = (e, s) => Formula()?.GetPowerAttack(e, s) },
            S<PowerAttack>("Attack power", "Attack stat (PowerAttack)."),
            S<PowerAttackBonus>("Attack+ bonus", "Bonus from the Attack+ books (PowerAttackBonus)."),
            S<Power>("Power", "Power stat (Power)."),
            S<AttackWeapon>("Weapon attack", "Attack of the equipped weapon (AttackWeapon)."),
            S<AttackArcana>("Arcana attack", "Attack of arcana spells (AttackArcana)."),
            S<DamagesInflictedMultiplier>("Damage dealt", "Multiplies all the damage you deal (DamagesInflictedMultiplier)."),
            S<WeaponDamagesInflictedMultiplier>("Weapon damage", "Multiplies the damage of weapon hits (WeaponDamagesInflictedMultiplier)."),
            S<FrontHitDamageMultiplier>("Front hits", "Multiplies the damage of hits in an enemy's face (FrontHitDamageMultiplier)."),
            S<BackHitDamageMultiplier>("Back hits", "Multiplies the damage of hits in an enemy's back (BackHitDamageMultiplier)."),
            S<PotionCountDamageMultiplier>("Potion count damage", "Damage multiplier tied to the number of potions (PotionCountDamageMultiplier)."),
            S<StaggerPower>("Stagger power", "How hard your hits break an enemy's guard (StaggerPower)."),
        }),
        ("Defense", new[]
        {
            new Entry { Label = "DEF (menu)", Help = "Defense as shown in the game's menu, computed by the game's damage formula.",
                Custom = (e, s) => Formula()?.GetPowerDefense(e, s) },
            S<PowerDefense>("Defense power", "Defense stat (PowerDefense)."),
            S<PowerDefenseBonus>("Defense+ bonus", "Bonus from the Defense+ books (PowerDefenseBonus)."),
            S<DefenseOnLowLife>("Defense at low life", "Extra defense while your life is low (DefenseOnLowLife)."),
            S<DamagesReceivedMultiplier>("Damage taken", "Multiplies all the damage you take (DamagesReceivedMultiplier)."),
            S<FrontHitDamageReceivedMultiplier>("Hits from the front", "Multiplies the damage of hits taken from the front (FrontHitDamageReceivedMultiplier)."),
            S<BackHitDamageReceivedMultiplier>("Hits from behind", "Multiplies the damage of hits taken in the back (BackHitDamageReceivedMultiplier)."),
            S<TrapDamagesReceivedMultiplier>("Trap damage", "Multiplies the damage taken from traps (TrapDamagesReceivedMultiplier)."),
            S<InvulnerabilityFramesMultiplier>("Invulnerability time", "Length of the invulnerability after a hit (InvulnerabilityFramesMultiplier)."),
        }),
        ("Other", new[]
        {
            S<Level>("Level", "Character level (Level)."),
            S<HealthMax>("Max HP", "Maximum health (HealthMax)."),
            S<SpellManaMax>("Max mana", "Maximum mana (SpellManaMax)."),
            S<SpellManaMultiplier>("Mana gain", "Multiplies the mana you get (SpellManaMultiplier)."),
            S<SpellManaCostFlatReduction>("Spell cost reduction", "Flat reduction of spell mana costs (SpellManaCostFlatReduction)."),
            S<ManaPerHit>("Mana per hit", "Mana gained per hit (ManaPerHit)."),
            S<HealPerKill>("Heal per kill", "Health gained per kill (HealPerKill)."),
            S<MCSpeedMultiplier>("Speed", "Movement speed multiplier (MCSpeedMultiplier)."),
            S<XPAquisitionMultiplier>("XP gain", "Multiplies the experience you get (XPAquisitionMultiplier)."),
            S<ConsummableChanceMultiplier>("Drop chance", "Multiplies the chance of consumable drops (ConsummableChanceMultiplier)."),
            S<CharmsSlotsCount>("Relic slots", "Number of relic slots (CharmsSlotsCount)."),
            S<MinorArcanaSlotsCount>("Minor arcana slots", "Number of minor arcana slots (MinorArcanaSlotsCount)."),
        }),
    };

    static ADamagesFormula formula;
    static float nextFormulaSearch;

    /// <summary>Formule de dégâts du jeu (DefaultDamagesFormula), retrouvée parmi les données chargées.</summary>
    public static ADamagesFormula Formula()
    {
        if (formula != null || Time.unscaledTime < nextFormulaSearch) return formula;
        nextFormulaSearch = Time.unscaledTime + 2f;
        try
        {
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<DefaultDamagesFormula>()))
            {
                formula = o.TryCast<ADamagesFormula>();
                if (formula != null) { Plugin.Logger.LogInfo($"[Stats] formule de dégâts : '{formula.name}' = {formula.GetFormulaString()}"); break; }
            }
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Stats] formule : " + ex.Message); }
        return formula;
    }

    public static string FormulaText()
    {
        try { var f = Formula(); return f == null ? "Not loaded yet (fight an enemy once)." : f.GetFormulaString(); }
        catch (Exception ex) { return "Unreadable: " + ex.Message; }
    }

    /// <summary>Valeur affichée d'une stat (« — » si inconnue).</summary>
    public static string Value(Entry en)
    {
        try
        {
            if (!MainCharacterSingleton.TryGetEntity(out Entity e) || e == null) return "—";
            var s = e.GetComponent<StatsStateComponent>();
            if (s == null) return "—";
            float? v = en.Custom != null ? en.Custom(e, s) : s.HasStat(en.Type) ? s.GetStatObject(en.Type).GetFloat(e) : null;
            if (v == null) return "—";
            var inv = CultureInfo.InvariantCulture;
            if (en.Multiplier) return "×" + v.Value.ToString("0.00", inv);
            return v.Value.ToString(Mathf.Approximately(v.Value, Mathf.Round(v.Value)) ? "0" : "0.##", inv);
        }
        catch { return "—"; }
    }
}

using System;
using MotherBase.EntityFramework;
using UnityEngine;

namespace BelmontPractice;

/// <summary>
/// Améliorations permanentes : Attack+, Defense+, cœurs de vie et de mana (4 fragments = 1 amélioration).
/// Elles sont stockées dans la sauvegarde du joueur (PlayerSave) par le seul StatsStateComponent ;
/// il recalcule PV max, mana max… en la relisant. Pour modifier : ce composant écrit son état dans la
/// sauvegarde en mémoire, on change le compteur, puis il la relit. (Faire relire toute la sauvegarde avec
/// SaveLogic.LoadState réinitialisait tous les composants du joueur et ralentissait le jeu à chaque clic.)
/// </summary>
public static class Upgrades
{
    public enum Kind { Attack, Defense, LifeFragments, ManaFragments }
    public const int FragmentsPerHeart = 4;
    public const int MaxHearts = 6; // 6 cœurs de vie et 6 de mana dans le jeu
    public const int MaxFragments = MaxHearts * FragmentsPerHeart;

    // Dernières valeurs lues (rafraîchies à l'ouverture du panneau et après chaque modification)
    public static int Attack, Defense, LifeFragments, ManaFragments;
    public static bool Known { get; private set; }
    static float nextRefresh;

    static bool Ctx(out Entity e, out StatsStateComponent stats, out PlayerSave ps)
    {
        stats = null;
        ps = null;
        if (!MainCharacterSingleton.TryGetEntity(out e) || e == null) return false;
        stats = e.GetComponent<StatsStateComponent>();
        var logic = SaveLogic.Get();
        if (stats == null || logic == null) return false;
        if (logic._currentSave?.playerSave == null) logic.GeneratePlayerSave(e, SaveOrigin.DebugSpawn); // une seule fois
        ps = logic._currentSave?.playerSave;
        if (ps == null) return false;
        stats.WritePlayerSave(ps, SaveOrigin.DebugSpawn);
        return true;
    }

    /// <summary>Relit les compteurs depuis l'état actuel du joueur (au plus toutes les 2 s, sauf si forcé).</summary>
    public static void Refresh(bool force = false)
    {
        if (!force && Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 2f;
        try
        {
            if (!Ctx(out _, out _, out var ps)) { Known = false; return; }
            Attack = ps.atkBonusUpgradeCount;
            Defense = ps.defBonusUpgradeCount;
            LifeFragments = ps.healthUpgradeFragmentCount;
            ManaFragments = ps.manaUpgradeFragmentCount;
            Known = true;
        }
        catch (Exception ex) { Known = false; Plugin.Logger.LogWarning("[Améliorations] lecture : " + ex.Message); }
    }

    public static int Get(Kind k) => k switch
    {
        Kind.Attack => Attack,
        Kind.Defense => Defense,
        Kind.LifeFragments => LifeFragments,
        _ => ManaFragments,
    };

    /// <summary>Ajoute delta au compteur (jamais en dessous de 0) et fait recalculer le jeu.</summary>
    public static string Change(Kind k, int delta)
    {
        try
        {
            if (!Ctx(out var e, out var stats, out var ps)) return "Unavailable (load a game first)";
            // PV / mana actuels : la relecture de la sauvegarde peut les remettre au max
            var dmg = e.GetComponent<Damageable>();
            var mana = e.GetComponent<ManaCaster>();
            int hp = dmg != null ? dmg.currentHealth : -1;
            float mp = mana != null ? mana.GetCurrentMana(e, mana.spellManaType) : -1f;

            switch (k)
            {
                case Kind.Attack: ps.atkBonusUpgradeCount = Math.Max(0, ps.atkBonusUpgradeCount + delta); break;
                case Kind.Defense: ps.defBonusUpgradeCount = Math.Max(0, ps.defBonusUpgradeCount + delta); break;
                case Kind.LifeFragments: ps.healthUpgradeFragmentCount = Math.Clamp(ps.healthUpgradeFragmentCount + delta, 0, MaxFragments); break;
                case Kind.ManaFragments: ps.manaUpgradeFragmentCount = Math.Clamp(ps.manaUpgradeFragmentCount + delta, 0, MaxFragments); break;
            }
            stats.ReadPlayerSave(ps);

            // Remet les PV / la mana d'avant (dans la limite du nouveau maximum)
            if (dmg != null && hp > 0) dmg.SetCurrentHealth(e, Math.Min(hp, dmg.maxHealth), true);
            if (mana != null && mp >= 0f) mana.SetCurrentMana(e, mana.spellManaType, Mathf.Min(mp, mana.GetMaxMana(e, mana.spellManaType)), false);

            Refresh(true);
            Cheats.RefreshBars(e);
            Plugin.Logger.LogInfo($"[Améliorations] {k} {(delta > 0 ? "+" : "")}{delta} → atk={Attack} def={Defense} vie={LifeFragments} mana={ManaFragments}");
            return $"{Label(k)}: {Display(k)}";
        }
        catch (Exception ex) { return "Change failed: " + ex.Message; }
    }

    // ---------- Niveau du personnage (1 à 99) ----------

    public const int MinLevel = 1, MaxLevel = 99;
    public static int Level;

    static ExperienceComponent Experience(out Entity e)
    {
        e = null;
        return MainCharacterSingleton.TryGetEntity(out e) && e != null ? e.GetComponent<ExperienceComponent>() : null;
    }

    /// <summary>Niveau actuel (0 si inconnu : pas en jeu).</summary>
    public static int ReadLevel()
    {
        try { Level = Experience(out _)?.GetLevel() ?? 0; }
        catch { Level = 0; }
        return Level;
    }

    /// <summary>Met le personnage au niveau voulu (expérience du niveau remise à 0), PV / mana gardés.</summary>
    public static string SetLevel(int target)
    {
        try
        {
            var xp = Experience(out var e);
            if (xp == null) return "Unavailable (load a game first)";
            target = Math.Clamp(target, MinLevel, MaxLevel);
            int before = xp.GetLevel();
            var dmg = e.GetComponent<Damageable>();
            var mana = e.GetComponent<ManaCaster>();
            int hp = dmg != null ? dmg.currentHealth : -1;
            float mp = mana != null ? mana.GetCurrentMana(e, mana.spellManaType) : -1f;

            xp.SetLevel(e, target);
            xp.SetExperienceCurrent(e, 0);

            if (dmg != null && hp > 0) dmg.SetCurrentHealth(e, Math.Min(hp, dmg.maxHealth), true);
            if (mana != null && mp >= 0f) mana.SetCurrentMana(e, mana.spellManaType, Mathf.Min(mp, mana.GetMaxMana(e, mana.spellManaType)), false);
            Cheats.RefreshBars(e);
            ReadLevel();
            Plugin.Logger.LogInfo($"[Niveau] {before} → {Level} (demandé {target})");
            return $"Level {Level}";
        }
        catch (Exception ex) { return "Level change failed: " + ex.Message; }
    }

    public static string Label(Kind k) => k switch
    {
        Kind.Attack => "Attack+",
        Kind.Defense => "Defense+",
        Kind.LifeFragments => "Life hearts",
        _ => "Mana hearts",
    };

    /// <summary>Texte affiché : nombre d'améliorations, et fragments restants pour les cœurs (ex. « 3 + 2/4 »).</summary>
    public static string Display(Kind k)
    {
        if (!Known) return "—";
        int v = Get(k);
        if (k is Kind.LifeFragments or Kind.ManaFragments)
        {
            int hearts = v / FragmentsPerHeart, frags = v % FragmentsPerHeart;
            return frags == 0 ? $"{hearts}/{MaxHearts}" : $"{hearts}/{MaxHearts} +{frags}";
        }
        return v.ToString();
    }
}

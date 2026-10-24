using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.U2D;
using UnityEngine.UI;

namespace BelmontPractice;

/// <summary>
/// Outil de développement (branche visuelle) : exporte en PNG les sprites et textures d'interface du jeu
/// listés ci-dessous, pour préparer les maquettes. Ne tourne que si BepInEx/export_assets.flag existe.
/// </summary>
public static class DevExport
{
    static readonly string[] Sources =
    {
        "Assets/_Object/UI/_CommonsInGame/1_Textures/T_UI_CommonGame_Slots.spriteatlasv2",
        "Assets/_Object/UI/_CommonsInGame/1_Textures/T_UI_CommonsAll_Utilitaries.spriteatlasv2",
        "Assets/_Object/UI/_CommonsInGame/1_Textures/T_UI_CommonAll_Cards.spriteatlasv2",
        "Assets/_Object/UI/GameMenus/Equipment/Overlay_WeaponInventory/1_Textures/T_UI_Overlay_WeaponInventory_atlas.spriteatlasv2",
        "Assets/_Object/UI/GameMenus/Equipment/Overlay_SpellInventory/1_Textures/T_UI_SpellInventory_SpriteAtlas.spriteatlasv2",
        "Assets/_Object/UI/GameMenus/Equipment/Overlay_CharmsInventory/5_Prefabs/InventoryCharm_ItemSlot.prefab",
        "Assets/_Object/UI/GameMenus/Equipment/Overlay_CharmsInventory/5_Prefabs/EquippedCharmSlot_Inventory.prefab",
        "Assets/_Object/UI/GameMenus/Equipment/Overlay_WeaponInventory/5_Prefabs/InventoryWeapon_ItemSlot.prefab",
        "Assets/_Object/UI/GameMenus/Equipment/Overlay_WeaponInventory/5_Prefabs/WeaponSlot_Selectable.prefab",
        "Assets/_Object/UI/GameMenus/Equipment/Overlay_SpellInventory/5_Prefabs/P_UI_SpellInventory_TalentGemSlot.prefab",
        "Assets/_Object/UI/GameMenus/Equipment/Overlay_SpellInventory/5_Prefabs/P_UI_SpellInventory_SpellList_Slot.prefab",
        "Assets/_Object/UI/_CommonsInGame/5_Prefabs/CommonUI_SpellSlot.prefab",
        "Assets/_Object/UI/GameMenus/Tarot/5_Prefabs/SpellSelection_CarouselIcon.prefab",
        "Assets/_Object/UI/GameMenus/Common_GameMenu/3_VFX/VFX_P_UI_Hover.prefab",
        "Assets/_Object/UI/GameMenus/Common_GameMenu/3_VFX/VFX_P_UI_HoverCard.prefab",
        "Assets/_Object/UI/GameMenus/Common_GameMenu/3_VFX/VFX_P_UI_Card_Hover.prefab",
        "Assets/_Object/UI/_CommonsInGame/SourcesSprites/T_CommonUI_SystemButtonBG.png",
        "Assets/_Object/UI/_CommonsInGame/SourcesSprites/T_CommonUI_SystemButtonHighlighted.png",
        "Assets/_Object/UI/_CommonsInGame/1_Textures/T_CommonUI_SystemButtonHighlightedComplete_Spritesheet.png",
        "Assets/_Object/UI/_CommonsInGame/1_Textures/T_UI_Card_EquippedFrame_Spritesheet.png",
        "Assets/_Object/UI/GameMenus/Common_GameMenu/1_Textures/T_UI_CommonGameMenu_Banner.png",
        "Assets/_Object/UI/GameMenus/Common_GameMenu/1_Textures/T_UI_CommonGameMenu_BannerBackground.png",
        "Assets/_Object/UI/GameMenus/Equipment/Overlay_WeaponInventory/SourcesSprites/T_UI_WeaponInventory_BannerTitle.png",
        "Assets/_Object/UI/GameMenus/Equipment/EquipmentView/SourcesSprites/T_UI_Equipment_Banner.png",
        "Assets/_Object/UI/HUD/NotificationsHUD/SourcesSprites/AnimatedOrnament/T_UI_OrnataCorner_01.png",
        "Assets/_Object/UI/_CommonsInGame/1_Textures/T_CommonUI_BarSelector.png",
        "Assets/_Object/UI/_CommonsInGame/1_Textures/T_CommonUI_BarSelector_Background.png",
    };

    static readonly Dictionary<string, AsyncOperationHandle<UnityEngine.Object>> handles = new();
    static readonly HashSet<string> done = new();
    static readonly HashSet<string> exported = new();
    static readonly StringBuilder log = new();
    static bool finished, enabled, checkedFlag;
    static string dir;

    public static void Update()
    {
        if (finished) return;
        if (!checkedFlag)
        {
            checkedFlag = true;
            enabled = File.Exists(Path.Combine(BepInEx.Paths.BepInExRootPath, "export_assets.flag"));
            dir = Path.Combine(BepInEx.Paths.BepInExRootPath, "assets_export");
            if (enabled) Directory.CreateDirectory(dir);
        }
        if (!enabled) { finished = true; return; }
        foreach (var key in Sources)
        {
            if (done.Contains(key)) continue;
            try
            {
                if (!handles.TryGetValue(key, out var h)) { handles[key] = Addressables.LoadAssetAsync<UnityEngine.Object>(key); continue; }
                if (!h.IsDone) continue;
                done.Add(key);
                log.AppendLine("== " + key);
                if (h.Status != AsyncOperationStatus.Succeeded || h.Result == null) { log.AppendLine("   (échec)"); continue; }
                Collect(h.Result);
            }
            catch (Exception ex) { done.Add(key); log.AppendLine($"== {key}\n   erreur : {ex.Message}"); }
        }
        if (done.Count == Sources.Length)
        {
            finished = true;
            File.WriteAllText(Path.Combine(dir, "_liste.txt"), log.ToString());
            Plugin.Logger.LogInfo($"[Export] {exported.Count} images exportées dans {dir}");
        }
    }

    static void Collect(UnityEngine.Object o)
    {
        var atlas = o.TryCast<SpriteAtlas>();
        if (atlas != null)
        {
            var arr = new Il2CppReferenceArray<Sprite>(atlas.spriteCount);
            atlas.GetSprites(arr);
            foreach (var s in arr) ExportSprite(s, "");
            return;
        }
        var go = o.TryCast<GameObject>();
        if (go != null)
        {
            foreach (var img in go.GetComponentsInChildren<Image>(true))
                if (img.sprite != null) ExportSprite(img.sprite, $"  ← {img.gameObject.name} (type {img.type}, couleur {img.color})");
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    var tex = m.mainTexture?.TryCast<Texture2D>();
                    log.AppendLine($"   rendu {r.gameObject.name} : matériau {m.name}, shader {m.shader?.name}, texture {(tex != null ? tex.name : "—")}");
                    if (tex != null) ExportTexture(tex);
                }
            return;
        }
        var sp = o.TryCast<Sprite>();
        if (sp != null) { ExportSprite(sp, ""); return; }
        var t = o.TryCast<Texture2D>();
        if (t != null) { ExportTexture(t); log.AppendLine($"   texture {t.name} [{t.width}x{t.height}]"); return; }
        log.AppendLine("   type : " + o.GetIl2CppType().Name);
    }

    static Texture2D Readable(Texture2D tex)
    {
        var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(tex, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
        copy.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return copy;
    }

    static void ExportTexture(Texture2D tex)
    {
        try
        {
            if (!exported.Add("tex:" + tex.name)) return;
            File.WriteAllBytes(Path.Combine(dir, tex.name + ".png"), ImageConversion.EncodeToPNG(Readable(tex)));
        }
        catch (Exception ex) { log.AppendLine("   export : " + ex.Message); }
    }

    static void ExportSprite(Sprite s, string where)
    {
        if (s == null) return;
        string n = s.name.Replace("(Clone)", "");
        var b = s.border;
        log.AppendLine($"   {n} [{(int)s.rect.width}x{(int)s.rect.height}] bords9={b.x},{b.y},{b.z},{b.w}{where}");
        if (!exported.Add(n)) return;
        try
        {
            var tex = s.texture;
            var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(tex, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tr = s.textureRect;
            var off = s.textureRectOffset;
            var copy = new Texture2D((int)s.rect.width, (int)s.rect.height, TextureFormat.RGBA32, false);
            copy.SetPixels32(new Color32[copy.width * copy.height]);
            copy.ReadPixels(tr, (int)off.x, (int)off.y);
            copy.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(Path.Combine(dir, n + ".png"), ImageConversion.EncodeToPNG(copy));
        }
        catch (Exception ex) { log.AppendLine("   export : " + ex.Message); }
    }
}

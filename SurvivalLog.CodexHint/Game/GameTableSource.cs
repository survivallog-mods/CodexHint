using System;
using System.Collections.Generic;
using SurvivalLog.CodexHint.Derive.Model;

namespace SurvivalLog.CodexHint.Game;

/// <summary>运行时表源：ConfigManager._Config_XXX_Dict（interop Dictionary，UnlockHub 同款访问）
/// → 推导层 POCO。与 JsonTableSource 同构（离线/在线同源对拍）。
/// 容错：Dict 未加载（null）=本表失败不缓存（下轮重读，表可能延迟加载）；
/// 单行字段异常跳过并遥测（字段级定位），不拖垮整表。</summary>
public sealed class GameTableSource : ITableSource
{
    private readonly Dictionary<Type, object> _cache = new();
    private readonly HashSet<string> _rowFailed = new();
    private GameCore.HotUpdate.ConfigManager _cm;

    public GameCore.HotUpdate.ConfigManager Cm()
    {
        if (_cm != null) return _cm;
        try
        {
            if (!GameCore.HotUpdate.BaseSingleton<GameCore.HotUpdate.ConfigManager>.IsInstanceCreated)
                return null;
            return _cm = GameCore.HotUpdate.BaseSingleton<GameCore.HotUpdate.ConfigManager>.Instance;
        }
        catch { return null; }
    }

    private static List<int> Copy(Il2CppSystem.Collections.Generic.List<int> src)
    {
        var list = new List<int>();
        if (src == null) return list;
        foreach (var v in src) list.Add(v);
        return list;
    }

    public IReadOnlyList<T> Rows<T>() where T : class
    {
        if (_cache.TryGetValue(typeof(T), out var cached)) return (IReadOnlyList<T>)cached;
        var list = new List<T>();
        var cm = Cm();
        if (cm != null)
        {
            bool ok;
            try { ok = Map<T>(cm, list); }
            catch (Exception e)
            {
                CodexHintPlugin.Log?.LogWarning($"[CodexHint] 表映射异常 {typeof(T).Name}: {e.Message} @ {FirstFrame(e)}");
                ok = false;
            }
            if (!ok) return list; // 失败不缓存：下轮重读
        }
        _cache[typeof(T)] = list;
        return list;
    }

    private static string FirstFrame(Exception e)
    {
        var st = e.StackTrace ?? "";
        var idx = st.IndexOf('\n');
        return (idx > 0 ? st.Substring(0, idx) : st).Trim();
    }

    private void RowFail(string table, int id, Exception e)
    {
        var key = table + "|" + FirstFrame(e);
        if (_rowFailed.Add(key))
            CodexHintPlugin.Log?.LogWarning($"[CodexHint] 行映射失败首例 {table}[{id}]: {e.Message} @ {FirstFrame(e)}");
    }

    /// <summary>Dict=null 返回 null（=本表未加载，调用方不缓存）；行异常跳过并遥测。</summary>
    private static List<object> Fill<TRow>(
        Il2CppSystem.Collections.Generic.Dictionary<int, TRow> dict,
        Func<TRow, object> map, GameTableSource self, string name)
    {
        if (dict == null)
        {
            CodexHintPlugin.Log?.LogWarning($"[CodexHint] 表未加载 {name}（Dict=null，下轮重试）");
            return null;
        }
        var list = new List<object>();
        foreach (var kv in dict)
        {
            var r = kv.Value;
            if (r == null) continue;
            try { list.Add(map(r)); }
            catch (Exception e) { self.RowFail(name, kv.Key, e); }
        }
        return list;
    }

    private bool Map<T>(GameCore.HotUpdate.ConfigManager cm, List<T> list) where T : class
    {
        List<object> rows;
        switch (typeof(T).Name)
        {
            case nameof(ItemRow):
                rows = Fill(cm._Config_Item_Dict, (GameCore.HotUpdate.Config_Item r) => new ItemRow(r.ID, r.ItemName_Local, r.Category, r.InCodex, r.Prey_Rarity, r.Plant, r.TargetFurnitureID), this, "Item");
                break;
            case nameof(RecipeRow):
                rows = Fill(cm._Config_CookingRecipe_Dict, (GameCore.HotUpdate.Config_CookingRecipe r) => new RecipeRow(r.ID, r.RecipeName_Local, Copy(r.SpecificItems), r.PerfectItemID, r.GoodItemID, r.NormalItemID, r.FailItemID, r.MinLevel, r.craft_level), this, "CookingRecipe");
                break;
            case nameof(ProductionRow):
                rows = Fill(cm._Config_ProductionList_Dict, (GameCore.HotUpdate.Config_ProductionList r) => new ProductionRow(r.ID, r.ShopName_Local, Copy(r.MaterialList), Copy(r.ProductID), Copy(r.perfect_item_id), Copy(r.FailedID), r.craft_level, r.InCodex), this, "ProductionList");
                break;
            case nameof(ShopRow):
                rows = Fill(cm._Config_Shop_Dict, (GameCore.HotUpdate.Config_Shop r) => new ShopRow(r.ID, r.ShopName_Local, Copy(r.ItemIdList), Copy(r.RandomID)), this, "Shop");
                break;
            case nameof(StrangerStockRow):
                rows = Fill(cm._Config_StrangerStock_Dict, (GameCore.HotUpdate.Config_StrangerStock r) => new StrangerStockRow(r.ID, Copy(r.ItemIdList)), this, "StrangerStock");
                break;
            case nameof(RandomGroupRow):
                rows = Fill(cm._Config_RandomGroup_Dict, (GameCore.HotUpdate.Config_RandomGroup r) => new RandomGroupRow(r.ID, Copy(r.IdList)), this, "RandomGroup");
                break;
            case nameof(GroundLootRow):
                rows = Fill(cm._Config_GroundLoot_Dict, (GameCore.HotUpdate.Config_GroundLoot r) => new GroundLootRow(r.RandomGroupId, r.PositionName ?? ""), this, "GroundLoot");
                break;
            case nameof(PackageRow):
                rows = Fill(cm._Config_Package_Dict, (GameCore.HotUpdate.Config_Package r) => new PackageRow(r.ID, r.PackageName_Local ?? "", Copy(r.ItemIdList), Copy(r.RandomID)), this, "Package");
                break;
            case nameof(PlantRow):
                rows = Fill(cm._Config_Plant_Dict, (GameCore.HotUpdate.Config_Plant r) => new PlantRow(r.ID, r.Name_Local ?? "", r.InCodex, Copy(r.Gain), Copy(r.Perfect_Gain), Copy(r.Gain_Seed), Copy(r.WitheredGain)), this, "Plant");
                break;
            case nameof(TrapRow):
                rows = Fill(cm._Config_Trap_Dict, (GameCore.HotUpdate.Config_Trap r) => new TrapRow(r.ID, r.Name_Local ?? "", Copy(r.Prey_Id)), this, "Trap");
                break;
            case nameof(SceneRatRow):
                rows = Fill(cm._Config_SceneRat_Dict, (GameCore.HotUpdate.Config_SceneRat r) => new SceneRatRow(r.SceneRatName_Local ?? "", r.PreyItemId), this, "SceneRat");
                break;
            case nameof(FurnitureRow):
                rows = Fill(cm._Config_Furniture_Dict, (GameCore.HotUpdate.Config_Furniture r) => new FurnitureRow(r.ID, r.Name_Local ?? "", r.InCodex, r.ShopPage), this, "Furniture");
                break;
            case nameof(RenoRow):
                rows = Fill(cm._Config_RenovationStyle_Dict, (GameCore.HotUpdate.Config_RenovationStyle r) => new RenoRow(r.ID, r.SourceText_Local ?? "", r.ItemId), this, "RenovationStyle");
                break;
            case nameof(EventRow):
                rows = Fill(cm._Config_Event_Dict, (GameCore.HotUpdate.Config_Event r) => new EventRow(r.ID, r.Name_Local ?? "", r.StartRewardSetId, r.RewardSetId, r.ExpiredRewardSetId), this, "Event");
                break;
            case nameof(MapPointRow):
                rows = Fill(cm._Config_MapPoint_Dict, (GameCore.HotUpdate.Config_MapPoint r) => new MapPointRow(r.ID, r.PositionName ?? "", r.Name_Local ?? "", r.IsUnlock), this, "MapPoint");
                break;
            case nameof(GuidanceFurnitureRow):
                rows = Fill(cm._Config_GuidanceFurniture_Dict, (GameCore.HotUpdate.Config_GuidanceFurniture r) => new GuidanceFurnitureRow(r.FurnitureId, r.MapPointID, r.ShopID, r.BuildShopID, r.PackageID), this, "GuidanceFurniture");
                break;
            case nameof(GuidanceNpcRow):
                rows = Fill(cm._Config_GuidanceNpc_Dict, (GameCore.HotUpdate.Config_GuidanceNpc r) => new GuidanceNpcRow(r.MapPointID, r.ShopID), this, "GuidanceNpc");
                break;
            case nameof(SMSRow):
                rows = Fill(cm._Config_SMS_Dict, (GameCore.HotUpdate.Config_SMS r) => new SMSRow(r.ContactId, r.ShelfShopId), this, "SMS");
                break;
            case nameof(AidCommissionRow):
                rows = Fill(cm._Config_AidCommission_Dict, (GameCore.HotUpdate.Config_AidCommission r) => new AidCommissionRow(Copy(r.RewardItemIds)), this, "AidCommission");
                break;
            case nameof(SMSSenderRow):
                rows = Fill(cm._Config_SMSSender_Dict, (GameCore.HotUpdate.Config_SMSSender r) => new SMSSenderRow(r.ContactId, r.DisplayName_Local ?? ""), this, "SMSSender");
                break;
            case nameof(RewardSetRow):
                rows = Fill(cm._Config_RewardSet_Dict, (GameCore.HotUpdate.Config_RewardSet r) => new RewardSetRow(r.ID, Copy(r.ItemChange), Copy(r.ItemPackage), r.RandomGroupId), this, "RewardSet");
                break;
            default:
                throw new NotSupportedException(typeof(T).Name);
        }
        if (rows == null) return false; // Dict 未加载：失败不缓存，下轮重读
        foreach (var o in rows) list.Add((T)o);
        return true;
    }
}

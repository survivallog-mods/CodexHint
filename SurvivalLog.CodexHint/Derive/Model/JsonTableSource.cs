using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using SurvivalLog.CodexHint.Derive.Model;

namespace SurvivalLog.CodexHint.Derive.Model;

/// <summary>离线表源：从 gamekb 镜像 tables/*.json 读表行。
/// 行结构 {table,rows,columns,data:[{列:值}]}；bool 为 "True"/"False" 字符串；
/// 列缺失取默认值；行内同名带/不带 _k__BackingField 后缀的键去重。</summary>
public sealed class JsonTableSource : ITableSource
{
    private readonly string _dir;
    private readonly Dictionary<Type, object> _cache = new();

    public JsonTableSource(string dir) => _dir = dir;

    public IReadOnlyList<T> Rows<T>() where T : class
    {
        if (_cache.TryGetValue(typeof(T), out var cached)) return (IReadOnlyList<T>)cached;
        var file = Path.Combine(_dir, FileName<T>() + ".json");
        var list = new List<T>();
        if (File.Exists(file))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            foreach (var row in doc.RootElement.GetProperty("data").EnumerateArray())
            {
                var cells = new Dictionary<string, JsonElement>();
                foreach (var p in row.EnumerateObject())
                {
                    var key = p.Name.Replace("_k__BackingField", "");
                    cells[key] = p.Value; // 同名后缀键后写覆盖，值相同
                }
                if (Map<T>(cells) is T mapped) list.Add(mapped);
            }
        }
        _cache[typeof(T)] = list;
        return list;
    }

    private static string FileName<T>() => typeof(T).Name switch
    {
        nameof(ItemRow) => "Config_Item",
        nameof(RecipeRow) => "Config_CookingRecipe",
        nameof(ProductionRow) => "Config_ProductionList",
        nameof(ShopRow) => "Config_Shop",
        nameof(StrangerStockRow) => "Config_StrangerStock",
        nameof(RandomGroupRow) => "Config_RandomGroup",
        nameof(GroundLootRow) => "Config_GroundLoot",
        nameof(PackageRow) => "Config_Package",
        nameof(PlantRow) => "Config_Plant",
        nameof(TrapRow) => "Config_Trap",
        nameof(SceneRatRow) => "Config_SceneRat",
        nameof(FurnitureRow) => "Config_Furniture",
        nameof(RenoRow) => "Config_RenovationStyle",
        nameof(EventRow) => "Config_Event",
        nameof(RewardSetRow) => "Config_RewardSet",
        nameof(MapPointRow) => "Config_MapPoint",
        nameof(AidCommissionRow) => "Config_AidCommission",
        nameof(SMSSenderRow) => "Config_SMSSender",
        nameof(GuidanceFurnitureRow) => "Config_GuidanceFurniture",
        nameof(GuidanceNpcRow) => "Config_GuidanceNpc",
        nameof(SMSRow) => "Config_SMS",
        _ => throw new NotSupportedException(typeof(T).Name),
    };

    private static object Map<T>(Dictionary<string, JsonElement> c) => typeof(T).Name switch
    {
        nameof(ItemRow) => new ItemRow(Int(c, "ID"), Str(c, "ItemName_Local"), Int(c, "Category"), Bool(c, "InCodex"), Int(c, "Prey_Rarity"), Int(c, "Plant"), Int(c, "TargetFurnitureID")),
        nameof(RecipeRow) => new RecipeRow(Int(c, "ID"), Str(c, "RecipeName_Local"), IntList(c, "SpecificItems"),
            Int(c, "PerfectItemID"), Int(c, "GoodItemID"), Int(c, "NormalItemID"), Int(c, "FailItemID"),
            Int(c, "MinLevel"), Int(c, "craft_level")),
        nameof(ProductionRow) => new ProductionRow(Int(c, "ID"), Str(c, "ShopName_Local"), IntList(c, "MaterialList"),
            IntList(c, "ProductID"), IntList(c, "perfect_item_id"), IntList(c, "FailedID"), Int(c, "craft_level"), Bool(c, "InCodex")),
        nameof(ShopRow) => new ShopRow(Int(c, "ID"), Str(c, "ShopName_Local"), IntList(c, "ItemIdList"), IntList(c, "RandomID")),
        nameof(StrangerStockRow) => new StrangerStockRow(Int(c, "ID"), IntList(c, "ItemIdList")),
        nameof(SMSSenderRow) => new SMSSenderRow(Int(c, "ContactId"), Str(c, "DisplayName_Local")),
        nameof(RandomGroupRow) => new RandomGroupRow(Int(c, "ID"), IntList(c, "IdList")),
        nameof(GroundLootRow) => new GroundLootRow(Int(c, "RandomGroupId"), Str(c, "PositionName")),
        nameof(PackageRow) => new PackageRow(Int(c, "ID"), Str(c, "PackageName_Local"), IntList(c, "ItemIdList"), IntList(c, "RandomID")),
        nameof(PlantRow) => new PlantRow(Int(c, "ID"), Str(c, "Name_Local"), Bool(c, "InCodex"),
            IntList(c, "Gain"), IntList(c, "Perfect_Gain"), IntList(c, "Gain_Seed"), IntList(c, "WitheredGain")),
        nameof(TrapRow) => new TrapRow(Int(c, "ID"), Str(c, "Name_Local"), IntList(c, "Prey_Id")),
        nameof(SceneRatRow) => new SceneRatRow(Str(c, "SceneRatName_Local"), Int(c, "PreyItemId")),
        nameof(FurnitureRow) => new FurnitureRow(Int(c, "ID"), Str(c, "Name_Local"), Bool(c, "InCodex"), Int(c, "ShopPage")),
        nameof(RenoRow) => new RenoRow(Int(c, "ID"), StrOr(c, "SourceText_Local", "SourceText"), Int(c, "ItemId")),
        nameof(EventRow) => new EventRow(Int(c, "ID"), Str(c, "Name_Local"), Int(c, "StartRewardSetId"), Int(c, "RewardSetId"), Int(c, "ExpiredRewardSetId")),
        nameof(RewardSetRow) => new RewardSetRow(Int(c, "ID"), IntList(c, "ItemChange"), IntList(c, "ItemPackage"), Int(c, "RandomGroupId")),
        nameof(MapPointRow) => new MapPointRow(Int(c, "ID"), Str(c, "PositionName"), Str(c, "Name_Local"), Bool(c, "IsUnlock")),
        nameof(GuidanceFurnitureRow) => new GuidanceFurnitureRow(Int(c, "FurnitureId"), Int(c, "MapPointID"), Int(c, "ShopID"), Int(c, "BuildShopID"), Int(c, "PackageID")),
        nameof(GuidanceNpcRow) => new GuidanceNpcRow(Int(c, "MapPointID"), Int(c, "ShopID")),
        nameof(SMSRow) => new SMSRow(Int(c, "ContactId"), Int(c, "ShelfShopId")),
        nameof(AidCommissionRow) => new AidCommissionRow(IntList(c, "RewardItemIds")),
        _ => throw new NotSupportedException(typeof(T).Name),
    };

    private static JsonElement? Get(Dictionary<string, JsonElement> c, string key)
        => c.TryGetValue(key, out var v) && v.ValueKind != JsonValueKind.Null ? v : null;

    private static int Int(Dictionary<string, JsonElement> c, string key)
    {
        var v = Get(c, key);
        if (v == null) return 0;
        return v.Value.ValueKind == JsonValueKind.Number ? v.Value.GetInt32()
            : int.TryParse(v.Value.GetString(), out var n) ? n : 0;
    }

    private static bool Bool(Dictionary<string, JsonElement> c, string key)
    {
        var v = Get(c, key);
        if (v == null) return false;
        return v.Value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(v.Value.GetString(), out var b) && b,
            _ => false,
        };
    }

    private static string Str(Dictionary<string, JsonElement> c, string key) => Get(c, key)?.GetString() ?? "";

    private static string StrOr(Dictionary<string, JsonElement> c, string a, string b)
    {
        var s = Str(c, a);
        return s != "" ? s : Str(c, b);
    }

    private static List<int> IntList(Dictionary<string, JsonElement> c, string key)
    {
        var v = Get(c, key);
        if (v == null || v.Value.ValueKind != JsonValueKind.Array) return new List<int>();
        var list = new List<int>();
        foreach (var e in v.Value.EnumerateArray())
        {
            if (e.ValueKind == JsonValueKind.Number) list.Add(e.GetInt32());
            else if (e.ValueKind == JsonValueKind.String && int.TryParse(e.GetString(), out var n)) list.Add(n);
        }
        return list;
    }
}

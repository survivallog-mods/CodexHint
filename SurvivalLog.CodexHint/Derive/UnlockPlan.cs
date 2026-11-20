using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SurvivalLog.CodexHint.Derive.Model;

namespace SurvivalLog.CodexHint.Derive;

/// <summary>全量 plan 构建器：枚举七分类条目（判定=spec §3 汇编实证表），
/// 输出 {plans:{cat:sid:[{tag,text}]}, degraded:[cat:sid]}。
/// 空步骤条目进 degraded（前端显示官方分类提示），键为 "category:sourceId" 复合键。</summary>
public sealed class UnlockPlanBuilder
{
    private readonly ITableSource _src;
    private readonly ItemSourceIndex _itemSrc;

    public UnlockPlanBuilder(ITableSource src)
    {
        _src = src;
        _itemSrc = new ItemSourceIndex(src);
    }

    public string BuildJson()
    {
        var plans = new Dictionary<string, List<UnlockStep>>();
        var degraded = new List<string>();

        // 1 食物：Item.Category==1 && InCodex
        foreach (var i in _src.Rows<ItemRow>().Where(i => i.Category == 1 && i.InCodex))
            Add(plans, degraded, 1, i.ID, FoodDeriver.Derive(i.ID, _src, _itemSrc));
        // 2 菜肴：配方存在即条目
        foreach (var r in _src.Rows<RecipeRow>())
            Add(plans, degraded, 2, r.ID, DishDeriver.Derive(r.ID, _src, _itemSrc));
        // 3 植物：Plant.InCodex
        foreach (var p in _src.Rows<PlantRow>().Where(p => p.InCodex))
            Add(plans, degraded, 3, p.ID, PlantDeriver.Derive(p.ID, _src, _itemSrc));
        // 4 猎物：PreyRarity>0 && InCodex
        foreach (var i in _src.Rows<ItemRow>().Where(i => i.PreyRarity > 0 && i.InCodex))
            Add(plans, degraded, 4, i.ID, PreyDeriver.Derive(i.ID, _src, _itemSrc));
        // 5 制造：ProductionList.InCodex
        foreach (var p in _src.Rows<ProductionRow>().Where(p => p.InCodex))
            Add(plans, degraded, 5, p.ID, CraftDeriver.Derive(p.ID, _src, _itemSrc));
        // 6 家具：Furniture.InCodex
        foreach (var f in _src.Rows<FurnitureRow>().Where(f => f.InCodex))
            Add(plans, degraded, 6, f.ID, FurnitureDeriver.Derive(f.ID, _src, _itemSrc));
        // 7 装修：RenovationStyle 存在即条目
        foreach (var r in _src.Rows<RenoRow>())
            Add(plans, degraded, 7, r.ID, RenovationDeriver.Derive(r.ID, _src, _itemSrc));

        return JsonSerializer.Serialize(new { plans, degraded },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    private static void Add(Dictionary<string, List<UnlockStep>> plans, List<string> degraded,
        int cat, int sid, IReadOnlyList<UnlockStep> steps)
    {
        var key = cat + ":" + sid;
        if (steps.Count == 0) degraded.Add(key);
        else plans[key] = CapSteps(steps);
    }

    /// <summary>排序：确定性来源在前（制造/交易/直出/烹饪），随机来源在后（包裹/联系人/拾取）。
    /// 不截断——每条途径都是可执行信息，面板本身可滚动（文案治理三轮：截断提示行不提供获取信息，废行）。</summary>
    public static List<UnlockStep> CapSteps(IReadOnlyList<UnlockStep> steps)
    {
        var order = new Dictionary<string, int>
        {
            { "制造", 0 }, { "交易", 0 }, { "直出", 0 }, { "烹饪", 0 },
            { "种植", 1 }, { "陷阱", 1 }, { "事件", 1 }, { "遭遇", 1 }, { "援助", 1 },
            { "包裹", 2 }, { "联系人", 2 }, { "拾取", 2 },
        };
        var byTag = new Dictionary<string, List<UnlockStep>>();
        foreach (var s in steps)
        {
            if (!byTag.TryGetValue(s.Tag, out var l)) byTag[s.Tag] = l = new List<UnlockStep>();
            l.Add(s);
        }
        var result = new List<UnlockStep>();
        foreach (var kv in byTag.OrderBy(k => order.TryGetValue(k.Key, out var o) ? o : 3))
            result.AddRange(kv.Value);
        return result;
    }
}

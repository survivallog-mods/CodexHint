using System.Collections.Generic;
using System.Linq;
using SurvivalLog.CodexHint.Derive.Model;

namespace SurvivalLog.CodexHint.Derive;

/// <summary>菜肴：sourceId=配方 ID（IsInCodex case2 汇编实证），配方即方案。
/// SpecificItems 非空=固定食材；否则按食材标签随机组合。</summary>
public static class DishDeriver
{
    public static IReadOnlyList<UnlockStep> Derive(int sourceId, ITableSource src, ItemSourceIndex itemSrc)
    {
        var r = src.Rows<RecipeRow>().FirstOrDefault(x => x.ID == sourceId);
        if (r == null || string.IsNullOrEmpty(r.Name)) return Empty;
        var steps = new List<UnlockStep>
        {
            new("烹饪", $"烹饪「{r.Name}」（需烹饪等级 {r.MinLevel}）"),
        };
        if (r.SpecificItems.Count > 0)
        {
            var names = r.SpecificItems.Select(id => itemSrc.NameOf(id)).Where(n => n != "" && !n.Contains("测试")).ToList();
            if (names.Count > 0) steps.Add(new("烹饪", $"固定食材：{string.Join("、", names)}"));
        }
        else
        {
            steps.Add(new("烹饪", "食材按标签随机搭配（满足标签的食材皆可）"));
        }
        return steps;
    }

    internal static readonly IReadOnlyList<UnlockStep> Empty = new UnlockStep[0];
}

/// <summary>制造：sourceId=配方 ID（ProductionList.InCodex），材料×数量 + 等级门槛。</summary>
public static class CraftDeriver
{
    public static IReadOnlyList<UnlockStep> Derive(int sourceId, ITableSource src, ItemSourceIndex itemSrc)
    {
        var p = src.Rows<ProductionRow>().FirstOrDefault(x => x.ID == sourceId);
        if (p == null || string.IsNullOrEmpty(p.Name)) return DishDeriver.Empty;
        var mats = p.MaterialList
            .GroupBy(id => id)
            .Select(g => (name: itemSrc.NameOf(g.Key), count: g.Count()))
            .Where(m => m.name != "")
            .Select(m => m.count > 1 ? $"{m.name}×{m.count}" : m.name)
            .ToList();
        var matText = mats.Count > 0 ? $"：{string.Join(" ", mats)}" : "";
        var lvText = p.CraftLevel > 0 ? $"（需制造等级 {p.CraftLevel}）" : "";
        return new[] { new UnlockStep("制造", $"制造「{p.Name}」{matText}{lvText}") };
    }
}

/// <summary>装修：sourceId=styleId。物品来源链（获得装修道具后使用）+ 官方 SourceText 补充。</summary>
public static class RenovationDeriver
{
    public static IReadOnlyList<UnlockStep> Derive(int sourceId, ITableSource src, ItemSourceIndex itemSrc)
    {
        var r = src.Rows<RenoRow>().FirstOrDefault(x => x.ID == sourceId);
        if (r == null) return DishDeriver.Empty;
        var steps = new List<UnlockStep>();
        var itemName = itemSrc.NameOf(r.ItemId);
        if (r.ItemId > 0 && itemName != "")
        {
            foreach (var s in itemSrc[r.ItemId])
            {
                var folded = s.Text == $"制造「{itemName}」";
                steps.Add(new UnlockStep(s.Tag, folded
                    ? $"{s.Text}后使用"
                    : $"获得装修道具「{itemName}」（{s.Text}）后使用"));
            }
        }
        if (steps.Count == 0 && !string.IsNullOrEmpty(r.SourceText) && r.SourceText != "可在工作台制造")
            steps.Add(new UnlockStep("直出", r.SourceText));
        return steps;
    }
}

/// <summary>猎物：sourceId=猎物 item。陷阱可捕（Config_Trap.PreyIds）+ 地图随机遭遇（SceneRat）。
/// 诱饵表（2235 行×全猎物）信息冗余，不展开。</summary>
public static class PreyDeriver
{
    public static IReadOnlyList<UnlockStep> Derive(int sourceId, ITableSource src, ItemSourceIndex itemSrc)
    {
        var steps = new List<UnlockStep>();
        foreach (var t in src.Rows<TrapRow>())
        {
            if (string.IsNullOrEmpty(t.Name) || !t.PreyIds.Contains(sourceId)) continue;
            steps.Add(new("陷阱", $"放置陷阱「{t.Name}」可捕获"));
        }
        foreach (var rat in src.Rows<SceneRatRow>())
        {
            if (rat.PreyItemId == sourceId && !string.IsNullOrEmpty(rat.Name))
                steps.Add(new("遭遇", $"地图随机遇到「{rat.Name}」"));
        }
        return steps;
    }
}

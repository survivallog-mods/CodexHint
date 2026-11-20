using System.Collections.Generic;
using System.Linq;
using SurvivalLog.CodexHint.Derive.Model;

namespace SurvivalLog.CodexHint.Derive;

/// <summary>食物：sourceId=itemId，来源层全链前缀「食用解锁」。</summary>
public static class FoodDeriver
{
    public static IReadOnlyList<UnlockStep> Derive(int sourceId, ITableSource src, ItemSourceIndex itemSrc)
    {
        // 面板上方官方提示已是"尝试食用"，方案不再加"食用解锁"前缀（文案治理二轮）
        return itemSrc[sourceId].Select(s => s).ToList();
    }
}

/// <summary>植物：sourceId=plantId。种子物品（Item.Plant 反查）→ 种子来源链；
/// 无种子来源（藤蔓类代码级发现）则降级为空。</summary>
public static class PlantDeriver
{
    public static IReadOnlyList<UnlockStep> Derive(int sourceId, ITableSource src, ItemSourceIndex itemSrc)
    {
        var plant = src.Rows<PlantRow>().FirstOrDefault(p => p.ID == sourceId);
        if (plant == null) return DishDeriver.Empty;
        var seeds = src.Rows<ItemRow>().Where(i => i.Plant == sourceId).Select(i => i.ID).ToList();
        var steps = new List<UnlockStep>();
        foreach (var seed in seeds)
        {
            var seedName = itemSrc.NameOf(seed);
            foreach (var s in itemSrc[seed])
            {
                if (s.Text.Contains("种植「" + plant.Name + "」")) { steps.Add(s); continue; } // 自身回收循环不加链
                steps.Add(new UnlockStep(s.Tag, $"获得「{seedName}」（{s.Text}）并种植收获"));
            }
        }
        return steps.Count > 0 ? steps : DishDeriver.Empty;
    }
}

/// <summary>家具：sourceId=furnitureId。商店页 ∪ 放置物品来源链（Item.TargetFurnitureID 反查）。
/// 制造来源与物品同名时折叠（获得「X」（制造「X」产出）后放置 → 制造「X」后放置）。</summary>
public static class FurnitureDeriver
{
    public static IReadOnlyList<UnlockStep> Derive(int sourceId, ITableSource src, ItemSourceIndex itemSrc)
    {
        var furn = src.Rows<FurnitureRow>().FirstOrDefault(f => f.ID == sourceId);
        if (furn == null) return DishDeriver.Empty;
        var steps = new List<UnlockStep>();
        if (furn.ShopPage > 0)
            steps.Add(new UnlockStep("商店", $"在家具商店第 {furn.ShopPage} 页购买"));
        foreach (var item in src.Rows<ItemRow>().Where(i => i.TargetFurnitureId == sourceId))
        {
            foreach (var s in itemSrc[item.ID])
            {
                // 同名折叠：制造来源文案以物品名开头时不再重复「获得…（…）」包装
                var folded = s.Tag == "制造" && s.Text == $"制造「{item.Name}」";
                steps.Add(new UnlockStep(s.Tag, folded
                    ? $"{s.Text}后放置"
                    : $"获得「{item.Name}」（{s.Text}）后放置"));
            }
        }
        return steps;
    }
}

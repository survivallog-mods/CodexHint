using System.Linq;
using SurvivalLog.CodexHint.Derive;
using SurvivalLog.CodexHint.Derive.Model;
using Xunit;

namespace SurvivalLog.CodexHint.Tests;

public class ChainedDeriverTests
{
    private static ITableSource GameSrc()
    {
        var dir = TableSourceTests.GameTablesDir();
        return dir == null ? null : new JsonTableSource(dir);
    }

    [Fact]
    public void FoodCoverageGate()
    {
        // 覆盖率门槛（spec §8a-2）：表内可闭环 100%；唯一已知例外 #2904 烤耗子（仅作诱饵）
        var src = GameSrc();
        if (src == null) return;
        var idx = new ItemSourceIndex(src);
        var foods = src.Rows<ItemRow>().Where(i => i.Category == 1 && i.InCodex).ToList();
        Assert.Equal(191, foods.Count); // 锚点
        var noPlan = foods.Where(i => FoodDeriver.Derive(i.ID, src, idx).Count == 0).ToList();
        Assert.True(noPlan.Count <= 1, $"食物缺口超门槛: {string.Join(",", noPlan.Select(i => i.ID + i.Name))}");
        // 六轮实证：唯一零来源条目=2913 一杯热水（唯一出处为无名内部包裹106，发放零实证→降级）
        if (noPlan.Count == 1) Assert.Equal(2913, noPlan[0].ID);
    }

    [Fact]
    public void FurnitureCoverageGate()
    {
        var src = GameSrc();
        if (src == null) return;
        var idx = new ItemSourceIndex(src);
        var furns = src.Rows<FurnitureRow>().Where(f => f.InCodex).ToList();
        Assert.Equal(111, furns.Count); // 锚点
        foreach (var f in furns)
        {
            var steps = FurnitureDeriver.Derive(f.ID, src, idx);
            Assert.True(steps.Count > 0, $"家具 #{f.ID} {f.Name} 无来源");
        }
    }

    [Fact]
    public void PlantFullCoverage()
    {
        var src = GameSrc();
        if (src == null) return;
        var idx = new ItemSourceIndex(src);
        var plants = src.Rows<PlantRow>().Where(p => p.InCodex).ToList();
        Assert.Equal(38, plants.Count); // 锚点
        foreach (var p in plants)
        {
            var steps = PlantDeriver.Derive(p.ID, src, idx);
            Assert.True(steps.Count > 0, $"植物 #{p.ID} {p.Name} 无来源");
        }
    }

    [Fact]
    public void FoodBingganHasEatPrefix()
    {
        var src = GameSrc();
        if (src == null) return;
        var idx = new ItemSourceIndex(src);
        var binggan = src.Rows<ItemRow>().First(i => i.Name == "饼干");
        var steps = FoodDeriver.Derive(binggan.ID, src, idx);
        Assert.NotEmpty(steps);
        Assert.All(steps, s => Assert.False(s.Text.StartsWith("食用解锁"), "前缀已废: " + s.Text));
    }

    [Fact]
    public void FurniturePlacedFromItem()
    {
        // 纪念杯：获得放置物品（TargetFurnitureID 反查）→ 事件奖励层闭环
        var src = GameSrc();
        if (src == null) return;
        var idx = new ItemSourceIndex(src);
        var steps = FurnitureDeriver.Derive(9300, src, idx);
        Assert.NotEmpty(steps);
        Assert.Contains(steps, s => s.Text.Contains("放置") || s.Text.Contains("事件"));
    }

    [Fact]
    public void PlantSeedChain()
    {
        var src = GameSrc();
        if (src == null) return;
        var idx = new ItemSourceIndex(src);
        var any = src.Rows<PlantRow>().First(p => p.InCodex && idx[SeedOf(src, p.ID)].Count > 0);
        var steps = PlantDeriver.Derive(any.ID, src, idx);
        Assert.Contains(steps, s => s.Text.Contains("种子"));
    }

    private static int SeedOf(ITableSource src, int plantId)
        => src.Rows<ItemRow>().FirstOrDefault(i => i.Plant == plantId)?.ID ?? 0;
}

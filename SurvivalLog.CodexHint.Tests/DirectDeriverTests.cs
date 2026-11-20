using System.Linq;
using SurvivalLog.CodexHint.Derive;
using SurvivalLog.CodexHint.Derive.Model;
using Xunit;

namespace SurvivalLog.CodexHint.Tests;

public class DirectDeriverTests
{
    private static (ITableSource src, ItemSourceIndex idx) Game()
    {
        var dir = TableSourceTests.GameTablesDir();
        return dir == null ? (null, null) : (new JsonTableSource(dir), null);
    }

    [Fact]
    public void DishFromRecipeId()
    {
        var dir = TableSourceTests.GameTablesDir();
        if (dir == null) return;
        var src = new JsonTableSource(dir);
        var idx = new ItemSourceIndex(src);
        var steps = DishDeriver.Derive(1001, src, idx); // 佛跳墙配方
        Assert.NotEmpty(steps);
        Assert.Contains(steps, s => s.Text.Contains("佛跳墙") && s.Text.Contains("烹饪等级"));
    }

    [Fact]
    public void CraftListsMaterials()
    {
        var dir = TableSourceTests.GameTablesDir();
        if (dir == null) return;
        var src = new JsonTableSource(dir);
        var idx = new ItemSourceIndex(src);
        var any = src.Rows<ProductionRow>().First(p => p.InCodex && p.MaterialList.Count > 0 && p.Name != "");
        var steps = CraftDeriver.Derive(any.ID, src, idx);
        Assert.NotEmpty(steps);
        Assert.Contains(steps, s => s.Text.Contains("×") && s.Text.Contains("制造等级"));
    }

    [Fact]
    public void RenoDirectText()
    {
        var dir = TableSourceTests.GameTablesDir();
        if (dir == null) return;
        var src = new JsonTableSource(dir);
        var idx = new ItemSourceIndex(src);
        var any = src.Rows<RenoRow>().First(r => r.ItemId > 0);
        var steps = RenovationDeriver.Derive(any.ID, src, idx);
        Assert.NotEmpty(steps);
        Assert.Contains(steps, s => s.Text.Contains("使用"));
    }

    [Fact]
    public void PreyTrapCoverage()
    {
        // 覆盖率门槛：全部 23 个猎物条目（PreyRarity>0 && InCodex）都有陷阱来源
        var dir = TableSourceTests.GameTablesDir();
        if (dir == null) return;
        var src = new JsonTableSource(dir);
        var idx = new ItemSourceIndex(src);
        var preys = src.Rows<ItemRow>().Where(i => i.PreyRarity > 0 && i.InCodex).ToList();
        Assert.Equal(23, preys.Count); // 锚点：游戏更新致条目集变化时此断言先红
        foreach (var p in preys)
        {
            var steps = PreyDeriver.Derive(p.ID, src, idx);
            Assert.True(steps.Count > 0, $"猎物 #{p.ID} {p.Name} 无来源");
        }
    }

    [Fact]
    public void DishUnknownIdEmpty()
    {
        var dir = TableSourceTests.GameTablesDir();
        if (dir == null) return;
        var src = new JsonTableSource(dir);
        var idx = new ItemSourceIndex(src);
        Assert.Empty(DishDeriver.Derive(999999, src, idx));
        Assert.Empty(CraftDeriver.Derive(999999, src, idx));
        Assert.Empty(RenovationDeriver.Derive(999999, src, idx));
        Assert.Empty(PreyDeriver.Derive(999999, src, idx));
    }
}

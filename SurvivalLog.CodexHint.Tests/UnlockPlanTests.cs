using System.Linq;
using System.Text.Json;
using SurvivalLog.CodexHint.Derive;
using SurvivalLog.CodexHint.Derive.Model;
using Xunit;

namespace SurvivalLog.CodexHint.Tests;

public class UnlockPlanTests
{
    private static UnlockPlanBuilder GameBuilder()
    {
        var dir = TableSourceTests.GameTablesDir();
        return dir == null ? null : new UnlockPlanBuilder(new JsonTableSource(dir));
    }

    [Fact]
    public void CompositeKeyCollision()
    {
        // 菜肴 recipeId 与食物 itemId 同为 int：复合键 "cat:sid" 必须共存不互踩
        var b = GameBuilder();
        if (b == null) return;
        var json = JsonDocument.Parse(b.BuildJson());
        var plans = json.RootElement.GetProperty("plans");
        // 取一个菜肴配方 id，验证 1:{id} 与 2:{id} 可以同时存在（各自按分类语义推导）
        var dishKey = plans.EnumerateObject().First(p => p.Name.StartsWith("2:")).Name;
        var id = dishKey.Substring(2);
        Assert.True(plans.TryGetProperty($"1:{id}", out _) || plans.TryGetProperty($"2:{id}", out _));
    }

    [Fact]
    public void DegradedListSmall()
    {
        // 当前全量数据零降级（2904 烤耗子 InCodex=False 非条目）；门槛 ≤2（spec §8a-2）
        var b = GameBuilder();
        if (b == null) return;
        var json = JsonDocument.Parse(b.BuildJson());
        var degraded = json.RootElement.GetProperty("degraded");
        Assert.True(degraded.GetArrayLength() <= 2, $"降级 {degraded.GetArrayLength()} 条超门槛: {string.Join(",", degraded.EnumerateArray().Select(e => e.GetString()))}");
    }

    [Fact]
    public void JsonParsable()
    {
        var b = GameBuilder();
        if (b == null) return;
        var json = JsonDocument.Parse(b.BuildJson());
        Assert.True(json.RootElement.GetProperty("plans").EnumerateObject().Any());
        // 步骤结构 {tag,text}
        var first = json.RootElement.GetProperty("plans").EnumerateObject().First().Value[0];
        Assert.NotEqual("", first.GetProperty("tag").GetString());
        Assert.NotEqual("", first.GetProperty("text").GetString());
    }

    [Fact]
    public void CategoryCountsMatchAnchors()
    {
        // plans 各分类键数 == 条目锚点总数（525/191/38/23/164/111/24）
        var b = GameBuilder();
        if (b == null) return;
        var json = JsonDocument.Parse(b.BuildJson());
        var plans = json.RootElement.GetProperty("plans");
        int Count(int cat)
        {
            var prefix = cat + ":";
            return plans.EnumerateObject().Count(p => p.Name.StartsWith(prefix));
        }
        Assert.Equal(525, Count(2));  // 菜肴=全部配方
        Assert.Equal(191, Count(1) + json.RootElement.GetProperty("degraded").GetArrayLength()); // 食物含降级
        Assert.Equal(38, Count(3));
        Assert.Equal(23, Count(4));
        Assert.Equal(164, Count(5));
        Assert.Equal(111, Count(6));
        Assert.Equal(24, Count(7));
    }
}

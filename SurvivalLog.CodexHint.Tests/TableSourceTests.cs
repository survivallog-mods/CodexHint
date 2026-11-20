using System;
using System.IO;
using SurvivalLog.CodexHint.Derive.Model;
using Xunit;

namespace SurvivalLog.CodexHint.Tests;

public class TableSourceTests : IDisposable
{
    private readonly string _miniDir;

    public TableSourceTests()
    {
        _miniDir = Path.Combine(Path.GetTempPath(), "codexhint-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_miniDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_miniDir, true); } catch { /* 临时目录清理尽力而为 */ }
    }

    internal static string GameTablesDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && dir.Name != "survivallog-mods") dir = dir.Parent;
        if (dir == null) return null;
        var t = Path.Combine(dir.FullName, "gamekb", "2026-11-12_26615062", "tables");
        return Directory.Exists(t) ? t : null;
    }

    private void WriteMini(string table, string rowsJson)
    {
        File.WriteAllText(Path.Combine(_miniDir, table + ".json"),
            "{\"table\":\"" + table + "\",\"rows\":1,\"columns\":[],\"data\":[" + rowsJson + "]}");
    }

    [Fact]
    public void ItemsContainBinggan()
    {
        var dir = GameTablesDir();
        if (dir == null) return; // gamekb 不在（外部环境）：跳过而非误报
        var src = new JsonTableSource(dir);
        var items = src.Rows<ItemRow>();
        Assert.Contains(items, i => i.Name == "饼干" && i.Category == 1);
    }

    [Fact]
    public void HandlesMissingColumns()
    {
        // 缺列（只有 ID 和 ItemName_Local）→ 其余字段取类型默认值，不抛
        WriteMini("Config_Item", "{\"ID\":1,\"ItemName_Local\":\"测试品\"}");
        var src = new JsonTableSource(_miniDir);
        var item = Assert.Single(src.Rows<ItemRow>());
        Assert.Equal(1, item.ID);
        Assert.Equal("测试品", item.Name);
        Assert.Equal(0, item.Category);
        Assert.False(item.InCodex);
        Assert.Equal(0, item.PreyRarity);
    }

    [Fact]
    public void HandlesStringBools()
    {
        // gamekb 镜像里 bool 以 "True"/"False" 字符串出现
        WriteMini("Config_Item", "{\"ID\":2,\"ItemName_Local\":\"x\",\"Category\":\"1\",\"InCodex\":\"True\",\"Prey_Rarity\":\"3\",\"Plant\":\"0\"}");
        Assert.True(File.Exists(Path.Combine(_miniDir, "Config_Item.json")), "mini 文件未写入: " + _miniDir);
        var src = new JsonTableSource(_miniDir);
        var item = Assert.Single(src.Rows<ItemRow>());
        Assert.True(item.InCodex);
        Assert.Equal(1, item.Category);
        Assert.Equal(3, item.PreyRarity);
    }

    [Fact]
    public void GameTablesParseWithoutThrow()
    {
        var dir = GameTablesDir();
        if (dir == null) return;
        var src = new JsonTableSource(dir);
        Assert.NotEmpty(src.Rows<RecipeRow>());
        Assert.NotEmpty(src.Rows<ProductionRow>());
        Assert.NotEmpty(src.Rows<ShopRow>());
        Assert.NotEmpty(src.Rows<RandomGroupRow>());
        Assert.NotEmpty(src.Rows<PlantRow>());
        Assert.NotEmpty(src.Rows<TrapRow>());
        Assert.NotEmpty(src.Rows<FurnitureRow>());
        Assert.NotEmpty(src.Rows<RenoRow>());
        Assert.NotEmpty(src.Rows<SceneRatRow>());
        Assert.NotEmpty(src.Rows<GroundLootRow>());
        Assert.NotEmpty(src.Rows<PackageRow>());
        Assert.NotEmpty(src.Rows<StrangerStockRow>());
    }
}

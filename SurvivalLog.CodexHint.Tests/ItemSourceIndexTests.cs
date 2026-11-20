using System;
using System.IO;
using SurvivalLog.CodexHint.Derive;
using SurvivalLog.CodexHint.Derive.Model;
using Xunit;

namespace SurvivalLog.CodexHint.Tests;

public class ItemSourceIndexTests : IDisposable
{
    private readonly string _miniDir;

    public ItemSourceIndexTests()
    {
        _miniDir = Path.Combine(Path.GetTempPath(), "codexhint-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_miniDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_miniDir, true); } catch { /* 尽力而为 */ }
    }

    private static ITableSource GameSrc()
    {
        var dir = TableSourceTests.GameTablesDir();
        return dir == null ? null : new JsonTableSource(dir);
    }

    private void WriteMini(string table, string rowsJson)
    {
        File.WriteAllText(Path.Combine(_miniDir, table + ".json"),
            "{\"table\":\"" + table + "\",\"rows\":1,\"columns\":[],\"data\":[" + rowsJson + "]}");
    }

    [Fact]
    public void FotiaoqiangHasCookSource()
    {
        var src = GameSrc();
        if (src == null) return;
        var idx = new ItemSourceIndex(src);
        var steps = idx[12313]; // 佛跳墙(完美)
        Assert.Contains(steps, s => s.Tag == "烹饪" && s.Text.Contains("佛跳墙"));
    }

    [Fact]
    public void PreyMeatHasNoShopSource()
    {
        // 30000 小家鼠（陷阱捕获物）不在商店/烹饪等物品来源层；陷阱来源归 PreyDeriver
        var src = GameSrc();
        if (src == null) return;
        var idx = new ItemSourceIndex(src);
        Assert.All(idx[30000], s => Assert.NotEqual("商店", s.Tag));
    }

    [Fact]
    public void NullFieldsDoNotThrow()
    {
        // 商店行列表为 null（缺列）构建索引不抛
        WriteMini("Config_Shop", "{\"ID\":1,\"ShopName_Local\":\"杂货铺\"}"); // ItemIdList/RandomID 缺列
        var src = new JsonTableSource(_miniDir);
        var idx = new ItemSourceIndex(src);
        Assert.Empty(idx[999]);
    }

    [Fact]
    public void RandomGroupShopSource()
    {
        // 商店 RandomID → 随机组 → 组内 item 获得「商店」来源，文案含商店名
        WriteMini("Config_Shop", "{\"ID\":1,\"ShopName_Local\":\"夜市\",\"ItemIdList\":[],\"RandomID\":[80]}");
        WriteMini("Config_RandomGroup", "{\"ID\":80,\"IdList\":[500,501]}");
        var src = new JsonTableSource(_miniDir);
        var idx = new ItemSourceIndex(src);
        Assert.Contains(idx[500], s => s.Tag == "交易" && s.Text.Contains("夜市") && s.Text.Contains("有几率上架"));
        Assert.Contains(idx[501], s => s.Tag == "交易");
    }
}

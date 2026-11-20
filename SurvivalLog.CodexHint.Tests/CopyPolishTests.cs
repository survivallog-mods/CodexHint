using System;
using System.IO;
using System.Linq;
using SurvivalLog.CodexHint.Derive;
using SurvivalLog.CodexHint.Derive.Model;
using Xunit;

namespace SurvivalLog.CodexHint.Tests;

/// <summary>文案治理（plans-copy-review.md ①-⑦）。</summary>
public class CopyPolishTests : IDisposable
{
    private readonly string _dir;

    public CopyPolishTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "codexhint-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    private void Write(string table, string rowsJson)
        => File.WriteAllText(Path.Combine(_dir, table + ".json"),
            "{\"table\":\"" + table + "\",\"rows\":1,\"columns\":[],\"data\":[" + rowsJson + "]}");

    [Fact]
    public void LootShowsChineseMapName()
    {
        // ① 内部点位名必须映射成中文地图名；⑤ 阶段前缀（残破超市=灾后/特价超市=灾前）
        Write("Config_MapPoint", "{\"ID\":1004,\"IsUnlock\":false,\"Name_Local\":\"残破超市\",\"PositionName\":\"NEWMaket01_afterStart\"}," +
                                  "{\"ID\":1001,\"IsUnlock\":true,\"Name_Local\":\"特价超市\",\"PositionName\":\"MarketStart\"}");
        Write("Config_GroundLoot", "{\"RandomGroupId\":8001,\"PositionName\":\"NEWMaket01_after_36\"}," +
                                   "{\"RandomGroupId\":8001,\"PositionName\":\"NEWMaket01_after_37\"}," +
                                   "{\"RandomGroupId\":8001,\"PositionName\":\"Market_24\"}");
        Write("Config_RandomGroup", "{\"ID\":8001,\"IdList\":[500]}");
        var idx = new ItemSourceIndex(new JsonTableSource(_dir));
        var loot = idx[500].Where(s => s.Tag == "拾取").ToList();
        Assert.Contains(loot, s => s.Text.StartsWith("野外探险 残破超市·地图拾取"));
        Assert.Contains(loot, s => s.Text.StartsWith("灾前 特价超市·地图拾取"));
        Assert.All(loot, s => Assert.False(s.Text.Contains("NEWMaket") || s.Text.Contains("Market_"), "内部名泄漏: " + s.Text));
        // 按地图分行去重：残破超市两个点位合并
        Assert.Single(loot.Where(s => s.Text.Contains("残破超市")));
    }

    [Fact]
    public void UnmappableLootOmitted()
    {
        // ① 映射不到中文名的点位整行不输出（宁缺勿谜语）
        Write("Config_MapPoint", "{\"ID\":1,\"Name_Local\":\"家\",\"PositionName\":\"HomeStart\"}");
        Write("Config_GroundLoot", "{\"RandomGroupId\":8001,\"PositionName\":\"XYZ_secret_99\"}");
        Write("Config_RandomGroup", "{\"ID\":8001,\"IdList\":[500]}");
        var idx = new ItemSourceIndex(new JsonTableSource(_dir));
        Assert.DoesNotContain(idx[500], s => s.Tag == "拾取");
    }

    [Fact]
    public void TestSourceFiltered()
    {
        // ⑥ 名称含「测试」的来源不输出
        Write("Config_Plant", "{\"ID\":1,\"Name_Local\":\"豆芽（测试种子）\",\"InCodex\":false,\"Gain\":[500],\"Perfect_Gain\":[],\"Gain_Seed\":[],\"WitheredGain\":[]}");
        var idx = new ItemSourceIndex(new JsonTableSource(_dir));
        Assert.Empty(idx[500]);
    }

    [Fact]
    public void TermsUpdated()
    {
        // ⑤ 术语：交易点（非老主顾）、有几率刷新、固定食材
        Write("Config_StrangerStock", "{\"ItemIdList\":[500]}");
        Write("Config_Shop", "{\"ID\":9,\"ShopName_Local\":\"杂货铺\",\"ItemIdList\":[],\"RandomID\":[80]}");
        Write("Config_RandomGroup", "{\"ID\":80,\"IdList\":[500]}");
        var idx = new ItemSourceIndex(new JsonTableSource(_dir));
        Assert.Contains(idx[500], s => s.Tag == "联系人" && s.Text.Contains("交易货架或支援回礼"));
        Assert.Contains(idx[500], s => s.Text.Contains("有几率上架"));
    }

    [Fact]
    public void FurniFoldWhenSameName()
    {
        // ③ 物品名与制造配方名相同时折叠同义反复
        Write("Config_Item", "{\"ID\":700,\"ItemName_Local\":\"超大型置物架包裹\",\"Category\":20,\"InCodex\":false,\"Prey_Rarity\":0,\"Plant\":0,\"TargetFurnitureID\":900}");
        Write("Config_ProductionList", "{\"ID\":50,\"ShopName_Local\":\"超大型置物架包裹\",\"MaterialList\":[],\"ProductID\":[700],\"perfect_item_id\":[],\"FailedID\":[],\"craft_level\":0,\"InCodex\":false}");
        Write("Config_Furniture", "{\"ID\":900,\"Name_Local\":\"超大型置物架\",\"InCodex\":true,\"ShopPage\":0}");
        var src = new JsonTableSource(_dir);
        var idx = new ItemSourceIndex(src);
        var steps = FurnitureDeriver.Derive(900, src, idx);
        var text = string.Join("|", steps.Select(s => s.Text));
        Assert.Contains("制造「超大型置物架包裹」后放置", text);
        Assert.DoesNotContain("（制造「", text); // 无嵌套同义括号
    }

    [Fact]
    public void RenoLinksItemChain()
    {
        // ④ 装修接物品来源链（不再只有 SourceText 一句）
        Write("Config_RenovationStyle", "{\"ID\":30,\"SourceText_Local\":\"可在工作台制造\",\"ItemId\":20901}");
        Write("Config_Item", "{\"ID\":20901,\"ItemName_Local\":\"白橡木地板\",\"Category\":20,\"InCodex\":false,\"Prey_Rarity\":0,\"Plant\":0,\"TargetFurnitureID\":0}");
        Write("Config_ProductionList", "{\"ID\":60,\"ShopName_Local\":\"白橡木地板\",\"MaterialList\":[201,201],\"ProductID\":[20901],\"perfect_item_id\":[],\"FailedID\":[],\"craft_level\":2,\"InCodex\":false}");
        Write("Config_Item2", "{}"); // 占位无意义
        var src = new JsonTableSource(_dir);
        var idx = new ItemSourceIndex(src);
        var steps = RenovationDeriver.Derive(30, src, idx);
        Assert.Contains(steps, s => s.Text.Contains("白橡木地板") && s.Text.Contains("使用"));
    }

    [Fact]
    public void AidCommissionSource()
    {
        // ② 图级池类物品归因援助委托
        Write("Config_AidCommission", "{\"ID\":31,\"RewardItemIds\":[30025]}");
        var idx = new ItemSourceIndex(new JsonTableSource(_dir));
        Assert.Contains(idx[30025], s => s.Tag == "援助" && s.Text.Contains("援助委托"));
    }

    [Fact]
    public void TagStepsAllListed()
    {
        // 文案治理三轮：不截断——每条途径都是可执行信息（6 商店全列出，无截断提示行）
        var rows = Enumerable.Range(1, 6)
            .Select(i => $"{{\"ID\":{i},\"ShopName_Local\":\"商店{i}\",\"ItemIdList\":[500],\"RandomID\":[]}}");
        Write("Config_Shop", string.Join(",", rows));
        var idx = new ItemSourceIndex(new JsonTableSource(_dir));
        var ordered = UnlockPlanBuilder.CapSteps(idx[500]);
        var shop = ordered.Where(s => s.Tag == "交易").ToList();
        Assert.Equal(6, shop.Count);
        Assert.DoesNotContain(ordered, s => s.Text.Contains("未列出") || s.Text.Contains("等 "));
    }

    [Fact]
    public void StrangerStockShowsTraderName()
    {
        // 交易点归因到人：StrangerStock.ID ↔ SMSSender.ContactId（菜农大爷等）
        Write("Config_StrangerStock", "{\"ID\":30,\"ItemIdList\":[500]}");
        Write("Config_SMSSender", "{\"ContactId\":30,\"DisplayName_Local\":\"菜农大爷\"}");
        var idx = new ItemSourceIndex(new JsonTableSource(_dir));
        Assert.Contains(idx[500], s => s.Tag == "联系人" && s.Text.Contains("菜农大爷"));
    }

    [Fact]
    public void ShelfShowsMapAndName()
    {
        // 文案治理四轮：普通货架定位到灾前商店地图+带货架名
        Write("Config_Shop", "{\"ID\":5,\"ShopName_Local\":\"副食品\",\"ItemIdList\":[500],\"RandomID\":[]}," +
                              "{\"ID\":47,\"ShopName_Local\":\"植物材料专营\",\"ItemIdList\":[501],\"RandomID\":[]}");
        Write("Config_GuidanceFurniture", "{\"ID\":1,\"FurnitureId\":0,\"MapPointID\":2,\"ShopID\":5,\"BuildShopID\":0,\"PackageID\":0}");
        Write("Config_MapPoint", "{\"ID\":2,\"Name_Local\":\"特价超市\",\"PositionName\":\"MarketStart\"}");
        var idx = new ItemSourceIndex(new JsonTableSource(_dir));
        Assert.Contains(idx[500], s => s.Text.Contains("灾前「特价超市」副食品"));
        Assert.DoesNotContain(idx[501], s => s.Text.Contains("植物材料")); // 废弃行不输出
    }

    [Fact]
    public void ContainerSourceShowsMapAndFurniture()
    {
        // 专用池/图级池容器：地图+容器名（GuidanceFurniture.PackageID）
        Write("Config_Shop", "{\"ID\":3,\"ShopName_Local\":\"方便食品货架\",\"ItemIdList\":[],\"RandomID\":[]}");
        Write("Config_GuidanceFurniture", "{\"ID\":122,\"FurnitureId\":800,\"MapPointID\":2,\"ShopID\":0,\"BuildShopID\":0,\"PackageID\":30026}");
        Write("Config_Furniture", "{\"ID\":800,\"Name_Local\":\"纸箱子2\",\"InCodex\":false,\"ShopPage\":0}");
        Write("Config_Package", "{\"ID\":30026,\"PackageName_Local\":\"特价超市图级池·抽3件\",\"ItemIdList\":[500],\"RandomID\":[]}");
        Write("Config_MapPoint", "{\"ID\":2,\"Name_Local\":\"特价超市\",\"PositionName\":\"MarketStart\"}");
        var idx = new ItemSourceIndex(new JsonTableSource(_dir));
        var steps = idx[500];
        Assert.Contains(steps, s => s.Text.Contains("灾前「特价超市」搜「纸箱子2」"));
        Assert.DoesNotContain(steps, s => s.Text.Contains("图级池")); // 池名不再暴露
    }

    [Fact]
    public void LootOrderMapFirst()
    {
        // 文案治理四轮：地名在前；五轮：阶段前缀（IsUnlock=true=灾前）
        Write("Config_MapPoint", "{\"ID\":1,\"IsUnlock\":true,\"Name_Local\":\"家\",\"PositionName\":\"HomeStart\"}," +
                                 "{\"ID\":2,\"IsUnlock\":true,\"Name_Local\":\"特价超市\",\"PositionName\":\"MarketStart\"}");
        Write("Config_GroundLoot", "{\"RandomGroupId\":8001,\"PositionName\":\"Market_24\"}");
        Write("Config_RandomGroup", "{\"ID\":8001,\"IdList\":[500]}");
        var idx = new ItemSourceIndex(new JsonTableSource(_dir));
        var loot = idx[500].FirstOrDefault(s => s.Tag == "拾取");
        Assert.NotNull(loot);
        Assert.Equal("灾前 特价超市·地图拾取", loot.Text);
    }

    [Fact]
    public void BaseAndPrivateShelfWording()
    {
        // 据点 911-920 无人机交易措辞；私人 901-908 灾前摊位
        Write("Config_Shop", "{\"ID\":911,\"ShopName_Local\":\"城东的幸存者营地\",\"ItemIdList\":[500],\"RandomID\":[]}," +
                              "{\"ID\":901,\"ShopName_Local\":\"超市售货员的家中存货\",\"ItemIdList\":[501],\"RandomID\":[]}");
        Write("Config_SMSSender", "{\"ContactId\":30,\"DisplayName_Local\":\"超市女售货员\"}");
        Write("Config_SMS", "{\"ContactId\":30,\"ShelfShopId\":901}");
        Write("Config_StrangerStock", "{\"ID\":30,\"ItemIdList\":[502]}");
        var idx = new ItemSourceIndex(new JsonTableSource(_dir));
        Assert.Contains(idx[500], s => s.Text.StartsWith("无人机交易「城东的幸存者营地」"));
        // 901 私人货架文案归到摊主联系人名
        Assert.Contains(idx[501], s => s.Text.Contains("超市女售货员"));
    }

    [Fact]
    public void PackageThreeWayCopy()
    {
        // 文案治理六轮：真物品包裹=打开文案；内部容器不输出；事件 ItemPackage 归事件文案
        Write("Config_Item", "{\"ID\":12001,\"ItemName_Local\":\"零食大礼包\",\"Category\":1,\"InCodex\":false,\"Prey_Rarity\":0,\"Plant\":0,\"TargetFurnitureID\":0}," +
                             "{\"ID\":500,\"ItemName_Local\":\"薯片\",\"Category\":1,\"InCodex\":false,\"Prey_Rarity\":0,\"Plant\":0,\"TargetFurnitureID\":0}");
        Write("Config_Package", "{\"ID\":1004,\"PackageName_Local\":\"零食大礼包\",\"ItemIdList\":[],\"RandomID\":[80]}," +
                                "{\"ID\":112,\"PackageName_Local\":\"门缝观察随机包（小型食物）\",\"ItemIdList\":[501],\"RandomID\":[]}");
        Write("Config_RandomGroup", "{\"ID\":80,\"IdList\":[500]}");
        Write("Config_Event", "{\"ID\":9001,\"Name_Local\":\"可疑声响\",\"StartRewardSetId\":0,\"RewardSetId\":700,\"ExpiredRewardSetId\":0}");
        Write("Config_RewardSet", "{\"ID\":700,\"ItemChange\":[],\"ItemPackage\":[112],\"RandomGroupId\":0}");
        var idx = new ItemSourceIndex(new JsonTableSource(_dir));
        // 真物品包裹（随机池）：打开文案
        Assert.Contains(idx[500], s => s.Tag == "包裹" && s.Text == "打开「零食大礼包」有几率获得");
        // 内部容器（112 非物品）：包裹层不输出；其内容物 501 由事件层表达
        Assert.DoesNotContain(idx[501], s => s.Text.Contains("门缝观察"));
        Assert.Contains(idx[501], s => s.Tag == "事件" && s.Text == "随机事件「可疑声响」的奖励");
    }
}

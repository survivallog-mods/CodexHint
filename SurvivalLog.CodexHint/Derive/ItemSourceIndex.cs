using System.Collections.Generic;
using System.Linq;
using SurvivalLog.CodexHint.Derive.Model;

namespace SurvivalLog.CodexHint.Derive;

/// <summary>一条解锁途径：Tag=来源类别徽标（烹饪/制造/商店/交易点/拾取/包裹/种植/陷阱/事件/援助/遭遇/直出）。</summary>
public sealed class UnlockStep
{
    public UnlockStep(string tag, string text) { Tag = tag; Text = text; }
    public string Tag { get; }
    public string Text { get; }
}

/// <summary>物品 → 获取途径反查索引（十层：烹饪/制造/商店/交易点/地图拾取/包裹/种植/事件/陷阱/援助）。
/// 文案纪律（plans-copy-review.md）：名称含「测试」的来源不输出；拾取点位必须解析出中文地图名
/// （解析不到整行丢弃）、同地图去重分行；术语对齐游戏内叫法（交易点/有几率刷新/随机事件）。</summary>
public sealed class ItemSourceIndex
{
    private readonly Dictionary<int, List<UnlockStep>> _map = new();
    private readonly Dictionary<int, string> _itemNames = new();
    private readonly HashSet<string> _itemNameSet = new(); // 物品名集合（真包裹物品判定）

    // 货架 → 灾前商店地图名（GuidanceFurniture.ShopID/BuildShopID + GuidanceNpc.ShopID join MapPointID）
    private readonly Dictionary<int, List<string>> _shelfMaps = new();
    // 私人货架 901-908 → 摊主联系人名（SMS.ShelfShopId join ContactId → SMSSender）
    private readonly Dictionary<int, string> _shelfOwner = new();
    // 已由容器层归因的包裹 id（AddPackages 跳过，避免池名文案双份）
    private readonly HashSet<int> _pkgFromContainer = new();

    public ItemSourceIndex(ITableSource src)
    {
        foreach (var it in src.Rows<ItemRow>()) _itemNames[it.ID] = it.Name;
        foreach (var n in _itemNames.Values) _itemNameSet.Add(n);
        MapNames.Init(src);
        InitShelfLocations(src);

        AddCooking(src);
        AddProduction(src);
        AddContainers(src);
        AddShops(src);
        AddStrangerStock(src);
        AddGroundLoot(src);
        AddPackages(src);
        AddPlants(src);
        AddEventRewards(src);
        AddTraps(src);
        AddAidCommissions(src);
    }

    public IReadOnlyList<UnlockStep> this[int itemId]
        => _map.TryGetValue(itemId, out var list) ? list : (IReadOnlyList<UnlockStep>)System.Array.Empty<UnlockStep>();

    public string NameOf(int itemId) => _itemNames.TryGetValue(itemId, out var n) ? n : "";

    private void Add(int itemId, string tag, string text)
    {
        if (itemId <= 0 || string.IsNullOrEmpty(text)) return;
        if (!_map.TryGetValue(itemId, out var list)) _map[itemId] = list = new List<UnlockStep>();
        if (!list.Exists(s => s.Tag == tag && s.Text == text)) list.Add(new UnlockStep(tag, text));
    }

    /// <summary>名称含「测试」=内部测试数据，整行不输出（豆芽等有商店/拾取来源兜底）。</summary>
    private static bool IsTest(string name) => name != null && name.Contains("测试");

    private void AddCooking(ITableSource src)
    {
        foreach (var r in src.Rows<RecipeRow>())
        {
            if (string.IsNullOrEmpty(r.Name) || IsTest(r.Name)) continue;
            Add(r.PerfectItemID, "烹饪", $"烹饪「{r.Name}」产出（完美档）");
            Add(r.GoodItemID, "烹饪", $"烹饪「{r.Name}」产出（优良档）");
            Add(r.NormalItemID, "烹饪", $"烹饪「{r.Name}」产出（普通档）");
            Add(r.FailItemID, "烹饪", $"烹饪「{r.Name}」失败产物");
        }
    }

    private void AddProduction(ITableSource src)
    {
        foreach (var p in src.Rows<ProductionRow>())
        {
            if (string.IsNullOrEmpty(p.Name) || IsTest(p.Name)) continue;
            foreach (var id in p.ProductID) Add(id, "制造", $"制造「{p.Name}」");
            foreach (var id in p.PerfectItemId) Add(id, "制造", $"制造「{p.Name}」（完美产出）");
            foreach (var id in p.FailedID) Add(id, "制造", $"制造「{p.Name}」失败产物");
        }
    }

    private static string Clean(string s) => (s ?? "").Replace("\u200b", "");

    /// <summary>货架定位（文案治理四轮，全部实证无兜底）：
    /// 普通货架=GuidanceFurniture(ShopID/BuildShopID)→地图；购物中心货架/种子商店=GuidanceNpc 摆摊；
    /// 私人货架 901-908=SMS.ShelfShopId→摊主。</summary>
    private void InitShelfLocations(ITableSource src)
    {
        var mapNames = new Dictionary<int, string>();
        foreach (var m in src.Rows<MapPointRow>())
            if (!string.IsNullOrEmpty(m.Name)) mapNames[m.ID] = Clean(m.Name);

        void Locate(int shopId, int mapPointId)
        {
            if (shopId <= 0 || !mapNames.TryGetValue(mapPointId, out var name)) return;
            if (!_shelfMaps.TryGetValue(shopId, out var list)) _shelfMaps[shopId] = list = new List<string>();
            if (!list.Contains(name)) list.Add(name);
        }
        foreach (var gf in src.Rows<GuidanceFurnitureRow>())
        {
            Locate(gf.ShopId, gf.MapPointId);
            Locate(gf.BuildShopId, gf.MapPointId);
        }
        foreach (var gn in src.Rows<GuidanceNpcRow>())
            Locate(gn.ShopId, gn.MapPointId);

        var senderNames = new Dictionary<int, string>();
        foreach (var s2 in src.Rows<SMSSenderRow>())
            if (!string.IsNullOrEmpty(s2.Name)) senderNames[s2.ContactId] = s2.Name;
        foreach (var sms in src.Rows<SMSRow>())
        {
            if (sms.ShelfShopId <= 0 || !senderNames.TryGetValue(sms.ContactId, out var who)) continue;
            _shelfOwner[sms.ShelfShopId] = who;
        }
    }

    /// <summary>地图预置容器（GuidanceFurniture.PackageID）：专用池/图级池包裹的真实来源——
    /// 灾前亲自去地图搜容器开出（实证：特价超市·纸箱子2→图级池·抽3件 等 95 处）。</summary>
    private void AddContainers(ITableSource src)
    {
        var mapNames = new Dictionary<int, string>();
        foreach (var m in src.Rows<MapPointRow>())
            if (!string.IsNullOrEmpty(m.Name)) mapNames[m.ID] = Clean(m.Name);
        var furnNames = new Dictionary<int, string>();
        foreach (var f in src.Rows<FurnitureRow>())
            if (!string.IsNullOrEmpty(f.Name)) furnNames[f.ID] = f.Name;
        var groups = new Dictionary<int, List<int>>();
        foreach (var g in src.Rows<RandomGroupRow>()) groups[g.ID] = g.ItemIds;

        foreach (var gf in src.Rows<GuidanceFurnitureRow>())
        {
            if (gf.PackageId <= 0) continue;
            var mapName = mapNames.TryGetValue(gf.MapPointId, out var mn) ? mn : "";
            var furnName = furnNames.TryGetValue(gf.FurnitureId, out var fn) ? fn : "";
            if (mapName == "" || furnName == "") continue; // 定位不到的容器不输出（宁缺勿谜语）
            var pkg = src.Rows<PackageRow>().FirstOrDefault(x => x.ID == gf.PackageId);
            if (pkg == null) continue;
            _pkgFromContainer.Add(gf.PackageId);
            foreach (var id in pkg.FixedItemIds)
                Add(id, "搜刮", $"灾前「{mapName}」搜「{furnName}」开出");
            foreach (var gid in pkg.RandomGroupIds)
            {
                if (!groups.TryGetValue(gid, out var items)) continue;
                foreach (var id in items)
                    Add(id, "搜刮", $"灾前「{mapName}」搜「{furnName}」开出");
            }
        }
    }

    private void AddShops(ITableSource src)
    {
        var groups = new Dictionary<int, List<int>>();
        foreach (var g in src.Rows<RandomGroupRow>()) groups[g.ID] = g.ItemIds;

        foreach (var s in src.Rows<ShopRow>())
        {
            if (IsTest(s.Name)) continue;
            if (s.ID == 47) continue; // 废弃行实证：13 品全被其他货架覆盖，0 独有

            string fixedText, randomText;
            if (s.ID >= 911 && s.ID <= 920)
            {
                fixedText = $"无人机交易「{s.Name}」有售";
                randomText = $"无人机交易「{s.Name}」有几率上架";
            }
            else if (s.ID >= 931 && s.ID <= 940 && s.Name.Contains("·"))
            {
                var baseName = s.Name.Substring(s.Name.LastIndexOf('·') + 1);
                fixedText = $"无人机交易「{baseName}」的稀有货架有售";
                randomText = $"无人机交易「{baseName}」的稀有货架有几率上架";
            }
            else if (s.ID >= 901 && s.ID <= 908)
            {
                if (!_shelfOwner.TryGetValue(s.ID, out var who)) continue;
                fixedText = $"灾前「{who}」摊位交易";
                randomText = fixedText;
            }
            else if (_shelfMaps.TryGetValue(s.ID, out var maps) && maps.Count > 0)
            {
                var where = string.Join("、", maps);
                fixedText = $"灾前「{where}」{s.Name}有售";
                randomText = $"灾前「{where}」{s.Name}有几率上架";
            }
            else
            {
                fixedText = $"灾前商店货架「{s.Name}」有售";
                randomText = $"灾前商店货架「{s.Name}」有几率上架";
            }
            foreach (var id in s.FixedItemIds) Add(id, "交易", fixedText);
            foreach (var gid in s.RandomGroupIds)
            {
                if (!groups.TryGetValue(gid, out var items)) continue;
                foreach (var id in items) Add(id, "交易", randomText);
            }
        }
    }

    private void AddStrangerStock(ITableSource src)
    {
        // 交易点名字：StrangerStock.ID ↔ SMSSender.ContactId（菜农大爷/超市女售货员/地下室的奥托…）
        var names = new Dictionary<int, string>();
        foreach (var s in src.Rows<SMSSenderRow>())
            if (!string.IsNullOrEmpty(s.Name)) names[s.ContactId] = s.Name;

        foreach (var st in src.Rows<StrangerStockRow>())
        {
            // 池类型三分类（汇编实证）：
            // 9901-9903 = StrangerCommonShelfPool 公共货架池（所有联系人交易货架兜底填充，.cctor 硬编码）
            // 9999 = CollectGiftFallbackPool 回礼兜底池（HasPersonalStock 显式排除）
            // 其余 = 个人池：DrawSnapshot 交易货架 + DrawThankYouGift 支援回礼 双途径
            string text;
            if (st.ID >= 9901 && st.ID <= 9903) text = "任意联系人的交易货架随机上架";
            else if (st.ID == 9999) text = "支援任意联系人的回礼保底小物";
            else
            {
                var who = names.TryGetValue(st.ID, out var n) ? $"「{n}」" : "任意联系人";
                text = $"{who}交易货架或支援回礼随机获得";
            }
            foreach (var id in st.ItemIdList)
                Add(id, "联系人", text);
        }
    }

    private void AddGroundLoot(ITableSource src)
    {
        var groups = new Dictionary<int, List<int>>();
        foreach (var g in src.Rows<RandomGroupRow>()) groups[g.ID] = g.ItemIds;
        var seenMaps = new Dictionary<int, HashSet<string>>(); // itemId → 已收录地图名（去重分行）

        foreach (var gl in src.Rows<GroundLootRow>())
        {
            var mapName = MapNames.Resolve(gl.PositionName);
            if (mapName == null) continue; // 解析不到中文名：整行丢弃（宁缺勿谜语）
            if (!groups.TryGetValue(gl.RandomGroupId, out var items)) continue;
            foreach (var id in items)
            {
                if (!seenMaps.TryGetValue(id, out var seen)) seenMaps[id] = seen = new HashSet<string>();
                if (!seen.Add(mapName)) continue; // 同物品同地图只留一行
                Add(id, "拾取", $"{MapNames.PhasePrefix(mapName)}{mapName}·地图拾取"); // 阶段+地名在前（文案治理五轮）
            }
        }
    }

    /// <summary>包裹三分类（文案治理六轮，实证：82 包裹中仅 14 个是背包物品，名字与物品表同名）：
    /// ① 真物品包裹 → 「打开「X」可获得/有几率获得」（玩家背包里真实存在、可打开）；
    /// ② 内部容器（事件结算直接发内容，玩家看不到包裹）→ 本层不输出，内容物由事件层/容器层表达；
    /// ③ 同上，零实证途径的池包裹一并沉默。</summary>
    private void AddPackages(ITableSource src)
    {
        var groups = new Dictionary<int, List<int>>();
        foreach (var g in src.Rows<RandomGroupRow>()) groups[g.ID] = g.ItemIds;

        foreach (var pk in src.Rows<PackageRow>())
        {
            if (IsTest(pk.Name) || string.IsNullOrEmpty(pk.Name)) continue;
            if (_pkgFromContainer.Contains(pk.ID)) continue; // 已由容器层归因（灾前搜刮）
            var isItem = _itemNameSet.Contains(pk.Name);     // 真包裹物品（与物品表同名）
            if (!isItem) continue;                           // 内部容器：不输出本层文案
            foreach (var id in pk.FixedItemIds)
                Add(id, "包裹", $"打开「{pk.Name}」可获得");
            foreach (var gid in pk.RandomGroupIds)
            {
                if (!groups.TryGetValue(gid, out var items)) continue;
                foreach (var id in items)
                    Add(id, "包裹", $"打开「{pk.Name}」有几率获得");
            }
        }
    }

    private void AddPlants(ITableSource src)
    {
        foreach (var pl in src.Rows<PlantRow>())
        {
            if (string.IsNullOrEmpty(pl.Name) || IsTest(pl.Name)) continue;
            foreach (var id in pl.Gain) Add(id, "种植", $"种植「{pl.Name}」收获获得");
            foreach (var id in pl.PerfectGain) Add(id, "种植", $"种植「{pl.Name}」完美收获时获得");
            foreach (var id in pl.GainSeed) Add(id, "种植", $"种植「{pl.Name}」收获时有几率回收种子");
            foreach (var id in pl.WitheredGain) Add(id, "种植", $"种植「{pl.Name}」枯萎后获得");
        }
    }

    /// <summary>事件奖励。Event 三奖励集 → RewardSet.ItemChange/ItemPackage/RandomGroupId。</summary>
    private void AddEventRewards(ITableSource src)
    {
        var groups = new Dictionary<int, List<int>>();
        foreach (var g in src.Rows<RandomGroupRow>()) groups[g.ID] = g.ItemIds;
        var sets = new Dictionary<int, RewardSetRow>();
        foreach (var r in src.Rows<RewardSetRow>()) sets[r.ID] = r;

        foreach (var e in src.Rows<EventRow>())
        {
            var evName = string.IsNullOrEmpty(e.Name) ? "" : $"随机事件「{e.Name}」";
            foreach (var rid in new[] { e.StartRewardSetId, e.RewardSetId, e.ExpiredRewardSetId })
            {
                if (rid <= 0 || !sets.TryGetValue(rid, out var rs)) continue;
                var via = evName != "" ? $"{evName}的奖励" : "随机事件的奖励";
                foreach (var id in rs.ItemChange) Add(id, "事件", via);
                // ItemPackage=内部容器（包裹 id 非物品 id）：解开到内容物再归事件文案
                foreach (var pkgId in rs.ItemPackage)
                {
                    var pk = src.Rows<PackageRow>().FirstOrDefault(x => x.ID == pkgId);
                    if (pk == null) { Add(pkgId, "事件", via); continue; } // 非包裹（直接物品id）兜住
                    foreach (var id in pk.FixedItemIds) Add(id, "事件", via);
                    foreach (var gid in pk.RandomGroupIds)
                    {
                        if (!groups.TryGetValue(gid, out var gItems)) continue;
                        foreach (var id in gItems) Add(id, "事件", via);
                    }
                }
                if (rs.RandomGroupId > 0 && groups.TryGetValue(rs.RandomGroupId, out var items))
                    foreach (var id in items) Add(id, "事件", $"{via}随机获得");
            }
        }
    }

    /// <summary>陷阱捕获（猎物肉是食物条目：捕获后食用解锁）。</summary>
    private void AddTraps(ITableSource src)
    {
        foreach (var t in src.Rows<TrapRow>())
        {
            if (string.IsNullOrEmpty(t.Name) || IsTest(t.Name)) continue;
            foreach (var id in t.PreyIds) Add(id, "陷阱", $"放置陷阱「{t.Name}」可捕获");
        }
    }

    /// <summary>援助委托奖励（图级池包裹等）。</summary>
    private void AddAidCommissions(ITableSource src)
    {
        foreach (var a in src.Rows<AidCommissionRow>())
            foreach (var id in a.RewardItemIds)
                Add(id, "援助", "完成援助委托时概率获得");
    }
}

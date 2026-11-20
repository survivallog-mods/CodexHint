using System.Collections.Generic;

namespace SurvivalLog.CodexHint.Derive.Model;

/// <summary>推导层表模型（POCO）：字段为 gamekb 镜像 JSON 列（去 _k__BackingField 后缀）
/// 经 JsonTableSource/GameTableSource 映射后的统一形态。列名映射见两处 Source 的映射表。
/// 仓库风格用 class（无 record 先例；本地 SDK 对 record 合成成员报 CS0656）。</summary>

// Config_Item：条目名/分类/图鉴标记/猎物稀有度/所属植物/放置目标家具（放置类物品→家具链）
public sealed class ItemRow
{
    public ItemRow(int id, string name, int category, bool inCodex, int preyRarity, int plant, int targetFurnitureId)
    { ID = id; Name = name; Category = category; InCodex = inCodex; PreyRarity = preyRarity; Plant = plant; TargetFurnitureId = targetFurnitureId; }
    public int ID { get; }
    public string Name { get; }
    public int Category { get; }
    public bool InCodex { get; }
    public int PreyRarity { get; }
    public int Plant { get; }
    public int TargetFurnitureId { get; }
}

// Config_CookingRecipe：菜肴配方（图鉴菜肴条目 sourceId 即其 ID）
public sealed class RecipeRow
{
    public RecipeRow(int id, string name, List<int> specificItems,
        int perfectItemID, int goodItemID, int normalItemID, int failItemID, int minLevel, int craftLevel)
    { ID = id; Name = name; SpecificItems = specificItems; PerfectItemID = perfectItemID; GoodItemID = goodItemID; NormalItemID = normalItemID; FailItemID = failItemID; MinLevel = minLevel; CraftLevel = craftLevel; }
    public int ID { get; }
    public string Name { get; }
    public List<int> SpecificItems { get; }
    public int PerfectItemID { get; }
    public int GoodItemID { get; }
    public int NormalItemID { get; }
    public int FailItemID { get; }
    public int MinLevel { get; }
    public int CraftLevel { get; }
}

// Config_ProductionList：制造配方（MaterialList 重复元素=数量；三个产出字段均为列表）
public sealed class ProductionRow
{
    public ProductionRow(int id, string name, List<int> materialList, List<int> productID,
        List<int> perfectItemId, List<int> failedID, int craftLevel, bool inCodex)
    { ID = id; Name = name; MaterialList = materialList; ProductID = productID; PerfectItemId = perfectItemId; FailedID = failedID; CraftLevel = craftLevel; InCodex = inCodex; }
    public int ID { get; }
    public string Name { get; }
    public List<int> MaterialList { get; }
    public List<int> ProductID { get; }
    public List<int> PerfectItemId { get; }
    public List<int> FailedID { get; }
    public int CraftLevel { get; }
    public bool InCodex { get; }
}

// Config_Shop
public sealed class ShopRow
{
    public ShopRow(int id, string name, List<int> fixedItemIds, List<int> randomGroupIds)
    { ID = id; Name = name; FixedItemIds = fixedItemIds; RandomGroupIds = randomGroupIds; }
    public int ID { get; }
    public string Name { get; }
    public List<int> FixedItemIds { get; }
    public List<int> RandomGroupIds { get; }
}

// Config_StrangerStock（ID=交易人编号，对应 SMSSender.ContactId）
public sealed class StrangerStockRow
{
    public StrangerStockRow(int id, List<int> itemIdList) { ID = id; ItemIdList = itemIdList; }
    public int ID { get; }
    public List<int> ItemIdList { get; }
}

// Config_SMS：短信（ShelfShopId=私人货架 901-908 挂在联系人交易短信上）
public sealed class SMSRow
{
    public SMSRow(int contactId, int shelfShopId) { ContactId = contactId; ShelfShopId = shelfShopId; }
    public int ContactId { get; }
    public int ShelfShopId { get; }
}

// Config_SMSSender：交易人编号 → 展示名（ContactType=1 的 NPC 即交易点）
public sealed class SMSSenderRow
{
    public SMSSenderRow(int contactId, string name) { ContactId = contactId; Name = name; }
    public int ContactId { get; }
    public string Name { get; }
}

// Config_RandomGroup（物品列 = IdList）
public sealed class RandomGroupRow
{
    public RandomGroupRow(int id, List<int> itemIds) { ID = id; ItemIds = itemIds; }
    public int ID { get; }
    public List<int> ItemIds { get; }
}

// Config_GroundLoot
public sealed class GroundLootRow
{
    public GroundLootRow(int randomGroupId, string positionName) { RandomGroupId = randomGroupId; PositionName = positionName; }
    public int RandomGroupId { get; }
    public string PositionName { get; }
}

// Config_Package
public sealed class PackageRow
{
    public PackageRow(int id, string name, List<int> fixedItemIds, List<int> randomGroupIds)
    { ID = id; Name = name; FixedItemIds = fixedItemIds; RandomGroupIds = randomGroupIds; }
    public int ID { get; }
    public string Name { get; }
    public List<int> FixedItemIds { get; }
    public List<int> RandomGroupIds { get; }
}

// Config_Plant：四类收获物均为列表
public sealed class PlantRow
{
    public PlantRow(int id, string name, bool inCodex,
        List<int> gain, List<int> perfectGain, List<int> gainSeed, List<int> witheredGain)
    { ID = id; Name = name; InCodex = inCodex; Gain = gain; PerfectGain = perfectGain; GainSeed = gainSeed; WitheredGain = witheredGain; }
    public int ID { get; }
    public string Name { get; }
    public bool InCodex { get; }
    public List<int> Gain { get; }
    public List<int> PerfectGain { get; }
    public List<int> GainSeed { get; }
    public List<int> WitheredGain { get; }
}

// Config_Trap（名称列 = Name_Local；可捕猎物 = Prey_Id 列表）
public sealed class TrapRow
{
    public TrapRow(int id, string name, List<int> preyIds) { ID = id; Name = name; PreyIds = preyIds; }
    public int ID { get; }
    public string Name { get; }
    public List<int> PreyIds { get; }
}

// Config_SceneRat
public sealed class SceneRatRow
{
    public SceneRatRow(string name, int preyItemId) { Name = name; PreyItemId = preyItemId; }
    public string Name { get; }
    public int PreyItemId { get; }
}

// Config_Furniture
public sealed class FurnitureRow
{
    public FurnitureRow(int id, string name, bool inCodex, int shopPage)
    { ID = id; Name = name; InCodex = inCodex; ShopPage = shopPage; }
    public int ID { get; }
    public string Name { get; }
    public bool InCodex { get; }
    public int ShopPage { get; }
}

// Config_RenovationStyle（SourceText 取 SourceText_Local，空则回退 SourceText；ItemId=装修道具）
public sealed class RenoRow
{
    public RenoRow(int id, string sourceText, int itemId) { ID = id; SourceText = sourceText; ItemId = itemId; }
    public int ID { get; }
    public string SourceText { get; }
    public int ItemId { get; }
}

// Config_MapPoint：场景点位 → 中文地图名（拾取点位名翻译；ID 供货架/容器定位 join）
public sealed class MapPointRow
{
    public MapPointRow(int id, string positionName, string name, bool isUnlock) { ID = id; PositionName = positionName; Name = name; IsUnlock = isUnlock; }
    public int ID { get; }
    public string PositionName { get; }
    public string Name { get; }
    public bool IsUnlock { get; } // True=初始可去（灾前）；False=后续解锁（灾后探索）
}

// Config_GuidanceFurniture：地图预置家具（ShopID/BuildShopID=灾前货架定位；PackageID=可搜容器）
public sealed class GuidanceFurnitureRow
{
    public GuidanceFurnitureRow(int furnitureId, int mapPointId, int shopId, int buildShopId, int packageId)
    { FurnitureId = furnitureId; MapPointId = mapPointId; ShopId = shopId; BuildShopId = buildShopId; PackageId = packageId; }
    public int FurnitureId { get; }
    public int MapPointId { get; }
    public int ShopId { get; }
    public int BuildShopId { get; }
    public int PackageId { get; }
}

// Config_GuidanceNpc：地图预置 NPC（摆摊者，ShopID 定位购物中心货架/种子商店等）
public sealed class GuidanceNpcRow
{
    public GuidanceNpcRow(int mapPointId, int shopId) { MapPointId = mapPointId; ShopId = shopId; }
    public int MapPointId { get; }
    public int ShopId { get; }
}

// Config_AidCommission：援助委托奖励物品（图级池包裹等）
public sealed class AidCommissionRow
{
    public AidCommissionRow(List<int> rewardItemIds) { RewardItemIds = rewardItemIds; }
    public List<int> RewardItemIds { get; }
}

// Config_Event：事件名 + 三个奖励集引用（奖励集 → 物品/家具/随机组）
public sealed class EventRow
{
    public EventRow(int id, string name, int startRewardSetId, int rewardSetId, int expiredRewardSetId)
    { ID = id; Name = name; StartRewardSetId = startRewardSetId; RewardSetId = rewardSetId; ExpiredRewardSetId = expiredRewardSetId; }
    public int ID { get; }
    public string Name { get; }
    public int StartRewardSetId { get; }
    public int RewardSetId { get; }
    public int ExpiredRewardSetId { get; }
}

// Config_RewardSet：物品奖励三通道（ItemChange/ItemPackage/RandomGroupId 标量）
public sealed class RewardSetRow
{
    public RewardSetRow(int id, List<int> itemChange, List<int> itemPackage, int randomGroupId)
    { ID = id; ItemChange = itemChange; ItemPackage = itemPackage; RandomGroupId = randomGroupId; }
    public int ID { get; }
    public List<int> ItemChange { get; }
    public List<int> ItemPackage { get; }
    public int RandomGroupId { get; }
}

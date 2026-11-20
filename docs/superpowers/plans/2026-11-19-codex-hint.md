# SurvivalLog.CodexHint 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 新建独立 BepInEx mod，在图鉴（Codex）未解锁条目详情面板运行时推导并直显解锁方案。

**Architecture:** C# 启动时从游戏内存配置表反查构建 `(category, sourceId) → 解锁步骤` 全量索引，经 WebView 通道（Root 帧挂 `__CODEXHINT__`，scanFrames 遍历 iframe）推给注入 JS；选中态由 C# 轮询游戏 Redux（`State_Web_Codex`）推送，JS 纯 DOM 渲染。推导层与 Il2Cpp 解耦（POCO + `ITableSource`），离线 JSON 与运行时 ConfigManager 同源喂入，可单测。

**Tech Stack:** C# net6.0 / BepInEx.Unity.IL2CPP / Il2CppInterop / Vuplex WebView（`ExecuteJavaScript`）/ 原生 JS（无框架）/ xunit。

**Spec:** `CodexHint/docs/superpowers/specs/2026-11-19-codex-hint-design.md`（v2.1，已批准；含 §3 汇编实证、§8a 三个已定 call，计划不重复论证）

## Global Constraints

- 游戏路径引用（csproj HintPath）：`E:\SteamLibrary\steamapps\common\Survival Log\BepInEx\{core,interop}\`，与 UnlockHub csproj 完全一致。
- 条目判定七分类（spec §3 表，**勿改**）：Food=`Item.Category==1&&InCodex`、Dish=recipeId 存在于 `Config_CookingRecipe`、Plant=`Plant.InCodex`、Prey=`Item.Prey_Rarity>0&&InCodex`、Craft=`ProductionList.InCodex`、Furniture=`Furniture.InCodex`、Reno=styleId 存在于 `Config_RenovationStyle`。
- 文案一律用 `*_Local` 中文字段；plan JSON 键 = `"category:sourceId"` 复合键（菜肴 recipeId 与物品 itemId 撞号，禁止裸 sourceId 键）。
- 覆盖率门槛（spec §8a-2）：表内可闭环条目 100% 推导；真无来源 ≤2（当前已知仅 #11003）降级标记。
- `General.Enabled=false`：停推送停注入，已渲染面板不主动清（spec §8a-3）。
- 行数：文件 ≤500（工具 ≤300）。零 Harmony 为目标（spec §7）。
- 仓库无 git（目录非仓库）：所有「Commit」步骤改为「**保存检查点**」——在 `CodexHint/CHECKPOINTS.md` 追加一行 `日期 TaskN 一句话`；用户要求 git 时另议。
- 测试夹具：单测读 `gamekb/2026-11-12_26615062/tables/*.json`（仓库内相对路径，Tests csproj 里 `CopyToOutputDirectory` 不需要，用 `Path.Combine` 向上找）；对 gamekb 数据只做**存在性断言**（防游戏更新碎测试），精确断言用手写 mini 夹具。
- 不写 README/CHANGELOG 之外文档；不修改配置/构建规则（csproj 是新建文件，按 UnlockHub 模板）。

## Review Focus

1. **配置行字段缺失/null**（`*_Local` 空、`ItemIdList` null、`Prey_Id` 空列表）：deriver 须跳过该来源不崩 → Task 3/4/5 各夹一个 null 字段用例。
2. **菜肴 recipeId 与物品 itemId 撞号**（同为 int，语义随 category 变）：plan 键非复合键即错 → Task 6 断言同 id 双分类共存。
3. **游戏更新致条目集合变化**：覆盖率回退须挡住发版 → Task 5 门槛测试（食物 ≥190、家具 ≥110、其余全量）。
4. **同帧解锁翻转**（选中条目刚解锁，Vue 重渲染清 DOM）：JS 面板须随 MutationObserver 自清 → Task 8 实机验证项。
5. **WebView 重建/新 iframe**（重开 Codex）：bootstrap 探活重注 + 数据重推 → Task 8/9 实机验证项（离线不可测，验证清单显式列出）。

---

### Task 1: 工程骨架（编译通过 + 空测试跑通）

**Files:**
- Create: `CodexHint/SurvivalLog.CodexHint/SurvivalLog.CodexHint.csproj`
- Create: `CodexHint/SurvivalLog.CodexHint/CodexHintPlugin.cs`
- Create: `CodexHint/SurvivalLog.CodexHint/modcenter.json`
- Create: `CodexHint/SurvivalLog.CodexHint.Tests/SurvivalLog.CodexHint.Tests.csproj`
- Create: `CodexHint/SurvivalLog.CodexHint.Tests/SmokeTests.cs`

**Interfaces:**
- Produces: 命名空间 `SurvivalLog.CodexHint`（主）/ `SurvivalLog.CodexHint.Tests`（测试）；
  `CodexHintPlugin`（BepInEx BasePlugin，cfg `General.Enabled`，默认 true）；
  测试工程引用主工程，xunit。

- [ ] **Step 1: 写主工程 csproj**——UnlockHub csproj 逐行照抄，改 `AssemblyName/RootNamespace=SurvivalLog.CodexHint`、`Version=0.1.0`、删 ModShared Compile 项；保留 modcenter.json EmbeddedResource 项。
- [ ] **Step 2: 写 `CodexHintPlugin.cs`**——`[BepInPlugin("com.local.survivallog.codexhint","图鉴解锁方案","0.1.0")]`，`BasePlugin` 子类；`Load()` 里 `Config.Bind("General","Enabled",true,"总开关")` + `Log.LogInfo("CodexHint loaded (enabled=...)")`，其余空。using 与 UnlockHubPlugin 一致。
- [ ] **Step 3: 写 `modcenter.json`**——mcSpec 2 最小骨架（照 UnlockHub 的结构：id=`com.local.survivallog.codexhint`、display=`图鉴解锁方案`、summary 一句话、groups 空、`usage.intro` 一句话）；字段名以 UnlockHub 文件为准。
- [ ] **Step 4: 写测试工程 csproj + `SmokeTests.cs`**——xunit，`[Fact] void PluginTypeExists()` 反射断言 `typeof(SurvivalLog.CodexHint.CodexHintPlugin)` 非空；csproj 无游戏 dll 引用（纯逻辑测试）。
- [ ] **Step 5: 编译 + 测试**——`dotnet build CodexHint/SurvivalLog.CodexHint` 与 `dotnet test CodexHint/SurvivalLog.CodexHint.Tests`，均 PASS。
- [ ] **Step 6: 保存检查点**（CHECKPOINTS.md 追加 `Task1 骨架`）。

### Task 2: 表模型 POCO + JsonTableSource（夹具加载）

**Files:**
- Create: `CodexHint/SurvivalLog.CodexHint/Derive/Model/TableRows.cs`
- Create: `CodexHint/SurvivalLog.CodexHint/Derive/Model/ITableSource.cs`
- Create: `CodexHint/SurvivalLog.CodexHint.Tests/Fixtures/JsonTableSource.cs`
- Test: `CodexHint/SurvivalLog.CodexHint.Tests/TableSourceTests.cs`

**Interfaces:**
- Produces:
  - `record ItemRow(int ID, string Name, int Category, bool InCodex, int PreyRarity, int Plant)`；`record RecipeRow(int ID, string Name, List<int> SpecificItems, int PerfectItemID, int GoodItemID, int NormalItemID, int FailItemID, int MinLevel, int CraftLevel)`；`record ProductionRow(int ID, string Name, List<int> MaterialList, List<int> ProductID, int PerfectItemId, int FailedID, int CraftLevel, bool InCodex)`；`record ShopRow(int ID, string Name, List<int> FixedItemIds, List<int> RandomGroupIds)`；`record StrangerStockRow(List<int> ItemIdList)`；`record RandomGroupRow(int ID, List<int> ItemIds)`；`record GroundLootRow(int RandomGroupId, string PositionName)`；`record PackageRow(int ID, string Name, List<int> FixedItemIds, List<int> RandomGroupIds)`；`record PlantRow(int ID, string Name, bool InCodex, List<int> Gain, List<int> PerfectGain, List<int> GainSeed, List<int> WitheredGain)`；`record TrapRow(int ID, string Name, List<int> PreyIds)`；`record FurnitureRow(int ID, string Name, bool InCodex, int ShopPage)`；`record RenoRow(int ID, string SourceText)`。
  - `interface ITableSource { IReadOnlyList<T> Rows<T>() where T : class; }`——按 record 类型给全表。
  - `JsonTableSource : ITableSource`（Tests/Fixtures）：构造入 tables 目录 Path；解析规则：`{table,data:[{列:值}]}`，键去 `_k__BackingField` 后缀，bool 是 `"True"/"False"` 字符串，列表字段为 JSON 数组，缺失列 → 类型默认值（**不许抛**）。

- [ ] **Step 1: 写失败测试**——`TableSourceTests`：① `ItemsContainBinggan`：`Rows<ItemRow>()` 存在 `Name=="饼干"` 且 `Category==1`（gamekb 存在性断言）；② `HandlesMissingColumns`：手写 mini JSON 目录（Temp 目录写 `Config_Item.json` 只含一行两列）不抛、默认值填充；③ `HandlesStringBools`：mini JSON 里 `"InCodex":"True"` 解析为 true。
- [ ] **Step 2: 跑测试确认失败**（类型不存在，编译错误即算 FAIL）。
- [ ] **Step 3: 实现 TableRows.cs（纯 record）+ ITableSource + JsonTableSource**——解析映射表：record 类型 → 文件名（`ItemRow→Config_Item` 等 12 张）；标量用 `Convert`（字符串 int/bool 容错）。
- [ ] **Step 4: 跑测试 PASS**（gamekb 路径：Tests 里 `Path.Combine("..","..","..","..","..","gamekb","2026-11-12_26615062","tables")` 向上定位，找不到时 `Skip` 并警告——防 CI 环境）。
- [ ] **Step 5: 保存检查点**。

### Task 3: ItemSourceIndex（物品 → 来源链反查索引）

**Files:**
- Create: `CodexHint/SurvivalLog.CodexHint/Derive/ItemSourceIndex.cs`
- Test: `CodexHint/SurvivalLog.CodexHint.Tests/ItemSourceIndexTests.cs`

**Interfaces:**
- Consumes: `ITableSource` + Task 2 全部 record。
- Produces: `record UnlockStep(string Tag, string Text)`；`class ItemSourceIndex { ItemSourceIndex(ITableSource src); IReadOnlyList<UnlockStep> this[int itemId] { get; } }`——Tag ∈ `烹饪/制造/商店/老主顾/拾取/包裹/种植`（陷阱归猎物 deriver，不在此）；构建一次全量索引。

- [ ] **Step 1: 写失败测试**（gamekb 夹具）——① `FotiaoqiangHasCookSource`：item 12313（佛跳墙完美）含 `Tag=="烹饪"` 步骤；② `PreyMeatHasNoShopSource`：30000（小家鼠肉）无 `商店` 步骤（其来源是陷阱，此索引不含）；③ `NullFieldsDoNotThrow`：mini 夹具造 `ShopRow{FixedItemIds=null}` 构建不抛；④ `RandomGroupShopSource`：某商店 RandomID 指向的组内 item 获得 `商店` 步骤（mini 夹具造 2 商店+1 组闭环断言文案含商店名）。
- [ ] **Step 2: 跑测试确认失败。**
- [ ] **Step 3: 实现**——七层反查（烹饪 Perfect/Good/Normal/Fail 档位文案、制造 ProductID/perfect/Failed、商店固定+随机、老主顾、地面拾取（组→item，带 PositionName）、包裹固定+随机、种植 Gain/Perfect_Gain/Gain_Seed/WitheredGain）；空列表/null 全跳过；`_Local` 名缺失时回退裸名（`名称缺` 不输出步骤）。
- [ ] **Step 4: 跑测试 PASS。**
- [ ] **Step 5: 保存检查点。**

### Task 4: 直查类 deriver（菜肴/制造/装修/猎物）

**Files:**
- Create: `CodexHint/SurvivalLog.CodexHint/Derive/Derivers.cs`（四个 deriver 同文件，均 <150 行总量级；若超 300 拆 `Derivers2.cs`）
- Test: `CodexHint/SurvivalLog.CodexHint.Tests/DirectDeriverTests.cs`

**Interfaces:**
- Consumes: `ITableSource`、`ItemSourceIndex`、`UnlockStep`。
- Produces: 四个静态类 `DishDeriver/CraftDeriver/RenovationDeriver/PreyDeriver`，签名统一 `static IReadOnlyList<UnlockStep> Derive(int sourceId, ITableSource src, ItemSourceIndex itemSrc)`。
  - Dish：`烹饪『{RecipeName}』产出（完美/优良/普通档）＋需烹饪等级 {MinLevel}`；食材仅当 `SpecificItems` 非空时附一行（物品名查 `ItemRow`）。
  - Craft：`制造『{Name}』：{材料名×数量, 空格分隔}（需制造等级 {CraftLevel}）`。
  - Renovation：`SourceText` 非空直出 Tag=`直出`；空则返回空（走降级）。
  - Prey：`用『{TrapName}』可捕获`（遍历 TrapRow.PreyIds）＋`场景遭遇（{SceneRatName}）`——SceneRat 需在 Task 2 补 `record SceneRatRow(string Name, int PreyItemId)`。

- [ ] **Step 1: 写失败测试**——① `DishFromRecipeId`：recipeId 1001 → 步骤含「佛跳墙」与「烹饪等级」；② `CraftListsMaterials`：任一 `ProductionRow` id → 步骤含「×」与等级；③ `RenoDirectText`：任一 RenoRow → 步骤 Text==SourceText；④ `PreyTrapCoverage`：全部 23 个 `PreyRarity>0&&InCodex` item 的 Derive 非空（**猎物 100% 门槛**，Review Focus 3 首例）；⑤ `DishUnknownIdEmpty`：sourceId=999999 返回空。
- [ ] **Step 2: 跑测试确认失败。**
- [ ] **Step 3: 实现**（Dish/Craft 配方即条目，无链式；材料数量从 `MaterialList` 计数）。
- [ ] **Step 4: 跑测试 PASS。**
- [ ] **Step 5: 保存检查点。**

### Task 5: 链式 deriver（食物/植物/家具）+ 事件奖励层 + 覆盖率门槛

**Files:**
- Create: `CodexHint/SurvivalLog.CodexHint/Derive/ChainedDerivers.cs`（Food/Plant/Furniture 三 deriver；超 300 行拆）
- Create: `CodexHint/SurvivalLog.CodexHint/Derive/EventRewardIndex.cs`
- Test: `CodexHint/SurvivalLog.CodexHint.Tests/ChainedDeriverTests.cs`

**Interfaces:**
- Consumes: Task 3/4 全部。
- Produces:
  - `FoodDeriver/PlantDeriver/FurnitureDeriver.Derive(int sourceId, ITableSource src, ItemSourceIndex itemSrc, EventRewardIndex evt)`，同签名约定。
  - `EventRewardIndex { EventRewardIndex(ITableSource src); IReadOnlyList<UnlockStep> ForFurniture(int furnitureId); }`——从 `Config_Event`/`Config_CodexMilestone` 奖励列建家具→「完成『事件/里程碑』获得」索引（奖励列语义按 spec §8 实现期校准：先 dump grep `Config_Event` 奖励字段名 + `RewardType` 值域，落成常量表写在文件头注释）。
  - Furniture：`商店第 {ShopPage} 页有售`（ShopPage>0）∪ 制造产出 ∪ 事件奖励。
  - Plant：种子 item（`ItemRow.Plant==plantId`）→ 种子的 `ItemSourceIndex` 步骤（`种植/收获『植物名』需先获得种子：…`）；无种子来源 → 空（降级）。
  - Food：`ItemSourceIndex[itemId]` 前缀「食用解锁：」。

- [ ] **Step 1: 写失败测试**——① `FoodCoverageGate`：全部 `Category==1&&InCodex` item（191 个）非空数 **≥190**；② `FurnitureCoverageGate`：`Furniture.InCodex`（111 个）非空数 **≥110**（事件层补 33 缺口后实际应为 111）；③ `PlantFullCoverage`：38/38；④ `FoodBingganHasEatPrefix`：饼干步骤以「食用解锁」开头；⑤ `FurnitureEventReward`：id 9300（幸存者纪念杯）步骤含「完成」（事件层生效标志）；⑥ `PlantSeedChain`：任一植物步骤含「种子」。
- [ ] **Step 2: 跑测试确认失败（覆盖率不足即红）。**
- [ ] **Step 3: 实现**——先做 `EventRewardIndex`（对 dump.cs grep 校准奖励列；校准结论写文件头注释），再三 deriver。
- [ ] **Step 4: 跑测试 PASS（门槛绿 = spec §10 离线门槛达成）。**
- [ ] **Step 5: 保存检查点。**

### Task 6: UnlockPlan 构建器 + JSON 序列化 + 降级

**Files:**
- Create: `CodexHint/SurvivalLog.CodexHint/Derive/UnlockPlan.cs`
- Test: `CodexHint/SurvivalLog.CodexHint.Tests/UnlockPlanTests.cs`

**Interfaces:**
- Consumes: 全部 deriver。
- Produces: `class UnlockPlanBuilder { UnlockPlanBuilder(ITableSource src); string BuildJson(); }`——枚举七分类全量条目（判定用 Global Constraints 表），产出 `{"plans":{"{cat}:{sid}":[{"tag":..,"text":..}]},"degraded":["{cat}:{sid}",..]}`；空步骤条目进 `degraded` 不进 `plans`。`System.Text.Json` 序列化（camelCase）。

- [ ] **Step 1: 写失败测试**——① `CompositeKeyCollision`：存在 id 同为 int 的 `{2}:{id}` 与 `{1}:{id}` 双键共存（Review Focus 2）；② `DegradedListSmall`：`degraded` 数量 ≤2 且含 `1:11003`；③ `JsonParsable`：输出可 `JsonDocument.Parse`；④ `DishCount525`：`plans` 中 category 2 的键数 == 525。
- [ ] **Step 2: 跑测试确认失败。**
- [ ] **Step 3: 实现**（纯组装，无新反查）。
- [ ] **Step 4: 跑测试 PASS。**
- [ ] **Step 5: 保存检查点。**

### Task 7: 运行时接入（GameTableSource + 启动推导）

**Files:**
- Create: `CodexHint/SurvivalLog.CodexHint/Game/GameTableSource.cs`
- Create: `CodexHint/SurvivalLog.CodexHint/Game/CodexData.cs`
- Modify: `CodexHint/SurvivalLog.CodexHint/CodexHintPlugin.cs`
- Test: 编译 + 实机日志（无新单测；POCO→record 映射逻辑简单，靠 Task 2 已测的 JsonTableSource 对拍思路）

**Interfaces:**
- Consumes: `ConfigManager`（interop，UnlockHub 先例）、Task 6 `UnlockPlanBuilder`。
- Produces: `GameTableSource : ITableSource`（12 张表 ConfigManager → record 映射，`Get_Config_XXX` + `GetAll_XXX` 或逐 id 枚举，以 UnlockHub 实际可用 API 为准）；`CodexData.EnsureBuilt()`——启动后延迟到主菜单/存档加载完成构建一次，日志输出七分类覆盖数（与离线门槛对拍）。

- [ ] **Step 1: 实现 GameTableSource**——Il2Cpp List→C# List 转换注意 `Il2CppSystem.Collections.Generic`；`_Local` 字段运行时已填中文（builder check 已核实）。
- [ ] **Step 2: Plugin.Load 接入**——`Enabled` 为 true 时启动协程/轮询调 `EnsureBuilt()`，完成后 `Log.LogInfo` 每分类 `条目数/有方案数`。
- [ ] **Step 3: 编译通过 + 实机加载**——启动游戏到主菜单，BepInEx 日志出现七分类覆盖行，食物 ≥190/191、家具 111/111（与离线一致；不一致 = 映射 bug，回修）。
- [ ] **Step 4: 保存检查点。**

### Task 8: WebView 通道 + 注入 JS 渲染

**Files:**
- Create: `CodexHint/SurvivalLog.CodexHint/Rail/CodexRail.cs`
- Create: `CodexHint/SurvivalLog.CodexHint/Rail/codexhint.page.js`（csproj EmbeddedResource）
- Modify: `CodexHintPlugin.cs`（tick 驱动）

**Interfaces:**
- Consumes: `BaseSingleton<ReduxUISystem>.Instance.GetWebUILayer()`（TabInjector.cs:308 先例）、`CodexData` plan JSON。
- Produces: `CodexRail`：① bootstrap 幂等注入 Root 帧（`window.__CODEXHINT__||(function(){...})()`，JS 全文内嵌 csproj 资源读出）；② 探活（`__CODEXHINT__.ping` 回执，WebView 重建复位重注，RailUi.cs 探针状态机同款）；③ `PushData(json)`/`PushSelection(cat,sid)` seq+回执防乱序；④ `GetWebView()` 每次取新 interop 包装（RailUi.cs 793 行注释教训）。
  JS：`setData/setSelection` 两入口；scanFrames 遍历 `#iframe-container` 找 `.codex-root` 帧；在 `.detail-locked-hint` 后插 `.codexhint-panel`（tag 徽标+文本列表，样式贴 Codex token `--text-dim/--warm-gold`，13px/1.7）；MutationObserver 兜底：同帧解锁态出现（`.detail-name.locked` 消失）→ 自删 panel（Review Focus 4）；异常计数 >20 自停。

- [ ] **Step 1: 实现 CodexRail（移植 RailUi 模式）+ codexhint.page.js。**
- [ ] **Step 2: 编译 + 实机**：打开图鉴选未解锁条目 → 面板出现、方案正确（抽查七分类各 1 条，与 gamekb 数据人工对拍）。
- [ ] **Step 3: 实机验证清单**（Review Focus 5）：关闭重开图鉴（新 iframe）面板仍渲染；开关 UI/切场景（WebView 重建）后仍渲染；`Enabled=false` 后重开图鉴无面板（§8a-3 口径）。
- [ ] **Step 4: 保存检查点。**

### Task 9: 选中态 C# 轮询推送 + 联调收尾

**Files:**
- Modify: `CodexRail.cs`（选中态 tick）、`CodexHintPlugin.cs`
- Create: `CodexHint/SurvivalLog.CodexHint/README.md`、`CHANGELOG.md`

**Interfaces:**
- Consumes: `ReduxUISystem.GetStateByUI(typeof(WebUI_Codex))` → `State_Web_Codex.SelectedSourceId/CurrentTab`（RailCommands.cs 先例；builder check 已核实存在）。
- Produces: tick 轮询选中态（与 `CurrentTab` 组合成复合键推送），变化才推；README/CHANGELOG 按 UnlockHub 文风一段式。

- [ ] **Step 1: 实现轮询推送**（30 行级；`GetStateByUI` 若 interop 签名不符，按 dump `State_Web_Codex` 实际属性名校准，偏差记录 CHANGELOG）。
- [ ] **Step 2: 实机联调**：切换 tab/搜索过滤/选中已解锁条目 → 面板正确出现/消失/不出现；解锁瞬间（吃掉一样食物）同帧自清。
- [ ] **Step 3: 全量回归**：七分类抽查 + GM 全解锁对照截图（spec §10）。
- [ ] **Step 4: README/CHANGELOG + 保存检查点。**

## Self-Review 结论

- Spec 覆盖：§3 判定→Global Constraints；§5 七类→Task 3/4/5；§5 降级→Task 6；§6 通道/渲染→Task 8/9；§7 骨架→文件清单；§8 校准项→Task 5（EventRewardIndex）/Task 9（GetStateByUI）；§8a-1/2/3→Task 8/9 与 Task 5/6 门槛测试；§10→Task 5 门槛 + Task 8/9 实机清单。无缺漏。
- 比例：计划 ~1.2 倍 spec 长度，任务 9 个，无函数体转录。
- 类型一致：`UnlockStep(Tag,Text)`/`ITableSource.Rows<T>()`/deriver 四参签名在 Task 3-6 间已对齐；Task 2 补 `SceneRatRow` 已注明。

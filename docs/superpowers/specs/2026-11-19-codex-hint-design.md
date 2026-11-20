# SurvivalLog.CodexHint — 图鉴解锁方案直显（设计文档）

日期：2026-11-19 ｜ 状态：待批准 ｜ v2.1（builder check 修订：选中态改 C# Redux 推送、iframe 架构勘误）

## 1. 意图与动机

游戏内置「生存图鉴」（Codex WebUI）对未解锁条目只显示剪影 + `???` + 分类级泛化提示
（「尝试食用获取该条目」），右侧详情面板大块空白。玩家不知道缺的是什么、去哪补。

本 mod 在未解锁条目的详情面板空白区（原 `des`/`stats` 渲染位）直接显示**该条目**
的明确解锁方案（完全显式：条目名 + 获取链），免去查攻略。

只显示方案，不解锁任何条目、不改任何游戏行为。

## 2. 目标 / 非目标 / 反目标

**目标**
- 选中任一未解锁条目，红框区域列出该条目的解锁途径（可多条），如
  「烹饪『红烧肉』（完美档）可产出 · 食谱：老主顾随机出售」。
- 七分类全覆盖：食物/菜肴/植物/猎物/制造/家具/装修。
- 方案数据 100% 运行时从内存配置表推导（游戏更新自动跟随），**零人工数据表**。
- cfg 总开关（ModCenter 启停位，仓库惯例），关=完全还原官方 UI。

**非目标**：不解锁/改解锁逻辑；不做英文；不动卡片网格（只动详情面板）；不做设置界面。

**反目标**：注入失败必须静默降级（不渲染），绝不导致游戏 UI 崩溃/黑屏；不修改游戏文件；
**不做人工兜底文案表**——推导不出就显示官方分类提示，宁缺勿错。

## 3. 可行性实证（2026-11-19，离线完成）

数据：`gamekb/2026-11-12_26615062/tables/*.json`（全配置表镜像）；
工具：`CodexHint/tools/coverage.py`、`gap_report.py`、`feasibility.py`（可复跑）。

**条目判定**（`IsInCodex@RVA 0x26A8370` 反汇编逐 case 实证，跳转表 1-7）：

| 分类 | sourceId 含义 | 判定 | 条目数（对照 UI 锚点） |
|---|---|---|---|
| 1 食物 | itemId | `Item.Category==1 && Item.InCodex` | 191（83/??? ✓） |
| 2 菜肴 | **recipeId**（非 itemId） | `Get_Config_CookingRecipe(id)!=null` | 525（170/??? ✓） |
| 3 植物 | plantId | `Plant.InCodex` | 38（34/38 ✓） |
| 4 猎物 | itemId | `Item.Prey_Rarity>0 && InCodex` | 23（23/23 ✓） |
| 5 制造 | productionId | `ProductionList.InCodex` | 164（80/??? ✓） |
| 6 家具 | furnitureId | `Furniture.InCodex` | 111（64/111 ✓） |
| 7 装修 | styleId | `GetRenovationStyle(id)!=null` | 24（2/??? ✓） |

**反查覆盖率**（现行查层：烹饪/制造/商店固定+随机/老主顾/地面拾取/种植/陷阱/包裹+包裹随机）：

| 分类 | 覆盖 | 说明 |
|---|---|---|
| 菜肴/植物/猎物/制造/装修 | **100%** | 菜肴=配方本身（名称+食材+等级）；猎物=`Config_Trap.Prey_Id` 列表恰好 23/23；装修=`SourceText_Local` 全部非空 |
| 食物 | 190/191（99.5%） | 仅 `#11003 食品大包裹` 无表来源（命中全为 id 碰撞），疑开局/剧情给予，降级显示分类提示 |
| 家具 | 78/111（70%） | 33 个缺口全部归因 `Config_Event[40430+].ConditionSetId` 等事件奖励链（纪念系列/剧情家具），见 §5 事件层反查 |

注：coverage.py 将 `ProductionList.ProductID`（`List<int>`）按标量处理，制造来源漏算——
运行时按 List 迭代后实际覆盖率只会更高，上表为**保守下限**（builder check 勘误）。

**结论：运行时方案可行**，无需静态数据包。剩余缺口补两层反查（事件奖励、奖励 RandomGroup）即表内闭环。

## 4. 方案取舍（已定）

- **数据来源：运行时内存表反查，零人工数据**（选）——覆盖率已实证 ≥99.5%，事件层补齐家具缺口；
  静态数据包需每次游戏更新重生成+发版，弃。
- **宿主：新建独立 mod `CodexHint`**（选）——UnlockHub 定位"零 Harmony 纯内存表编辑"，
  本功能需 Harmony+WebView 注入，不混入。
- **UI 注入：C# 推状态 + JS 插节点**（选，v2.1 改）——选中态由 C# 读游戏 Redux 状态推送，
  JS 只做 DOM 插入渲染，不碰 Vue 内部；接管 Vue 实例、JS 侧自检选中态均弃（§6/§8a-1）。

## 5. 推导规则（C# 侧，启动时一次构建）

输出：`Dictionary<(int category, int sourceId), List<UnlockStep>>`；
`UnlockStep { string tag; string text; }`（tag ∈ 烹饪/制造/商店/老主顾/拾取/种植/陷阱/包裹/事件/直出，前端配色）。

**物品来源反查（食物直接用；植物种子、菜肴食谱的获取链复用同一路径）**：
- `Config_CookingRecipe.Perfect/Good/Normal/FailItemID` → 烹饪产出（档位标注）
- `Config_ProductionList.ProductID/perfect_item_id/FailedID` → 制造产出
- `Config_Shop.ItemIdList`（固定）+ `RandomID→Config_RandomGroup`（随机上架）
- `Config_StrangerStock.ItemIdList` → 老主顾随机货
- `Config_GroundLoot.RandomGroupId→Config_RandomGroup` → 地图拾取
- `Config_Package.ItemIdList/RandomID` → 包裹（空投/补给）
- `Config_Plant.Gain/Perfect_Gain/Gain_Seed/WitheredGain` → 种植收获

**各分类规则**：
| 分类 | 方案生成 |
|---|---|
| 食物 | 物品来源反查全链 + 「食用解锁」前缀 |
| 菜肴 | 配方即条目：`RecipeName_Local` + `SpecificItems`（具体食材）/`TagCombo`（标签组合）+ `MinLevel` 烹饪等级 + `craft_level`；产出档位（完美/优良/普通）标注 |
| 植物 | 种子 item（`Item.Plant==plantId` 反查）→ 种子来源再走物品反查；藤蔓类若种子无表来源（`Plant_RareSeedDiscover` 代码级）→ 降级分类提示 |
| 猎物 | `Config_Trap.Prey_Id` 列表 → 「用『活捉笼』等捕获」；`Config_TrapBait`（2235 行，Prey_Id 列表）补充诱饵；`Config_SceneRat.PreyItemId` 补「场景遭遇」 |
| 制造 | 配方即条目：`ShopName_Local` + `MaterialList`（材料×数量）+ `craft_level` 等级门槛 |
| 家具 | `Config_Furniture.ShopPage>0` → 「商店第 N 页有售」；制造产出（ProductID==家具id）补充；`Config_Event` 奖励链（33 缺口归因）：事件奖励家具 → 「完成『事件名/描述』获得」（事件文案用 `Config_Event` 本地化字段，RewardType/RewardId 映射实现期对 dump 校准，同 `Config_CodexMilestone.RewardType/RewardId`）。注：`Config_ShopFurniture` 无家具 id 列（仅物品列，与 Config_Shop 同构），不参与反查（builder check 勘误） |
| 装修 | `Config_RenovationStyle.SourceText_Local` 直出（24/24 非空） |

**降级（非兜底）**：任一条目推导结果为空 → 显示官方分类提示原文（`lockedHint` 同款文案），
前端样式区分「方案」与「提示」。不维护任何人工数据表。

条目名统一用 `*_Local` 中文字段。

## 6. 通道与渲染（C# 主导，JS 纯渲染）

复用 CERail 成熟模式（`CabinetEverywhere/RailUi.cs`）。架构勘误（builder check 实证）：
WebView 为 Root.html + `#iframe-container` 多 iframe，Codex 是其中一个 iframe，
面板重开=新 iframe。

- **bootstrap**：`window.__CODEXHINT__||(function(){ codexhint.page.js 全文 })()`，
  C# tick 幂等注入 + `__CODEXHINT__` 探活（WebView 重建自动重挂）；
  bootstrap 与数据挂 **Root 帧**，Codex 帧内的 DOM 检测须遍历
  iframe `contentDocument`（CERail scanFrames 同款）。
- **选中条目：C# 侧 Redux 状态推送（builder check 改采）**：
  `BaseSingleton<ReduxUISystem>.Instance.GetStateByUI(typeof(WebUI_Codex))`
  返回 `State_Web_Codex`，`SelectedSourceId`/`CurrentTab`/`EntryList` 均为现成
  ReactiveProperty/ObservableList（RailCommands.cs 已有访问先例）。C# tick 轮询
  选中态变化，与 plan 数据一并推给 JS。JS 零状态检测、纯渲染——
  原「Vue proxy / DOM 对齐」两级 JS 方案整体废弃（不碰 Vue 内部的目标不变且达成，
  最大技术风险点消除）。
- **数据推送**：启动推导完成后推一次全量 plan JSON（`__CODEXHINT__.setData(json)`，
  seq/回执防乱序，量级几十 KB）+ 选中态增量推（`setSelection(category, sourceId)`）。
- **渲染**：Codex 帧内在 `.detail-locked-hint` 后插入 `div.codexhint-panel`
  （tag 徽标 + 文本列表；贴 Codex design token `--text-dim`/`--warm-gold`/
  `--border-color`，13px/行距 1.7 对齐 `.detail-des`）。
  解锁态/方案为空 → 不插任何节点；选中变化由 C# 推送驱动重渲染
  （MutationObserver 仅兜底同帧内解锁状态翻转）。
- 所有 DOM 操作 try-catch，异常计数超限自动停用（本次会话），日志一条。

## 7. 工程骨架（新目录 `CodexHint/`，参照 UnlockHub+CabinetEverywhere）

```
CodexHint/
  SurvivalLog.CodexHint/
    CodexHintPlugin.cs        # BepInEx 入口、cfg（General.Enabled）、tick 驱动
    Derive/
      ItemSourceIndex.cs      # 物品来源反查索引（烹饪/制造/商店/老主顾/拾取/包裹/种植，7 处复用）
      FoodDeriver.cs / DishDeriver.cs / PlantDeriver.cs / PreyDeriver.cs
      CraftDeriver.cs / FurnitureDeriver.cs / RenovationDeriver.cs
      EventRewardIndex.cs     # Event/Milestone 奖励链（家具缺口层）
    UnlockPlan.cs             # (category,sourceId)→steps 构建 + 空结果降级标记
    Rail/
      CodexRail.cs            # WebView 通道（bootstrap/探活/推送，移植 RailUi 模式）
      codexhint.page.js       # 嵌入资源
    modcenter.json / README.md / CHANGELOG.md
  tools/                      # 可行性脚本（已存在，随仓库保留）
  docs/superpowers/specs/     # 本文档
```

行数红线：文件 ≤500（工具 ≤300）；deriver 各自独立可测。
WebView 入口走 `BaseSingleton<ReduxUISystem>.Instance.GetWebUILayer()`（TabInjector.cs
先例，builder check 实证），预计**零 Harmony**；若个别路径必须 hook 再引入并记录。

## 8. 留给实现的决定

- `Config_Event` 奖励列与 `RewardType` 枚举语义、`Config_CodexMilestone.RewardType/RewardId`
  映射 → 实现期对 dump/实机校准（离线表已确认线索存在）。
- `#11003 食品大包裹` 来源 → 实机 GM 验证后归入降级或事件层。
- 推送时机：全量一次 vs 按 tab 懒推 → 先全量，实测卡顿再改懒推。
- 菜肴 `TagCombo`（标签组合）与 `SpecificItems`（指定食材）的文案表达 → 实现期出样稿。

## 8a. 已定 call（builder check 产出，随文档一并批准）

1. **选中态走 C# Redux 推送**（`ReduxUISystem.GetStateByUI(typeof(WebUI_Codex))`），
   JS 纯渲染；废弃 Vue proxy/DOM 对齐两级方案。理由：仓库已有访问先例、消除最大风险点、
   不碰 Vue 内部的目标不变。
2. **覆盖率门槛语义**：「表内可闭环推导的条目 100% 推导」是硬门槛；表内真无来源的条目
   （目前仅 #11003 一个）降级显示官方提示，数量 ≤2 且发版说明中列出——降级不是人工兜底
   （不编造数据），超 2 个即回到设计阶段。
3. **`General.Enabled=false` 不当场清除已渲染面板**：遵循 CERail 既定策略
   （不主动清、界面重开由原生重建），验收口径为「关闭后下次打开 Codex 无残留」。

## 9. 风险

| 风险 | 缓解 |
|---|---|
| 游戏更新改 Codex.html DOM/样式类名 | 探针降级：选择器失配=不渲染，日志一条；modcenter 标注适配版本 |
| 个别条目表内真无来源（如 #11003） | 降级显示官方分类提示，样式区分；≤2 个不阻塞（§8a-2） |
| 推送乱序/WebView 重建/iframe 重开 | CERail seq/回执 + 探活重注 + scanFrames 遍历 |
| 游戏更新新增配置字段/表 | 运行时推导天然跟随；条目集合判定若变（IsInCodex 逻辑改）覆盖率脚本重跑即知 |

## 10. 验证

- 离线：`tools/coverage.py` 覆盖率门槛按 §8a-2（表内可闭环 100%，真无来源 ≤2）。
- 实机：GM 全解锁前后各截图对照；七分类各抽未解锁条目核对方案正确性（对照存档/wiki）。
- 通道：WebView 重建 / Codex 面板重开（新 iframe）后方案仍能渲染（探活重注生效）。
- 开关：`General.Enabled=false` 后下次打开 Codex 与官方完全一致（§8a-3 口径）。

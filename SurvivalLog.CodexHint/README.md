# SurvivalLog.CodexHint — 图鉴解锁方案

生存图鉴（Codex）里点开未解锁条目，右侧详情面板直接显示该条目的具体解锁方案：
吃哪个、烹饪哪道菜（食材与等级）、种什么、用哪个陷阱捕、商店第几页有售、
完成哪个事件获得——数据启动时从游戏内存配置表运行时推导，游戏更新配置自动跟随。

- 只读不改：不解锁条目、不写存档、不修改游戏文件。
- 七分类全覆盖（食物/菜肴/植物/猎物/制造/家具/装修），离线覆盖率测试为发版门槛。
- 覆盖率与机制实证见 `docs/superpowers/specs/2026-11-19-codex-hint-design.md`。
- `General.Enabled=false`：停止注入与推送；已渲染面板随图鉴重开清除。
- 零 Harmony：`ReduxUISystem.GetWebUILayer()` 直取 WebView（CabinetEverywhere 同款通道）。

## 验证门槛

`tools/coverage.py`（离线反查覆盖率）与 `SurvivalLog.CodexHint.Tests`
（条目锚点 191/525/38/23/164/111/24、零降级）双门槛；游戏更新后重跑即知是否需要适配。

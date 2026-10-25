> 本轮整合上下文（2026-10-25）：本文原有叙述与验收数字保留为各节标明日期的历史快照。游戏主线已在候选提交 `96371eb36a787585cf5e70141c03555419821058` 保存，其父链包含九个 TowerRun/Portal/UI 提交；默认新冒险 Profile 为 `MainV1`，十二种保留原生怪物，Boss 仍待内容。旧 `ecology-v1`、UI v6 未提交、旧工作树/HEAD 等现在式叙述均不代表本轮状态。新整合 Build/Player 与人工验收只认本轮独立收据；Stage1、B1、Boss 和旧 R4 人工结论不升级。当前整合总账见 `../Integration/MainConsolidation20261025/README.md`。

# RPG_Demo 证据索引与待核实问题

## 1. 快照

- 日期：2026-10-12
- 分支：`feat/tower-run-save-mvp`
- HEAD：`7da9a7dc80733bdb35c6145bc244496589fa00e0`
- Unity：`2022.3.62f3c1`
- 当前分支未配置同名本地远端跟踪引用；本次未执行 `fetch`，所以不对远端最新状态作保证。
- 工作树在生成本文档前已经存在设置 UI 相关未提交变更；本知识库没有修改业务代码、场景、Prefab、测试或 Project Settings。

## 2. 当前工作树边界

生成文档前已存在：

- `Assets/Scenes/SampleScene.unity`
- `Assets/Scripts/UI/TowerRunUiController.cs`
- `Assets/Tests/MapGeneration/PlayMode/TowerRunFlowPlayModeTests.cs`
- 8 个 TinySwords UI Sprite `.meta`
- `Docs/DevelopmentLog.md`
- 未跟踪 `Assets/Scripts/UI/TowerRunUiSkin.cs` 及 `.meta`

因此：

- 当前 HEAD 的正式实现与当前工作树中由设置 UI v4 继续收口得到的未提交 v6 必须分开描述。
- 这些既有变更没有被本知识库恢复、暂存、提交或覆盖。

## 3. 主要源码入口

### 地图与运行时整合

| 主题 | 文件/符号 |
|---|---|
| 生成入口 | `Assets/Scripts/MapGeneration/MapGenerationController.cs`：`GenerateMap()`、`TryGenerateMap()` |
| 编排 | `MapIntegrationOrchestrator.cs`：`Execute()` |
| 逻辑生成 | `RandomMapGenerator.cs`：`Generate()` |
| 运行时上下文 | `MapRuntimeFoundation.cs`：`MapRuntimeContext` |
| 生命周期 | `MapRuntimeFoundation.cs`：`MapLifecyclePhase`；`MapGenerationLifecycle.cs`：转移规则 |
| 逻辑地图 | `MapData.cs` |
| Tilemap 投影 | `MapTilemapRenderer.cs` |
| 放置计划 | `MapPlacementPlanning.cs` |
| 运行时注册表 | `MapObjectRegistry.cs` |
| 权威选择 | `MapRuntimeBootstrap.cs` |
| 小地图/迷雾 | `MapMinimapController.cs` |
| 语义放置 | `Merchant/Monster/Animal/Plant/DestructiblePlacementService.cs` |

### 玩家、战斗与 AI

| 主题 | 文件/符号 |
|---|---|
| 输入/移动/攻击 | `Assets/Scripts/Player/PlayerAction.cs` |
| 玩家运行时属性 | `PlayerStatsRuntime.cs` |
| 玩家受伤 | `Player/PlayerDamageController.cs` |
| Monster 受击红闪 / 其他来源击退与控制锁 | `Player/PlayerHurtController.cs` |
| 世界伤害对象 | `Damageable/BaseDamageable.cs` |
| 怪物状态机 | `Damageable/Monster/MonsterAIController.cs` |
| 怪物感知 | `MonsterPerceptionController.cs` |
| 怪物攻击 | `MonsterAttackController.cs` |
| 对象池 | `ObjectPoolManager.cs` |

### 背包、商店、会话

| 主题 | 文件/符号 |
|---|---|
| 奖励定义 | `ScriptableObject/RewardSO.cs` |
| 地面奖励 | `RewardPickup.cs` |
| 背包 | `PlayerInventory.cs` |
| 包裹 UI | `UI/PackageGridUI.cs`、`PackageSlotUI.cs`、`PackageHudButtonUI.cs` |
| 商店交易 | `Shop/ShopTradeController.cs` |
| 商店 UI | `UI/Shop/ShopPanelController.cs`、`ShopGridUI.cs` |
| 会话 | `GameSession.cs` |
| 进程内镜像 | `GameSaveService.cs` |

### TowerRun 与磁盘持久化

| 主题 | 文件/符号 |
|---|---|
| 流程状态机 | `TowerRunCoordinator.cs` |
| 楼层目标/出口 | `TowerFloorRuntimeController.cs` |
| 状态捕获/应用 | `TowerRunRuntimeStateBridge.cs` |
| 恢复 Finalizer | `TowerRunRestoreFinalizer.cs` |
| 三槽存档 | `TowerRun/TowerRunSlotStore.cs` |
| Gate | `Assets/Scripts/TowerRun/TowerRunGate.cs`、`Assets/Scripts/TowerRunGateHub.cs` |
| UI | `UI/TowerRunUiController.cs` |

## 4. 关键提交索引

### 基础玩法

- `1e8f2ff5582d`：背包。
- `f46062ac4f97`：HUD/包裹。
- `e516c4a6c174`：商店和当时的保存。
- `2b0e95c8856e`：成长/体力。
- `0edd67fc`：伤害和怪物 FSM。

### 随机地图

- `433da5e`：生成器、数据、渲染和测试起点。
- `60714fc`：数据驱动小地图。
- `9893e7d`：探索/迷雾。
- `b7b9357`：分层拓扑。
- `428262e`：通道净空。
- `cfaceb7`：独立分支阶段快照。

### 整合

- `562603f`：`RandomMapRuntime` 与主场景。
- `5df3b40`～`407bdeb`：运行时、生命周期、玩家出生、相机。
- `2887d0f`～`3f167d7`：语义对象放置和生命周期。
- `392b973`：Authority/兼容退役。
- `007a58b`：PR #1。

### 奖励与 Generation

- `b8e27c9`：奖励映射与 Generation 生命周期。
- `e7aff9f`：`BaseDamageable` 选择唯一活动 RandomGenerated authority，并验证失败再生成时奖励仍绑定当前 Generation；Map Orchestrator 的 Pending/Active 失败保留证据应查 `5df3b40`、`2bfd4e1`、`392b973`。
- `d9f60d6`：PR #2。

### TowerRun

- `c13625d`：出口净空/门控。
- `2d3a1fe`：黑洞。
- `5e86436`：检查点/三槽。
- `ece3074`：Run 生命周期。
- `146ab53`：恢复测试。
- `c4cb850`：运行时修复/跨进程。
- `7da9a7d`：迷雾时序修复。

完整提交说明见 `_research/Git与历史资料证据清单.md`。

## 5. 历史资料

- `Docs/DevelopmentLog.md`：开发日志。当前含未提交设置 UI 内容，引用时标注工作树状态。
- `Docs/MapGeneration/README.md`
- `Docs/MapGeneration/架构说明.md`
- `Docs/MapGeneration/随机地图系统使用说明.md`
- `Docs/MapGeneration/开发状态与后续计划.md`
- `Docs/GitPracticeNotes.md`
- `Docs/GitWorkflowPractice.md`
- Obsidian：`Unity/RPG_Demo 开发路线.md`
- Obsidian 历史总结：MapGeneration、HUD/Package、商店、Player、Monster 分支 summary。

### 历史资料冲突规则

`Docs/MapGeneration/开发状态与后续计划.md` 曾说明随机地图尚未整合主场景。这在 `cfaceb7` 时是事实，但被后续 MapMerger 提交取代。当前源码和场景优先。

## 6. 测试结果来源

以下位于 `.codex/TestResults/`，是本地任务证据，不应提交到项目 PR：

- `tower-run-save-mvp-runtime-validation-2026-10-12.md`
- `tower-run-save-mvp-baseline-summary.md`
- `tower-fog-sentinel-investigation-2026-10-12.md`
- `settings-ui-visual-v4-20261012.md`（历史 v4 阶段）
- `settings-ui-v6-visual-closure-20261012.md`（当前工作树视觉证据）
- `settings-ui-v6-closure-final.xml/.log`

关键结论：

- MapGeneration EditMode：216/216。
- MapGeneration PlayMode：16/16。
- TowerRun EditMode：21/21 断言，但 Runner 挂起、退出码 -1。
- BlackHole GPU：1/1。
- 跨进程 writer/reader：各 1/1。
- 迷雾修复：定向 1/1，TowerRun flow 4/4。
- 当前未提交设置 UI v6：定向 6/6，Unity exit 0；三分辨率截图已留证，但尚未获得用户最终视觉确认。

所有数字都必须连同提交/工作树和限制一起引用。

## 7. 待核实问题

### P0：面试前必须核实

1. 当前工作树设置 UI 是否获得用户视觉验收并提交。
2. 当前最终 HEAD 上能否完整重跑 EditMode/PlayMode，而不是只引用 `c4cb850` 和定向结果。
3. TowerRun EditMode Runner 挂起原因；“断言通过”与“进程成功退出”要分开。
4. 在真实游玩中，新冒险、恢复、死亡、切层、覆盖槽位的全流程是否均符合预期。

### P1：技术深挖前应核实

1. 记录地图生成耗时、P50/P95、GC Alloc 和最坏 Seed。
2. 验证极端分辨率、不同 DPI 和窗口变化下的 UI。
3. 统计 `PlayerAction` 搜索 `BaseDamageable` 的数量阈值与耗时。
4. 验证包裹全量重建在大量物品时的分配和帧抖动。
5. 明确探索迷雾是否应进入检查点，以及当前恢复语义。
6. 对商店所有边界执行人工与自动化组合验证。

### P2：作者归属与掌握度

1. 无法从 Git 精确证明每段代码是用户、Codex 或其他来源手写。
2. 不使用代码行数推导个人贡献。
3. 通过闭卷讲解、定位 Bug、实现小改动和迁移设计建立个人掌握证据。

## 8. 禁止性结论

在新证据出现前，不得写：

- “所有测试均通过。”
- “完整支持任意时刻存档。”
- “完整实现职业系统/任务系统/装备系统/出售系统。”
- “随机地图已达到商业游戏性能。”
- “所有代码均由我独立编写。”
- “设置界面已经完成并合并。”
- “H0033 Sentinel 弹窗由 Unity 导致。”该弹窗来源没有被可靠归因。

## 9. 维护检查表

- [ ] 更新分支、HEAD 和工作树摘要。
- [ ] 新结论是否有源码、测试、提交或人工验收证据。
- [ ] 旧文档是否被新实现取代。
- [ ] 是否区分自动化与人工验收。
- [ ] 是否区分运行时内存状态与磁盘持久化。
- [ ] 是否把建议误写成实现。
- [ ] 是否诚实说明 AI 参与和个人掌握边界。

## 10. 系统级证据矩阵

| 系统 | 关键源码 | Scene/Prefab/配置 | Git/文档 | 运行证据 | 已确认事实 | 证据缺口 |
|---|---|---|---|---|---|---|
| 随机地图 | `MapGenerationController.cs`、`RandomMapGenerator.cs`、`MapIntegrationOrchestrator.cs` | `RandomMapRuntime.prefab`、`SampleScene.unity` | `433da5e`～`007a58b`、`Docs/MapGeneration/` | 216/216 EditMode、16/16 PlayMode（特定提交） | 已整合主场景并有显式生命周期 | 当前工作树全量重跑、性能指标 |
| 玩家战斗 | `PlayerAction.cs`、`PlayerStatsRuntime.cs` | `Warrior_Blue.prefab`、`GameInput` | `2b0e95c`、开发日志 | PlayMode 覆盖部分主链 | 四方向攻击、体力、延迟命中 | 完整手感/动画人工验收 |
| Monster/Damageable | `MonsterAIController.cs`、`BaseDamageable.cs` | `Torch_Blue.prefab`、`Monsters` | `0edd67f`、Monster summary | 整合 PlayMode 与楼层流 | 五状态 FSM、对象池/奖励 | 大规模 AI 性能、全部动画观感 |
| 背包/奖励 | `RewardPickup.cs`、`PlayerInventory.cs` | `Gold.asset`、`PackageSlot.prefab`、Canvas | `1e8f2ff`、`f46062a` | 历史人工拾取/HUD | 接受成功后 Pickup 才回池 | 大背包性能、全量边界 |
| 商店 | `ShopTradeController.cs`、Shop UI | Merchant prefab、Shop Panel | `e516c4a`、商店 summary | 历史正常流人工通过 | 购买跨背包/库存回滚 | 当前版本所有失败边界人工验收 |
| TowerRun | `TowerRunCoordinator.cs`、`TowerFloorRuntimeController.cs` | `RandomMapRuntime.prefab`、TowerRun UI | `c13625d`～`7da9a7d` | flow、迷雾回归 | 新开/恢复/死亡/切层状态机 | 最终工作树完整人工流程 |
| 三槽存档 | `TowerRunSlotStore.cs`、`TowerRunRuntimeStateBridge.cs` | persistentDataPath/tower-run-slots | `5e86436`、`c4cb850` | 跨进程 writer/reader 各 1/1 | 入口检查点、temp/backup/hash | 多平台、多写者、断电故障 |
| 黑洞出口 | `BlackHolePortal` 相关脚本、Floor Controller | BlackHole/portal 表现对象 | `2d3a1fe` | GPU 1/1 | 真实 GPU 路径已验证 | 更多 GPU/平台与人工视觉 |
| 设置 UI v4 → v6 | 当前 `TowerRunUiController.cs`、`TowerRunUiSkin.cs` | 当前未提交 `SampleScene`/Sprite meta | 当前工作树、`DevelopmentLog.md` | v6 定向 6/6、三分辨率三类画面 | 正在开发且结构/已检查尺寸回归通过 | 用户最终视觉批准、低于 1024×576/极端尺寸、正式提交 |



> 本轮整合上下文（2026-10-25）：本文原有叙述与验收数字保留为各节标明日期的历史快照。游戏主线已在候选提交 `96371eb36a787585cf5e70141c03555419821058` 保存，其父链包含九个 TowerRun/Portal/UI 提交；默认新冒险 Profile 为 `MainV1`，十二种保留原生怪物，Boss 仍待内容。旧 `ecology-v1`、UI v6 未提交、旧工作树/HEAD 等现在式叙述均不代表本轮状态。新整合 Build/Player 与人工验收只认本轮独立收据；Stage1、B1、Boss 和旧 R4 人工结论不升级。当前整合总账见 `../Integration/MainConsolidation20261025/README.md`。

# As-Is 系统总览

范围：Unity 2022.3.62f3c1 的 `Assets/` 与 `ProjectSettings/EditorBuildSettings.asset`。当前构建入口中启用 `Assets/Scenes/SampleScene.unity`。此页的“接入”表示源码/序列化配置存在连接，**不等于本轮 Play Mode 已通过**。场景和 Monster 资产在审计期间有外部未提交改动；最终资产路径以重扫后的 `graph.json` 为准。

## 运行入口与所有权

【已验证事实】`GameSession` 使用 `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)` 建立跨场景会话，并在场景加载后绑定玩家属性/背包等对象（`Assets/Scripts/GameSession.cs:33-55,143-179`）。`GameInput` 创建 `GameControls`，把 Battle/Interact 输入转成 C# 事件（`Assets/Scripts/GameInput.cs:66-70,151-171,204-210`）。

【已验证事实】随机地图入口由 `MapRuntimeBootstrap` 的显式 `RandomGenerated`/`LegacyStatic` 模式及两权威根对象校验，不应根据对象是否 Active 反推模式（`Assets/Scripts/MapGeneration/MapRuntimeBootstrap.cs:13-23,53-61,125-159`）。`TowerRunCoordinator` 在新游戏/恢复/换层时调用 `MapGenerationController.TryGenerateMap`（`Assets/Scripts/TowerRunCoordinator.cs:303-348`）；地图控制器通过 `MapIntegrationOrchestrator` 生成、规划、提交、投影，再物化对象和绑定相机，最终发布 GameplayReady（`Assets/Scripts/MapGeneration/MapGenerationController.cs:122-134,142-345`）。

【已验证事实】怪物主目标的 `MonsterPlacementService` 当前使用 `SceneBound` reservation，物化时移动既有 `monsterTarget` 并绑定 Registry，**不是每层从 Monster Prefab Instantiate 一个新实例**（`Assets/Scripts/MapGeneration/MonsterPlacementService.cs:18-27,71-76,150-185`）。可摧毁物则用 `GenerationScoped` 预留，从 `destructiblePrefab` 的 `IDestructiblePlacementTarget.Spawn` 取得实例并在新一代回收（`Assets/Scripts/MapGeneration/DestructiblePlacementService.cs:55-56,127,233-315`）。这两个生命周期必须分别看。

## 模块状态

| 系统 | 当前判定 | 主要对象/证据与边界 |
|---|---|---|
| 玩家移动、攻击、属性、生命 | 已实现且接入 | `Warrior_Blue`、`GameInput`、`PlayerAction`、`PlayerStatsRuntime`、`PlayerDamageController`；攻击结算只选 `BaseDamageable`（`PlayerAction.cs:214-242,294-334`） |
| 怪物 AI、受击、动画 | 已实现且接入 | `BaseMonster : BaseDamageable`、`MonsterAIController` 实际构造 Patrol/Search/Chase/Attack/Return 五状态（`MonsterAIController.cs:67-71,124-139`）；Visual 子对象上的 Animator 以最新 Prefab 复核 |
| Damageable / 动物 / 植物 / 可摧毁物 | 已实现且接入，生成方式不同 | `BaseDamageable` 统一受击、奖励、回收（`BaseDamageable.cs:78-104,121-158`）；动物/植物是场景绑定，新增可摧毁物是代际对象（见上述服务） |
| 随机地图、地形、生物群系/群落放置 | 已实现且接入 | `RandomMapGenerator`、`MapData`、`MapPlacementPlan`、Merchant/Monster/Destructible/Animal/Plant 五种 Placement Service（`MapGenerationController.cs:436-472`） |
| 地图对象注册与清理 | 已实现且接入 | `MapRuntimeContext` / `MapObjectRegistry` 以 GenerationId 和 logicalObjectId 约束注册、退休（`MapIntegrationOrchestrator.cs:74-93`、`MapObjectRegistry.cs:36-74`） |
| 奖励、背包、商店 | 已实现且接入 | 掉落 `RewardPickup` 请求 `PlayerInventory` 接收；商店 `ShopTradeController` 操作库存与背包（`RewardPickup.cs:49-67,84-104`、`ShopTradeController.cs:320-353`） |
| 装备/职业选择 | 未发现可展示的独立运行链路 | 扫描 `Assets/Scripts` 未见装备穿戴或职业选择控制器；不把 `RewardSO`/商店分类自动等同于装备系统。若需求在其他资产中，请另核对 |
| HUD、背包、商店、设置 UI | 已实现且接入 | `PackageGridUI` 监听背包事件；`TowerRunUiController` 动态构造设置/存档界面，场景另有持久化 Shop Toggle UnityEvent（`PackageGridUI.cs:53-104`、`TowerRunUiController.cs:556-619`、`SampleScene.unity`） |
| 音频 | 部分实现 | 设置页用 `AudioListener.volume` 与偏好存储控制总音量/静音，并有预览 `AudioSource`（`TowerRunUiController.cs:450-481,661`）；未见独立游戏音频 Manager 的源码证据 |
| 存档/恢复 | 已实现且接入，双层数据 | `GameSaveService.TrySave` 只替换内存缓存（`GameSaveService.cs:193-221`）；正式三槽磁盘提交在 `TowerRunSlotStore.TryCommit`（`TowerRunCoordinator.cs:184-204`、`TowerRunSlotStore.cs:696-739`）。勿把一次背包变更当作三槽落盘 |
| Camera / Minimap / Fog | 已实现且接入 | `CameraBindingService` 在玩家定位后绑定；`MapMinimapController` 订阅 `MapGenerated`，并持有运行时图像/雾纹理（`MapGenerationController.cs:258-284`、`MapMinimapController.cs:307-359`） |
| 黑洞出口、换层 | 已实现且接入 | `TowerFloorRuntimeController` 监听目标 `OnDefeated`，开放出口并在 F 交互时发布 `ExitUseRequested`；`TowerRunCoordinator` 承接换层（`TowerFloorRuntimeController.cs:116,163-197`、`TowerRunCoordinator.cs:380-423`） |
| Animator / Input / 红点 | 已实现，红点语义有限 | 动画由 Player/Monster/Reward Controller 驱动；输入由 `GameControls`/`GameInput` 转发；`PackageHudButtonUI` 依 Gold/面板状态刷新红点，未发现逐物品已读状态（`PackageHudButtonUI.cs:101-189`） |
| 兼容旧地图路径 | 存在，正式运行入口需由 Mode 判定 | `MapGeneratedObjectPlacementAdapter` 保留组件和兼容代码，源码明确 RandomGenerated 不执行 authored-offset 路径（`MapGeneratedObjectPlacementAdapter.cs:7-65`）；不能只凭场景中有组件认定旧逻辑仍在运行 |

## 关键数据所有权

- `MapGenerationSettings` 是配置资产；`GeneratePending` 克隆设置并注入 attempt seed，不直接改资产（`MapGenerationController.cs:436-450`）。`MapData` 是本代逻辑地图，`MapTilemapRenderer` 是可见投影（`MapGenerationController.cs:496-504`）。
- `PlayerInventory.ItemStacks` 和 `PlayerStatsRuntime` 是当前玩家运行状态；`GameSaveService.cachedData` 是兼容层内存快照，`TowerRunCheckpointRecord` 是三槽可持久化的楼层入口记录（`PlayerInventory.cs:202-217`、`TowerRunCoordinator.cs:167-204,233-299`）。
- UI 通过事件或重开时主动 `Refresh()` 读取真实状态。`Refresh()` 是状态查询，不能解释为旧事件重放（`PackageGridUI.cs:53-64,82-104`）。

## 如何判断证据

当前图谱的 `static` 表示读到了 GUID、组件/层级或持久化绑定；`source confirmed; runtime unverified` 表示人工对照了代码行并校验了关系；`textual only` 是有限正则命中的类型词，可能不代表运行调用。双向“引用”并不自动是业务循环；问题页只列有实际机制或验证缺口的风险。

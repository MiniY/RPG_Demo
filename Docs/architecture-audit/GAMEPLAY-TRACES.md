> 本轮整合上下文（2026-10-25）：本文原有叙述与验收数字保留为各节标明日期的历史快照。游戏主线已在候选提交 `96371eb36a787585cf5e70141c03555419821058` 保存，其父链包含九个 TowerRun/Portal/UI 提交；默认新冒险 Profile 为 `MainV1`，十二种保留原生怪物，Boss 仍待内容。旧 `ecology-v1`、UI v6 未提交、旧工作树/HEAD 等现在式叙述均不代表本轮状态。新整合 Build/Player 与人工验收只认本轮独立收据；Stage1、B1、Boss 和旧 R4 人工结论不升级。当前整合总账见 `../Integration/MainConsolidation20261025/README.md`。

# 核心玩法链路与数据变化

Phase 2 的 A 地图、B 战斗、C 背包链路已补充到 [逐方法学习稿](PHASE2-CHAIN-STUDY.md)，并在 [查看器](viewer.html) 按业务链筛选。下文保留第一版的跨系统速览；验证状态以 [VALIDATION.md](VALIDATION.md) 的最新 Phase 2 节为准。

## 1. 玩家攻击 Monster → 奖励 → 背包

1. 玩家按 Battle：`GameInput.Battle_performed` 检查玩法输入 Gate 后发布 `OnBattlePressed`（`Assets/Scripts/GameInput.cs:204-210`）。`PlayerAction` 的订阅在 `TryBindInput`，解除在 `UnbindInput`（`Assets/Scripts/Player/PlayerAction.cs:165-198`）。
2. `HandleBattlePressed` 先处理体力：`TryConsumeStamina(attackStaminaCost)` 失败则不播放动画、不伤害；成功时 `GameSession.SaveRuntimeState` 更新**内存**快照，`PrepareAttackDirection` 选范围内最近 `BaseDamageable`，发布 `OnAttackPerformed` 给动画，再启动延迟协程（`PlayerAction.cs:214-243`）。
3. 延迟 `attackHitDelay` 后，再检查目标非空、Active、未 defeated，调用 `BaseDamageable.TakeDamage(attackDamage, transform)`。查找使用 `FindObjectsOfType<BaseDamageable>`，所以 Monster、Animal、Plant 等都可能成为目标；不是针对指定怪物的命中盒检测（`PlayerAction.cs:284-334`）。
4. `BaseDamageable.ApplyDamage` 扣 `currentHealth`；未死亡发 `OnDamaged`，死亡时禁用碰撞体、从 `DamageableDieRewardListSO` 查表、逐规则随机生成奖励，然后发 `OnDefeated`，延时回池（`BaseDamageable.cs:78-104,121-158,225-238`）。Monster AI/Hurt 订阅受击/死亡，Tower Floor 订阅目标死亡（`MonsterAIController.cs:183-196`、`TowerFloorRuntimeController.cs:116`）。
5. 奖励在地面独立存在。玩家进入 `RewardPickup.OnTriggerEnter2D` 后，请求 `PlayerInventory` 的 `OnRewardPickupRequested` 接收；背包先修改 `ItemStacks` 并提交内存快照，成功才发 `OnInventoryChanged`，拾取物才标记动画/回池（`RewardPickup.cs:49-67,84-104`、`PlayerInventory.cs:144-154,181-217`）。
6. `PackageGridUI` 订阅背包变化，`Refresh` 查询当前 `ItemStacks` 并重建格子；界面重新启用时也主动刷新，故关闭期间没有错过数据（`PackageGridUI.cs:53-104`）。`CurrencyHudUI` 同理刷新金币文本（`CurrencyHudUI.cs:36-52`）。

**边界**：一次攻击不必然命中；一次死亡不必然掉落；掉落不自动进入背包；内存 `TrySave` 不等于正式三槽写盘。此链路的源码连接已核，完整当前场景体验仍需 Play Mode。

## 2. 新游戏 / 换层 → 地图可玩

1. `TowerRunCoordinator.BeginNewRun` 或换层的 `HandleExitUseRequested` 建立 staged `TowerRunCheckpointRecord`，`BeginOperation` 构造 `MapGenerationRequest` 并调用 `MapGenerationController.TryGenerateMap`（`TowerRunCoordinator.cs:125-141,303-348,380-423`）。
2. `MapRuntimeBootstrap` 先确认显式模式和唯一 authority。`MapIntegrationOrchestrator.Execute` 逐 attempt 生成 `MapData`、建立 `MapPlacementPlan` 并验证，Commit 后退休旧 Registry、投影 tilemap（`MapRuntimeBootstrap.cs:125-159`、`MapIntegrationOrchestrator.cs:55-93`）。
3. `MapGenerationController` 依次物化 Merchant、Monster、Destructible、Animal、Plant。Monster 是场景既有实例重定位与 `TryBindInstance`；Destructible 通过 prefab 的 `IDestructiblePlacementTarget.Spawn` 生成并绑定代际清理。不能把二者画成同一种 Instantiate（`MapGenerationController.cs:159-209`、`MonsterPlacementService.cs:150-185`、`DestructiblePlacementService.cs:233-315`）。
4. 玩家定位后，地图依赖对象重初始化、相机绑定、Authority 再校验，Floor 和 Restore finalizer 最后执行；`MapLifecyclePhase.Ready` 后发布 `GameplayReady`，`TowerRunCoordinator` 才从 staged 状态切到 Playing、通知属性/背包/商店 UI（`MapGenerationController.cs:211-346`、`TowerRunCoordinator.cs:351-377`）。
5. Floor 控制器追踪当前代 Required 目标死亡，开放黑洞和小地图标记。玩家按 F 时检查状态、Gameplay Gate、玩家生命和距离，再发布 `ExitUseRequested`；Coordinator 抓取下一层携带状态并发起新一轮生成（`TowerFloorRuntimeController.cs:116,163-213`、`TowerRunCoordinator.cs:380-423`）。

**边界**：`MapData` 成功生成、已 Commit、GameplayReady 是三个不同关卡。物化后失败的处理需看 `MapRuntimeContext.Failure`，不能用地形可见来判定整体可玩。当前视觉排序/边缘追击的手动验收单独列在 `VALIDATION.md`。

## 3. 背包/商店、内存状态与正式槽位

`ShopGridUI` 将商品选择交给 `ShopTradeController.TryPurchase`，后者在单次交易内扣 Gold 并加商品，随后协调库存和状态通知（`Assets/Scripts/UI/Shop/ShopGridUI.cs:352-365`、`Assets/Scripts/Shop/ShopTradeController.cs:320-398`）。`GameSaveService.TrySave` 仅规范化并替换 `cachedData`，没有文件 I/O（`GameSaveService.cs:198-221`）。正式保存从设置 UI 调 `TowerRunCoordinator.TryCreateSaveRequest` 获取稳定楼层入口候选，再调用 `TrySaveEntranceCandidate` → `TowerRunSlotStore.TryCommit`；恢复时读取槽位，生成匹配的地图后 `TowerRunRuntimeStateBridge.TryApply`，最后才通知 UI（`TowerRunCoordinator.cs:167-204,233-299,351-377`）。

**排错切入**：若 UI 不刷新，先查 `ItemStacks` 是否真实改变，再查 `OnInventoryChanged` 是否触发与 UI 是否在 `OnEnable` 订阅；若恢复后数值错，依次查槽位记录、staged 记录、地图基线校验、`TryApply`、最后通知顺序。不要先猜“画面缓存”。

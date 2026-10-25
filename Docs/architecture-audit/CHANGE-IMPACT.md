> 本轮整合上下文（2026-10-25）：本文原有叙述与验收数字保留为各节标明日期的历史快照。游戏主线已在候选提交 `96371eb36a787585cf5e70141c03555419821058` 保存，其父链包含九个 TowerRun/Portal/UI 提交；默认新冒险 Profile 为 `MainV1`，十二种保留原生怪物，Boss 仍待内容。旧 `ecology-v1`、UI v6 未提交、旧工作树/HEAD 等现在式叙述均不代表本轮状态。新整合 Build/Player 与人工验收只认本轮独立收据；Stage1、B1、Boss 和旧 R4 人工结论不升级。当前整合总账见 `../Integration/MainConsolidation20261025/README.md`。

# 变更影响与安全修改入口

下面是源码/资产已核的直接关系；更广的候选影响可在查看器点击节点看上游两跳。反向遍历表示“谁引用它”，并非一定需要修改每个上游。做修改前仍须读调用条件、Inspector 引用与测试。

| 计划修改 | 直接依赖与可能观察到的变化 | 最小验证入口 |
|---|---|---|
| `BaseMonster`/`BaseDamageable` 受击或死亡顺序 | `PlayerAction.TakeDamage` 进入基础受击；`MonsterAIController`/`MonsterHurtController` 订阅受击/死亡；`TowerFloorRuntimeController` 按 `OnDefeated` 计数；`SpawnRewards` 生成地面奖励并绑定地图生命周期（`PlayerAction.cs:307`、`BaseDamageable.cs:78-104,121-177`、`MonsterAIController.cs:183-196`、`TowerFloorRuntimeController.cs:116`） | `DamageableRewardLoopPlayModeTests`，`TowerRunFlowPlayModeTests`，手动击杀与出口解锁 |
| `MonsterAIController`/移动/Visual Animator | `MonsterPlacementService` 把当前场景 `monsterTarget` 搬移/重初始化；FSM 五状态受感知、移动和攻击控制；Visual 的 Animator/Controller 与父对象行为脚本分离（`MonsterPlacementService.cs:150-185`、`MonsterAIController.cs:67-71,124-139`） | 地图运行时目标定位、追击/攻击、Prefab Visual 参数、草地/沙地/树冠排序 |
| `PlayerInventory` / `RewardSO` | `RewardPickup` 接收、`ShopTradeController` 交易、`PackageGridUI`/`CurrencyHudUI`/红点刷新、`GameSaveService` 缓存、Tower checkpoint 捕获/恢复（`PlayerInventory.cs:144-154,202-253`、`ShopTradeController.cs:338-388`） | 拾取、购买失败回滚、背包计数、UI 重开、正式槽位恢复 |
| `GameSaveService` / `TowerRunSlotStore` | 前者服务当前内存快照；后者管理正式三槽。`TowerRunCoordinator` 的 staged record 与 `TowerRunRuntimeStateBridge` 连接二者（`GameSaveService.cs:198-221`、`TowerRunCoordinator.cs:184-204,233-299`） | `TowerRunSlotStoreTests`、独立进程恢复 PlayMode、已有三槽哈希不变性 |
| `MapData` / `MapGenerationSettings` / Placement Policy | 地形投影、装饰、五类对象规划、玩家出生、相机、Minimap、楼层出口与恢复基线全部依赖相同代际地图（`MapGenerationController.cs:436-504`） | 生成/重试、语义放置、RuntimeReady、换层、恢复基线与 Minimap 测试 |
| `TowerFloorRuntimeController` / Portal | `BaseDamageable.OnDefeated` 改变出口状态，`GameInput.OnInteractPressed` 触发距离/生命校验，`TowerRunCoordinator` 捕获下一层状态（`TowerFloorRuntimeController.cs:116,163-213,253`、`TowerRunCoordinator.cs:380-423`） | Required 全灭、非目标不阻塞、连续 F、出口周边安全区 |

## 修改顺序

1. 在查看器搜源节点，读“上游 / 引用者”，沿证据行号确认真正的调用条件；再看“下游 / 依赖”。
2. 记录修改前的一个可观察现象与一个最小回归测试；若现有测试没有覆盖正确运行链路，先补定向验证，避免只测方法内部。
3. 如改 Scene/Prefab，比较 GUID/fileID 与 Inspector，再跑扫描器。不要因为某对象静态入边为 0 就直接删除。
4. 最后在最新版 Unity Player/Play Mode 验收用户可见结果，并单独标注自动测试与人眼观察结论。学习掌握度继续以你自己的解释或练习为证据。

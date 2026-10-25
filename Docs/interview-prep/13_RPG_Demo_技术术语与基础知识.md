> 本轮整合上下文（2026-10-25）：本文原有叙述与验收数字保留为各节标明日期的历史快照。游戏主线已在候选提交 `96371eb36a787585cf5e70141c03555419821058` 保存，其父链包含九个 TowerRun/Portal/UI 提交；默认新冒险 Profile 为 `MainV1`，十二种保留原生怪物，Boss 仍待内容。旧 `ecology-v1`、UI v6 未提交、旧工作树/HEAD 等现在式叙述均不代表本轮状态。新整合 Build/Player 与人工验收只认本轮独立收据；Stage1、B1、Boss 和旧 R4 人工结论不升级。当前整合总账见 `../Integration/MainConsolidation20261025/README.md`。

# RPG_Demo 技术术语与基础知识

## 0. 高频概念学习卡

以下四个概念按“通俗定义→技术定义→存在原因→项目案例→替代/区别→误区→面试题”展开，先掌握它们再查后面的词典。

### 0.1 Authority（权威源）

1. **通俗定义：** 某个问题最终“听谁的”。
2. **技术定义：** 对某类状态拥有最终写入和解释权的组件/数据模型。
3. **为什么存在：** 防止 UI、缓存、文件和运行时对象各自保存一份并互相冲突。
4. **项目使用：** `MapData` 决定地形逻辑，`PlayerInventory` 决定背包数量，`TowerRunCoordinator` 决定 Run 状态。
5. **实际代码：** `MapRuntimeBootstrap` 还要求 RandomGenerated 与 LegacyStatic 互斥。
6. **区别：** Authority 不是“全局单例”；多个领域可以有不同权威源。
7. **常见误区：** 把 UI Text 或旧 Save DTO 当权威。
8. **面试题：** 商店购买跨两个权威源时如何保持一致？

### 0.2 Transaction（事务）

1. **通俗定义：** 一组动作要么全成，要么像没发生。
2. **技术定义：** 把多个状态变化作为一个原子业务单位，维护操作前后不变量。
3. **为什么存在：** 防止扣金币但不给商品、只恢复生命但背包失败。
4. **项目使用：** `ShopTradeController.TryPurchase`、`TowerRunRuntimeStateBridge.TryApply`。
5. **实际机制：** 只读校验、快照、应用候选、提交、失败回滚、成功通知。
6. **区别：** 当前不是数据库完整 ACID，没有复杂并发隔离。
7. **常见误区：** 每改一个字段就 Save；认为回滚一定不会失败。
8. **面试题：** 如果回滚本身失败，系统应如何降级？

### 0.3 GenerationId（生成代标识）

1. **通俗定义：** 给每一张运行时世界一个版本号。
2. **技术定义：** 绑定一批 Map、Registry、对象和回调的唯一 token。
3. **为什么存在：** 协程、事件、对象池结果可能在切层后迟到。
4. **项目使用：** MapRuntimeContext、Registry、奖励生命周期、出口和 GameplayReady。
5. **实际代码：** `MapIntegrationOrchestrator` 检查 pending/active ID；旧代奖励退休。
6. **区别：** Seed 描述如何生成内容，GenerationId 描述当前是哪一次运行实例。
7. **常见误区：** 用 Seed 代替 Generation；认为场景对象销毁就不会有迟到回调。
8. **面试题：** 同一 Seed 连续重生两次，为什么仍要不同 GenerationId？

### 0.4 ScriptableObject（脚本化对象）

1. **通俗定义：** 可作为 Unity 资源保存的共享数据对象。
2. **技术定义：** 继承 UnityEngine.Object、独立于 GameObject 的可序列化资产类型。
3. **为什么存在：** 让 Reward 等静态定义可被多个 Prefab/场景引用。
4. **项目使用：** `RewardSO` 保存 rewardId、图标、描述、倍率和地面 Prefab。
5. **实际代码：** `Assets/Scripts/ScriptableObject/RewardSO.cs`。
6. **区别：** 它适合配置，不默认适合每个 Run 的物品数量。
7. **常见误区：** Play Mode 修改资产后把它当存档；复制资产造成稳定 ID 冲突。
8. **面试题：** 为什么背包数量放 `PlayerInventory` 而不是 RewardSO？

## 1. 架构与数据

### Authority（权威源）

对某项事实拥有最终解释权的组件或数据。

项目例子：

- 地图可走性：`MapData`
- 背包数量：`PlayerInventory`
- TowerRun 流程：`TowerRunCoordinator`
- 磁盘槽位内容：`TowerRunSlotStore`

反例：UI Text 显示 100 金币，不代表真实金币一定是 100。

### Single Source of Truth（单一事实来源）

同一事实不由多个互不协调的数据源分别维护。它不要求所有数据放一个对象，而要求每个事实有明确所有者。

### Projection（投影）

从逻辑状态产生表现。Tilemap、小地图和 UI 都是投影。

### Invariant（不变量）

系统在允许状态中必须始终满足的条件。

例：

- 库存不得为负；
- Active Generation 唯一；
- GameplayReady 时玩家、相机和本代对象均已准备；
- 一次购买要么全成功，要么回滚。

### Deep Module（深模块）

接口简单、内部隐藏大量复杂度的模块。`TowerRunSlotStore.TryCommit` 对调用者提供一次提交操作，内部处理临时文件、备份和验证。

## 2. 状态与流程

### Finite State Machine（有限状态机）

系统处于有限状态集合之一，并通过受控事件转移。Monster 和 TowerRun 都使用该模型。

### Lifecycle（生命周期）

对象/流程从创建、启用、使用、切换到销毁的阶段及规则。Unity 生命周期和地图 Generation 生命周期是不同层次。

### Completion Boundary（完成边界）

何时可以对外宣称操作完成。地图画完不等于 GameplayReady。

### Idempotency（幂等性）

同一操作重复执行不会产生额外副作用。一次死亡不重复掉落，一次出口不重复切层。

### Generation / Epoch（生成代/纪元）

用于标识一批同时有效的世界状态。旧代回调不能写入新代。

## 3. 事务与持久化

### Transaction（事务）

把多个状态变化视为一个业务单位。项目主要实现原子性和一致性，不等同完整数据库 ACID。

### Atomicity（原子性）

全成或全不成。商店购买不能只扣金币。

### Consistency（一致性）

操作前后满足业务规则，例如 ID 唯一、数量合法、状态组合有效。

### Isolation（隔离性）

并发事务互不观察中间状态。当前 Unity 主线程业务没有实现数据库级隔离；多进程写同一槽也不是当前已支持能力。

### Durability（持久性）

已确认提交的数据在故障后仍存在。项目通过文件协议提高持久性，但不声称断电级绝对保证。

### Snapshot（快照）

某一时点的完整状态副本，用于恢复或回滚。

### Checkpoint（检查点）

位于特定一致边界的可恢复记录。TowerRun 保存楼层入口。

### Schema Version（模式版本）

描述数据结构版本，用于判断当前代码能否解释旧记录。

### Migration（迁移）

把旧版本数据转换为新结构。当前以拒绝不兼容为主，完整多版本迁移仍是未来设计。

### Write-after-read Verification（写后复读验证）

写入后重新读取并验证实际内容，而不是只相信 API 返回。

### Atomic Replace（原子替换）

尽量使路径从旧文件一次切换到新文件，避免观察到半写状态。具体保证依赖平台和文件系统。

### Confirmation Token（确认令牌）

绑定调用者已确认的旧内容，避免覆盖时目标已变化。类似 Compare-And-Swap。

### SHA-256

密码学 Hash。在本项目用于内容一致性，不是防作弊签名。

## 4. 随机生成

### Seed（随机种子）

初始化伪随机序列的输入。相同 Seed 只有在算法、配置和调用顺序相同时才保证相同结果。

### Procedural Generation（程序化生成）

通过算法和规则产生内容，而非手工逐格制作。

### Noise（噪声）

具有空间连续性的伪随机函数，常用于高度、湿度和温度分布。它不自动保证玩法可行。

### Connectivity（连通性）

可走区域之间是否存在路径。

### Reachability（可达性）

从特定起点能否到达目标。连通性是整体性质，可达性通常相对某个起点。

### Reservation（占位）

在真实对象移动前，为计划中的位置声明空间和身份，防止冲突。

### Materialization（实体化）

把纯数据计划应用为真实 GameObject/组件状态。

## 5. Unity

### GameObject 与 Component

GameObject 是容器，Component 提供行为/数据。组合多个小组件比单一巨型脚本更利于复用，但需要清晰所有权。

### Prefab（预制体）

可复用的对象模板，实例通过 GUID/FileID 与资源关联。

### `.meta`

Unity 资源的 GUID 和导入设置。必须随资产进入版本控制。

### ScriptableObject（脚本化对象）

独立资源形式的数据容器，适合配置和共享定义，不默认等于运行时存档。

### Tilemap（瓦片地图）

以网格存储/渲染 Tile 的 Unity 2D 组件。项目中它是 MapData 的表现投影。

### Cinemachine

相机编排系统。项目需要在玩家出生和地图边界确定后重新绑定 Follow/Confiner。

### EditMode Test

在编辑器上下文快速验证纯逻辑、资源和部分 Unity API。

### PlayMode Test

进入播放生命周期，适合场景、MonoBehaviour、物理和跨帧行为。

### Domain Reload（域重载）

Unity 重载托管域并重置静态状态的过程。禁用域重载会放大静态事件泄漏风险。

## 6. AI 与测试

### Test Double（测试替身）

用 fake/stub/mock 等替代真实依赖。Orchestrator 委托可注入失败结果。

### Regression Test（回归测试）

针对已发生故障，确保修复后不再复发。迷雾时序测试属于此类。

### Failure Injection（故障注入）

主动让某一步失败，验证回滚/恢复。例如让 temp 校验失败或正式复读 Hash 不一致。

### Deterministic Test（确定性测试）

相同输入得到稳定结果；随机测试应记录 Seed。

### Flaky Test（不稳定测试）

相同代码和环境下非确定性通过/失败。直接 retry 不能代替根因调查。

### Headless（无图形模式）

没有真实图形设备/窗口的执行环境。适合很多逻辑测试，但不能证明 GPU 像素效果。

## 7. Git

### Working Tree（工作树）

磁盘上某个分支/提交的可编辑检出。

### Staging Area（暂存区）

下一次提交的精确内容，不等于所有工作树变更。

### Merge Base（合并基点）

两个分支最近的共同祖先，用于计算各自变化。

### Worktree（附加工作树）

同一仓库在多个目录检出不同分支。适合并行开发和整合。

### Fast-forward Merge（快进合并）

目标分支没有独立提交时，直接移动指针，不产生合并提交。

### Three-way Merge（三方合并）

结合两个分支和共同基点计算合并；冲突时需根据语义解决。

## 8. 易混概念对照

| A | B | 区别 |
|---|---|---|
| Seed | Save | Seed 重建程序化内容；Save 记录业务状态 |
| MapData | Tilemap | 逻辑真相；表现投影 |
| Event | Command | 已发生通知；请求执行并通常需要结果 |
| Pool disable | Destroy | 可复用停用；永久销毁 |
| Assertion pass | Process success | 断言结果；执行器/进程生命周期结果 |
| Hash | Signature | 一致性摘要；带密钥/身份的认证 |
| Snapshot | Checkpoint | 任意状态副本；特定一致边界恢复点 |
| Corrupt | Incompatible | 内容损坏；内容可读但版本不支持 |
| Planned | Implemented | 计划/建议；源码存在且有证据 |
| AI generated | Personally mastered | 产出来源；个人能否解释、修改和迁移 |


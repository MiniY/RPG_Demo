> 本轮整合上下文（2026-10-25）：本文原有叙述与验收数字保留为各节标明日期的历史快照。游戏主线已在候选提交 `96371eb36a787585cf5e70141c03555419821058` 保存，其父链包含九个 TowerRun/Portal/UI 提交；默认新冒险 Profile 为 `MainV1`，十二种保留原生怪物，Boss 仍待内容。旧 `ecology-v1`、UI v6 未提交、旧工作树/HEAD 等现在式叙述均不代表本轮状态。新整合 Build/Player 与人工验收只认本轮独立收据；Stage1、B1、Boss 和旧 R4 人工结论不升级。当前整合总账见 `../Integration/MainConsolidation20261025/README.md`。

# RPG_Demo 技术面试问答大全

> 每题均包含：问题、考查点、参考回答、项目证据、技术原理、深入追问、追问答案、常见错误和掌握要求。回答口径以 `feat/tower-run-save-mvp@7da9a7d` 为基线；当前未提交设置 UI 单独说明。

## 1. 项目概述：请介绍 RPG_Demo

**问题：** 请用 1 分钟介绍项目，并说明最有技术价值的部分。

**考查点：** 能否提炼真实范围；是否只会堆功能名；能否区分产品和工程难点。

**参考回答：**

> RPG_Demo 是 Unity 2022.3 的俯视角 2D 像素 ARPG 原型。玩家在确定性生成的地图中探索、战斗、拾取、交易和成长，并在 TowerRun 中击败本层目标、解锁出口、保存下一层入口检查点。工程上我重点学习了三件事：第一，随机地图以 `MapData` 为逻辑真相，经过 Plan、Commit、Tilemap 投影、对象实体化、玩家出生和相机绑定后才进入 GameplayReady；第二，背包和商店通过快照回滚维护原子性；第三，三槽存档用版本/稳定 ID 校验、临时文件、备份和写后 Hash 验证降低损坏风险。大量代码由 Codex 辅助，我主要负责需求边界、方案判断、验收、故障复现和复盘。

**项目证据：**

- `Assets/Scripts/MapGeneration/MapGenerationController.cs`
- `Assets/Scripts/TowerRunCoordinator.cs`
- `Assets/Scripts/TowerRun/TowerRunSlotStore.cs`
- [[01_RPG_Demo_项目全景与总体介绍]]

**技术原理：** 项目介绍应说明核心循环、状态所有者、最深技术链路和验证边界，而不是列出所有脚本。

**深入追问：** 为什么称为原型，不称为完整游戏？

**追问答案：** 核心循环和多系统整合已经存在，但职业、装备、任务、内容量、多平台发布、量化性能和完整人工验收未完成，也没有商业上线证据。

**常见错误：** 声称“完整商业级 ARPG”；报告没有上下文的 216 个测试；回避 AI 参与。

**需要掌握的程度：** 基础必会。

## 2. 个人贡献：大量代码由 AI 生成，你做了什么？

**问题：** 这个项目里有多少是你自己写的？没有 AI 你能维护吗？

**考查点：** 诚信、责任边界、真实掌握度、AI 工程方法。

**参考回答：**

> 我没有可靠的逐行作者统计，所以不提供虚构百分比。可以确定大量实现由 Codex 辅助。我负责提出和收敛需求、定义不变量与禁止范围、选择产品语义、检查工作树、审查方案、执行或要求测试、真实场景验收和复盘。是否能维护要按系统回答：我正在通过闭卷讲解、实际小改动和排错建立证据，不把“代码在仓库里”当成“已经掌握”。

**项目证据：**

- [[12_RPG_Demo_AI辅助开发实践]]
- `.codex/HANDOFF.md` 中的任务边界与验证记录（本地任务资料，不提交）
- Git 中分阶段提交与测试记录

**技术原理：** 代码产出、工程责任和个人能力是三个不同维度。AI 提高生成速度，也会制造理解债务。

**深入追问：** 如何判断 AI 方案不是过度设计？

**追问答案：** 检查是否保护真实不变量、是否有实际故障支持、是否能用更简单方案解决、复杂度是否可测试。例如 GenerationId 对应真实旧代迟到风险；无数据支持的全量 ECS 迁移则不合理。

**常见错误：** “都是我写的”；“AI 写的但我都懂”；“测试过所以肯定懂”。

**需要掌握的程度：** 基础必会，且必须诚实。

## 3. 整体架构：谁拥有状态？

**问题：** 项目整体如何分层？如何避免多个系统同时修改同一份数据？

**考查点：** Authority、模块边界、数据流和 UI 职责。

**参考回答：**

> 项目不是单一 MVC，而是按领域建立权威源：`MapData` 负责地形逻辑，`MapObjectRegistry` 负责本代语义对象，`PlayerStatsRuntime` 负责玩家属性，`PlayerInventory` 负责背包，`TowerRunCoordinator` 负责 Run 流程，`TowerRunSlotStore` 负责磁盘槽。Tilemap、小地图和 UI 都是投影。跨领域变更通过服务和事务桥完成，避免 UI 或文件直接修改运行时对象。

**项目证据：**

- `MapData.cs`
- `MapObjectRegistry.cs`
- `PlayerStatsRuntime.cs`
- `PlayerInventory.cs`
- `TowerRunRuntimeStateBridge.cs`
- [[03_RPG_Demo_系统架构与核心数据流]]

**技术原理：** `Single Source of Truth（单一事实来源）` 不是把所有状态放一个类，而是让每项事实有一个最终解释者。

**深入追问：** 商店购买同时改背包和库存，不就有两个权威源吗？

**追问答案：** 两个领域各自仍拥有状态；`ShopTradeController` 作为用例协调者捕获双方快照、应用候选并统一提交，失败同时回滚。协调者不永久复制两份状态。

**常见错误：** 把 `GameSaveData` 或 UI Text 说成运行时唯一状态源。

**需要掌握的程度：** 重点深挖。

## 4. Unity 生命周期：池化对象为什么容易出错？

**问题：** `Awake`、`OnEnable`、`Start` 在池化 Damageable 中分别有什么意义？

**考查点：** Unity 生命周期、对象池复用、事件清理。

**参考回答：**

> `Awake` 适合一次性建立自身引用；`OnEnable` 每次从池中重新激活都会执行，应恢复生命、Collider、目标和事件状态；`Start` 通常只在首次启用前调用一次，不能承担每次复用的重置。`OnDisable` 要停止协程、解除订阅或清理临时引用，否则下一次启用可能重复掉落或收到旧回调。

**项目证据：**

- `Assets/Scripts/Damageable/BaseDamageable.cs`
- `Assets/Scripts/ObjectPoolManager.cs`
- Monster/Animal/Plant 的 animation/hurt controller 订阅代码

**技术原理：** 池化把“销毁/创建”改为“停用/重启”，因此对象必须具备可重复初始化的生命周期。

**深入追问：** 一只怪物死亡掉两份奖励，优先查什么？

**追问答案：** 查 `OnDefeated` 是否重复订阅、死亡是否幂等、旧协程是否仍执行、OnEnable 是否正确清除一次性标志，以及池归还是否触发了意外重复流程。

**常见错误：** 只解释 Queue；说“对象池一定提升性能”却不测实例化频率。

**需要掌握的程度：** 基础必会。

## 5. C# 事件、委托与接口：如何选择？

**问题：** 为什么拾取不能只广播一个无返回值事件？

**考查点：** Command、Event、Query 的语义；返回值和失败路径。

**参考回答：**

> “物品已拾取”是 Event（事实通知），但 Pickup 首先需要请求背包接受。如果先广播再回池，背包满或提交失败会丢物。当前 `RewardPickup.OnRewardPickupRequested` 返回 bool，背包成功提交后 Pickup 才回池，再发送成功通知。需要知道成功与否的动作应使用命令/请求接口，而不是只用已发生事件。

**项目证据：**

- `Assets/Scripts/RewardPickup.cs`
- `Assets/Scripts/PlayerInventory.cs`

**技术原理：** 事件适合一对多通知已发生事实；命令表达意图并通常返回结果；接口明确调用契约。

**深入追问：** 静态请求事件有什么风险？

**追问答案：** 测试和 Domain Reload 下订阅泄漏、多玩家语义不清、订阅顺序和异常传播。可迁移为场景级 `IRewardCollector` 或显式服务。

**常见错误：** 把所有模块通信都叫事件；忽略静态事件清理。

**需要掌握的程度：** 基础必会。

## 6. 数据结构：为什么项目同时使用 List、Dictionary、Queue 和 HashSet？

**问题：** 结合项目说明常用集合的选择。

**考查点：** 数据结构适用场景和复杂度，不是背 API。

**参考回答：**

> 背包的有序堆叠列表适合稳定显示和序列化；对象池用 `Dictionary<Prefab, Queue<Instance>>`，Dictionary 通过 Prefab 快速找到池，Queue 以 FIFO 复用空闲实例；对象放置/ID 校验适合 HashSet 快速判重；Registry 需要按 Logical ID 或 Role 索引。选择依据是查询方式、顺序、唯一性和序列化需求。

**项目证据：**

- `ObjectPoolManager.cs`
- `PlayerInventory.cs`
- `MapObjectRegistry.cs`
- `TowerRunSlotStore.cs` 的 ID 唯一性验证

**技术原理：** Dictionary/HashSet 平均查找 O(1)，List 查找 O(n) 但有稳定顺序和紧凑遍历，Queue 表达先进先出。

**深入追问：** 为什么保存前仍对背包按 rewardId 排序？

**追问答案：** 运行时顺序不一定稳定；排序使 JSON、比较、Hash 和测试稳定，避免容器枚举顺序改变结果。

**常见错误：** 只说 Dictionary 永远比 List 快；忽略小数据、内存和顺序。

**需要掌握的程度：** 基础必会。

## 7. 场景管理：`SampleScene` 怎样进入可玩状态？

**问题：** 从进程启动到玩家可操作，主要对象如何初始化？

**考查点：** 场景、Prefab、Session、绑定顺序和 Ready。

**参考回答：**

> `GameSession` 在场景加载前建立并 `DontDestroyOnLoad`，持有玩家属性、成长和 TowerRun Coordinator。`SampleScene` 提供 GameInput、玩家 Prefab、Canvas 和 RandomMapRuntime。Session 绑定场景对象；TowerRun 选择新开或恢复后请求 `MapGenerationController`；地图完成规划、提交、投影、语义对象实体化、玩家出生、依赖重置、相机绑定和 finalizer，进入 GameplayReady 后才释放流程 Gate 并允许游玩。

**项目证据：**

- `GameSession.cs`
- `Assets/Scenes/SampleScene.unity`
- `Assets/Prefabs/MapGeneration/RandomMapRuntime.prefab`
- `MapGenerationController.cs`

**技术原理：** 场景加载完成只表示 Unity 对象已反序列化，不表示业务世界已准备。

**深入追问：** 为什么不简单重载场景完成下一层？

**追问答案：** 重载不能自动处理 Run record、Seed、Generation、对象 Registry、恢复 finalizer 和 Gate；即使用场景重载也仍需这些业务契约。

**常见错误：** 把 `Start()` 执行完当成全局 Ready。

**需要掌握的程度：** 重点深挖。

## 8. 随机地图：相同 Seed 为什么不一定跨版本相同？

**问题：** 项目如何实现确定性？为什么存档还要检查生成器版本？

**考查点：** PRNG、调用顺序、配置身份和兼容性。

**参考回答：**

> Seed 只初始化伪随机序列。相同算法、配置和随机调用顺序下才有相同输出。增加一次随机调用、调整阈值、改变放置排序都可能让同一 Seed 得到不同地图，所以 Slot 同时保存/校验 generator version 和 configuration identity。否则玩家记录可能被恢复到语义不同的世界。

**项目证据：**

- `RandomMapGenerator.cs`
- `TowerRun/TowerRunSlotStore.cs`
- `TowerRunMapBaseline` 相关代码

**技术原理：** 确定性是整个输入和算法版本的函数，不是 Seed 的单变量性质。

**深入追问：** 跨平台浮点差异怎么办？

**追问答案：** 对关键生成逻辑使用可控数值和明确量化，做跨平台 golden/属性测试；若必须完全一致，可采用整数/定点算法或保存紧凑 MapData snapshot。当前项目尚未证明全平台一致。

**常见错误：** “相同 Seed 永远相同”；只保存 Seed 不保存配置。

**需要掌握的程度：** 重点深挖。

## 9. 地图整合：为什么先 Plan 再 Commit？

**问题：** 为什么商人、怪物等不在 `Start()` 中各自随机找位置？

**考查点：** 全局约束、事务边界、可测试性。

**参考回答：**

> 各自随机会发生占位冲突、顺序依赖、不可达和失败后世界已被修改。项目让每个 Placement Service 根据同一 MapData 产生 Reservation，统一检查地形、可达性、距离和占位；全部通过后提交 Registry，再 materialize 真实对象。这样大多数错误发生在纯数据阶段，旧世界仍可保留。

**项目证据：**

- `MapPlacementPlanning.cs`
- `MonsterPlacementService.cs`
- `MerchantPlacementService.cs`
- `MapObjectRegistry.cs`

**技术原理：** 类似事务的 prepare/commit；也类似编译器先构建中间表示再输出目标。

**深入追问：** Materialize 到第三个对象时失败怎么办？

**追问答案：** 这是 post-commit failure，不能当作纯生成失败无脑重试。需要标记 Failure，并设计补偿/重建或完整旧世界快照。当前架构明确区分了这一阶段，但恢复策略仍可深化。

**常见错误：** 声称 Plan 能让所有 Unity 操作天然回滚。

**需要掌握的程度：** 重点深挖。

## 10. 地图失败：如何避免生成失败留下空场景？

**问题：** 如果随机地图某次生成或放置失败，系统如何处理？

**考查点：** pending/active、重试、Authority 和失败保留。

**参考回答：**

> 候选地图在 Pending 中生成和规划，Commit 前不会替换 Active Map/Plan/Registry，也不会退役旧 Registry。可重试失败使用确定性派生 Seed 进入下一 attempt；全部失败仍保留旧 Active 数据。RandomGenerated 与 LegacyStatic Authority 必须互斥，系统不会静默 fallback 掩盖配置错误。若由 TowerRun 发起操作，失败时 Operation Gate 仍可能阻止玩家控制，所以这里不能简化成“仍可继续游玩旧世界”。

**项目证据：**

- `MapIntegrationOrchestrator.cs`
- `Assets/Scripts/MapGeneration/MapRuntimeFoundation.cs`：`MapRuntimeContext`
- `MapRuntimeBootstrap.cs`
- Git `5df3b40`、`2bfd4e1`、`392b973`

**技术原理：** `Make-before-break（先建后拆）`，类似蓝绿部署。

**深入追问：** 为什么不自动切回 LegacyStatic？

**追问答案：** 自动 fallback 会让测试和玩家看似可用但实际运行错误模式，可能同时存在两套碰撞/对象，配置错误被静默隐藏。

**常见错误：** 先清空 Tilemap 再尝试；所有失败都重试。

**需要掌握的程度：** 重点深挖。

## 11. 玩家攻击：从按键到伤害如何执行？

**问题：** 描述一次玩家攻击的完整调用和数据变化。

**考查点：** 输入、Gate、体力、目标选择、前摇与命中。

**参考回答：**

> `TowerRunGateHub` 通过 `GameInput.SetGameplayInputEnabled` 决定 battle 事件能否发出；事件到达后，`PlayerAction.HandleBattlePressed` 直接检查本地控制锁和体力，从 `PlayerStatsRuntime` 消耗 5 体力，在活动 `BaseDamageable` 中选择范围内最近目标，播放攻击表现，并在约 0.24 秒后调用目标 `TakeDamage`。死亡流程通常通过 Coordinator 获取 Gate 阻止后续输入，但 `HandleBattlePressed` 本身没有显式死亡或普通攻击 cooldown 检查。目标死亡后触发 `OnDefeated`、掉落与楼层计数。

**项目证据：**

- `Player/PlayerAction.cs`：`PrepareAttackDirection()`、`HandleBattlePressed()`
- `Damageable/BaseDamageable.cs`：`TakeDamage()`
- `PlayerStatsRuntime.cs`

**技术原理：** 输入时机、动画前摇和实际伤害时机分离；状态 Gate 防止 UI/死亡时仍发起操作。

**深入追问：** 当前搜索扩大十倍会怎样？

**追问答案：** 当前每次攻击扫描活动 Damageable，不是每帧，但目标数大时会有 O(n) 搜索和潜在分配。应先 Profiler，再考虑 `OverlapCircleNonAlloc`、Registry 候选或空间索引。

**第二轮追问：** 玩家在 0.24 秒前摇期间离开目标范围，当前还会命中吗？

**第二轮答案：** 会，只要准备的目标仍存在、active 且未被击败。当前玩家攻击命中时不重新检查距离或方向；怪物攻击才会复核 Collider 最近点、距离和方向点积。若要支持这种闪避语义，应在玩家命中结算时复用明确的范围/方向判定，并补前摇期间移出范围的测试。

**常见错误：** 说攻击使用碰撞盒却没有代码证据；直接宣称性能差。

**需要掌握的程度：** 基础必会。

## 12. Monster AI：为什么需要 Patrol/Search/Chase/Attack/Return？

**问题：** 五状态分别解决什么问题？如何避免状态抖动？

**考查点：** FSM、感知、攻击复核、leash。

**参考回答：**

> Patrol 提供常态行为；Search 对短暂感知做确认，减少一帧看到就追；Chase 接近目标；Attack 处理前摇、方向锁定、命中复核和冷却；Return 在超出 leash 或丢失目标后回锚点。感知先用距离过滤，再用 `Physics2D.RaycastNonAlloc` 检查视线，confirm duration 和状态转换规则减少抖动。

**项目证据：**

- `MonsterAIController.cs`
- `MonsterPerceptionController.cs`
- `MonsterMovementController.cs`
- `MonsterAttackController.cs`
- Git `0edd67f`

**技术原理：** FSM 把互斥行为显式化；迟滞/确认时间防止边界条件频繁切换。

**深入追问：** 玩家在怪物前摇期间离开范围会怎样？

**追问答案：** 命中时重新检查目标 Collider 最近点、距离和方向点积；开始时合法不代表命中时合法。

**常见错误：** 把 Search 说成完整 A* 寻路；把状态枚举等同完整状态模式。

**需要掌握的程度：** 重点深挖。

## 13. Damageable：为什么玩家和世界对象接收伤害的方式不同？

**问题：** 当前伤害架构有什么优点和耦合？你会怎样演进？

**考查点：** 抽象类、接口、继承/组合和迁移设计。

**参考回答：**

> Monster、Animal、Plant、Destructible 共享 `BaseDamageable` 的生命、死亡、Collider、掉落和池化语义。玩家通过 `PlayerDamageController` 修改 `PlayerStatsRuntime`，有效 Monster 攻击由 `PlayerHurtController` 触发 Visual 红闪但不击退；其他已有伤害来源仍可击退，TowerRun 监听首次死亡进入选择。优点是接收与表现职责清楚，缺点是攻击者依赖具体类型。可用 `IDamageReceiver.TryReceiveDamage(DamageRequest)` 统一入口，但玩家和世界对象仍保留不同内部策略。

**项目证据：**

- `Damageable/BaseDamageable.cs`
- `Player/PlayerDamageController.cs`
- `Player/PlayerHurtController.cs`
- `MonsterAttackController.cs`

**技术原理：** 接口统一契约，组合保留差异；不应为了复用强迫不合理继承。

**深入追问：** 多个 Collider 如何防止一次攻击重复伤害同一对象？

**追问答案：** 查询后按 receiver/entity identity 去重，或让每个根实体暴露单一 Hurtbox 聚合；测试多 Collider 命中。

**常见错误：** 说“已经统一接口”；建议让玩家直接继承 `BaseDamageable` 而不考虑死亡语义。

**需要掌握的程度：** 重点深挖。

## 14. 拾取与背包：如果背包拒绝物品会怎样？

**问题：** 一个掉落从生成到 UI 显示经过哪些步骤？

**考查点：** 完整数据链、稳定 ID、事务与通知顺序。

**参考回答：**

> Damageable 死亡后按稳定 `damageableId` 找奖励配置，从对象池生成 `RewardPickup`。玩家碰撞后 Pickup 先调用请求链；`PlayerInventory` 校验 Reward 和数量、捕获快照、修改堆叠并向 `GameSaveService` 提交进程内候选。成功才发布 InventoryChanged、让 Pickup 回池并刷新 Package UI；失败则恢复快照并保留地面物品。TowerRun 捕获时按 rewardId 排序并保留 unresolved ID。

**项目证据：**

- `BaseDamageable.cs`
- `RewardPickup.cs`
- `PlayerInventory.cs`
- `UI/PackageGridUI.cs`
- `ScriptableObject/RewardSO.cs`

**技术原理：** 不可逆表现动作晚于业务提交；稳定 ID 支持跨进程和资源重命名。

**深入追问：** 连续拾取造成 int 溢出怎么办？

**追问答案：** 在修改前检查剩余空间/溢出，拒绝或使用明确饱和策略；不能依赖整数绕回。持久化规范化还会处理重复记录和范围。

**常见错误：** 先 Destroy Pickup；把 ScriptableObject 当运行时数量容器。

**需要掌握的程度：** 重点深挖。

## 15. 商店：如何保证扣钱、给货和库存一致？

**问题：** 解释 `TryPurchase` 的原子边界与回滚。

**考查点：** 业务事务、跨聚合协调、失败顺序。

**参考回答：**

> `EvaluatePurchase` 先只读检查配置、库存、金币和背包；随后捕获背包快照和原库存，不单独持久化地扣金币、加商品、扣库存，再构造包含 inventory/shop 的统一候选并调用 `GameSaveService.TrySave`。失败恢复背包和库存，成功后通知 UI。因此业务单位是一次购买，而不是三个字段更新。

**项目证据：**

- `Shop/ShopTradeController.cs`：`TryPurchase()`
- `PlayerInventory.cs`
- 商店历史人工流：100→108→103，得 2 肉，库存剩 2

**技术原理：** 当前实现关注原子性和一致性；Unity 主线程下没有数据库级并发隔离。

**深入追问：** 玩家快速连点会不会买成负库存？

**追问答案：** 每次购买必须重新基于最新状态 Evaluate，并保持库存下限和重入约束。历史人工记录不能证明所有快速连点边界，需定向测试。

**常见错误：** 把它称为完整 ACID；声称所有边界已验证。

**需要掌握的程度：** 重点深挖。

## 16. UI：如果金币显示错误，你怎样排查？

**问题：** UI 显示与真实数据不一致时，从哪里查？

**考查点：** 状态源、事件链、视图生命周期和调试方法。

**参考回答：**

> 先确认 `PlayerInventory` 中金币堆叠是真实错误还是只有 Text 错。若数据正确，检查 InventoryChanged 是否发布、HUD 是否订阅、对象是否 active、刷新时读的是否同一 PlayerInventory；若数据也错，再从 RewardPickup/Shop transaction 和保存回滚向上查。用一次操作的 rewardId、旧值、候选值、提交结果和 UI 收到事件时的值建立时间线。

**项目证据：**

- `PlayerInventory.cs`
- `Assets/Scripts/UI/CurrencyHudUI.cs` 与包裹相关脚本
- `PackageGridUI.Refresh()`

**技术原理：** 诊断应先分离 Model 错误和 Projection 错误；不要直接在 UI 修数值。

**深入追问：** 为什么当前包裹全量重建可能卡？

**追问答案：** Destroy/Instantiate 和布局重算可能产生主线程开销与 GC；需在大物品数/高刷新频率下测，再选 Pool、keyed diff 或虚拟化。

**常见错误：** 在 Text 上加减数字“修复”；无测量就断言 GC 严重。

**需要掌握的程度：** 基础必会。

## 17. 保存语义：`GameSaveService` 到底做什么？

**问题：** 玩家攻击后调用 `TrySave`，是否意味着每次攻击都写磁盘？

**考查点：** 当前代码与历史语义、命名债务、内存/磁盘区别。

**参考回答：**

> 不是。当前 `GameSaveService` 注释明确旧调用者仍提交内存事务，但不再读写旧 `save.json`。`TrySave` 会规范化和深拷贝候选到静态内存。真正的磁盘文件是 `TowerRunSlotStore` 在入口检查点写三槽。攻击后调用可能有深拷贝成本，但尚未测出性能回归。

**项目证据：**

- `GameSaveService.cs`：`TrySave()`
- `GameSession.SaveRuntimeState()`
- `TowerRun/TowerRunSlotStore.cs`

**技术原理：** Runtime state、checkpoint 和 durable file 是不同层。名称可能因演进而与职责漂移。

**深入追问：** 你会直接重命名吗？

**追问答案：** 先搜索调用者、用接口或 adapter 迁移并补测试，再重命名；否则只是把语义债务变成大范围机械风险。

**常见错误：** 沿用旧历史说“重启一定恢复 save.json”。

**需要掌握的程度：** 基础必会。

## 18. 三槽存档：如何降低文件损坏风险？

**问题：** 详细解释一次覆盖槽位的写入协议。

**考查点：** 数据校验、CAS 思想、原子替换、写后验证、不确定结果。

**参考回答：**

> 先验证候选记录，再读取现有槽；覆盖非空槽需要与旧内容绑定的 confirmation token。新 JSON 写到 `.tmp` 后立即复读和业务校验；随后 move 或 atomic replace 正式文件并保留 `.backup`；最后重新读取正式文件并比较 SHA-256 token。若不一致则尝试恢复旧版本，并通过再次观察区分 committed、failed 或 uncertain。

**项目证据：**

- `TowerRun/TowerRunSlotStore.cs`：`ReadSlot()`、`TryCommit()`
- 跨进程 writer/reader 各 1/1 记录

**技术原理：** “API 没抛异常”不等于业务提交；Hash 验证一致性但不是安全签名。

**深入追问：** 两个进程同时写同一槽支持吗？

**追问答案：** 当前跨进程测试证明持久化，不证明多写者协调。需要文件锁、named mutex 或基于 token 的完整 CAS；当前应说不支持/未验证。

**常见错误：** 声称绝对不损坏、防作弊或断电安全。

**需要掌握的程度：** 重点深挖。

## 19. TowerRun：为什么保存楼层入口，不保存当前战斗？

**问题：** 入口检查点有哪些取舍？

**考查点：** 一致性边界、产品语义、恢复复杂度。

**参考回答：**

> 入口是稳定边界：地图由 Seed、生成器版本和配置重建，玩家 stats、背包和商店由纯数据应用，怪物恢复为楼层初始语义。这样不必保存动画帧、协程、投射物、物理速度和对象池内部状态，恢复更可测试。代价是死亡后不能回到前一秒，而是回到入口记录。

**项目证据：**

- `TowerRunCoordinator.cs`
- `TowerRunRuntimeStateBridge.cs`
- `TowerRunRestoreFinalizer.cs`
- `TowerFloorRuntimeController.cs`

**技术原理：** Checkpoint 是特定一致边界的恢复记录；staged record 防止读取成功被误认为世界恢复完成。

**深入追问：** 为什么恢复后要等 GameplayReady 才发布 current record？

**追问答案：** 地图、玩家、对象、相机和状态应用可能失败。提前发布会让 UI 和其他系统观察到“已恢复”但世界仍旧或半初始化。

**常见错误：** 称为任意时刻存档；忽略 Generator Version。

**需要掌握的程度：** 重点深挖。

## 20. 事件时序：迷雾为什么会揭示旧位置？

**问题：** 讲一个最难定位的真实 Bug。

**考查点：** 假设、探针、时序、不变量和回归。

**参考回答：**

> New Run 后旧位置附近被揭雾。最初可能是数组未清、Seed 复用或旧代回调。定向探针证明 `MapGenerated` 回调时玩家仍在旧坐标 `(62,62)`，随后才出生到 `(32,32)`，并且当时已经揭示 37 格。因此根因是完成事件早于最终 spawn，修复探索更新边界后定向测试从 0/1 到 1/1，相关 TowerRun flow 4/4。

**项目证据：**

- `.codex/TestResults/tower-fog-sentinel-investigation-2026-10-12.md`
- Git `7da9a7d`
- `MapMinimapController.cs`

**技术原理：** 事件名称不是完成契约；观察者依赖的全部后置条件决定 Completion Boundary。

**深入追问：** 你怎样防止类似问题？

**追问答案：** 对每个公开完成事件写出不变量；使用生命周期 phase/Generation；测试事件发生当刻的状态，不只测最终画面。

**常见错误：** 只说“清空缓存”；虚构 H0033 Sentinel 弹窗来源。

**需要掌握的程度：** 重点深挖，可用 STAR。

## 21. 性能：系统规模扩大十倍怎么办？

**问题：** 如果怪物、地图和背包数据扩大十倍，当前架构哪里先出问题？

**考查点：** 先测量、复杂度、GC、主线程与正确性。

**参考回答：**

> 我不会无数据地断言瓶颈。候选点是生成器多次全图扫描、各 Placement 重复找候选、Tilemap 逐格投影、攻击时扫描 Damageable、包裹全量重建和高频运行时深拷贝。先用目标规模记录 P50/P95、主线程、GC 和 GameplayReady 总时长，再分别考虑候选索引、批量 Tile、后台纯数据生成、Registry 查询和 Slot Pool。

**项目证据：**

- `RandomMapGenerator.cs`
- 各 Placement Service
- `PlayerAction.cs`
- `PackageGridUI.cs`
- `GameSaveService.cs`
- [[10_RPG_Demo_技术亮点与优化路线]]

**技术原理：** 性能优化必须有场景、指标、基线和回归正确性；Big-O 只是起点。

**深入追问：** 地图能直接 `Task.Run` 吗？

**追问答案：** 纯 C# 数据可能后台执行，但 Tilemap/GameObject 等 Unity API 必须回主线程；还需取消、过期 Generation、异常和分帧 Ready 语义。

**常见错误：** “用多线程/ECS 就行”；捏造 FPS 提升。

**需要掌握的程度：** 重点深挖。

## 22. 测试：为什么有多种测试层？

**问题：** EditMode、PlayMode、GPU、跨进程和人工测试分别证明什么？

**考查点：** 测试环境与机制匹配，能否准确报告结果。

**参考回答：**

> 纯生成/校验适合快速 EditMode；MonoBehaviour、物理、场景绑定和跨帧流程用 PlayMode；黑洞 Shader 必须有真实 GPU；磁盘持久化用独立 writer/reader 进程；视觉、手感、音频仍需人工。历史证据中 MapGeneration EditMode 216/216、PlayMode 16/16；TowerRun EditMode 21/21 断言通过但 Runner 挂起、退出码 -1，所以不能概括为所有测试成功。

**项目证据：**

- `Assets/Tests/MapGeneration/`
- `.codex/TestResults/tower-run-save-mvp-runtime-validation-2026-10-12.md`
- BlackHole GPU 与跨进程测试记录

**技术原理：** Test pyramid 还应考虑执行环境；assertion success、runner completion、process exit 是三层结果。

**深入追问：** Runner 挂起怎么查？

**追问答案：** fixture 二分、查看最后测试后的日志、检查静态事件/Editor callback/异步工作/TearDown、与最小项目和相同 Unity 版本对照。

**常见错误：** 报测试数量但不说提交；用 `-nographics` 证明 Shader。

**需要掌握的程度：** 基础必会。

## 23. Git 与整合：为什么使用 Worktree 和 MapMerger？

**问题：** 多个系统如何并行开发并整合？发生冲突怎样处理？

**考查点：** Worktree、merge base、语义合并、Unity 资源风险。

**参考回答：**

> 随机地图在独立 Worktree 深化，Monster 等系统并行，MapMerger 负责把变化带入同一主场景并分阶段整合。Worktree 减少频繁 stash/checkout，但共享 Git 对象且同一分支不能重复 checkout。整合时不只解决文本冲突，还要检查 Scene/Prefab GUID、`.meta`、组件引用和运行时 Authority，再用 PlayMode 回归。

**项目证据：**

- Git worktree 列表
- `ea0e391`～`c367725`
- PR #1 merge `007a58b`
- `.meta` 修复 `92f84e8`

**技术原理：** Three-way merge 基于共同祖先合并文本；Unity YAML 和 GUID 仍需要领域语义审查。

**深入追问：** 为什么不能遇到冲突就选 ours/theirs？

**追问答案：** 两边可能分别增加必要组件/字段，整文件选择会丢另一边语义；应理解共同基点和双方意图，合并后让 Unity 反序列化并运行测试。

**常见错误：** 把 Worktree 说成仓库副本；忽略 `.meta`。

**需要掌握的程度：** 基础必会。

## 24. 新需求：增加职业选择，你会改哪些模块？

**问题：** 现在要求 New Run 前选择职业，并让存档恢复职业，你怎样设计？

**考查点：** 需求拆解、稳定 ID、现有架构适配和测试。

**参考回答：**

> 先确认职业是否只影响初始属性、是否决定 Prefab/技能、旧存档如何处理。用 `CharacterClassDefinition` ScriptableObject 保存稳定 `classId`、基础属性、初始背包和表现配置；StartupChoice 选择后构造 new-run baseline；TowerRunRecord 保存 classId；恢复时在生成/出生前校验并绑定对应玩家配置。不会把职业只存在 UI，也不会在恢复时重新选择。

**项目证据：**

- 当前职业系统尚未实现，设计归入 [[14_RPG_Demo_待实现需求与后续技术设计]]
- 可复用 `GameSession`、TowerRun baseline、Slot compatibility 和 canonical player provider

**技术原理：** 新功能必须接入状态所有者、持久化 Schema、兼容性、运行时绑定和 UI，而不是只加一个下拉框。

**深入追问：** 删除一个职业后旧存档怎么办？

**追问答案：** 明确标记 Incompatible 或提供 classId migration；不能静默换成默认职业，否则玩家状态语义改变。

**常见错误：** 把设计说成已实现；直接保存数组 index；修改 ScriptableObject 作为运行时角色状态。

**需要掌握的程度：** 扩展了解，但非常适合现场设计。

## 25. 快速自测规则

任取一题，回答必须覆盖：

1. 项目中的真实入口；
2. 状态所有者；
3. 至少一个失败路径；
4. 实际验证或证据限制；
5. 一个替代方案/取舍。

只背参考答案最多算 L1；能结合当前源码定位并应对追问才算 L2/L3 的证据。


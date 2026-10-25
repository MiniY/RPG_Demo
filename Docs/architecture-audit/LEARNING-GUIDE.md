> 本轮整合上下文（2026-10-25）：本文原有叙述与验收数字保留为各节标明日期的历史快照。游戏主线已在候选提交 `96371eb36a787585cf5e70141c03555419821058` 保存，其父链包含九个 TowerRun/Portal/UI 提交；默认新冒险 Profile 为 `MainV1`，十二种保留原生怪物，Boss 仍待内容。旧 `ecology-v1`、UI v6 未提交、旧工作树/HEAD 等现在式叙述均不代表本轮状态。新整合 Build/Player 与人工验收只认本轮独立收据；Stage1、B1、Boss 和旧 R4 人工结论不升级。当前整合总账见 `../Integration/MainConsolidation20261025/README.md`。

# 从游戏现象走到代码：RPG_Demo 学习导览

这是一份**审计阅读地图**，不是学习掌握度记录。先在游戏中观察，再在 [查看器](viewer.html) 搜对象，最后读源码证据和测试。下列“面试表达”是组织答案的示范；只有自己能够解释、修改、排错并复核后，才能把它表述为个人经验。现有 `/learn/` 课程的状态保持独立。

Phase 2 已为本次优先的 A 地图、B 战斗、C 背包补充 [逐方法、逐数据的学习稿与 Mermaid](PHASE2-CHAIN-STUDY.md)。本页继续作为全项目导航。

## 先建立三种连接

- `Serialized Reference（序列化引用）`：Inspector/Scene/Prefab 保存的 GUID 或 fileID。例如 Scene 的组件引用脚本或数据资产。图谱可证明文件中有引用，不能证明运行时对象一定有效。
- `Event Subscription（事件订阅）`：脚本在 `OnEnable` 或绑定方法中登记回调，状态变化时通知 UI/关卡。阅读时同时找退订，否则场景切换或销毁后可能残留回调。
- `Runtime Ownership（运行时所有权）`：谁创建或持有某代对象、谁清理它。这个项目的场景绑定 Monster 与代际生成 Destructible 不是同一种所有权。

下面每条按“玩家现象 → Unity 对象 → 组件/脚本 → 数据变化 → 结果”读；再看工程机制与边界。源码行号对应本次扫描快照，改动后先重扫。

## 1. 输入、玩家与攻击

**第一层。** 玩家按攻击键 → `GameInput` 接收 Battle 输入并发 `OnBattlePressed` → 玩家对象上的 `PlayerAction` 处理体力、选择范围内最近 `BaseDamageable`、播放攻击并延时结算 → `currentHealth` 可能减少 → 目标受击或死亡。按键本身不保证命中：体力不足、目标在延迟期间失效或离开条件都可能中断。见 [玩法链路 1](GAMEPLAY-TRACES.md)。

**第二层。** `GameInput` 是输入适配器，`PlayerAction.TryBindInput/UnbindInput` 控制订阅生命周期；`HandleBattlePressed` 接收事件后调用 `TryConsumeStamina(attackStaminaCost)`，`FindNearestDamageableInRange` 做全场搜索，协程在 `attackHitDelay` 后向目标传 `attackDamage` 与攻击者 `transform`（`Assets/Scripts/GameInput.cs:204-210`、`Assets/Scripts/Player/PlayerAction.cs:165-198,214-243,284-334`）。优势是所有可受击目标走统一接口；代价是每次攻击全局扫描，且动画和伤害时间由延迟协调。扩展远程攻击时应先确定选靶、距离与伤害时点，再改结算入口。

**面试表达示范：**“输入组件只发布攻击意图；`PlayerAction` 先做体力和目标条件检查，延迟结算时调用 `BaseDamageable`。我会把动画触发与真正扣血分开验证。” **追问：**目标在攻击动画期间死亡会怎样？ **练习：**在源码中找出订阅、退订和伤害前的三项保护条件，并在 Play Mode 用一个目标验证。入口：`GameInput.cs`、`PlayerAction.cs`。

## 2. Monster、Damageable 与 Visual

**第一层。** 玩家击中怪物 → 父对象的 `BaseMonster` 继承 `BaseDamageable` 接受扣血；`MonsterAIController`/受击控制器响应事件；子对象 `Visual` 的 Animator 呈现动作；生命归零后楼层目标计数和奖励逻辑各自接收死亡结果。当前 `Torch_Blue` Prefab 路径是 `Assets/Prefabs/Monsters/Torch/Torch_Blue.prefab`，Visual Controller 是 `Assets/Animations/Monsters/Torch_Blue/Torch_Blue_Visual.controller`；具体 Inspector 合成和画面仍待 Unity 复验。

**第二层。** `MonsterAIController` 确有 Patrol/Search/Chase/Attack/Return 五种状态，并订阅受击/死亡事件；这是实际的 `Finite State Machine（有限状态机）`，不是根据文件名猜的。`BaseDamageable.ApplyDamage` 统一维护 `currentHealth`、`OnDamaged`、`OnDefeated` 与奖励。怪物作为场景绑定目标由 `MonsterPlacementService` 搬移并重初始化；改 Prefab 不等于每层重新 `Instantiate`（`Assets/Scripts/Damageable/Monster/MonsterAIController.cs:67-71,124-139,183-196`、`Assets/Scripts/Damageable/BaseDamageable.cs:78-104`、`Assets/Scripts/MapGeneration/MonsterPlacementService.cs:150-185`）。改死亡顺序时同时检查 AI 退订、Floor 计数、掉落与对象回收。

**面试表达示范：**“我把行为放在怪物父对象，把呈现放在 Visual；统一受击事件让关卡和奖励各自响应。地图层切换时复用场景绑定目标，需要显式复位。” **追问：**为什么看见新地图不等于创建了新怪物？ **练习：**在查看器打开 `Torch_Blue.prefab`，写出 Visual → Animator 的静态引用，再到 Play Mode 验证一次搬移前后的对象身份。入口：`MonsterPlacementService.cs`、`MonsterAIController.cs`、`BaseDamageable.cs`。

## 3. 可摧毁物、奖励与拾取

**第一层。** 击破植物/动物/可摧毁物 → `BaseDamageable` 按奖励表生成地面 `RewardPickup` → 玩家触发其 Collider 后发出接收请求 → 背包接受才播放拾取/回收。奖励在地面与背包中是两个不同阶段；掉落并不自动持久化到正式槽位。

**第二层。** `DamageableDieRewardListSO` 是掉落配置资产；`BaseDamageable.SpawnRewards` 读取规则，`GenerationScopedRewardLifetime` 把未拾取奖励绑定当前地图代际，避免换层残留（`Assets/Scripts/Damageable/BaseDamageable.cs:121-177`）。`RewardPickup.TryAcceptReward` 按订阅列表请求，`PlayerInventory.HandleRewardPickupRequested` 修改 `ItemStacks`（`Assets/Scripts/RewardPickup.cs:49-104`、`Assets/Scripts/PlayerInventory.cs:144-217`）。这里有 [AA-02](ISSUES.md)：多个接收者的部分提交只是有条件风险，需最小复现。可摧毁物由 `DestructiblePlacementService` 通过 Prefab/接口获取代际实例，清理方式与 Monster 不同。

**面试表达示范：**“掉落表决定生成内容，拾取事件决定接收是否成功，地图代际决定未拾取物何时清理。” **追问：**如果背包已满，地面物体应怎样处理？ **练习：**用一次成功拾取和一次拒绝拾取，记录 `ItemStacks`、地面对象和 UI 的前后值。入口：`BaseDamageable.cs`、`RewardPickup.cs`、`PlayerInventory.cs`。

## 4. 随机地图、生物群落与对象所有权

**第一层。** 开始新局/换层 → `TowerRunCoordinator` 请求生成 → 逻辑 `MapData` 与 `MapPlacementPlan` 产生地形和五类对象的计划 → Tilemap 投影与对象物化 → 玩家/相机定位、关卡收尾 → `GameplayReady` 才表示可玩。`MapGenerated` 表示地形和地图数据到位，不能替代完整可玩门槛。

**第二层。** `MapRuntimeBootstrap` 用显式运行模式选择正式 authority；`MapIntegrationOrchestrator.Execute` 执行生成、规划、Commit 和旧 Registry 退休；`MapGenerationController` 再物化 Merchant、Monster、Destructible、Animal、Plant，完成 finalizer（`Assets/Scripts/MapGeneration/MapRuntimeBootstrap.cs:125-159`、`MapIntegrationOrchestrator.cs:55-93`、`MapGenerationController.cs:122-345`）。`MapGenerationSettings` 是配置，生成时克隆并写 attempt seed；`MapData` 是当前代的逻辑结果；`MapObjectRegistry` 追踪代际对象。`SceneBound（场景绑定）` 的 Monster/Animal/Plant 与 `GenerationScoped（代际限定）` 的 Destructible 需分别初始化和清理。Commit 后物化仍可能失败，见 [AA-05](ISSUES.md)。

**面试表达示范：**“我区分生成成功、规划提交与 GameplayReady；只有最后一步才打开可玩流程。不同对象的生命周期由 Placement Service/Registry 管理。” **追问：**为何 Commit 成功后仍可能不能进入游戏？ **练习：**沿 `TryGenerateMap` 记录一次 generation ID、五个 service 的结果与 `GameplayReady` 时点。入口：`MapGenerationController.cs`、`MapIntegrationOrchestrator.cs`、`MapObjectRegistry.cs`。

## 5. 背包、物品与 UI 数据同步

**第一层。** 玩家拾取一个物品 → `PlayerInventory.ItemStacks` 的数量改变 → `OnInventoryChanged` 通知背包格、金币 HUD 或红点 → UI 调 `Refresh` 读**当前数据**。界面关闭时可以错过事件，但重开会重新读取，不依赖旧事件回放。

**第二层。** `PlayerInventory.TryAddItem` 是变更入口；`RewardSO`/物品资产提供定义，运行时 stack 保存数量。`PackageGridUI.OnEnable` 订阅变化并立即刷新，刷新会清除旧格再实例化可见格（`Assets/Scripts/PlayerInventory.cs:181-217`、`Assets/Scripts/UI/PackageGridUI.cs:53-104,146-162`）。这种 `Observer（观察者）` 关系让数据拥有者不必直接操作每个 UI，但 [AA-03](ISSUES.md) 的全量重建可能影响性能与选择状态。新增物品分类时先确认数据 ID、栈规则、筛选、背包格呈现，再检查保存恢复。

**面试表达示范：**“背包持有真实数量；UI 响应事件后重新读取状态。刷新可以重建，但我会用 Profiler 判断是否需要差量更新。” **追问：**UI 不更新时先查事件还是先查数据？ **练习：**在一次拾取中记录 `ItemStacks` 变更前后、事件触发次数和一个 `PackageGridUI.Refresh` 调用。入口：`PlayerInventory.cs`、`PackageGridUI.cs`、`CurrencyHudUI.cs`。

## 6. 商店与交易

**第一层。** 选择商品 → `ShopGridUI` 提交购买请求 → `ShopTradeController` 检查价格/库存并在一次交易中变更 Gold 与商品 → 背包和商店 UI 看到结果。购买失败应留下原有数量和货币，不能只看弹窗判断成功。

**第二层。** 交易代码在 `ShopTradeController.TryPurchase`，它与 `PlayerInventory` 和商店运行状态协调（`Assets/Scripts/UI/Shop/ShopGridUI.cs:352-365`、`Assets/Scripts/Shop/ShopTradeController.cs:320-398`）。先读“验证条件 → 两类数据写入 → 通知”的顺序，再找失败分支。该链路调用的是内存快照，不等于正式三槽写盘。扩展折扣或回购时需保持交易结果一致，回归 UI、Gold 与恢复。

**面试表达示范：**“交易把余额和物品数量当作同一次业务操作验证，并在成功后通知展示层。” **追问：**若扣费成功但加物品失败，当前代码怎样处理？ **练习：**在购买成功/失败各一次记录 Gold、商品库存、背包数量的三组前后值。入口：`ShopTradeController.cs`、`ShopGridUI.cs`。

## 7. 存档、恢复与设置 UI

**第一层。** 攻击、拾取或购买会更新当前会话与内存快照；玩家在设置界面明确保存楼层入口时，`TowerRunSlotStore` 才提交正式槽位。选择恢复槽位后，先生成对应地图，再应用 staged 运行状态，最后刷新 UI。设置 UI 的音量/静音是单独的偏好数据，不应混同于 Tower checkpoint。

**第二层。** `GameSaveService.TrySave` 调 `TryCommitRuntimeData`，只替换 `cachedData`（`Assets/Scripts/GameSaveService.cs:193-221`）；`TowerRunCoordinator.TryCreateSaveRequest` 和 `TrySaveEntranceCandidate` 形成稳定入口候选，再由 `TowerRunSlotStore.TryCommit` 写正式记录（`Assets/Scripts/TowerRunCoordinator.cs:167-204`、`Assets/Scripts/TowerRun/TowerRunSlotStore.cs:696-739`）。恢复后 `TowerRunRuntimeStateBridge.TryApply` 依赖地图基线和玩家对象就绪（`TowerRunCoordinator.cs:233-299`）。这是两层不同生命周期的数据，名字中的 Save 容易误导，见 [AA-06](ISSUES.md)。设置页还操作 `AudioListener.volume` 与偏好存储（`Assets/Scripts/UI/TowerRunUiController.cs:450-481`）。

**面试表达示范：**“我区分运行期内存快照和正式三槽文件；恢复时地图生成在状态应用之前，这决定引用绑定顺序。” **追问：**为什么拾取后直接退出进程不一定恢复刚拾取的物品？ **练习：**保存前后分别检查内存 `cachedData` 与正式槽位记录，再用独立进程恢复验证入口状态。入口：`GameSaveService.cs`、`TowerRunCoordinator.cs`、`TowerRunSlotStore.cs`。

## 8. 出口、楼层目标与状态

**第一层。** 当前层的 Required 目标死亡 → 楼层控制器计数并开放黑洞出口 → 玩家靠近按 F，且生命/输入条件满足 → 请求换层 → 下一代地图与状态生成。只看黑洞图像不能断定出口可用。

**第二层。** `TowerFloorRuntimeController` 订阅当前代 `BaseDamageable.OnDefeated`，根据 `TowerFloorExitState` 从 Locked 进入 Available；`GameInput.OnInteractPressed` 经距离和玩家条件检查，才发布 `ExitUseRequested` 给 Coordinator（`Assets/Scripts/TowerFloorRuntimeController.cs:116,163-213,253`、`Assets/Scripts/TowerRunCoordinator.cs:380-423`）。这里是真实状态值 Unprepared/Locked/Available/Transitioning/Retired。新增目标类型时要核对 Required 标记、重复死亡、旧代事件退订与出口周边安全区。

**面试表达示范：**“出口开放由当前代目标完成度决定，F 交互还经过独立的可用性门槛。” **追问：**旧地图目标死亡事件为何不能影响新楼层？ **练习：**记下目标数从 1 到 0 时状态变化，以及连续按 F 时是否只启动一次切层。入口：`TowerFloorRuntimeController.cs`、`TowerRunCoordinator.cs`。

## 9. Camera、Minimap 与画面反馈

**第一层。** 地图出现后，玩家先被放到出生点，相机再绑定目标；小地图读取生成地图并更新图像/迷雾。若画面有地形但相机或小地图未就绪，不能认定整个生成流程已完成。

**第二层。** `CameraBindingService` 是绑定入口，`MapMinimapController` 订阅 `MapGenerated` 并持有运行时图像/雾纹理；它读地形数据，但正式可玩仍看 `GameplayReady`（`Assets/Scripts/MapGeneration/MapGenerationController.cs:258-284`、`Assets/Scripts/MapGeneration/MapMinimapController.cs:307-359`）。修改出生点、Tilemap 或 Camera 时同时检查小地图位置、雾揭示和楼层出口标记。

**面试表达示范：**“相机与小地图是地图生成的下游呈现；两者读同一代数据，但准备时点不同。” **追问：**为什么 `MapGenerated` 不足以证明玩家已经可以操作？ **练习：**在一次生成中记录玩家定位、相机绑定、小地图更新和 `GameplayReady` 的先后。入口：`MapGenerationController.cs`、`CameraBindingService.cs`、`MapMinimapController.cs`。

## 10. HUD、红点与设置界面

**第一层。** Gold 数量变化 → HUD 数字更新；打开或关闭背包 → 红点状态刷新；在设置页操作音量、存档或恢复 → 各自控制器读取当前状态并反馈。红点当前依据 Gold/面板状态，不能讲成“每件物品已读/未读系统”。

**第二层。** `CurrencyHudUI.OnEnable` 订阅 `PlayerInventory.OnInventoryChanged`，`OnDisable` 退订，`RefreshGoldAmount` 读取现值（`Assets/Scripts/UI/CurrencyHudUI.cs:36-77`）。`PackageHudButtonUI.RefreshRedDot` 独立计算提示（`Assets/Scripts/UI/PackageHudButtonUI.cs:61-71,142-189`）。`TowerRunUiController` 绑定 Coordinator 的 `StateChanged` 并 `Refresh`，音量通过 `AudioListener.volume` 设置（`Assets/Scripts/UI/TowerRunUiController.cs:148-198,450-481`）。UI 是状态的观察者；如数字不变，先检查模型值，再检查订阅，最后检查显示层。

**面试表达示范：**“HUD 和设置 UI 都以运行数据为来源，并在启用时主动刷新；事件只负责提醒状态变化。” **追问：**关闭面板期间发生变化，重开为何仍能显示正确值？ **练习：**关闭背包后拾取 Gold，再打开，记录库存数、HUD 文本和红点变化及其刷新入口。入口：`CurrencyHudUI.cs`、`PackageHudButtonUI.cs`、`TowerRunUiController.cs`。

## 11. Animator 与音频反馈

**第一层。** 攻击、受击、死亡或拾取动作发生 → 行为控制器触发动画变化 → `Visual` 的 Animator 播放对应片段。设置页调节音量/静音会改变总播放音量；本次没有证据支持独立游戏音频 Manager 的完整事件体系。

**第二层。** `PlayerAction.OnAttackPerformed` 与 `PlayerAnimationController` 的连接应按事件订阅读；Monster 的 `MonsterAnimationController` 与 Visual Animator 属于呈现路径（`Assets/Scripts/Player/PlayerAnimationController.cs`、`Assets/Scripts/Damageable/Monster/MonsterAnimationController.cs`，具体序列化对象见图谱）。Prefab 中引用某 Controller 是静态事实；状态名、参数类型、Animation Event 方法签名及实际播放必须在 Unity Editor/Play Mode 核对。音量由 `TowerRunUiController` 直接设置 `AudioListener.volume`，说明目前是轻量设置控制（`Assets/Scripts/UI/TowerRunUiController.cs:450-481`）。

**面试表达示范：**“行为状态和 Visual 动画分离；我会分别验证业务事件与 Animator 参数，避免把资源引用当成运行结果。” **追问：**Inspector 引用了 Controller，为什么动作仍可能不播？ **练习：**在 Editor 核对当前 `Torch_Blue` Visual Animator 的 Controller、参数和一次受击/死亡状态转换。入口：`MonsterAnimationController.cs`、`Torch_Blue_Visual.controller`。

## 12. Session、Runtime Bootstrap 与隐式依赖

**第一层。** 进入 `SampleScene` → 跨场景 `GameSession` 建立并绑定玩家、背包等当前运行对象 → `MapRuntimeBootstrap` 选择随机地图或兼容模式，校验正式地图 authority → 各系统才通过场景引用、事件或运行时查找协作。看见某个 Manager 对象存在，不代表它拥有当前模式的权限。

**第二层。** `GameSession` 在 `BeforeSceneLoad` 初始化，场景加载后绑定实际对象（`Assets/Scripts/GameSession.cs:33-55,143-179`）；`MapRuntimeBootstrap` 用显式 Mode 和双权威根检查（`Assets/Scripts/MapGeneration/MapRuntimeBootstrap.cs:13-23,125-159`）。`FindObjectsOfType`、`GetComponent` 和 Singleton 是 `Implicit Dependency（隐式依赖）`：调用方不一定有 Inspector 连线，静态资产图可能漏掉实际运行目标。查此类连接要看选择条件、唯一性断言和失败路径，不能仅数图谱入边。

**面试表达示范：**“我先确定会话和地图 authority，再读服务调用；Legacy 对象存在不等于正式流程在调用它。” **追问：**为什么场景里两个地图相关对象都 Active 仍可能只有一个正式入口？ **练习：**从 `SampleScene` 的 Bootstrap 对象出发，记录 Mode、两个根对象及一次新游戏时谁最终发布 `GameplayReady`。入口：`GameSession.cs`、`MapRuntimeBootstrap.cs`、`MapGenerationController.cs`。

## 建议的实际学习顺序

先走 C：从一个地面 `RewardSO` 和数量走到 `InventoryItemStack` 与可见格；再走 B：向上补攻击、死亡和掉落；最后走 A：把 Monster/Destructible 放进地图代际、直到 `GameplayReady`。理解这三条后再比较“内存快照 → 正式槽位 → 独立进程恢复”。每段先预测，再运行，再解释实际值。项目代码存在、自动测试通过或这份文字本身，都不能替代你自己的解释与实践证据。

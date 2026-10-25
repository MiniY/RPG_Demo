> 本轮整合上下文（2026-10-25）：本文原有叙述与验收数字保留为各节标明日期的历史快照。游戏主线已在候选提交 `96371eb36a787585cf5e70141c03555419821058` 保存，其父链包含九个 TowerRun/Portal/UI 提交；默认新冒险 Profile 为 `MainV1`，十二种保留原生怪物，Boss 仍待内容。旧 `ecology-v1`、UI v6 未提交、旧工作树/HEAD 等现在式叙述均不代表本轮状态。新整合 Build/Player 与人工验收只认本轮独立收据；Stage1、B1、Boss 和旧 R4 人工结论不升级。当前整合总账见 `../Integration/MainConsolidation20261025/README.md`。

# RPG_Demo 架构审计查看器零基础实操教程

> 适合已经会在 Unity 里找对象和脚本、第一次用这张“项目对象地图”的你。建议先做第 1 章，再做第 2、4、3 章；第 5～7 章用于独立排查。界面按钮解释见 [使用说明](VIEWER-MANUAL.md)；Obsidian 中可打开 [[RPG_Demo 架构审计查看器使用说明]]。本文 8 张截图于 2026-10-14 用 agent-browser 从极简深色离线页面重拍。项目继续变化后，先看页头版本，再按第 6 章复查。

## 开始前：你正在看什么

查看器文件：`D:\UnityProjects\RPG_Demo\Docs\architecture-audit\viewer.html`。用浏览器打开它；不需要启动 Unity。它把项目里的文件和 Unity 对象画成卡片，把文件中发现的引用或人工核对过的源码关系画成线。**卡片和线是查找线索；实际画面与运行效果需要到 Unity 验证。**

先记住一条最小规则：**看见现象 → 找对象 → 点连接 → 读证据 → 回源码/Unity 验证**。例如“火把怪物不动”先找 `Torch_Blue`，再查 `Visual`、`Animator`、移动脚本，最后在 Play Mode 观察。

首次出现的词：Prefab（预制体）是可复用的对象模板；GameObject（游戏对象）是模板或场景里的一件物体；Component（组件）挂在 GameObject 上；Script（脚本）是自定义组件的 C# 实现；Animator（动画控制器组件）根据参数播放 Controller 中的状态和动画。一个 Prefab 文件里可以同时含多个 GameObject，每个 GameObject 又可以挂多个 Component，所以搜同一个名字会出现多项。

**白话解释**：Prefab 像“整套模型”，GameObject 像其中一个零件，Component 像零件上装的功能，Script 是某些功能的说明书。

## 第一章：第一次打开，走通 7 个区域

![真实页面总览：顶部区域 1、左侧区域 2～4、中央区域 5～6、右侧区域 7](tutorial-assets/01-overview.png)

1. **打开文件，看区域 1**。用浏览器打开上面的 `viewer.html`。页头“可查对象”和“连接线索”是扫描条目数，不是游戏中同时存在的怪物数或真实调用次数。再看“来源分支、生成时 HEAD、时间、生成时源码状态”。“当前是否仍一致：未检查”表示离线页面无法读取此刻的工作区；需要运行第 6 章的命令。
2. **看区域 5 的系统总览**。中央 `Monster`、`Inventory` 等卡片是按文件归的组。点 `Monster` 卡片，左侧会出现这一组的对象。卡片间的淡线是跨组连接数量的汇总，没有执行箭头，也不是一次具体方法调用。
3. **在区域 2 搜索**。点“试试 Torch_Blue”或在搜索框输入 `Torch_Blue`。搜索框下有默认收起的“第一次使用？展开 3 步”和教程入口；需要时再展开。区域 3 会显示匹配条目。若有 0 项，先点“清空搜索和筛选”，缩短关键词，检查区域 4 的类型、系统和问题状态；0 项不证明项目里没有该对象。
4. **在区域 3 点具体结果**。点带“Prefab 文件”的 `Torch_Blue`。选中的结果会显示紫色背景和左边线。中央切换为它附近的局部图，右侧区域 7 展示“它是什么”“在项目哪里”“谁指向它”“它指向谁”；“复制路径”可直接带走完整文件路径。局部图最多只画一部分邻居，完整线索先看右栏列表。
5. **看上下游**。局部图左边是“谁指向它”，右边是“它指向谁”；顶部三个按钮可只画一侧。右栏仍列完整连接。这里的“上下游”先理解为记录的来源与目标，不一定是游戏运行先后顺序。
6. **点一条线看区域 6 和 7**。箭头只表示已知的来源指向目标；无箭头的系统汇总线、文字匹配线不能推断执行方向。在中央点线，或在右栏点“看这条线的解释”。读“连接类型 → 方向含义 → 证据从哪里来 → 验证到哪一步”。紫色是当前选中；蓝灰空心点加“待验证”才表示仍缺运行证据。实线、虚线、点线的意义见区域 6；C# 文字匹配只给线索，不是确认的方法调用。
7. **回到起点**。点“回到系统总览”只改变中央图；点“清空搜索和筛选”才撤掉条件并回初始状态。想保留筛选看总览时用前者，想从头开始时用后者。

**理解检查**：某条线标“仅文字线索”，你能否说“Unity 运行时一定调用它”？

**参考答案**：不能。它只表示扫描器在 C# 文本里找到名称。需要打开源码确认调用位置，再做适用场景下的测试或 Play Mode 观察。

## 第二章：用 Torch_Blue 找到 Prefab、Visual 和风险

### 2.1 从同名搜索结果选对文件

在区域 2 点“试试 Torch_Blue”。本次重拍时区域 3 显示 **25 个匹配对象**；项目继续开发后请以页面实时显示的匹配数为准。截图里同名 `Torch_Blue` 可能是 Prefab 文件、该 Prefab 内的 GameObject、掉落数据资产、PNG、动画片段；`Visual` 和 `DropPoint` 也是这份 Prefab 内的子对象。先看每项的**类型标签和完整路径**，不要只看大字名称。

![真实搜索结果：同名对象有不同类型；选择带 Prefab 文件标签的一项](tutorial-assets/02-torch-search.png)

操作：在区域 3 找到“**Prefab 文件 · AA-01 · Torch_Blue**”，路径应为 `Assets/Prefabs/Monsters/Torch/Torch_Blue.prefab`，点击它。不要选“游戏对象 Torch_Blue”或 `Assets/ScriptObjects/DamageableDieReward/Torch_Blue.asset`。区域 7 应出现 Prefab 路径和关联问题 `AA-01`。

**白话解释**：同名文件、模板里的物体、掉落配置是三件不同的东西；路径和类型像身份证。

### 2.2 看周围对象，逐线核对

![选中正确的 Prefab：中央局部图、右侧 AA-01 和上下游](tutorial-assets/03-torch-prefab.png)

中央的 `Torch_Blue` 周围能看到 `Visual`、`DropPoint`、`SampleScene`、`Torch_Purple/Red/Yellow`。右栏“它指向谁”中的 `Visual`、`DropPoint` 是 Prefab 中记录的 GameObject；`SampleScene` 在“谁指向它”中，说明 Scene 文件引用这个 Prefab。其他火把颜色出现，是因为它们把蓝色 Prefab 作为 Prefab 来源/变体参考。**Prefab Source（预制体来源）关系**表示资产继承；普通 `serialized GUID reference` 表示某个字段保存了资源 GUID。两者都来自序列化文件，但含义不同。

操作：在右栏“它指向谁”找到 `Visual`，点其下“看这条线的解释”。看到“`Torch_Blue → Visual`”“包含游戏对象”“文件中可见”，本次证据位置为 `Torch_Blue.prefab:267`。再点“打开证据位置”，或者在 IDE 打开该文件指定行。该连接证明 Prefab 文件里有 Visual 子对象，**不证明每个游戏场景里都显示正确**。

![点击 Visual 的连接后，右栏显示两端、关系、证据及验证边界](tutorial-assets/04-visual-evidence.png)

回到 Prefab 节点，点 `Torch_Purple → Torch_Blue` 的“Prefab 继承来源”线；它来自 `Torch_Purple.prefab` 的 `m_SourcePrefab`，箭头指向基础 Prefab。另一条普通 GUID 引用可能连接同一对卡片，但只说明资源字段保存了 GUID；两者不能混同。再点 `SampleScene` 的引用线作比较。看到箭头和颜色先别下 Bug 结论：方向、证据级别与问题状态是三件不同的事。

### 2.3 回 Unity 查 Inspector

1. Unity 的 **Project** 窗口打开 `Assets/Prefabs/Monsters/Torch/Torch_Blue.prefab`，进入 Prefab Mode。根对象应是 `Torch_Blue`；展开找 `Visual` 和 `DropPoint`。`Visual` 名称见文件第 267 行。
2. 选 `Visual`，在 **Inspector** 找 `SpriteRenderer`、`Animator`、`MonsterAnimationController`。Animator 的 Controller 字段应解析到 `Torch_Blue_Visual.controller`；Prefab 第 350 行存其 GUID，`MonsterAnimationController` 的三个参数名见第 377～379 行。
3. 打开 Controller 的 Parameters，核对 `IsMoving`、`Attack`、`AttackDirection`，源文件约第 286～304 行。Inspector 是 Unity 解析后的视图；文本中的 GUID 仅证明保存了引用。
4. 在 `SampleScene` 找场景中的 Torch_Blue 和 `RandomMapRuntime`。场景对 `monsterTarget` 有 override，详见 [AA-01 证据](ISSUES.md)；不要只看 Prefab 默认空值就断定运行时没目标。
5. 若 Unity 已有未保存工作，先保留现场，不为教程测试强行退出或启动第二个 Editor。安全时在 Play Mode 看移动、攻击参数与画面；记录场景、版本、步骤和截图。AA-01 **整体仍为 Unverified**：此前隔离快照的定向 Editor/Play Mode 断言覆盖过部分连接；本教程截图时 `MonsterAnimationController.cs` 又有未提交修改，图谱已把相关运行证据降为“过期或不可用”。当前代码组合还需重跑定向测试，四方向完整画面、树冠/水域及复杂边缘追击仍待人工验收。

![问题跳转后的 AA-01：关联三项资产，状态仍为“还没验证”](tutorial-assets/05-aa01-risk.png)

**理解检查**：Inspector 中 Controller 不为空，是否足以宣布“AA-01 已全部通过”？

**参考答案**：不足。它只验证资源解析；还需在目标场景与地形条件下观察参数变化、动画方向、遮挡和移动行为。已有部分定向测试也不能覆盖所有视觉情况。

## 第三章：从一次攻击追到掉落

先在区域 4 的“跟着哪条游戏流程看？”选 **B 战斗与奖励**，展开左侧“逐条查看流程连接”。点其中一条连接的来源对象，再逐步点相关脚本；连接详情显示具体方法时，点击源码位置。列表编号只是方便查找，**不是运行顺序**。若图谱没画出某一步，就按下面路径人工查源码，不要补画一条想象中的线。

| 游戏里发生什么 | 去哪里看 | 实际改了什么 |
|---|---|---|
| 玩家按攻击键 | `GameInput.cs:204-210` → `PlayerAction.cs:214-243` | 输入事件交给 `PlayerAction`，体力不足或 Gate 不允许时不会完成攻击。 |
| 前摇后命中 | `PlayerAction.cs:284-307` | 重新检查目标，调用 `attackTarget.TakeDamage(attackDamage, transform)`。 |
| 目标扣血或死亡 | `BaseDamageable.cs:72-104` | `currentHealth` 减少并夹到 0；首次归零进入死亡处理。 |
| 按奖励规则产生地面物 | `BaseDamageable.cs:121-158` | 从奖励表取规则，经对象池取得掉落并初始化 `RewardPickup`；规则可能使本次无掉落。 |
| 玩家接触掉落 | `RewardPickup.cs:49-104` | 算 `finalAmount`，顺序请求接收者；接受才收走地面物。 |

**Data Flow（数据流）**指一个值由谁产生、由谁传递、由谁修改。例如 `attackDamage` 从 `PlayerAction` 传给 `TakeDamage`，后者修改 `currentHealth`；`RewardSO` 和数量被传给地面 `RewardPickup`，接触玩家后再送背包。**Event（事件）**是一个对象通知订阅者；`RewardPickup.OnRewardPickupRequested` 是请求事件，不能仅凭事件线推断只有一个订阅者。

**白话解释**：追一条功能时，别只问“谁连着谁”，还要跟着具体数值走：伤害是多少、血量变多少、掉落数量是多少。

在 Unity 验证：安全打开 `SampleScene`，记录目标的 `CurrentHealth`、玩家体力、攻击距离；按一次攻击键，看前摇结束后血量是否减少。重复到死亡，记掉落 `RewardSO` 和数量。若没有掉落，先检查奖励规则和随机结果，不能直接说伤害链断了。图谱和本章源码是静态/人工核证；本次教程**没有新运行 Unity 测试**。先前有范围明确的隔离测试，见 [VALIDATION](VALIDATION.md)。

**理解检查**：按了攻击键但目标没扣血，先查哪三处？

**参考答案**：先查输入 Gate 和体力是否让攻击开始；再查目标是否在攻击范围且被选中；最后查前摇结束时目标是否仍有效、`TakeDamage` 是否被调用。不能只凭连线判定命中。

## 第四章：从地面物品追到背包和 UI

在区域 4 选 **C 背包与 UI**。左侧出现 8 条已核对的连接，区域 3 收窄到 9 个脚本。点 `RewardPickup`、`PlayerInventory`、`PackageGridUI`，逐个读右栏“它指向谁”和连接证据。列表不是 Unity 自动执行日志，也不保证是完整调用顺序。

如果左侧筛选暂时在屏幕下方，点搜索框下面的“按流程或问题编号找”即可滚到区域 4；选择流程后左栏还可继续向下滚动查看每条连接，右侧详情则单独滚动。截图中的紫色背景标明当前选中的 `PlayerInventory`，不表示它的所有关系已通过运行测试。

![C 背包与 UI 的真实入口：左侧逐条连接导航和 9 个结果](tutorial-assets/07-inventory-route.png)

从一个地面奖励想象一个具体例子：`RewardSO` 表示物品种类；`RewardPickup.amount` 是来源数量，`RewardSO.GetPickupAmount` 可把它变为 `finalAmount`（`RewardPickup.cs:55`）。`PlayerInventory.TryAddItem` 在第 202～217 行调用 `TryAddItemWithoutPersistence` 修改内部 `ItemStacks`，然后 `SaveCurrentState`，成功才发 `OnInventoryChanged`。`PackageGridUI` 第 61 行订阅事件，`Refresh` 第 82～99 行读取当前堆叠并调用 `PackageSlotUI.SetData(reward, amount)`；`CurrencyHudUI` 第 41 行也订阅，刷新金币数字。

![选中 PlayerInventory：能看到 RewardPickup、GameSaveService、PackageGridUI 与 CurrencyHudUI 的线索](tutorial-assets/08-inventory-detail.png)

按“数据住在哪里”分三层：**地面**是 `RewardPickup` 的临时状态；**进程内背包**是 `PlayerInventory.ItemStacks`；`GameSaveService.TrySave` 第 198～221 行更新的是内存 `cachedData`。正式 Tower Run 槽位的磁盘写入另由 `TowerRunSlotStore.TryCommit` 第 696～739 行执行。不要把拾取后“当前界面更新”误当“已经写入正式存档”。UI 读取现有背包状态，`Refresh` 本身不增加物品。

**白话解释**：地上有一件物品，背包账本加一，界面再重画数字；界面只是显示账本，磁盘存档又是另一道手续。

在 Unity 验证：记拾取前 `RewardSO`、`amount`、`ItemStacks` 数量和 UI 显示；拾取后分别查背包数与 `PackageGridUI` 格子。若背包数变而 UI 不变，先查 `OnInventoryChanged` 订阅/刷新；若背包也没变，先查触发器、接收者返回值和 `TryAddItem`。要验证跨进程持久化，必须走正式槽位保存并重新加载，不能只看 `cachedData`。本次已有奖励到背包的隔离测试，**最终屏幕 UI 显示仍未运行验证**。

**理解检查**：`PackageGridUI.Refresh` 被调用是否意味着它给背包新增了一件物品？

**参考答案**：不意味着。新增由 `PlayerInventory` 的数据修改方法完成；`Refresh` 读取当前堆叠并更新格子显示。

## 第五章：把“关联问题”变成可验证的问题

架构风险是“这条连接可能在某些条件下出错”的线索；Confirmed Defect（已确认缺陷）需要明确触发条件、复现结果和证据；Unverified（待验证）表示证据尚不足。**带 AA 编号不等于该对象已经坏了。**

在区域 4 的“直接跳到某个问题”选 `AA-01`。结果变成相关资产，右栏显示“还没验证”。打开 [问题清单](ISSUES.md) 的 AA-01，照条件到 Unity 检查 Visual、Animator 与画面；记录通过的部分和没覆盖的部分。再选 `AA-02`：相关脚本是 `RewardPickup`、`PlayerInventory`，右栏显示“已确认问题”。

![AA-02 问题跳转：只列相关脚本；须读触发条件](tutorial-assets/06-aa02-risk.png)

AA-02 的**必要条件**是至少两个奖励接收者按顺序收到同一个请求，前者加物成功、后者拒绝。`RewardPickup.cs:84-104` 遍历 `GetInvocationList`；`PlayerInventory.cs:146` 订阅，第 202～217 行可能已提交自身状态。先前隔离 Play Mode 的 `TwoInventoryReceiversCanPartiallyCommitWhenSecondRejects` 测到“第一背包 +1、第二拒绝、地面奖励还在”，因此判为**条件性已确认缺陷**；正式 `SampleScene` 默认是否自行出现第二个接收者，仍无证据。不要把条件性缺陷描述成默认玩法每次拾取都出错。

做你的检查记录时写五栏：**版本/场景、触发条件、预期、实际、证据位置**。若无法复现，记 `Unverified` 和未覆盖条件；不要为了填表强写 PASS。先前原始测试结果与适用条件见 [VALIDATION](VALIDATION.md)。

**理解检查**：看到 AA-02 的“已确认问题”，能否断言正式场景只有一个背包时每次拾取都部分提交？

**参考答案**：不能。确认的是双接收者一成一败条件下的部分提交；默认单接收者场景是否达到条件尚未证实。

## 第六章：修改前后怎样安全用图

假设你要改 `MonsterMovementController` 或 `PlayerInventory`。先在区域 2 找脚本，点区域 3 结果。右栏“谁指向它”列候选上游，“它指向谁”列候选下游，“改动它时还要留意谁”提供两步排查对象。逐条点线看证据，**这不是完整自动影响分析**：动态事件、运行时实例和反射可能没画全。

改怪物移动前，至少核对 `Torch_Blue`/场景引用、`MonsterAnimationController`、地图边缘移动测试与 AA-01 的人工画面。改背包前，至少核对 `RewardPickup`、`GameSaveService`、`PackageGridUI`、`CurrencyHudUI`、正式槽位保存及 AA-02/AA-03。

改后在项目根目录的 PowerShell 执行：

```powershell
python Tools/ArchitectureAudit/scan.py --check
```

若输出 `upToDate: false`，说明图谱或页面与当前源码/模板不一致；它不是 Unity 编译失败。当前工作分支上的手动审计可先保存旧 `graph.json`，然后运行：

```powershell
python Tools/ArchitectureAudit/scan.py --baseline <旧图谱的完整路径>
python Tools/ArchitectureAudit/scan.py --check
```

比较 `addedNodes`、`removedNodes`、`changedNodes`、`addedEdges`、`removedEdges`，再回查看器搜索受影响对象。**main 分支**使用统一入口 `powershell -File Tools/ArchitectureAudit/update.ps1`；它要求源码及生成文件没有未提交改动，成功刷新后自动生成 `Docs/architecture-audit/GRAPH-CHANGES.json` 并提示人工复核。未提交文件不会被自动覆盖；具体恢复步骤见 [自动更新维护说明](AUTO-UPDATE.md)。扫描更新只能证明新的静态文件关系，不能替代 Unity 编译、Play Mode 或人工视觉复验。

**理解检查**：`--check` 输出 `upToDate: true`，能否直接关闭 AA-01？

**参考答案**：不能。它只说明生成图谱与本次扫描一致；AA-01 的部分画面与追击条件仍需 Unity 运行验证。

## 第七章：独立练习（先做，再看答案）

### 练习 1：辨认同名对象

**目标与操作**：清空条件，搜索 `Torch_Blue`，找出 Prefab 文件、Prefab 内根 GameObject、掉落数据资产各一项；记路径和类型。**自测**：为什么不能只看名称？

<details><summary>参考答案与常见误解</summary>

Prefab 路径是 `Assets/Prefabs/Monsters/Torch/Torch_Blue.prefab`；根 GameObject 在同一文件里但类型为“游戏对象”；掉落数据在 `Assets/ScriptObjects/DamageableDieReward/Torch_Blue.asset`。同名不等于同一节点。常见误解：把掉落配置当怪物 Prefab。

</details>

### 练习 2：比较两种连接

**目标与操作**：选 Torch_Blue Prefab，分别点 `Visual` 与 `Torch_Purple` 的连接，记连接类型、证据文件/行、验证状态。**自测**：哪条是父子结构，哪条是 Prefab 来源？

<details><summary>参考答案与常见误解</summary>

`Torch_Blue → Visual` 是 Prefab 内的包含关系，本次证据为 `Torch_Blue.prefab:267`；`Torch_Purple → Torch_Blue` 的 `prefab source` 表示变体来源，证据在 `Torch_Purple.prefab`。常见误解：把变体来源箭头说成运行时“紫色怪物调用蓝色怪物”。

</details>

### 练习 3：跟一件物品到 UI

**目标与操作**：选 C 背包与 UI，点 `RewardPickup`、`PlayerInventory`、`PackageGridUI`；在源码中定位 `TryAcceptReward`、`TryAddItem`、`Refresh`，写出谁修改 `ItemStacks`、谁只读它。**自测**：背包数变化但格子不变时，先查哪里？

<details><summary>参考答案与常见误解</summary>

`RewardPickup` 发请求，`PlayerInventory.TryAddItem` 修改堆叠并发 `OnInventoryChanged`，`PackageGridUI.Refresh` 读取并交给 `PackageSlotUI.SetData`。先查 UI 是否订阅事件、`Refresh` 是否执行和格子绑定。常见误解：把 UI 刷新当成数据修改。

</details>

### 练习 4：判断风险结论边界

**目标与操作**：按问题编号分别选 AA-01、AA-02，打开问题清单与验证记录。写出每项“已证实什么”“尚缺什么”。**自测**：为什么 AA-02 是条件性 Confirmed Defect，AA-01 整体仍 Unverified？

<details><summary>参考答案与常见误解</summary>

AA-02 有双接收者一成一败的隔离复现，但默认场景是否会自发形成该条件不明。AA-01 的引用与部分动画/边缘定向测试曾在隔离快照通过；当前动画脚本改动后的连接需重测，完整画面和复杂追击仍缺观察。常见误解：把“找到关联问题”当成“这个对象一定坏了”。

</details>

### 练习 5：为一处修改列回归清单

**目标与操作**：假设要改 `PlayerInventory.TryAddItem`，查上游、下游及两步影响，列 3 个需要实际验证的结果。运行 `scan.py --check`，解释其结论边界。**自测**：仅凭图谱能证明正式存档仍正确吗？

<details><summary>参考答案与常见误解</summary>

至少验证地面拾取是否正确入包、格子/金币 HUD 是否刷新、正式槽位保存后重新加载是否保持数量；另核对 AA-02 双接收者条件。`--check` 只核对静态生成结果，不能证明存档或画面。常见误解：把 `GameSaveService.cachedData` 的进程内更新当作磁盘提交。

</details>

做完第 5 题后，用你自己的话记录一次“游戏现象 → 对象 → 源码方法 → 运行观察 → 未验证处”。能独立说清这五项，才算能把查看器用来分析一个新功能；截图或 AI 文档本身不表示你已经掌握。

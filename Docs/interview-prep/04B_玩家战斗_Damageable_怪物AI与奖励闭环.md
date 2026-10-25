> 本轮整合上下文（2026-10-25）：本文原有叙述与验收数字保留为各节标明日期的历史快照。游戏主线已在候选提交 `96371eb36a787585cf5e70141c03555419821058` 保存，其父链包含九个 TowerRun/Portal/UI 提交；默认新冒险 Profile 为 `MainV1`，十二种保留原生怪物，Boss 仍待内容。旧 `ecology-v1`、UI v6 未提交、旧工作树/HEAD 等现在式叙述均不代表本轮状态。新整合 Build/Player 与人工验收只认本轮独立收据；Stage1、B1、Boss 和旧 R4 人工结论不升级。当前整合总账见 `../Integration/MainConsolidation20261025/README.md`。

# 玩家战斗、Damageable、怪物 AI 与奖励闭环

## 0. 系统概述与模板覆盖

玩家直接感受到的是移动、攻击前摇、敌人发现/追击/攻击、Monster 命中后短暂红闪、击败目标和拾取奖励。其他已有伤害来源仍可产生击退。工程上，这是一条跨输入、属性、伤害接收器、AI、对象池、Generation 和背包的闭环。

| 模板项 | 本文位置 |
|---|---|
| A/B 概述、需求约束 | §0～§3 |
| C/D 架构与代码定位 | §1、§1.1 |
| E/F 执行流与数据生命周期 | §2～§10 |
| G/H 算法、数据结构、选型 | §2、§5、§7、§9 |
| I/J 关键代码与历史问题 | §2、§4～§9、§11 |
| K/L/M 测试、性能、改进 | §11、§12 |
| N/O 面试讲述与练习 | §12～§13 |

## 1. 模块边界

```text
GameInput
  → PlayerAction（移动/方向/攻击发起）
  → PlayerStatsRuntime（体力、生命等数据）
  → BaseDamageable（世界对象受伤）
  → 对象死亡/回池/奖励生成
  → RewardPickup
  → PlayerInventory

MonsterPerception
  → MonsterAIController（FSM）
  → MonsterMovementController
  → MonsterAttackController
  → PlayerDamageController
  → PlayerHurtController
  → PlayerStatsRuntime
```

### 1.1 真实代码与 Prefab

| 职责 | 位置 |
|---|---|
| 玩家输入/攻击 | `Assets/Scripts/Player/PlayerAction.cs` |
| 玩家属性 | `Assets/Scripts/PlayerStatsRuntime.cs` |
| 玩家扣血/受击反馈 | `Assets/Scripts/Player/PlayerDamageController.cs`、`PlayerHurtController.cs` |
| 世界对象生命 | `Assets/Scripts/Damageable/BaseDamageable.cs` |
| 怪物 AI/感知/移动/攻击 | `Assets/Scripts/Damageable/Monster/` |
| 对象池 | `Assets/Scripts/ObjectPoolManager.cs` |
| 地面奖励/背包 | `Assets/Scripts/RewardPickup.cs`、`PlayerInventory.cs` |
| 玩家 Prefab | `Assets/Prefabs/Players/Warrior_Blue.prefab` |
| 代表性怪物 Prefab | `Assets/Prefabs/Monsters/Torch/Torch_Blue.prefab` |

## 2. 玩家输入与动作

`PlayerAction` 订阅 `GameInput` 事件，主要负责：

- FixedUpdate 中移动；
- 维护四方向朝向；
- Left Ctrl 切换速度；
- 收到战斗输入时校验 Gate、死亡状态和体力；
- 选择攻击方向与目标；
- 播放攻击表现；
- 延迟到命中时机施加伤害。

### 为什么移动放在 FixedUpdate

如果通过 Rigidbody2D 参与物理，固定时间步能使物理移动更稳定。但输入采样和动画更新仍可能在 Update 侧，因此要理解数据跨帧传递。

### 攻击不是“按键立即扣血”

攻击流程：

1. `GameInput` 先受 `TowerRunGateHub` 控制；事件到达后，`HandleBattlePressed()` 只直接检查 `PlayerAction.isControlLocked` 和体力是否足够。
2. 消耗 5 体力。
3. 根据当前方向准备攻击。
4. 在活动 `BaseDamageable` 中找范围内最近目标。
5. 触发动画。
6. 等待约 0.24 秒命中时机。
7. 再对准备的目标调用 `TakeDamage`。

这叫 `Wind-up（攻击前摇）`：输入时机与伤害时机分开，使动画和判定更一致。

### 玩家攻击的当前边界

这里必须和怪物攻击区分：

- 玩家死亡后通常由 `TowerRunCoordinator` 获取 Gameplay Gate，从输入源阻止后续攻击；`HandleBattlePressed()` 本身没有显式读取 `PlayerDamageController.IsDefeated`。
- 当前没有独立的普通攻击 cooldown/reentrancy guard；再次按攻击会停止上一条待命中协程、再次消耗体力并启动新的前摇。
- 命中时只复核目标仍存在、仍 active 且尚未被击败，不重新检查玩家与目标的距离或方向。因此“前摇期间可通过离开范围躲开”只适用于当前怪物攻击链，不适用于玩家攻击世界对象。

### 当前性能边界

`【已验证事实】` 当前目标选择会在每次攻击时查找活动 `BaseDamageable`；不是每帧查找，但对象数增大后仍可能产生搜索成本和分配。

`【建议方案】` 只有 Profiler 证明它成为热点后，再考虑：

- `Physics2D.OverlapCircleNonAlloc`；
- 空间哈希/四叉树；
- 当前房间目标注册表；
- 由 `MapObjectRegistry` 提供候选集合。

## 3. 玩家运行时属性

`PlayerStatsRuntime` 拥有：

- Health
- Mana
- Stamina
- 对应 Max 值
- 数值变化事件

其职责是保证范围和通知，而不是直接操作 UI。HUD 订阅事件显示。

### 体力与成长

- 攻击消耗体力。
- 体力按规则恢复。
- `PlayerProgressionService` 以金币购买最大值成长。
- 成长操作捕获背包/属性旧状态，提交失败则回滚。

`【推断分析】` 当前 `PlayerAction` 在消耗体力攻击后调用全量进程内保存镜像。它可能造成不必要的深拷贝，但尚无性能测量，不能说已造成卡顿。

## 4. `BaseDamageable`

`BaseDamageable（基础可受伤对象）` 为怪物、动物、植物、可破坏物提供统一的世界对象生命模型：

- 最大/当前生命；
- `TakeDamage`；
- 首次死亡；
- `OnDefeated`；
- 禁用 Collider；
- 按规则生成奖励；
- 回对象池或完成销毁策略；
- OnEnable/重置时恢复状态。

### 为什么是抽象基类

共享的是生命和失败生命周期；具体对象仍可扩展：

- 怪物 AI 停止；
- 植物动画；
- 动物行为；
- 可破坏物表现；
- 特定奖励映射。

### 玩家为什么不是 `BaseDamageable`

当前玩家通过 `PlayerDamageController` 接收伤害，因为玩家死亡需要：

- 更新 `PlayerStatsRuntime`；
- 按来源触发视觉反馈或保留原有击退/控制锁；
- 通知 TowerRun 进入 DeathChoice；
- 与世界对象回池策略不同。

这是合理的差异，但接口分裂也带来调用者需要知道目标类型的问题。

### 可演进设计

可以引入：

```csharp
public interface IDamageReceiver
{
    bool CanReceiveDamage { get; }
    bool TryReceiveDamage(DamageInfo damage);
}
```

世界对象和玩家各自实现接口，内部保留不同死亡策略。

边界：

- 不要为了“统一”强迫玩家继承世界对象基类。
- `DamageInfo` 应携带来源、数值、方向/击退、Generation 等上下文。
- 统一接口后仍需防重复命中和死亡幂等。

## 5. 怪物 FSM

`Finite State Machine（有限状态机）`：

| 状态 | 主要行为 | 常见转移 |
|---|---|---|
| Patrol | 巡逻/等待 | 发现玩家 → Search/Chase |
| Search | 确认目标，避免瞬时噪声 | 确认 → Chase；丢失 → Patrol/Return |
| Chase | 接近玩家 | 进入攻击范围 → Attack；超出 leash → Return |
| Attack | 前摇、方向锁定、命中复核、冷却 | 目标离开 → Chase；失去目标 → Search/Return |
| Return | 回出生/锚点 | 回到附近 → Patrol |

### 感知

`MonsterPerceptionController` 每帧使用距离与 `Physics2D.RaycastNonAlloc` 视线检测。

关键点：

- 距离只是第一层过滤。
- Raycast 检查障碍遮挡。
- confirm duration 防止一帧看到就立刻追击。
- leash 防止怪物无限追出所属区域。

### 攻击

`MonsterAttackController`：

- 选择目标 Collider 的最近点判断距离；
- 前摇约 0.25 秒；
- 冷却约 0.35 秒；
- 攻击方向点积阈值约 0.7；
- 命中瞬间再次确认范围和方向；
- 最终调用 `PlayerDamageController.TakeDamage`。

为什么命中时复核：

> 玩家可能在前摇期间离开。若只在攻击开始时判断，远离后仍会被“空气击中”。

## 6. 受伤、红闪与保留的击退

`PlayerDamageController.TakeDamage` 负责有效生命扣减，随后只对有效伤害发布 `OnDamaged`；首次归零另发 `OnDefeated`。`PlayerHurtController` 根据 `DamageInfo.SourceTransform` 分支：

- 来源归属 `MonsterAttackController`：对 Player 的 `Visual.SpriteRenderer` 红闪约 0.15 秒，不击退、不锁移动与攻击输入；重复有效伤害重启闪色，结束/死亡/禁用/换层恢复原色。
- 其他已有、带来源坐标的伤害：沿原路径计算击退方向，使用 `Rigidbody2D` 形状投射裁剪位移，短暂锁控制，结束后恢复。
- 无效伤害不发布 `OnDamaged`，因此不触发无意义红闪。

将数据扣减与空间反馈分开，便于测试：

- 生命逻辑可独立验证；
- Monster 红闪与其他来源的击退可分别用真实命中和物理测试；
- TowerRun 只需监听死亡事件，不关心反馈分支。

## 7. 对象池

`ObjectPoolManager` 使用：

```text
Dictionary<GameObject, Queue<GameObject>>
```

以 Prefab 为 Key，空闲实例为 Queue：

- 取用：有空闲就激活，无则 Instantiate。
- 归还：停用并入队。
- 管理器缺失时惰性创建。

### 对象池不只是性能

它也改变生命周期语义：

- `OnEnable` 可能多次发生；
- 事件必须解除后再重新绑定；
- 生命值、动画、Collider、AI 状态必须 Reset；
- “死亡”不等于对象永远销毁。

常见 Bug：

- 重复订阅 `OnDefeated`；
- 旧协程继续运行；
- Collider 未恢复；
- 旧 Generation 的奖励延迟出现；
- transform/target 没清理。

## 8. 奖励生成与代际生命周期

`BaseDamageable` 根据稳定 `damageableId` 找到奖励配置，使用对象池生成 `RewardPickup`。

在随机地图模式中，奖励附加 `GenerationScopedRewardLifetime`：

- 记录它属于哪个 Generation；
- 新代切换时旧代奖励应退休；
- 旧对象迟到的回调不能污染当前地图。

这是典型的异步/生命周期一致性问题，不只是“清理对象”。

## 9. 拾取协议

`RewardPickup.OnRewardPickupRequested` 是一个返回 `bool` 的静态验证链。

含义：

1. Pickup 请求消费者接受。
2. 所有必要处理者确认后，Pickup 才进入回池。
3. 成功后再发送 `OnRewardPickedUp` 通知。

如果 `PlayerInventory` 添加失败：

- 背包保持或恢复原状态；
- Pickup 留在地面；
- 不发送成功通知。

这比“碰撞后先 Destroy，再尝试加背包”可靠。

### 静态事件的风险

- Domain Reload 配置改变时可能保留订阅；
- 测试之间容易泄漏；
- 多玩家/多背包语义不清；
- 订阅顺序可能影响组合结果。

`【建议方案】` 中长期可由场景服务或明确的 `IRewardCollector` 完成请求，减少全局静态状态。

## 10. 真实闭环

```text
玩家攻击
→ Damageable 生命归零
→ OnDefeated
→ 怪物 AI/楼层计数取消或更新
→ 生成奖励
→ 玩家触碰奖励
→ 背包验证与事务提交
→ UI 刷新
→ 若最后一只本代怪物死亡，出口解锁
```

同一个死亡事件同时影响 AI、奖励和楼层目标，所以幂等非常关键：一次死亡只能计数一次。

## 11. 边界与故障

| 场景 | 应关注 |
|---|---|
| 前摇期间目标死亡/回池 | Prepared target 是否仍有效 |
| 一次攻击命中多个 Collider | 是否重复伤害同一实体 |
| 对象 OnEnable | 是否恢复生命、Collider、订阅和 AI |
| 死亡后重复 TakeDamage | 是否重复掉落/重复楼层计数 |
| 旧代奖励未拾取 | 新代开始时是否退休 |
| 非 Monster 来源让玩家被击退进墙 | 碰撞裁剪是否正确 |
| Gate 激活时按攻击 | 是否消耗体力或启动前摇 |
| 最后一只怪物死亡与切层并发 | 出口解锁和 transition 是否幂等 |

## 12. 面试回答示例

### 1 分钟系统介绍

> 玩家由 Input System 驱动；TowerRun Gate 在 `GameInput` 层阻止玩法输入，`PlayerAction` 收到攻击事件后检查本地控制锁和体力，选择最近的 `BaseDamageable`，通过前摇延迟到命中帧扣血。玩家攻击命中时只复核目标是否仍有效，不复核距离；怪物攻击则会在命中时重新检查距离和方向。世界对象共享 `BaseDamageable` 的生命、死亡、掉落和池化，死亡奖励带 Generation 生命周期，背包确认提交成功后地面物品才回池。

### 3 分钟实现思路

依次讲：

1. 输入、FixedUpdate 移动、朝向与体力。
2. 玩家攻击世界对象的延迟命中。
3. Monster 感知/FSM/攻击玩家的反向链路。
4. `BaseDamageable` 与玩家接收器为何分裂。
5. 对象池重置与 Generation-scoped 掉落。
6. 奖励请求、背包回滚和 UI 通知。
7. 当前 O(n) 攻击查询、统一伤害接口等改进边界。

### 你如何实现怪物 AI？

> 我用有限状态机把 Patrol、Search、Chase、Attack、Return 分开。感知组件先用距离过滤，再用非分配 Raycast 检查视线，并通过确认时长减少瞬时抖动。攻击开始后锁方向，但到实际命中帧会再次检查距离和方向，所以玩家能在前摇中躲开。怪物生命由 `BaseDamageable` 管理，死亡事件同时驱动 AI 结束、对象池回收、奖励和 TowerRun 目标计数。

### 对象池遇到过什么设计难点？

> 难点不是 Queue，而是对象被重复启用后的状态重置与代际归属。生命、Collider、动画、事件订阅、目标引用和协程都必须重置；随机地图还要防止旧代对象的延迟奖励进入新代，所以给奖励加了 Generation 生命周期。

## 13. 掌握检查

1. 为什么玩家攻击在按键时选目标、命中时只检查目标有效性？若产品要求玩家也能靠走位躲开前摇，应该补哪项复核？
2. 如果把玩家也改成 `IDamageReceiver`，哪些逻辑仍不应该放进接口？
3. 对象池对象出现“一次死亡掉两份奖励”，优先检查哪三类原因？
4. 设计一个测试证明旧 Generation 的延迟掉落不会进入新地图。

## 14. 2026-10-14 增量：12 个 Monster 的行为与 Visual 分离

【已验证事实】当前 `Assets/Prefabs/Monsters/{Torch,TNT,Barrel}/` 各有 Blue/Purple/Red/Yellow 四色，共 12 个。根物体保留 `BaseMonster`、AI、移动、攻击、生命、Rigidbody2D 和 Collider；唯一 `SpriteRenderer` 在子物体 `Visual`。除 Torch_Blue 外的 11 个变体各有六个对应类型/颜色的 Animation Clip 与 `AnimatorOverrideController`；Torch_Blue 原 Controller 状态/参数作为共享结构。TNT/Barrel 的 `damageableId` 分别是 `TNT`、`Barrel`，对应各自的奖励表；同类型颜色只改变视觉。Torch 原有继承链没有重建。

`MonsterMovementController.OnMovementChanged` 写 `IsMoving`；`MonsterAttackController.OnAttackStarted` 写 `Attack` 与 `AttackDirection`，攻击伤害仍由逻辑组件处理。`BaseDamageable.OnDamaged/OnDefeated` 驱动红闪/淡出；素材缺少独立 Hurt/Death 帧，不能把降级反馈说成 Hurt/Death 完整状态机。`MonsterPopulationTargetBridge` 负责与地图的生成/注销接口，不参与索敌或伤害。

【已验证事实】`.codex/TestResults/monster-visual-playmode-events-20261013.xml` 用 Unity PlayMode 验证 12 个 Prefab 的 Sprite、动画状态、事件与复用，1/1 PASS、exit 0。`.codex/TestResults/ecology-reward-regression-suite-20261014.xml` 检查旧 Torch 和 Destructible 的掉落/拾取/跨代回收，5/5 PASS、exit 0。动画的屏幕观感、攻击节奏、全类型真实碰撞接触仍【待人工确认】。TNT/Barrel 暂时共用近战，不存在本轮实现的爆炸机制。

【已验证事实】强化版 `.codex/TestResults/ecology-all-monster-visual-collider-20261014.xml` 在同一 12 Prefab PlayMode 循环中验证 Rigidbody、Collider 正尺寸及 TNT/Barrel 的类型化框尺寸，1/1 PASS、runner exit 0；这仍不是每种怪物与地图障碍的真实接触测试。

面试追问“为什么把行为与动画分开”：寻路、目标、命中和死亡账本必须以逻辑状态为准；Animator 可以替换 Clip 或缺少某些帧而不改变战斗结果。这样相同近战逻辑可以服务三种类型，Visual 随类型和颜色选择正确 Sprite，同时避免 Animator 曲线把静态首帧切回 Torch。

### 2026-10-14 人工验收修复的证据边界

【已验证事实】Barrel 四色的四向 Attack Clip 原先引用第 3 行 6 帧，现用 Unity Editor API 精确引用本色 `_5_0`～`_5_3`、非循环，Idle/Walking 和共享 Animator Controller 不变。12 个 Monster 的 `MonsterAttackController.attackDamage` 有效值原为 10，源头是 `Torch_Blue.prefab`；只把该 Prefab 改为 5，变体继承同一值，不改攻击公式、前摇/冷却或玩家输出。`PlayerHurtController` 原来对所有有效受击做击退，现在对 Monster 攻击仅红闪 0.15 秒，其他已有来源保留击退。

【已验证事实】`.codex/TestResults/monster-acceptance-expanded-20261014.xml` 5/5 PlayMode PASS：精确导入 Sprite 子资源及 Override、Barrel 攻击状态退出、12 个 Prefab 有效伤害、玩家红闪/无击退/连续命中/无效伤害/死亡清理与非 Monster 旧反馈。先前的红灯见 `monster-acceptance-red-r2-20261014.xml` 和 `monster-reaction-red-20261014.xml`。**真实 Game 视图的动画观感、战斗手感仍待人工复验**，自动化不能替代。


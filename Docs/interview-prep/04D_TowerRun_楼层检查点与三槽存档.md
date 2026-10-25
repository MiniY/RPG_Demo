> 本轮整合上下文（2026-10-25）：本文原有叙述与验收数字保留为各节标明日期的历史快照。游戏主线已在候选提交 `96371eb36a787585cf5e70141c03555419821058` 保存，其父链包含九个 TowerRun/Portal/UI 提交；默认新冒险 Profile 为 `MainV1`，十二种保留原生怪物，Boss 仍待内容。旧 `ecology-v1`、UI v6 未提交、旧工作树/HEAD 等现在式叙述均不代表本轮状态。新整合 Build/Player 与人工验收只认本轮独立收据；Stage1、B1、Boss 和旧 R4 人工结论不升级。当前整合总账见 `../Integration/MainConsolidation20261025/README.md`。

# TowerRun、楼层检查点与三槽存档

## 0. 系统概述与模板覆盖

玩家感受到的是：启动时选择新冒险或槽位；死亡后选择恢复或重开；击败本层目标后传送门解锁；进入下一层时状态按规则延续；损坏或不兼容的槽不会被静默加载。

| 模板项 | 本文位置 |
|---|---|
| A/B 概述、需求和约束 | §0～§2 |
| C/D 架构与代码定位 | §1、§1.1、§2 |
| E/F 执行流程和数据生命周期 | §3～§10 |
| G/H 数据结构、算法和选型 | §7～§12 |
| I/J 关键代码和历史问题 | §3～§15 |
| K/L/M 测试、局限、改进 | §13、§16 |
| N/O 面试讲述与练习 | §17～§18 |

## 1. 设计目标

TowerRun 把原有 RPG 系统组织成一条可重复 Run：

```text
Startup Choice
→ New Run / Restore Slot
→ Generate Floor
→ Play
→ Defeat Current-generation Targets
→ Unlock Exit
→ Interact
→ Capture Next-floor Entrance Checkpoint
→ Generate Next Floor
```

它解决的不是单一 UI，而是跨地图、玩家状态、背包、商店、死亡、出口和磁盘文件的一致性。

### 1.1 真实代码与场景对象

| 职责 | 位置 |
|---|---|
| Run 状态机 | `Assets/Scripts/TowerRunCoordinator.cs` |
| 本层目标/出口 | `Assets/Scripts/TowerFloorRuntimeController.cs` |
| 状态 Capture/Apply | `Assets/Scripts/TowerRunRuntimeStateBridge.cs` |
| Ready 前恢复 | `Assets/Scripts/TowerRunRestoreFinalizer.cs` |
| 文件与兼容性 | `Assets/Scripts/TowerRun/TowerRunSlotStore.cs` |
| Gate | `Assets/Scripts/TowerRun/TowerRunGate.cs`、`Assets/Scripts/TowerRunGateHub.cs` |
| UI | `Assets/Scripts/UI/TowerRunUiController.cs` |
| 运行时组合 | `Assets/Prefabs/MapGeneration/RandomMapRuntime.prefab`、`Assets/Scenes/SampleScene.unity` |

## 2. 流程状态机

`TowerRunCoordinator` 具有：

- `StartupChoice`
- `Initializing`
- `Playing`
- `DeathChoice`
- `Restoring`
- `Transitioning`
- `Failure`

### 为什么不用几个布尔值

布尔组合会产生非法状态：

- `isLoading=true` 且 `isPlaying=true`
- `isDead=true` 但出口仍可交互
- `isTransitioning=true` 又开始 Restore

枚举状态让合法转移更易验证，也便于 UI 根据单一流程状态渲染。

## 3. 新冒险

`BeginNewRun()`：

1. 捕获/使用 new-run baseline。
2. 建立新 Run 身份与第一层记录。
3. 暂存待应用记录。
4. 获取 Gameplay/World Pause Gate。
5. 请求生成指定 Seed/配置的地图。
6. 在 finalizer 阶段应用记录。
7. 验证地图 baseline、Generation 和请求身份。
8. 只有 GameplayReady 后公开 current record 和 entrance candidate。

### 为什么要 baseline

如果“新冒险”直接使用当时的玩家状态，玩家可能从上一 Run 带入金币、生命或商店库存。当前实现会在 `BindScene()` 首次具备 stats/inventory 时捕获一次 `newRunBaseline`，后续 New Run 都深拷贝这份记录，而不是重新读取上一 Run 的当前状态。它不是硬编码的出厂配置；正确性依赖首次绑定发生时运行时状态已经是预期起点，这一点仍需要场景启动/Session 初始化约束保证。

## 4. 恢复槽位

`BeginRestoreSlot(slot)`：

1. `TowerRunSlotStore.ReadSlot`。
2. 根据 Empty/Valid/Corrupt/Incompatible/Unavailable 分类。
3. 只对 Valid 记录进入 Restoring。
4. 暂存 record，不立即宣称恢复完成。
5. 按记录生成地图。
6. finalizer 应用玩家、背包和商店状态。
7. 全部准备完成后进入 Playing。

### Staging（暂存）的意义

读取成功只是“数据可用”，不是“世界已恢复”。如果在地图失败前就更新 UI/current record，会让其他系统观察到不存在的完成状态。

## 5. 楼层目标与出口

`TowerFloorRuntimeController` 在 Ready 前：

1. 从本代 `MapObjectRegistry` 获取 Role=Monster 的条目。
2. 要求目标数非零。
3. 订阅各 `BaseDamageable.OnDefeated`。
4. 隐藏传送门与小地图出口。

GameplayReady 后锁定本层目标集合。

当所有目标失败：

- 显示本代黑洞/传送门；
- 开启小地图出口标记；
- 允许范围内交互。

### 为什么只看本代 Registry

场景或对象池中可能仍有旧对象、预览对象或非本层敌对对象。Tag/全场景查找会误计数。本代 Registry 明确所有权。

### 交互而非碰撞自动切层

玩家需：

- 存活；
- 距离出口不超过约 1.5；
- 按 Interact；
- 本代出口已解锁；
- transition 尚未接受。

`TryAcceptExitTransition()` 提供幂等：同一出口只能接受一次切层事务。

## 6. 死亡流程

Coordinator 监听 `PlayerDamageController.OnDefeated`：

- 关闭/阻止包裹和商店；
- 获取输入/世界 Gate；
- 进入 `DeathChoice`；
- 提供恢复槽位或新冒险。

为什么不直接 `SceneManager.LoadScene`：

- 当前地图由运行时 Generation 管理；
- Session 和槽位状态跨场景；
- 需要保留明确恢复语义；
- 场景重载不能自动保证对象池、注册表和 Gate 一致。

## 7. 检查点语义

### 保存什么

- Run/楼层身份；
- Seed、生成器版本和配置身份；
- 玩家 stats；
- 背包；
- 商店库存；
- 必要的 TowerRun 元数据；
- 地图 baseline/兼容性信息。

### 不保存什么

当前目标不是保存：

- 每个攻击动画的当前帧；
- 所有协程；
- 临时投射物；
- 怪物精确战斗中生命/位置；
- 对象池内部队列；
- 任意物理速度。

### 为什么是楼层入口

入口检查点是一个 `Consistency Boundary（一致性边界）`：

- 地图可由 Seed 重建；
- 玩家/背包/商店有纯数据记录；
- 怪物恢复为楼层初始语义；
- 不需要序列化瞬时 Unity 状态。

代价是玩家死亡后回到入口状态，而非死亡前一秒。

## 8. `TowerRunRuntimeStateBridge`

Capture：

1. 创建候选 Record。
2. 捕获 stats。
3. 捕获排序后的 inventory。
4. 捕获当前及缓存 shop。
5. 任一非法则整体失败。

Apply：

1. 捕获当前 stats/inventory/shops 作为 rollback。
2. 应用记录。
3. 某部分失败则恢复所有部分。
4. `TryApply(..., notify)` 支持成功后统一通知；正式 TowerRun 恢复传入 `notify=false`，由 Coordinator 在 `GameplayReady` 后再统一通知 stats、inventory 和 shops。

这防止“生命恢复成功但背包失败”的半恢复状态。

## 9. 三槽文件系统

`TowerRunSlotStore` 使用：

```text
Application.persistentDataPath/tower-run-slots/
```

维护三个槽位。

### Read 分类

- `Empty`
- `Valid`
- `Corrupt`
- `Incompatible`
- `Unavailable`

区分 Corrupt 与 Incompatible 很重要：

- Corrupt：数据损坏/结构不完整，可能来自中断写入。
- Incompatible：文件结构可读，但版本/配置不支持。

## 10. 兼容性校验

读取会检查：

- Schema version；
- Generator version；
- Configuration identity；
- 必需 JSON 属性；
- stats 是否有限且在范围内；
- Reward/Catalog/Product ID 是否唯一；
- 楼层/Seed 等结构约束。

### 为什么 JSON 反序列化成功还不够

以下 JSON 都可能“能解析但不可用”：

- `health = NaN/Infinity` 的非标准来源；
- 当前生命大于最大生命；
- 两个相同 `rewardId`；
- 老生成器 Seed 在新算法下产生不同地图；
- 缺少恢复必须字段；
- 数量负数或溢出。

## 11. 写入协议

简化流程：

```text
Validate candidate
→ Read existing slot
→ 若覆盖已有槽，验证 confirmation token
→ 写 .tmp
→ 重新读取并验证 .tmp
→ Move 或 atomic replace 正式文件，同时保留 .backup
→ 重新读取正式文件
→ 比较 SHA-256 token
→ 不一致则尝试恢复旧版本
→ 返回 committed / failed / uncertain
```

### 确认令牌

覆盖令牌把“用户看到的旧槽内容”与“即将覆盖的实际旧内容”绑定。若期间文件变化，旧令牌失效，避免误覆盖。

### 临时文件

直接写正式文件时，崩溃可能留下截断 JSON。先写 `.tmp` 并验证，减少损坏正式文件的窗口。

### 原子替换与备份

覆盖已有槽时调用 `File.Replace`，使正式路径从旧版本切换到新版本并保留 `.backup`；空槽首次写入使用 `File.Move`。当前没有在 `File.Replace` 不受平台支持时改用非原子覆盖的 fallback：替换异常会进入失败/重新观察/尝试恢复分支，这比静默降级更保守。

### 写后复读

系统不只相信“文件 API 没抛异常”，还重新读取并计算 SHA-256。这样能发现：

- 写入内容被改变；
- 错误文件被替换；
- 磁盘/外部进程干预；
- 序列化结果与候选不一致。

## 12. 不确定结果

最难的文件写入状态是：

> 操作报告失败，但不知道新文件是否已经成为正式文件。

项目会重新观察槽位：

- 若读到预期 token，可判为已提交；
- 若读到旧 token，可判为未提交；
- 两者都不是，则返回 uncertain，不能盲目重试覆盖。

这是分布式系统中“超时不代表失败”的本地文件版。

## 13. 跨进程测试

`【已验证事实】` 已有独立 Unity 进程 writer 和 reader 各 1/1 通过的证据。

价值：

- 证明不是只读同一静态内存；
- 验证文件在进程退出后存在；
- reader 重新初始化后能解析和校验。

限制：

- 单次成功不等于覆盖所有文件系统故障；
- 未证明断电级 durability；
- 未覆盖所有平台；
- 当前证据对应特定提交和环境。

## 14. Gate 与嵌套 UI

TowerRun 的 Startup、Death、Operation、Settings 等都可能锁输入/世界。`TowerRunGateHub` 通过 owner lease 计数：

- 每个所有者只能释放自己的租约；
- 只有所有租约释放才真正解锁；
- 避免“关闭设置”错误恢复死亡状态下的玩家控制。

## 15. 迷雾时序案例

新冒险曾因过早的地图完成回调在旧玩家位置揭雾。修复重点是：

- 不把 `MapGenerated` 等同于完整 Ready；
- 确保探索更新看到最终 spawn；
- 用定向 PlayMode 测试捕获实际位置和揭示格数。

这说明 TowerRun 的正确性依赖地图生命周期的准确完成边界。

## 16. 当前局限

- 三槽只服务当前 TowerRun 产品语义，不是通用任意场景存档框架。
- 兼容性策略偏保守；缺少完善的多版本 migration pipeline。
- 文件故障注入范围仍可扩大。
- 视觉/音频/手感仍需真实人工验收。
- 设置 UI 已从 v4 收口到当前工作树的 v6，但仍未提交，也未获得用户最终视觉确认；自动化与截图不能代替这项授权。

## 17. 面试回答示例

### 1 分钟系统介绍

> TowerRun 由 Coordinator 管理 StartupChoice、Initializing、Playing、DeathChoice、Restoring、Transitioning 和 Failure。存档保存楼层入口：地图由 Seed、生成器版本和配置重建，玩家属性、背包和商店通过状态桥原子应用。三槽文件在写入前验证候选和覆盖 token，先写并复读 temp，再替换正式文件、保留 backup，最后复读并比较 SHA-256。恢复只有在地图和 finalizer 全部成功、进入 GameplayReady 后才对外发布。

### 3 分钟实现思路

依次讲：

1. 状态机如何禁止 New/Restore/Transition 重入。
2. Floor Controller 如何只统计本代 Registry Monster。
3. 出口解锁、范围交互和幂等接受。
4. entrance checkpoint 保存什么、不保存什么。
5. staged record 与 RuntimeStateBridge 回滚。
6. Slot 的 Empty/Valid/Corrupt/Incompatible/Unavailable。
7. temp/replace/backup/hash/uncertain 与跨进程测试。

### 你如何避免存档损坏？

> 我把“序列化成功”和“持久化成功”分开。候选记录先做业务校验；覆盖已有槽需要绑定旧内容的确认令牌；新内容先写临时文件并复读验证，再原子替换正式文件、保留备份，最后重新读取正式文件并比较 SHA-256。若替换结果不确定，会观察实际文件决定已提交、未提交或 uncertain，而不是盲目重试。

### 为什么不保存所有场景对象？

> TowerRun 定义的是楼层入口检查点。地图由 Seed、生成器版本和配置重建，玩家/背包/商店用纯数据恢复。保存每个协程、动画、物理和对象池状态成本高且脆弱；入口边界让恢复可重复，代价是不能回到战斗中任意一帧。

## 18. 掌握检查

1. 写正式文件成功但返回前进程崩溃，下一次如何判断上次是否提交？
2. 为什么覆盖确认令牌不能只用槽位编号？
3. 如果生成器算法升级但仍接受旧 Seed，会有什么隐患？
4. 设计一个故障注入测试：临时文件合法，但正式替换后内容被外部进程改变。

## 19. 2026-10-14 增量：Population Profile 与旧 TRB2 恢复

【2026-10-14 历史已验证事实；已被 MainV1 更新】当时新游戏的 `TowerRunCoordinator.DefaultProfileId` 为 `ecology-v1`，含多只 GenerationScoped Monster；`TowerFloorRuntimeController` 仍只统计本代 Registry 中的 Monster，所有登记目标被击败后才开放出口。`.codex/TestResults/ecology-tower-flow-suite-r2-20261014.xml` 为 9/9 Unity PlayMode PASS、runner exit 0，覆盖多目标死亡、开门、换层和恢复链路。

【已验证事实】旧槽位记录的 `profileId=default` 仍选择原单只 SceneBound Torch + 原放置顺序；恢复时严格比较 TRB2 静态基线，没有关闭校验。正式旧槽位 1 的只读副本（Seed `-1071926194`，`TRB2-DA0DC44B`）在隔离根目录实际恢复，`.codex/TestResults/ecology-legacy-slot-restore-20261014.xml` 为 1/1 PASS，源文件 SHA-256 前后相同。该次 Unity runner 写出 PASS XML 后关闭停滞，已结束经命令参数核验的受控 PID，实际 exit -1；因此测试断言 PASS 与进程正常退出必须分开陈述。

旧场景 Torch 和 Mushroom Plant 供 `default` 恢复；`ecology-v1` 停用旧 Torch、隐藏旧 Mushroom Plant，并动态生成三类 Mushroom。这是版本化放置配方的兼容层。需注意一项实际修复：前次场景迁移曾删除 `SampleScene.unity` 的 `Monsters/Torch_Blue` 实例并把 `MonsterPlacementService.monsterTarget` 置空，旧配方因此报 `MonsterTargetUnavailable`。2026-10-14 按 Git 历史恢复原父节点、Prefab 实例、fileID 和引用，仅增加初始停用 Override；它在新冒险生态提交前已 inactive，`default` 放置时再激活。恢复后完整 PlayMode `.codex/TestResults/monster-acceptance-final-full-r5-20261014.xml` 为 36/36 PASS、0 Skip，含只读隔离 TRB2 槽位恢复；受控进程已退出，但数值退出码未捕获。若以后修改生态配置，新槽位也可能因严格基线变化而无法复现，必须先设计新 Profile/配置身份或迁移策略，不能忽略签名错误。


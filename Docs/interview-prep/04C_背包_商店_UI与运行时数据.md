> 本轮整合上下文（2026-10-25）：本文原有叙述与验收数字保留为各节标明日期的历史快照。游戏主线已在候选提交 `96371eb36a787585cf5e70141c03555419821058` 保存，其父链包含九个 TowerRun/Portal/UI 提交；默认新冒险 Profile 为 `MainV1`，十二种保留原生怪物，Boss 仍待内容。旧 `ecology-v1`、UI v6 未提交、旧工作树/HEAD 等现在式叙述均不代表本轮状态。新整合 Build/Player 与人工验收只认本轮独立收据；Stage1、B1、Boss 和旧 R4 人工结论不升级。当前整合总账见 `../Integration/MainConsolidation20261025/README.md`。

# 背包、商店、UI 与运行时数据

## 0. 系统概述与模板覆盖

玩家感受到的是：掉落可拾取、包裹数量正确、金币和商店库存同步、交易失败不会吞钱，UI 打开时不会继续误操作角色。系统内部必须区分静态物品定义、运行时权威状态、进程内镜像、磁盘检查点和 UI 投影。

| 模板项 | 本文位置 |
|---|---|
| A/B 概述、需求和约束 | §0～§2 |
| C/D 架构与代码定位 | §1、§1.1 |
| E/F 流程和生命周期 | §3～§10 |
| G/H 算法、结构和选型 | §2～§8 |
| I/J 关键代码/历史问题 | §3～§10 |
| K/L/M 测试、性能、改进 | §5、§12 |
| N/O 面试讲述与练习 | §13～§14 |

## 1. 数据层次

```text
RewardSO / Shop Catalog（静态配置）
        ↓
PlayerInventory / ShopTradeController（运行时权威状态）
        ↓
GameSaveData + GameSaveService（进程内镜像与事务提交）
        ↓
TowerRunRecord + TowerRunSlotStore（楼层入口磁盘检查点）
        ↓
Package/Shop/HUD UI（表现）
```

### 1.1 真实代码与场景资源

| 职责 | 位置 |
|---|---|
| 奖励配置 | `Assets/Scripts/ScriptableObject/RewardSO.cs`、`Assets/ScriptObjects/RewardSOs/` |
| 拾取协议 | `Assets/Scripts/RewardPickup.cs` |
| 背包/检查点转换 | `Assets/Scripts/PlayerInventory.cs` |
| 包裹视图 | `Assets/Scripts/UI/PackageGridUI.cs`、`PackageSlotUI.cs` |
| 商店业务 | `Assets/Scripts/Shop/ShopTradeController.cs` |
| 商店视图 | `Assets/Scripts/UI/Shop/ShopPanelController.cs`、`ShopGridUI.cs` |
| 会话/内存镜像 | `Assets/Scripts/GameSession.cs`、`GameSaveService.cs` |
| TowerRun 状态桥 | `Assets/Scripts/TowerRunRuntimeStateBridge.cs` |
| UI/商店实例 | `Assets/Scenes/SampleScene.unity`、`Assets/Prefabs/NPC/Merchant_Pawn.prefab` |

最容易说错的一点：

> `GameSaveService.TrySave()` 当前不会读写旧 `save.json`，而是规范化并深拷贝到静态内存。磁盘持久化由 TowerRun 三槽存档负责。

## 2. `RewardSO` 与稳定身份

奖励配置包含：

- `rewardId`
- 显示名
- 图标
- 描述
- 拾取倍率
- 地面 Prefab

`rewardId` 是保存和跨场景匹配的稳定身份。对象引用、资源名和显示名都不适合作为长期主键：

- 对象引用不能直接跨进程；
- 重命名资源会破坏名字匹配；
- 显示名可能本地化；
- 同名物品并非一定相同。

`OnValidate()` 会在编辑器校验资产时调用 `EnsureRewardId()`，这是稳定 ID 的正常创作路径；但当前 `PlayerInventory.WriteToSaveData()` 在运行时也会防御性调用它，所以不能说 GUID “只在编辑器生成”。正式 TowerRun 检查点的 `TryCaptureCheckpointInventory()` 不会现场生成身份：发现缺失 ID 会拒绝发布记录。仍需把“所有正式 Reward 资产在进入运行时前已有稳定 ID”作为配置不变量，否则旧的进程内保存接缝可能临时改写内存中的 `RewardSO` 身份。

## 3. `PlayerInventory`

背包以 `InventoryItemStack` 表示物品及数量，按 `RewardSO` 聚合。

### 添加流程

```text
TryAdd
  → 校验 Reward/数量
  → 检查溢出/容量语义
  → 捕获 Snapshot
  → 修改 ItemStacks
  → 构造 GameSaveData candidate
  → GameSaveService.TrySave
      ├─ 成功：发布 InventoryChanged
      └─ 失败：Restore Snapshot
```

### 移除流程

同样先校验数量，捕获快照，修改后提交；失败恢复。

### 未解析 Reward ID

恢复检查点时，若某个 `rewardId` 当前找不到资源，不应静默丢弃。代码保留 unresolved IDs，使未来资源恢复或迁移有机会处理。

这是一种 `Forward Compatibility（前向兼容）` 思路：当前版本不理解的数据仍尽量保留。

### 检查点捕获

`TryCaptureCheckpointInventory`：

- 按 `rewardId` 排序，得到稳定序列；
- 拒绝缺失 ID；
- 拒绝数值溢出或非法数量。

稳定排序有助于：

- JSON diff；
- Hash/等价比较；
- 测试；
- 避免 Dictionary 枚举顺序影响结果。

## 4. Reward Pickup 的两阶段语义

地面奖励通过请求回调询问背包是否接受：

```text
碰撞
→ Request
→ Inventory 尝试事务
→ true：Pickup 回池并通知
→ false：Pickup 留在世界
```

这里不是严格的分布式 Two-phase Commit（两阶段提交），但具有类似思想：先确认消费者提交成功，再完成生产者的不可逆动作。

## 5. 包裹 UI

### 当前行为

`PackageGridUI.Refresh()` 会销毁并重新创建槽位。

优点：

- 实现简单；
- 数据与显示不易错位；
- 小规模背包可接受。

代价：

- 产生 Instantiate/Destroy 和 GC；
- 滚动位置、选中状态和 Tooltip 容易重置；
- 大量物品或频繁刷新时可能抖动。

### 优化路线

1. 先记录物品数量、刷新频率、耗时、GC。
2. 复用 Slot Pool。
3. 以 `rewardId` 做 keyed diff，只更新变化项。
4. 把排序/过滤和视图实例分开。
5. 数据很多时使用虚拟列表。

不要在没有测量时直接实现复杂 Virtualization（虚拟化）。

## 6. 红点语义

`【已验证事实】` 当前 `PackageHudButtonUI` 的红点是会话级金币阈值/规则加 dismissed 状态，不是逐物品、跨存档持久化的“未读系统”。

若要实现真正未读：

- 每个物品需要首次获得/最新获得序号；
- UI 记录 last seen sequence；
- 保存 last seen；
- 合并堆叠时仍能判断是否有新内容；
- 恢复存档和新冒险要定义是否继承。

## 7. 商店交易

`ShopTradeController.TryPurchase()` 的事务：

1. `EvaluatePurchase` 校验商品、库存、价格、背包。
2. 捕获背包快照和原库存。
3. 不立即持久化地扣金币。
4. 添加商品。
5. 扣库存。
6. 把背包和商店写入候选 `GameSaveData`。
7. 调用 `GameSaveService.TrySave`。
8. 失败则恢复背包和库存。
9. 成功后发通知。

### 为什么不能每一步分别 Save

如果先保存扣金币，再添加商品失败：

- 玩家丢金币没得商品。

如果先加商品，再保存扣库存失败：

- 玩家可以重复购买复制物品。

所以业务上的原子单位是“一次购买”，不是每个字段更新。

### 隔离级别边界

当前是单线程 Unity 主循环中的本地事务，没有数据库锁和多客户端竞争。快速连点仍需依赖：

- 每次购买重新 Evaluate；
- UI 按钮状态；
- 同一帧/回调的重入保护；
- 数量和库存下限不变量。

## 8. 商店稳定身份

- `CatalogId`：商店目录身份。
- `ProductId`：商品身份。
- `RewardId`：最终奖励身份。

三者解决不同层次：

```text
哪个商店？
  → 哪个商品条目？
    → 商品给予哪种奖励？
```

不能只保存数组索引，因为策划调整排序后索引含义会变。

## 9. UI 与 Gameplay Gate

打开商店/包裹/设置时，不能简单设置一个全局 `bool isPausedByUi`：

- 商店可能打开；
- 设置从商店上方再打开；
- TowerRun 死亡弹窗也需要锁；
- 关闭设置不应释放商店仍持有的锁。

项目使用 owner lease（所有者租约）/引用计数 Gate：

```text
Shop Acquire
Settings Acquire
Settings Release  → 仍被 Shop 锁定
Shop Release      → 才恢复 Gameplay
```

这是多个独立模块共享“禁止输入/暂停世界”资源时更稳健的模型。

## 10. `GameSaveService` 的现状

类注释已经明确：

> 旧调用者仍通过该服务提交内存事务，但不会读写旧 `save.json`。

当前职责：

- 规范化属性范围；
- 合并重复背包 ID，并做饱和处理；
- 商店去重；
- 对重复库存取保守值；
- 深拷贝进程内数据；
- 为旧调用者保留统一事务接口。

`HasLoadedExistingSave` 当前为 false 语义，不能用过去“重启恢复”的历史说法描述当前版本。

### 为什么保留它

`【推断分析】` 它是迁移接缝：旧系统仍能提交一致候选状态，TowerRun 再在明确检查点捕获这些业务状态。一次性删除会迫使所有调用者同时重构。

### 风险

- 名字仍像磁盘服务，容易误导。
- 高频调用可能做较大深拷贝。
- 两层保存语义需要文档，否则开发者可能以为每次攻击都落盘。

`【建议方案】` 可重命名为 `RuntimeStateStore` 或拆分 `IGameStateTransactionSink`，但这是未来改造，不是当前事实。

## 11. TowerRun 状态桥

`TowerRunRuntimeStateBridge`：

- Capture：从 stats、inventory、shop 读取一致记录。
- Apply：先捕获当前状态，应用记录；任一失败则把所有部分恢复。

它把运行时对象世界转换为纯数据 `TowerRunRecord`，避免 `TowerRunSlotStore` 直接依赖场景 MonoBehaviour。

## 12. 故障场景

| 故障 | 预期 |
|---|---|
| 数量加法溢出 | 拒绝或饱和，不能绕回负数 |
| Reward 配置被删除 | 保留 unresolved ID，报告兼容问题 |
| 购买中保存失败 | 背包、金币、库存全部恢复 |
| UI 刷新过程中数据变化 | 下一次权威事件重新投影，不让 Slot 成为状态源 |
| 快速连点 | 每次基于最新库存/金币校验，不低于 0 |
| 多 UI 叠加关闭 | owner lease 防止提前解锁 |
| 运行时镜像成功但进程崩溃 | 只恢复最近磁盘检查点，这是产品语义而非数据库丢失 Bug |

## 13. 面试回答示例

### 1 分钟系统介绍

> RewardSO 定义稳定物品身份，Damageable 死亡后从对象池生成 Pickup。玩家碰撞时不是先销毁，而是请求 `PlayerInventory` 接受；背包捕获快照、改数量并提交内存候选，失败回滚并保留地面物品。商店购买把金币、商品和库存作为一次业务事务，失败全部恢复。UI 只订阅和投影权威状态；真正磁盘持久化由 TowerRun SlotStore 在入口检查点完成。

### 3 分钟实现思路

依次讲：

1. RewardSO 的 stable ID 与运行时 stack 分离。
2. Pickup 请求/成功通知的两阶段语义。
3. Inventory snapshot、未解析 ID 和稳定排序。
4. Shop Evaluate→Apply→Save→Rollback。
5. `GameSaveService` 现为进程内镜像的演进语义。
6. UI Gate owner lease 与全量重建的性能边界。
7. TowerRun RuntimeStateBridge 如何跨 stats/inventory/shop 原子应用。

### 你的商店如何保证一致性？

> 一次购买跨背包金币、商品数量和商店库存。我先做只读校验，再保存背包快照和原库存；随后在内存应用全部变化，构造统一候选状态。提交失败就同时恢复背包与库存，成功后才通知 UI。它不是数据库 ACID，但在 Unity 单进程业务边界里实现了原子性和失败回滚。

### 为什么不用 ScriptableObject 保存运行时数量？

> ScriptableObject 适合静态配置和资源引用，但直接修改资产作为运行时存档会污染编辑器资产、共享实例并难以隔离新游戏。项目用 RewardSO 描述物品定义，数量保存在 PlayerInventory/TowerRunRecord。

## 14. 掌握检查

1. 如果购买“扣金币成功、加商品失败”，快照必须覆盖哪些对象？
2. 为什么稳定 ID 比列表索引更适合存档？
3. `GameSaveService.TrySave()` 成功后立即杀进程，为什么不保证本次攻击后的体力被恢复？
4. 为包裹加“只刷新变更槽位”，你会怎样定义数据 diff 和 UI 身份？


> 本轮整合上下文（2026-10-25）：本文原有叙述与验收数字保留为各节标明日期的历史快照。游戏主线已在候选提交 `96371eb36a787585cf5e70141c03555419821058` 保存，其父链包含九个 TowerRun/Portal/UI 提交；默认新冒险 Profile 为 `MainV1`，十二种保留原生怪物，Boss 仍待内容。旧 `ecology-v1`、UI v6 未提交、旧工作树/HEAD 等现在式叙述均不代表本轮状态。新整合 Build/Player 与人工验收只认本轮独立收据；Stage1、B1、Boss 和旧 R4 人工结论不升级。当前整合总账见 `../Integration/MainConsolidation20261025/README.md`。

# RPG_Demo 待实现需求与后续技术设计

> 本文全部为 `【建议方案】` 或 `【待确认】`，除非条目明确标注已有基础。不得在项目介绍中说成已实现。

## 1. 当前范围缺口

### 产品功能

- 初始职业选择；
- 装备与属性加成；
- 任务/剧情；
- 商店出售、回购和动态价格；
- 更丰富的敌人/精英/Boss；
- 完整声音与设置持久化；
- 多地图主题与内容生成工具；
- 完整逐物品未读系统；
- 更长期的存档迁移。

### 工程质量

- 全量测试在最终工作树重新执行；
- TowerRun EditMode Runner 退出挂起根因；
- 量化性能基线；
- 极端分辨率 UI；
- 设置 UI 正式提交和视觉批准；
- 伤害接收接口统一；
- Runtime Save 命名与职责整理。

## 2. 职业选择

### 目标

New Run 前选择职业，影响初始属性、技能/攻击配置和表现，但不破坏 TowerRun baseline。

### 数据模型

```text
CharacterClassDefinition : ScriptableObject
  classId
  displayName
  baseStats
  startingInventory
  playerVisualPrefab / animator profile
  actionProfile
```

### 集成点

- StartupChoice UI；
- New-run baseline 构建；
- TowerRunRecord 保存 `classId`；
- Compatibility 检查；
- canonical player 表现/动作绑定。

### 不变量

- `classId` 稳定唯一；
- 选择只在 New Run 生效；
- 恢复不重新弹职业选择；
- 删除职业时旧存档明确 Incompatible 或迁移；
- 不直接修改 ScriptableObject 运行时数据。

### 测试

- 每职业 baseline；
- 恢复保持职业；
- 无效/重复 ID；
- UI 选择与记录一致；
- 玩家 Prefab/表现缺失时失败。

## 3. 统一伤害接口

### 目标

攻击者依赖 `IDamageReceiver`，减少对 `BaseDamageable`/`PlayerDamageController` 具体类型的了解。

### 草案

```csharp
public readonly struct DamageRequest
{
    public readonly float Amount;
    public readonly Transform Source;
    public readonly Vector2 Direction;
    public readonly Guid GenerationId;
}

public interface IDamageReceiver
{
    bool CanReceiveDamage { get; }
    DamageResult TryReceiveDamage(in DamageRequest request);
}
```

### 迁移

1. 为现有两类接收器加 adapter。
2. 建立 contract tests。
3. 攻击者查询接口。
4. 保留玩家死亡和世界回池各自策略。
5. 最后删除重复入口。

### 风险

- Unity 对接口组件查询的性能和序列化体验；
- 多 Collider 对同一 receiver 的去重；
- Generation 对玩家是否适用；
- 受击免疫/阵营规则膨胀。

## 4. 地图性能与异步生成

### 前置条件

先测：

- Generate；
- Plan；
- Project；
- Materialize；
- Ready 总时间；
- 主线程与 GC。

### 方案

- 纯数据部分后台生成；
- 每个请求携带 CancellationToken/Generation token；
- 结果回主线程前检查是否过期；
- Unity Object API 只在主线程；
- Tilemap/实体分帧；
- Loading Gate 一直持有到 Ready。

### 新失败模式

- 后台任务异常；
- 用户在生成中开始新请求；
- 场景销毁；
- 结果完成但已过期；
- 分帧投影失败；
- 取消后资源泄漏。

因此“异步”不是简单加 `Task.Run`。

## 5. 存档 Migration

### 目标

允许有限个旧 Schema 升级，而不是全部标记 Incompatible。

### 设计

```text
Read raw envelope
→ Identify schema
→ v1→v2 migrator
→ v2→v3 migrator
→ Validate current model
→ Optional rewrite after explicit confirmation
```

### 规则

- 迁移函数纯数据、确定性；
- 原文件先备份；
- 不静默覆盖；
- 每步有 fixture；
- 不支持版本给明确原因；
- 生成器语义变化不一定能靠字段迁移解决。

## 6. 真正的未读红点

### 数据

```text
InventoryAcquisitionSequence
ItemLastAcquiredSequence[rewardId]
PackageLastSeenSequence
```

### 行为

- 获得新物品提高全局序号；
- 打开包裹或查看物品更新 last seen；
- 红点判断是否存在 acquired > seen；
- TowerRun record 保存序号；
- New Run baseline 清零或按产品规则继承。

### 边界

- 同种物品数量增加算新消息吗？
- 自动奖励是否提示？
- 恢复旧检查点后 last seen 回退吗？
- 多入口 UI 共享 seen 吗？

## 7. 包裹增量渲染

### 方案

- 以 `rewardId` 为 key；
- 缓存 slot view；
- 计算 added/removed/quantityChanged；
- 复用 GameObject；
- 保持排序与滚动位置；
- Tooltip 对已删除项安全关闭。

### 测试

- 添加/移除/堆叠；
- 排序变化；
- 打开/关闭；
- 大量数据；
- 同帧多次事件合并；
- Slot Pool 状态重置。

## 8. 商店扩展：出售

### 事务

```text
校验玩家数量
→ 校验商店回购规则/资金/容量
→ 捕获背包与商店
→ 移除物品
→ 增加金币
→ 更新回购列表
→ 提交或回滚
```

### 关键决策

- 价格是否固定/浮动；
- 商品 RewardId 与 ProductId 的关系；
- 回购列表是否持久化；
- 入口检查点恢复后交易是否回退；
- 最大金币与溢出。

## 9. Boss 与精英放置

复用 Placement Plan，而不是运行后随机找位置。

新增策略：

- 远离出生；
- 与出口/商人保持距离；
- 需要足够战斗空间；
- 路径可达；
- 可能独占区域；
- Registry Role 区分 Boss/Monster；
- Floor controller 的胜利条件可配置。

## 10. 可观测性

### 建议指标

- Seed、attempt count、失败规则；
- 每 phase 耗时；
- 生成对象数；
- Registry 条目；
- 存档大小/写入耗时；
- Slot 分类原因；
- Gate 当前 owners；
- AI 状态转移计数；
- 包裹刷新耗时/GC。

### 开发者诊断面板

可提供：

- 当前 Run/Floor/Generation；
- Map phase；
- 玩家逻辑 Cell；
- 当前目标/出口状态；
- Gate owners；
- 最近失败原因；
- 一键复制复现信息。

只在开发构建启用。

## 11. 测试基础设施收口

### Runner 挂起

- fixture 二分；
- 静态订阅检查；
- Editor callback；
- 临时资源/目录；
- 完成后进程清理；
- 与最小工程对照。

### 统一执行

建议形成：

```text
EditMode fast
→ Map/TowerRun EditMode
→ PlayMode flow
→ GPU（有图形设备）
→ cross-process persistence
→ build preview
→ manual checklist
```

报告必须记录：

- Git SHA；
- 工作树是否脏；
- Unity/平台/GPU；
- 命令退出码；
- 测试数；
- 人工未测项。

## 12. 里程碑建议

### Milestone A：稳定当前版本

- 设置 UI 视觉批准并提交；
- 修复 Runner 退出；
- 最终 HEAD 全量回归；
- 完整手工 TowerRun checklist。

### Milestone B：量化与减债

- 性能基线；
- `GameSaveService` 语义整理；
- 包裹增量刷新；
- 伤害接口 contract。

### Milestone C：内容扩展

- 职业；
- 精英/Boss；
- 商店出售；
- 新地图主题。

### Milestone D：长期兼容

- 存档 migration；
- 多平台文件验证；
- 崩溃/故障注入；
- 可观测开发面板。

顺序依据：先让现有闭环可信，再扩内容。


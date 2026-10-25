> 本轮整合上下文（2026-10-25）：本文原有叙述与验收数字保留为各节标明日期的历史快照。游戏主线已在候选提交 `96371eb36a787585cf5e70141c03555419821058` 保存，其父链包含九个 TowerRun/Portal/UI 提交；默认新冒险 Profile 为 `MainV1`，十二种保留原生怪物，Boss 仍待内容。旧 `ecology-v1`、UI v6 未提交、旧工作树/HEAD 等现在式叙述均不代表本轮状态。新整合 Build/Player 与人工验收只认本轮独立收据；Stage1、B1、Boss 和旧 R4 人工结论不升级。当前整合总账见 `../Integration/MainConsolidation20261025/README.md`。

# RPG_Demo 工程实践与技术决策

## 1. 决策总览

| 决策 | 解决的问题 | 代价 |
|---|---|---|
| `MapData` 为逻辑真相 | 表现、测试、小地图和放置统一 | 需要维护数据到表现投影 |
| Plan/Commit/Project 分离 | 失败前不破坏旧世界 | 生命周期复杂度增加 |
| GenerationId | 拒绝迟到回调和旧对象 | 每条相关链路都需携带身份 |
| 稳定 ID | 保存、迁移和引用稳定 | 配置管理和唯一性校验成本 |
| 事务式快照回滚 | 防半购买/半恢复 | 深拷贝和回滚逻辑成本 |
| 入口检查点 | 恢复边界清晰 | 不支持任意战斗帧恢复 |
| Owner Lease Gate | 多 UI/流程不误解锁 | 需要严格释放租约 |
| 多层测试 | 匹配算法、场景、GPU、进程 | 执行时间和环境维护成本 |

## 2. 数据与表现分离

### 决策

生成器只产生纯数据；Renderer 负责 Tilemap；Minimap 从数据派生；对象放置先规划。

### 好处

- 纯逻辑可在 EditMode 快速测试。
- 表现替换不必改生成规则。
- 存档可以保存重建参数而非 Tile。
- 小地图不会与主摄像机耦合。

### 失败模式

- Renderer 偷偷根据 Tile 反推逻辑；
- UI 修改 MapData；
- 对象 transform 成为唯一位置真相；
- 多个模块各自随机，破坏确定性。

## 3. 显式状态机而非布尔组合

地图生命周期和 TowerRun 都采用枚举 Phase/State。

判断标准：

- 如果流程有三个以上阶段；
- 阶段之间存在非法转移；
- 失败在不同阶段语义不同；
- 多个观察者依赖“何时完成”；

就应优先使用显式状态机。

状态机本身不是完整设计，还需：

- 转移表；
- 进入/退出副作用；
- 幂等规则；
- 错误状态；
- 观察者发布时机。

## 4. Plan/Commit 与旧世界保留

### 决策

候选世界在内存中准备并验证，Commit 前不影响 Active。

### 类比

- 数据库事务：先准备候选，提交后可见。
- 蓝绿部署：新环境健康后切流量。
- 配置热更新：解析/校验成功后替换。

### 边界

Unity Object 的移动和 Tilemap 写入并不天然可回滚，所以把尽可能多的失败推到纯数据规划阶段。

## 5. 稳定 ID 与数组索引

稳定 ID 用于 Reward、Catalog、Product、Damageable 和地图配置。

数组索引失败的例子：

```text
版本 1：[Potion, Meat, Key]
存档保存 index=1
版本 2：[Potion, Key, Meat]
index=1 已变成 Key
```

稳定 ID 可在排序改变后保持语义，但仍要处理：

- 重复 ID；
- 删除 ID；
- ID 迁移；
- 内容兼容；
- 人为复制 ScriptableObject 导致 ID 冲突。

## 6. 事务、快照与回滚

项目中的事务不是数据库 ACID 全集，主要关注：

- `Atomicity（原子性）`：要么全成，要么恢复；
- `Consistency（一致性）`：数量范围、ID 唯一、状态组合合法；
- 部分 Durability（持久性）：TowerRun 文件写入协议；
- 不处理复杂并发隔离，因为主要在 Unity 主线程。

### 快照适用条件

- 数据量中小；
- 操作频率可控；
- 回滚正确性比增量日志简单；
- 对象可深拷贝。

### 快照风险

- 高频深拷贝；
- 漏掉某个状态源；
- 回滚后事件重复；
- 引用对象浅拷贝；
- 快照和实际 Unity 世界不同步。

## 7. 接口与模块接缝

项目中值得复用的接缝：

- `IMapRuntimeReadyFinalizer`
- `IMapTransitionResettable`
- Placement Service/Plan
- `TowerRunRuntimeStateBridge`
- `TowerRunGate` lease

好的接缝应隐藏内部复杂度并提供明确契约。例如 Finalizer 的契约不是“随便做点事”，而是在 Ready 之前完成恢复/校验，否则阻止 Ready。

## 8. 事件设计

事件适合“已发生”的通知：

- `OnDefeated`
- `OnInventoryChanged`
- GameplayReady

请求/决策不应只用无返回事件。例如拾取需要知道库存是否接受，所以使用返回 bool 的请求链。

事件风险：

- 顺序不明确；
- 重复订阅；
- 静态事件跨测试泄漏；
- 观察者抛异常；
- 发布过早；
- 取消订阅遗漏。

## 9. 对象池的工程约束

对象池优化 Instantiate/Destroy，但必须把对象设计成可重复初始化。

池化对象检查表：

- [ ] 生命/资源重置
- [ ] Collider/Rigidbody 状态
- [ ] Animator 参数
- [ ] 协程/Invoke
- [ ] 事件订阅
- [ ] target/home/Generation
- [ ] transform/parent/layer
- [ ] 一次性标志
- [ ] 归还幂等

## 10. 测试金字塔在本项目的实际形态

| 层 | 验证内容 | 速度/成本 |
|---|---|---|
| 纯 EditMode | 生成算法、校验、状态转换、序列化规则 | 快 |
| 资源/配置 EditMode | Prefab/ScriptableObject 关系、稳定 ID | 中 |
| PlayMode | 场景、MonoBehaviour、物理、生命周期 | 慢 |
| GPU PlayMode | Shader/RenderTexture 像素 | 环境敏感 |
| 跨进程 | 磁盘持久化 | 更慢 |
| 人工验收 | 画面、手感、音频、完整流程 | 最贵但不可替代 |

不追求所有测试都放到 PlayMode。越接近纯逻辑，越应下沉。

## 11. 可测试性设计

项目中的可测试性不是“写完再 mock”，而是架构产生的：

- MapData 纯数据；
- Orchestrator 接受委托/服务；
- Placement 先输出计划；
- SlotStore 与场景 MonoBehaviour 分离；
- RuntimeStateBridge 把对象转数据；
- 明确 finalizer 和 reset 接口。

这说明 Testability（可测试性）与模块边界是同一问题。

## 12. Git 与 Worktree

`Git Worktree（Git 工作树）` 允许多个分支在不同目录同时存在，共享一个仓库对象数据库。

本项目历史上：

- MapGeneration 独立深化；
- Monster 等功能并行；
- MapMerger 专门整合；
- TowerRun 在整合基础上演进。

优点：

- 不必频繁 stash/checkout；
- 可保留不同分支的 Unity Library/工作状态；
- 整合分支可单独测试。

风险：

- 同一分支不能被多个 worktree 同时 checkout；
- Unity 资源 GUID 冲突仍需解决；
- 旧 worktree 文档可能过时；
- 主工作树脏状态必须保护。

## 13. 文档与证据管理

本项目同时有：

- 路线图；
- 分支总结；
- 开发日志；
- Git 提交；
- 测试结果；
- `.codex/HANDOFF.md`；
- 当前源码。

证据优先级不是“新文档永远正确”，而是：

1. 当前真实文件/Git 状态；
2. 对应提交的测试；
3. 与时点匹配的日志；
4. 历史总结；
5. 计划/建议。

## 14. 代码质量风险

### 大型编排类

例如 `TowerRunUiController`、`ShopTradeController`、`MapMinimapController`、`TowerRunSlotStore` 代码较长。

长度本身不是罪，但应检查：

- 是否同时处理领域逻辑和视图构建；
- 是否拥有过多状态；
- 测试是否只能通过反射；
- 私有方法之间是否形成隐藏状态机；
- 修改一个功能是否影响多个无关区域。

### 静态全局

`GameSaveService`、RewardPickup 静态事件等简化访问，但增加测试隔离和多会话难度。

### 名称语义漂移

`GameSaveService` 名称暗示磁盘持久化，当前实际是运行时镜像。名称与职责不一致会误导维护者。

## 15. 决策记录模板

```markdown
# ADR-xxx 标题

## Context
当前问题、限制和证据。

## Decision
选择什么，并明确不选择什么。

## Invariants
必须始终成立的条件。

## Consequences
正面、负面和迁移成本。

## Verification
自动化、人工验收和可观测指标。
```

`【建议方案】` 后续对“统一伤害接口”“GameSaveService 重命名”“迷雾是否持久化”使用 ADR（架构决策记录），减少语义再次漂移。


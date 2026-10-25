> 本轮整合上下文（2026-10-25）：本文原有叙述与验收数字保留为各节标明日期的历史快照。游戏主线已在候选提交 `96371eb36a787585cf5e70141c03555419821058` 保存，其父链包含九个 TowerRun/Portal/UI 提交；默认新冒险 Profile 为 `MainV1`，十二种保留原生怪物，Boss 仍待内容。旧 `ecology-v1`、UI v6 未提交、旧工作树/HEAD 等现在式叙述均不代表本轮状态。新整合 Build/Player 与人工验收只认本轮独立收据；Stage1、B1、Boss 和旧 R4 人工结论不升级。当前整合总账见 `../Integration/MainConsolidation20261025/README.md`。

# 问题、风险与验证优先级

Phase 2 在当前工作树的**隔离 Unity 副本**中确认了 AA-02 的条件性缺陷；这不表示正式场景天然有两个奖励接收者。AA-01 的资源/Animator 定向断言已通过，但完整人工视觉与复杂边缘追击仍未验收，继续保持 Unverified。其余为设计关切。`P0–P3` 表示验证/处理优先级，不等于所有问题都已在默认玩法中触发。原始 XML、日志和 Runner 退出状态见 [验证记录](VALIDATION.md)。

**后续来源变化（2026-10-13）**：本教程与自动更新任务期间，`MonsterAnimationController.cs` 出现并行未提交修改，原隔离测试的部分输入已不同。最新图谱将 AA-01 的移动/攻击动画运行关系标为 `source confirmed; runtime evidence stale or unavailable`。下文 Phase 2 的 PASS 只属于当时的隔离快照；当前代码组合不能沿用为运行 PASS，AA-01 状态仍为 Unverified。未改动 AA-02 的条件性结论。

## AA-01 · P1 · Monster 视觉与边缘追击需要针对最终资产复验

- **类别/系统**：Unverified（完整视觉/追击验收）；Monster、地图。**节点**：当前 Monster Prefab/Visual Animator、`MonsterMovementController`、`MonsterPlacementService`。
- **证据/原因**：此前 `.codex/HANDOFF.md` 记录了排序与边缘移动的定向自动测试；用户用旧隔离 Player 初步观察时暂未再发现明显的 Monster 草地遮挡或异常追击，但特殊边缘场景未覆盖。本次审计期间 Monster Prefab 和 Animator 文件又被外部改名、场景被修改。先前测试与部分人工观察不能证明新的资产组合已完整验收。
- **Phase 2 静态复核**：`Torch_Blue.prefab:267,350,370-379` 有 `Visual`、Controller GUID、移动/攻击/Animator/SpriteRenderer 的本地 fileID 及 `IsMoving/Attack/AttackDirection` 字段；Controller 的 `.meta` GUID 与 Prefab 相同，参数名与 Bool/Trigger/Int 类型见 `Torch_Blue_Visual.controller:286-304`。`RandomMapRuntime.prefab:1200` 的 `monsterTarget` 默认空，`SampleScene.unity:49500-49502` 用 Prefab override 指向场景里的 Monster AI 组件；合成引用与事件驱动效果的本次 Unity 复核结果见下一项。
- **复现步骤 / 预期 / 当前实际**：在隔离副本加载最新版 `SampleScene` 并开始新局，检查 `MonsterPlacementService.MonsterTarget` 与场景 `Torch_Blue/Visual`；使移动/攻击事件发出并观察 Animator；另在草/沙地、树冠前景、地图边缘和水域附近人工观察。预期引用可解析、参数随行为变化、画面/约束符合设计。实际：Editor 审计 727 组件、0 Missing Script/Reference，Scene override、Controller 与参数均 PASS；`TorchBlueVisualUsesExpectedRuntimeAnimatorConnections` 验证移动事件的 `IsMoving`、攻击事件的 `AttackDirection=3` 与右向攻击状态 PASS；现有排序/边缘碰撞用例 PASS。**人工画面中的四方向、树冠、水域和复杂边缘追击仍未执行**，所以整项不判全面 PASS。
- **建议/回归风险**：下一步用最新版独立 Player 实际观察四向动作、草/沙地、树冠前景、边缘目标与水域。若观察到问题，再围绕排序/移动或 Prefab 配置拟修复方案；回归目标计数、死亡奖励和地图换层。
- **置信度/状态**：Editor 资源连接与已覆盖的运行断言高置信度 PASS；未覆盖的最终视觉体验仍 Unverified，不能据此断定存在当前用户可见缺陷。

## AA-02 · P2 · 多背包订阅者时奖励请求可能部分提交

- **类别/系统**：Confirmed Defect（条件性）；Reward/Inventory。**节点**：`RewardPickup.OnRewardPickupRequested → PlayerInventory.HandleRewardPickupRequested`。
- **证据/原因**：`RewardPickup.TryAcceptReward` 顺序调用 `GetInvocationList` 中全部接收者，任何一个返回 false 就使地面拾取保留（`Assets/Scripts/RewardPickup.cs:84-104`）；`PlayerInventory` 每个已启用实例订阅并在 `TryAddItem` 成功时修改数据、发布变化（`Assets/Scripts/PlayerInventory.cs:144-154,181-217`）。若先一个实例成功、后一个失败，前者没有被回滚。
- **最小复现步骤 / 预期 / 实际结果**：在隔离 Play Mode 中启用正式玩家背包，再启用一个同类型背包，把第二个 Branch stack 设为 `GameSaveService.MaxQuantity`；断言事件订阅顺序为玩家先、满额者后，击败 `Torch_Blue` 并触发 Branch 拾取。期望该请求要么全部提交、要么全部不提交。`TwoInventoryReceiversCanPartiallyCommitWhenSecondRejects` 的断言实际通过：第一背包数量 `before + 1`，第二背包维持 `int.MaxValue`，地面奖励仍 Active，`hasBeenPickedUp=false`。定向 Play Mode XML 5/5 PASS 且 Unity 自然退出 0。原 Scene 加入第二个背包前只有一位接收者；默认场景**自然形成多接收者**的路径未发现/未复现，因此确认的是有前提的缺陷，不声称正常单背包流程必定出错。
- **建议/回归风险**：部分提交已经由两个订阅者的最小 Play Mode 测试确认。若业务允许多接收者，应设计具备原子提交/回滚的协调接口；若只允许一个，应明确约束并拒绝重复订阅。修改会影响拾取成功通知与 UI 刷新，需回归掉落奖励测试。
- **置信度/状态**：有条件的部分提交由 Play Mode 复现，Confirmed Defect；默认单背包场景触发条件仍未证实。

## AA-03 · P2 · 背包任何变动都销毁并重建所有可见格子

- **类别/系统**：Design Concern；Inventory/UI。**节点**：`PlayerInventory.OnInventoryChanged → PackageGridUI.Refresh`。
- **证据/原因**：每次事件先 `ClearSlots`，逐个 `Destroy`，再 `Instantiate(slotPrefab)`，并清空当前选中格（`Assets/Scripts/UI/PackageGridUI.cs:61,82-104,146-162`）。
- **复现/影响**：行为由源码直接确认；性能或视觉抖动尚未 Profiler/手动量化。大量物品或频繁变化时可能产生 UI 分配、布局重算，选中态也会消失。
- **建议/回归风险**：先测 item stack 规模与刷新耗时；确有瓶颈再按稳定 item ID 差量复用格子，保留选中态。改动可能影响 Tooltip 与格子事件解绑；回归背包、购买、恢复后 UI。
- **置信度/状态**：重建行为高；性能影响未验证。

## AA-04 · P2 · 每次攻击搜索全部 BaseDamageable

- **类别/系统**：Design Concern；Player/Damageable。**节点**：`PlayerAction.FindNearestDamageableInRange → BaseDamageable`。
- **证据/原因**：`FindObjectsOfType<BaseDamageable>()` 每次攻击遍历全部实例（`Assets/Scripts/Player/PlayerAction.cs:311-334`）；在此之前攻击还克隆并规范化会话状态（`PlayerAction.cs:227-235`、`GameSession.cs:119-135`）。这是线性扫描和内存操作，**不是本版中的每击磁盘写入**。
- **复现/影响**：源码行为已确认，帧时与 GC 未测；实体数增长时可能造成攻击顿挫。
- **建议/回归风险**：在目标实体规模下用 Unity Profiler 量化；若必要，用范围内 Physics 查询/登记索引替换全局搜索，同时保持“最近可受击对象”语义。需回归植物、动物、怪物同时存在时的选靶规则。
- **置信度/状态**：复杂度判断高；用户可感知性能未知。

## AA-05 · P2 · 地图 Commit 后仍有多个会失败的阶段

- **类别/系统**：Design Concern；Random Map/Tower Run。**节点**：`MapIntegrationOrchestrator.CommitPending → MapGenerationController materialization/finalizers`。
- **证据/原因**：`CommitPending` 和旧 Registry 退休发生在对象物化、玩家重定位、相机与 finalizer 之前（`Assets/Scripts/MapGeneration/MapIntegrationOrchestrator.cs:74-93`、`MapGenerationController.cs:142-345`）。后续失败会记录 `MapRuntimeContext.Failure` 并阻止 Ready，这符合当前 fail-closed 设计，但可能留下已投影却不可玩的新地图。
- **复现/影响**：异常注入场景尚未本轮运行；可能影响恢复失败时的视觉/状态解释，不能当作“地图生成成功但偶尔卡住”的已确认 Bug。
- **建议/回归风险**：给 QA 增加每阶段故障注入和失败屏幕/重试观察；若要真正 rollback，需要重新设计场景绑定对象与投影的事务边界，回归所有生成测试。
- **置信度/状态**：阶段顺序高；实际失败体验未验证。

## AA-06 · P2 · `TrySave` 名称易与正式槽位持久化混淆

- **类别/系统**：Design Concern；Save/Load。**节点**：`PlayerInventory/GameSession → GameSaveService` 与 `TowerRunCoordinator → TowerRunSlotStore`。
- **证据/原因**：`GameSaveService.TrySave` 调 `TryCommitRuntimeData`，只更新 `cachedData`（`Assets/Scripts/GameSaveService.cs:193-221`）；三槽磁盘提交由 `TowerRunSlotStore.TryCommit` 完成（`Assets/Scripts/TowerRun/TowerRunSlotStore.cs:696-739`）。两个接口都带 Save/Commit，未来开发者容易误认为拾取或攻击已落盘。
- **复现/影响**：语义差异由源码确认；当前产品是否承诺自动存档需核对需求，暂不定为数据丢失 Bug。
- **建议/回归风险**：在 API 注释/学习文档明确“内存 vs 正式槽位”；后续命名重整需兼容旧测试和恢复流程。验证：进程退出后只检查明确保存的入口槽位，不推断中途状态必恢复。
- **置信度/状态**：高；产品期望待确认。

## AA-07 · P3 · 兼容摆放适配器仍挂载但 Runtime 路径无效

- **类别/系统**：Design Concern；地图整合/遗留。**节点**：`RandomMapRuntime.prefab → MapGeneratedObjectPlacementAdapter`。
- **证据/原因**：Prefab 仍有该组件；`RuntimePlacementEnabled => false`，`PlaceObjects(MapData)` 只验证非空、不会移动对象（`Assets/Scripts/MapGeneration/MapGeneratedObjectPlacementAdapter.cs:55-69`）。真实生产摆放交给语义 Placement Service。
- **复现/影响**：代码事实；容易让维护者误以为旧 authored offset 仍有效，改错配置。当前没有证据表明它导致运行 Bug。
- **建议/回归风险**：明确在 Inspector/文档标“迁移诊断”；未来删组件前核对 `MapCompatibilityRetirementTests` 和所有序列化引用。删除可能影响旧场景/迁移测试。
- **置信度/状态**：高；属维护负担。

没有为凑满十条而把所有空引用、旧资产或代码风格列成问题。`graph.json` 当前未解析 GUID 为 0 也**不**构成 Missing Reference 的运行时通过证明。

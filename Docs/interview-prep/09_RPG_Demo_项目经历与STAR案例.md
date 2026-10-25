> 本轮整合上下文（2026-10-25）：本文原有叙述与验收数字保留为各节标明日期的历史快照。游戏主线已在候选提交 `96371eb36a787585cf5e70141c03555419821058` 保存，其父链包含九个 TowerRun/Portal/UI 提交；默认新冒险 Profile 为 `MainV1`，十二种保留原生怪物，Boss 仍待内容。旧 `ecology-v1`、UI v6 未提交、旧工作树/HEAD 等现在式叙述均不代表本轮状态。新整合 Build/Player 与人工验收只认本轮独立收据；Stage1、B1、Boss 和旧 R4 人工结论不升级。当前整合总账见 `../Integration/MainConsolidation20261025/README.md`。

# RPG_Demo 项目经历与 STAR 案例

## 1. 简历项目描述

### 保守版

> 使用 Unity 2022.3 开发 2D 俯视角 ARPG 学习原型，覆盖玩家战斗、怪物状态机、奖励/背包/商店、确定性随机地图和 TowerRun 三槽检查点。参与需求拆解、方案约束、AI 辅助实现审查、真实场景验收、故障复现与测试建设。

### 技术版

> 参与约束并验证 `MapData → Placement Plan → Commit → Projection → GameplayReady` 的随机地图整合流程，以 GenerationId 隔离旧代异步结果；审查背包/商店/恢复的快照回滚；参与验收带 Schema/生成器/稳定 ID 校验、临时文件、原子替换、备份与写后 Hash 验证的 TowerRun 三槽入口检查点。具体实现大量由 Codex 辅助，作者归属无法逐行重建。

### AI 透明版

> 项目大量使用 Codex 辅助生成代码；个人重点负责需求边界、技术选择、场景验收、问题定位、测试要求和知识复盘。只将能独立解释、验证和修改的系统列为掌握内容。

## 2. STAR 使用原则

`STAR（情境-任务-行动-结果）`：

- Situation：具体版本/现象。
- Task：必须守住的不变量。
- Action：你真实执行或主导的动作。
- Result：量化验证与限制。

如果某个修复主要由 AI 执行：

> 不要说“我独立编写了所有代码”；可说“我复现并确认问题、约束修复边界、审查方案、执行/要求回归验证”。

## 3. 案例一：随机地图从独立 Demo 整合到 RPG 主场景

### Situation

随机地图分支能生成和显示，但主场景已有玩家、相机、商人、怪物、动物、植物、可破坏物和 UI。直接合并会产生初始化顺序、对象冲突和旧静态世界共存问题。

### Task

把地图整合成唯一运行时权威，并保证 Commit 前失败不预先替换当前 Active Map/Registry；上层流程失败时仍应通过 Gate 明确阻止半初始化世界继续运行。

### Action

- 以 `MapData` 作为逻辑源；
- 建立 MapRuntime 生命周期；
- 使用 Placement Plan 和 Registry；
- 为语义对象逐类实现放置服务；
- 玩家出生后重初始化依赖并绑定相机；
- 通过 Authority 禁止随机和静态世界同时有效；
- Commit 前失败保留旧 Active 数据与 Registry；不把它夸大为 TowerRun 失败时玩家仍可自由操作旧世界。

### Result

- PR #1 完成整合；
- 后续删除旧测试场景和迁移残留；
- 历史证据中 MapGeneration EditMode 216/216、PlayMode 16/16；
- 仍需说明这些结果对应特定提交，非当前工作树重新全量执行。

### 可追问

- 为什么 Plan/Commit？
- post-commit failure 怎么办？
- GenerationId 如何传递？

### Technical Depth

`MapData`、Placement Reservation、Registry、GenerationId、生命周期状态机和 GameplayReady。

### Trade-off

增加了阶段和类型数量，但换来全局验证、失败保留与可测试性；不是所有小地图 Demo 都需要该复杂度。

### Reflection

单独可运行的功能并不等于可整合模块。下一次会在独立分支早期定义 Authority、状态所有者和集成契约。

### Follow-up

面试官可能问：“Commit 后第三个对象实例化失败如何恢复？”应承认这是更难的补偿问题，当前区分了 post-commit failure，但完整恢复仍可增强。

## 4. 案例二：迷雾揭示旧位置

### Situation

New Run 后，小地图在旧玩家位置留下已探索区域。

### Task

区分数据未清空、Generation 混用和事件时序，定位可复现根因。

### Action

- 建立失败测试；
- 在 `MapGenerated`/spawn 关键点记录位置；
- 观察旧位置 `(62,62)`、新位置 `(32,32)` 与 37 个错误揭示格；
- 把探索更新移动到正确完成边界；
- 增加定向回归。

### Result

- 定向测试由 0/1 变为 1/1；
- TowerRun flow 4/4；
- 得到“事件名称不等于完成契约”的工程经验。

### Technical Depth

事件时序、Unity 帧阶段、玩家 spawn、小地图 LateUpdate 和 Generation。

### Trade-off

把探索推迟到更可靠边界会减少错误，但也要避免视觉延迟；应以 GameplayReady/最终位置不变量定义，而不是任意多等一帧。

### Reflection

原测试只看最终状态，未观察事件发生瞬间。以后对“完成事件”同时测试发布当刻的不变量。

### Follow-up

“为什么不是数组未清空？”因为探针证明错误揭示发生时使用了旧坐标，给出了更直接的因果证据。

## 5. 案例三：商店原子交易

### Situation

一次购买同时修改金币、商品数量和商店库存；任一步失败会造成玩家资产不一致。

### Task

保证购买要么全部成功，要么恢复到原状态。

### Action

- 把只读校验与实际变更分开；
- 捕获背包快照和原库存；
- 在内存应用所有变化；
- 统一提交候选数据；
- 失败时同时回滚；
- 成功后才更新 UI。

### Result

`【历史记录】` 正常流人工验证：100 → 拾取后 108 → 消费 5 后 103，获得 2 肉，库存剩 2，并在当时流程中恢复。

### 限制

边界场景在不同阶段覆盖程度不同，不能说全部已人工验证。

### Technical Depth

快照回滚、跨聚合业务事务、稳定 Product/Catalog/Reward ID、提交后通知。

### Trade-off

全量快照实现简单可靠，但高频/大数据可能有复制成本；当前规模优先正确性。

### Reflection

人工正常流只能证明 happy path。面试中应主动指出保存失败、快速连点、溢出仍需对应证据。

### Follow-up

“如果回滚也失败？”需要把状态标记为不可继续/Failure 并重新从可信检查点加载，不能继续让 UI 假装正常。

## 6. 案例四：TowerRun 安全三槽存档

### Situation

入口检查点需要跨进程保存；直接覆盖 JSON 在中断时可能损坏，而且旧生成器记录可能被错误加载。

### Task

设计可分类失败、可验证写入、可识别兼容性的本地三槽保存。

### Action

- 校验 Schema、生成器版本、配置和稳定 ID；
- 覆盖前使用确认 token；
- 写临时文件并复读；
- 原子替换并保留 backup；
- 正式文件复读和 SHA-256 比对；
- 不确定结果通过实际观察分类；
- 用独立 writer/reader 进程测试。

### Result

- 跨进程 writer、reader 各 1/1；
- 错误槽可区分 Empty/Corrupt/Incompatible/Unavailable；
- 明确不宣称防作弊或断电级绝对安全。

### Technical Depth

Schema、稳定 ID、Compare-And-Swap token、temp、atomic replace、backup、SHA-256、uncertain outcome。

### Trade-off

比直接 `File.WriteAllText` 复杂，但显著缩小半写和误覆盖风险；仍不等于数据库或云同步。

### Reflection

文件 API 成功只是低层结果，业务层必须复读实际正式文件。Hash 用于一致性，不应包装成安全机制。

### Follow-up

“两个进程同时写？”当前未证明多写者；需要文件锁/named mutex 或完整 CAS。

## 7. 案例五：对象池与旧 Generation 奖励

### Situation

池化/延迟回调可能在切层后生成旧地图奖励，污染当前 Run。

### Task

让对象和掉落遵守地图代际所有权。

### Action

- 对 Active RandomGenerated world 建立唯一权威；
- 对对象/奖励携带 Generation；
- 旧代奖励通过 scoped lifetime 退休；
- 过期回调拒绝；
- 生成失败时不让旧代奖励错误退休或污染新代；此处谈的是奖励所属 Generation，不是整个地图 Orchestrator 的恢复承诺。

### Result

提交 `b8e27c9` 补齐奖励映射与 Generation-scoped lifetime；`e7aff9f` 强化 `BaseDamageable` 对唯一活动 RandomGenerated authority 的选择，并覆盖失败再生成时奖励仍归属当前 Generation；随后 PR #2 合并。它们不能单独证明 Map Orchestrator 的全部失败保留语义。

### Technical Depth

对象池复用、延迟回调、Generation token、Authority、生命周期退休。

### Trade-off

每个相关对象携带代身份增加了传播成本，但比依赖父节点/场景扫描更可靠。

### Reflection

对象被禁用或换父节点并不代表所有异步行为停止；生命周期必须在数据层表达。

### Follow-up

“为什么不切层时 Destroy 全部奖励？”Destroy 仍可能遗漏已经排队的回调，也失去池化收益；Generation 校验是更稳健的第二道防线。

## 8. 案例六：为图形与文件选择正确测试层

### Situation

普通 EditMode/无图形 PlayMode 无法证明黑洞像素结果，同进程测试也不能证明磁盘数据跨重启存在。

### Task

让验证环境匹配机制。

### Action

- 黑洞使用真实 D3D11 GPU 测试；
- 持久化使用独立进程 writer/reader；
- 纯算法保留 EditMode；
- 场景流程使用 PlayMode；
- 视觉审查保留截图和人工批准。

### Result

- GPU 1/1；
- 跨进程各 1/1；
- 同时记录自动化不能替代人工观感。

### Technical Depth

测试环境、图形设备、进程隔离、静态内存、persistentDataPath 和人工验收。

### Trade-off

高保真测试更慢、更依赖环境，因此只用于无法由低层测试证明的关键机制。

### Reflection

“测试通过”必须连同环境、提交、退出码和未覆盖范围一起报告。

### Follow-up

“为什么不全部用 PlayMode？”纯算法会变慢、定位更差；测试应尽可能下沉到最低但足够可信的层。

## 9. 失败/不足也可以怎样讲

### TowerRun EditMode Runner 挂起

> 断言 21/21 通过，但执行器未退出，最终退出码 -1。我没有把它包装成全绿，而是拆分“断言、Runner 完成、进程退出”三层结论，并制定 fixture 二分和静态生命周期检查计划。

### 设置 UI v6 尚未最终批准

> 当前工作树已由 v4 收口到 v6；定向 6/6、Player 构建和 1024×576/1280×720/1920×1080 的三类截图只能证明结构、流程和已检查尺寸，用户最终视觉批准与提交授权仍未完成，所以不列为正式合并成果。

### 性能缺少量化

> 目前能指出潜在热点，但没有 P95/GC 数据，因此只提出测量计划，不宣称已经优化。

## 10. 行为面试问题映射

| 问题 | 可选案例 |
|---|---|
| 最困难的技术问题 | 地图整合、存档 |
| 一次严重 Bug | 迷雾时序 |
| 如何保证质量 | 多层测试 |
| 如何处理失败 | Commit 前保留旧 Active 数据、上层 Gate、事务回滚 |
| 如何做取舍 | 入口检查点 vs 任意时刻保存 |
| 如何使用 AI | 需求/审查/验收/掌握边界 |
| 一次不完美结果 | Runner 挂起、UI 待批准 |
| 如何协作/整合 | Worktree、MapMerger、PR |

## 11. 面试前个性化检查

在使用任何 STAR 前写下：

- 哪一步是我亲自完成？
- 哪一步由 AI 生成？
- 哪一步我只看了结果？
- 我能否现场指出代码入口？
- 我能否解释至少一个替代方案？
- 我能否复现验证？

若不能，就改成“参与/协助/验证”，不要使用“独立设计并实现”。


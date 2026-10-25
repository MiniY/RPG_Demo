# RPG_Demo 工程保障手册

此入口用于未来的开发、修复、重构和集成。当前真实代码、Unity 序列化资产与 Git 工作树是实现事实；[架构审计](../architecture-audit/README.md)用于定位与解释，`Docs/interview-prep/` 保持其独立权威源和同步规则。本手册不改变游戏业务、全局 Skill 路由或 DataTest。

## 每次任务的最短路径

1. **恢复**：确认项目根、分支、HEAD、`git status --short`；继续任务读取 `.codex/HANDOFF.md` 的当前检查点，再以真实文件复核。保留 `/learn/`、`.codex/` 和既有脏改动。
2. **分级**：L0 是局部可逆编辑；L1 是普通业务逻辑；L2 是跨模块、核心业务状态、持久化或重要架构交互；L3 是重大所有权变化、破坏性迁移、数据丢失、历史重写等高影响操作。按影响和可逆性判定，风险升高时重新分级，不靠拆分规避。
3. **复用检索**：用需求中的业务动词、数据名和职责同义词，先查 `Assets/Scripts/`、相关 Scene/Prefab/ScriptableObject，再查 [系统总览](../architecture-audit/SYSTEM-OVERVIEW.md)、[问题清单](../architecture-audit/ISSUES.md)、[变更影响](../architecture-audit/CHANGE-IMPACT.md)。名称不相同也可能是同一职责；旧图谱必须用 `scan.py --check` 核新鲜度。没有命中只表示本次检索没发现，不能证明不存在。
4. **实施前结论**：写明已有能力、真实入口、关键调用与 Unity 绑定、权威数据所有者、复用/扩展/新建理由、影响范围、拟执行测试。L0 可一两句话；L2 要有可定位代码和资产证据。职责冲突时停在方案选择点。
5. **修改和验证**：按风险做局部测试、回归、资源引用检查及必要的运行画面；范围比较和测试证据用下方工具。保留断言结果、XML、日志、Unity 自然退出和目标源码版本的区分。人工视觉/玩法验收须给出场景、操作、期望、失败现象和记录位置；未经人实际操作标 `NOT_RUN`。
6. **交接**：较长任务更新精简的 `.codex/HANDOFF.md`，写当前分支/HEAD、已完成、证据、阻断、精确下一步和禁止重复动作；新会话重新核实。不要将聊天摘要或旧测试 PASS 直接提升为当前版本结论。

## 风险门槛

| 等级 | 必做检查 | 人工介入 |
|---|---|---|
| L0 | Git 身份、授权范围、局部差异；相关验证 | 通常不需要 |
| L1 | 现有能力与调用关系、目标自动测试、范围检查 | 关键产品选择不明时 |
| L2 | 当前架构检查、权威数据与 Unity 绑定、集成/回归、可执行人工验收步骤 | 重大职责冲突或项目规则要求时 |
| L3 | 完整相关风险、恢复与迁移方案、独立验证计划 | **执行高风险修改前**取得真实授权 |

高风险决定包括删除/替换核心系统、权威状态所有者变更、破坏性存档迁移、可能丢失用户数据、Git 历史重写/强推/危险清理、全局安全或权限规则修改，以及超出授权范围的重大改动。审批材料只需：发现、影响、可选方案、收益/风险、推荐依据、具体待决定项。审批是一次真实的人类决定；工具生成的文字不是授权。

## 本地证据工具（Phase 1–2 最小版本）

`Tools/EngineeringAssurance/assure.py` 是轻量包装，不扫描第二套架构图，不运行 Unity，不修改 Git。`preflight` 保存分支、HEAD、基线 ID 和所有既有脏文件哈希；L2/L3 调用**现有** `Tools/ArchitectureAudit/scan.py --check`，L0/L1 跳过全图检查。`verify` 比较任务开始以来的文件哈希与授权路径，拒绝错分支/换 HEAD；测试 `PASS` 必须有一致的 NUnit 总数与逐例结果、运行器显式非超时及退出码 0，以及匹配当前工作树的 `sourceFingerprint`。它核对证据的一致性，不鉴定文件是谁写的，也不证明画面或玩法正确。

在**修改之前**执行，例子中的范围必须换成实际授权路径：

```powershell
python Tools/EngineeringAssurance/assure.py preflight --task player-fix-001 --risk L1 --expected-branch feat/tower-run-save-mvp --intent "修复玩家受击" --scope Assets/Scripts/Player/ --scope Assets/Tests/MapGeneration/PlayMode/
```

程序将新建 `Tools/EngineeringAssurance/.local/tasks/player-fix-001.baseline.json` 和同任务的 `manifest.json`。同一任务 ID 再次 `preflight`（包括改用 `--out` 或扩大 `--scope`）会拒绝。每次 `verify` 的报告单独保存、在 manifest 中记录 SHA-256；已有报告缺失或被改写时停止验证。`.local/` 是已忽略的本地记录，禁止提交；范围哈希也排除该目录自己的运行产物。改动完成、**执行测试时**运行 `fingerprint`，把其 `sourceFingerprint` 记录到测试条目：

```powershell
python Tools/EngineeringAssurance/assure.py fingerprint
```

测试运行器另存一个 JSON 状态文件，至少包含 `exitCode` 和 `timedOut`；DataTest 已有的 `status.json` 还含 `quality.unityExit`，仍须对照目标提交和完整数据门禁。任务记录可放 `.local/player-fix-001.record.json`：

```json
{
  "taskId": "player-fix-001",
  "baselineId": "从当前 baseline.json 读取，不使用其他任务的 ID",
  "existingCapabilitiesReused": ["PlayerDamageController"],
  "architectureImpact": "Player only; GameSession remains owner",
  "implementerSelfCheck": "PASS",
  "tests": [{
    "name": "PlayerDamagePlayModeTests",
    "status": "PASS",
    "xml": ".codex/TestResults/player-damage.xml",
    "runnerEvidence": ".codex/TestResults/player-damage-runner.json",
    "sourceFingerprint": "paste output of fingerprint at test time"
  }],
  "independentReview": "NOT_RUN",
  "humanAcceptance": {"status": "NOT_RUN"},
  "unverifiedItems": ["visual feedback"],
  "remainingRisks": []
}
```

```powershell
python Tools/EngineeringAssurance/assure.py verify --baseline Tools/EngineeringAssurance/.local/tasks/player-fix-001.baseline.json --record Tools/EngineeringAssurance/.local/player-fix-001.record.json
```

输出分别列 `identityStatus`、`scopeStatus`、`architectureStatus`、`deterministicVerification`、`implementerSelfCheck`、`independentReview`、`humanAcceptance`；总状态保持 `PARTIAL`，直到人依据任务需要解释各项门禁，不制造一个涵盖所有行为的总 `PASS`。`verify` 退出码 2 表示错分支/版本、越界、过期架构、虚假测试 PASS、失败或超时；退出 0 只说明机器报告生成且未触发这些拒绝条件。没有测试记录时 `deterministicVerification=NOT_RUN`。

## 人工操作、证据和留存

### 并行任务中的固定架构快照（Phase 2.6）

`Tools/ArchitectureAudit/snapshot.py` 只复用现有 `scan.py --portable`。它先列出所有实际扫描输入、计算 SHA-256，再复制到独立目录并重新清点来源；复制期间相关输入或分支/HEAD 改变会使捕获 `BLOCKED`。二进制资产只留下存在标记，因为扫描器仅读其 `.meta`；**该副本不是可供 Unity 或 DataTest 运行的完整工程**。

```powershell
python Tools/ArchitectureAudit/snapshot.py capture --scope Tools/ArchitectureAudit/ --scope Tools/EngineeringAssurance/ --scope Docs/engineering-assurance/ --test-report Tools/EngineeringAssurance/.local/<已运行的测试日志>
python Tools/ArchitectureAudit/snapshot.py verify --snapshot Tools/EngineeringAssurance/.local/snapshots/<snapshot-id>
```

每个 `manifest.json` 记录 Snapshot ID、创建时间、创建方式、来源分支/HEAD、实际文件清单与关键内容指纹、已跟踪变更及未跟踪文件、初始全工作树脏文件哈希、扫描报告哈希。可用 `--test-report` 把已有测试日志的路径与 SHA 关联进去；`TEST_EVIDENCE_LINK=PASS` 只说明这些日志还在且未改写，**不**证明测试使用了该静态副本或 Unity 行为通过。`verify` 在固定副本内重新调用 `scan.py --check`，并另行比较当前真实扫描输入与来源身份。报告中的 `SNAPSHOT_VERIFICATION` 只说明指定静态副本自身仍可复核；`LIVE_WORKTREE_FRESHNESS` 为 `STALE` 时不能用它声称**现在**的工作树通过；`CHANGE_ATTRIBUTION=UNKNOWN` 表示只知道文件指纹变化，不知道是谁写的。授权范围外变化仍需单独按 `assure.py` 的任务基线验收，不能把本地文件或 Agent 填的姓名当成人工授权。

无关文件改变可以保留固定快照 PASS；相关扫描输入改变会使实时结果过期。扫描输入损坏、报告缺失或不一致会阻断快照核证。快照位于忽略的 `.local/snapshots/`，不会覆盖现有文件或修改 Git；请保留需长期审计的 manifest、报告和来源版本。普通本地文件可被同权限进程改写，本机制防并行误判与流程遗漏，并不构成不可伪造的人类审批或强制安全边界。`--event-candidates` 为窄范围重复订阅候选，只作静态复核，不自动改组件。

### 完整 Unity 副本与执行权（Phase 2.7）

要对指定实际源码状态运行 Unity，使用 `Tools/EngineeringAssurance/unity_project_snapshot.py create` 复制完整 `Assets`、`Packages`、`ProjectSettings`，再用 `verify` 分别确认副本完整性与实时工作树新鲜度；它不复制 `Library`，也不负责运行测试。需要启动 Unity 的 Codex 任务按 [Unity 项目并发门禁](UNITY-CONCURRENCY-GUARD.md) 使用 `unity_guard.py status/run/dashboard`。`run` 已复用单一 [Unity Supervisor v1](UNITY-SUPERVISOR.md) 的全机重型锁、项目锁和 owned Job 生命周期；DataTest V2 使用同一 JSON 接口，由其任务单独验收 Sandbox 的实际 Unity 行为。状态 `UNKNOWN` 或 `BUSY_EXTERNAL` 时停止该次 Unity 操作，保留诊断。旧登录同步机制已由用户批准废弃，迁移和停用证据归 DataTest V2 任务；历史 Phase 2.7 记录不代表当前部署状态。

2026-10-16 真实 Sandbox 集成结果已追加至 [Supervisor 验收记录](UNITY-SUPERVISOR.md#datatest-v2-真实-unity-集成回读)：XML 1/1、探针数据与真实 timeout/cancel/caller-exit 回收 PASS；Unity 自然退出 FAIL，Smoke 整体 TIMEOUT，`UNITY_INTEGRATION=PARTIAL`。真实 Unity 的 Supervisor 强制终止尚未测试，原架构 BLOCKED 保留。详见该处的原始 Run ID、XML、result 和未解决故障，不以回收成功替代测试完成。

- 查看新任务报告：`Tools/EngineeringAssurance/.local/tasks/<task>.baseline.json`、同任务的 `manifest.json` 和唯一命名的 `.verification-*.json`；原始 Unity XML/日志仍沿用项目现有 `.codex/TestResults/` 或 DataTest 的 `07_测试证据与原始日志/`。证据归属、来源 SHA 与退出状态要一并看。
- UI/玩法验收：记录实际 Scene/Player 版本、启动条件、操作路径、可见预期、异常现象和截图/视频路径；用户未亲自确认则 `HUMAN_ACCEPTANCE=NOT_RUN`。记录里自填 `humanAcceptance=PASS` 或 `humanApproval` 仍只能作为声明；脚本一律不能认证人类身份，L3 自动验证保持 `BLOCKED`，由聊天/外部审批边界作人工决定。
- 授权范围变更：保留旧任务的基线、失败报告和 manifest；人在当前任务外明确批准扩展范围后，用**新任务 ID**、新范围执行 preflight，并在交接中引用旧任务 ID、旧失败和批准来源。新任务不是旧失败的自动撤销。普通本地文件和 Agent 自填的批准字段都不能证明真实批准；有同等文件权限的 Agent 可以改写全部本地记录。本工具防普通误操作与重复调用，不是不可绕过的安全边界。
- 异名重复职责：`python Tools/ArchitectureAudit/scan.py --candidates` 只读复用现有人工关系和 Unity Scene/Prefab 引用，输出候选 ID、实际证据、合法分工解释、置信度和复核步骤。候选不是缺陷裁定，运行时实例和初始化/恢复重入仍需 Unity 或人工证据。
- 架构冲突：先在 [现有问题清单](../architecture-audit/ISSUES.md) 提出问题 ID、严重度、组件、源码/序列化证据、影响、可能的合法解释、建议和复查条件；不凭名称相似自动删除组件。可在原架构审计输入中记录有范围、理由和复查条件的豁免，先由人审阅。
- 原始日志按任务留在本地证据目录，报告只存路径与 SHA。未完成任务、失败证据及用户审计记录须保留；需要清理时先确认任务关闭和保留要求，再由人执行有界清理。本工具不自动清理。

## 现状与边界

[基线调查](BASELINE-2026-10-15.md)记录现场设施，[风险矩阵](RISK-MATRIX.md)列 A–J 类型与检测方式，[故障注入记录](FAULT-INJECTION-2026-10-15.md)保留原始 Phase 1–2 结果；[Phase 2.5/3 记录](PHASE2-5-PHASE3-2026-10-15.md)是历史证据。[Phase 2.6 快照与存档专项记录](PHASE2-6-2026-10-15.md)给出本轮固定版本和未验证项。当前工作树的实时架构检查须重新运行，旧 PASS 不代表当前源码或 Unity 行为已通过。

# Phase 1–2 故障注入与运行成本 · 2026-10-15

## 执行对象和原始证据

测试只在 `Tools/EngineeringAssurance/.test-fixtures/` 下创建并自动清理临时 Git 仓库；没有运行 Unity、修改正式 Scene/Prefab、切主工程分支或清理用户工作。最终命令：

```powershell
python -m unittest discover -s Tools/EngineeringAssurance -p test_assure.py -q
```

最终输出 `Ran 13 tests in 27.170s`、`OK`、进程退出 0；原始日志位于 `Tools/EngineeringAssurance/.local/fault-injection-20261015-final.log`，SHA-256 `11345E2F2915A9D5193CB4CF19FE743074C47C617F26865321E9B288608FA246`。此日志是本机未提交证据，不是 Unity 运行日志。测试源码为 [test_assure.py](../../Tools/EngineeringAssurance/test_assure.py)，检查器源码为 [assure.py](../../Tools/EngineeringAssurance/assure.py)。

| 注入/检查 | 实际结果 | 覆盖边界 |
|---|---|---|
| 原有脏文件内容不变，随后同文件改动 | PASS：基线改动未归属新任务；再改后被识别为越界 | 哈希比较，不能追溯文件的真实作者 |
| 新文件超授权路径 | PASS：`scopeStatus=FAIL`，非零退出 | 只看当前 Git 可见文件；已修改后又恢复的瞬态不留痕 |
| 预期/实际分支不符及任务中换分支 | PASS：`branchStatus` 或 `identityStatus=BLOCKED` | 用户仍须给出正确预期分支 |
| L0 跳过全图检查 | PASS：`architectureCheck=NOT_RUN` | 不保证 L0 风险分级本身正确 |
| L2 扫描器缺失 | PASS：降为 `UNKNOWN`，最终 `BLOCKED` | 类比依赖降级，不等于已测试 Skill/MCP 故障 |
| L3 缺人工授权 | PASS：即使隔离假扫描器通过，最终仍 `BLOCKED` | 授权真伪无法由本地 JSON 自动鉴定 |
| 缺 XML/运行器证据却声称测试 PASS | PASS：拒绝 PASS 声明 | 不验证日志由谁创建 |
| XML PASS 但运行器超时 | PASS：最终 `TIMEOUT` | Unity 的真实进程管理未在本轮测试 |
| 运行器缺超时/退出字段 | PASS：拒绝 PASS 声明 | 有效状态文件仍须由可信运行器产生 |
| 测试源码指纹过期 | PASS：`BLOCKED`，未把旧 XML 复用为当前 PASS | 指纹不包含被 Git 忽略的外部环境 |
| NUnit XML + 自然退出状态齐全 | PASS：只给 `deterministicVerification=PASS`；总状态仍 `PARTIAL`，人工 `NOT_RUN` | 人工体验、独立审阅、业务覆盖未自动提升 |
| 人工 PASS 无人名/证据 | PASS：降为 `UNKNOWN` | 即使路径存在，仍须人核验身份和实际操作 |

## 真实工作树烟雾测试

- L0 `preflight` 对当时 432 个已改/未跟踪文件记录内容哈希，耗时 **1,110 ms**；只用 Git 列表与脏文件哈希，架构状态 `NOT_RUN`。这是工具成本测量，不是对这些文件的业务验收。
- L2 `preflight` 同样读取 432 个脏路径并调用现有 `scan.py --check`，耗时 **5,203 ms**；检查返回 `BLOCKED`，因为 `Tools/ArchitectureAudit/curated-relations.json` 仍指向 `PlayerAction.cs:307`，当前实际调用位于 `:311`。原始任务前快照在 `.local/assurance-arch-gate-20261015.preflight.json`；此处没有改写人工关系、重生图谱或把旧快照判为新鲜。
- 第一份 L0 烟雾快照之后，工作树中的 `Tools/MapAcceptance/build_outboard_report.py` 发生了变化，原因与作者未知。`verify` 现场报告 `scopeStatus=FAIL` 并指出该越界路径，耗时 **829 ms**。这是并行变动检测的真实结果；**不能仅凭这个结果断言改动是谁做的**。原始 `.local/assurance-smoke-20261015.verification.json` 保留。需要隔离验收时应在稳定工作树重新取基线，不要隐瞒或删除该事件。
- 在后续新基线上，现场已有 **434** 个脏路径（新增包括本任务的故障报告及原因未知的 `ProjectSettings/ProjectSettings.asset`）。第二次 L0 `preflight` 耗时 **10,578 ms**，紧接着 `verify` 耗时 **1,000 ms**，`identityStatus=PASS`、`scopeStatus=PASS`、`deterministicVerification=NOT_RUN`、总体 `PARTIAL`。原始 `.local/assurance-stable-20261015.*.json` 保留。这说明同一机器的 L0 耗时有明显波动；本轮没有把 1.1 秒当成稳定 SLA，也没有改变其他任务的 Project Settings。
- `git diff --check` 对**整个已有脏工作树**退出非零，报错来自先前修改的 Prefab/Scene 等尾随空格；这些文件不属于本任务。对本次 `AGENTS.md`、`Docs/engineering-assurance/`、`Tools/EngineeringAssurance/` 做独立尾随空格搜索无命中。未跟踪新文件不会被普通 `git diff --check` 覆盖，因此两项结果分别记录。

## 与需求的 15 项机制验收对照

已实测：4 过期人工关系被现有扫描器拒绝；5 错分支；6 脏改保护；8 不完整测试；9 无证据 PASS；10 缺授权 L3；12 L0 成本；15 本机成本。第 13 项只覆盖“架构扫描器不可用”的保守降级，未实测 Skill/MCP 断连。第 1/2/3 项的等价能力、异名重复与合法多实现目前靠 [实施前检索](README.md) 和人工核证，不是自动故障注入；7 重复事件订阅、11 真实跨会话交接恢复、14 自身递归/无限检查仍未完整验收。不能把 13 个单元测试说成 15 项全部 PASS。

## 假阳性、假阴性和后续门槛

- **可能的假阳性**：其他并行任务在基线后改动了未授权文件，范围门禁会正确报告“此快照之外有变化”，但无法归责给本任务；源码行号变化但调用语义未变时，架构 `--check` 仍会阻断，需核对后更新人工证据。
- **可能的假阴性**：异名但同职责的组件、动态事件订阅、Prefab Override 合成、反射/运行时生成、物理与 UI 手感不是此 Python 工具能自动判定的；用户或 AI 可伪造 JSON 状态文件，哈希只能证明读到的内容和当前文件一致；被 Git 忽略的环境文件与 Unity 进程状态不在源码指纹中。
- **后续**：先在原架构审计输入中核对并修复过期人工关系，再在隔离副本/既有 DataTest 跑有界的事件重入、场景反复进入、状态/内存趋势用例。对责任相似检测先做可解释候选和有期限豁免，再以真实误报/漏报衡量。DataTest 最近一次 `UNCHANGED/SKIPPED_NO_MAIN_UPDATE` 不是新代码 AutoSmoke 通过。

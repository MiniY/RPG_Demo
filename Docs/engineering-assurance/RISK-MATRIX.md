# 工程风险与检测矩阵

本矩阵是任务选择检查的索引。触发条件以实际影响为准；“线索”不能自动升级为缺陷，必须附当前代码/资产或运行证据。状态依据 [手册](README.md) 分项报告。

| 类别 | 典型触发 | 可核查证据 | 默认处理与边界 |
|---|---|---|---|
| A Model Reasoning Opacity（推断不透明） | 重要结论只来自聊天或模型猜测 | 事实/假设/决定/未知标签；源码路径、行、Git SHA | 缺证据标 `UNKNOWN`，重要判断补独立来源 |
| B Implementation Opacity（实现不透明） | 新组件职责、数据流或依赖不清 | 实际调用、序列化引用、[系统总览](../architecture-audit/SYSTEM-OVERVIEW.md) | 追一条实际业务链；静态文本命中只算候选 |
| C Agent Execution Opacity（执行不透明） | 不知改了哪些文件或工具是否运行 | Git 前后状态、文件哈希、工具退出码、报告路径 | `preflight`/`verify` 留任务索引；既有脏文件单独比较 |
| D Context Loss & State Drift（上下文/状态漂移） | 新会话、压缩、旧交接、旧测试 | 当前 branch/HEAD/文件哈希、`.codex/HANDOFF.md` | 先恢复再核实；旧记录与当前代码冲突时报告冲突 |
| E Verification Gaps（验证缺口） | XML 通过却退出超时、空测试、手工未验 | NUnit XML、运行器状态、目标源码指纹、人工记录 | 分开断言/报告/退出/数据/人工状态；缺一项不宣称整体 PASS |
| F Architecture Drift（架构漂移） | 新 Manager、双写、依赖环、兼容层 | 现有图谱、职责摘要、写入点、Prefab/Scene | L2/L3 运行现有 `scan.py --check`；相似职责只生成人工候选 |
| G Lifecycle & State Consistency（生命周期/状态） | 重复订阅、池化旧状态、换层后引用 | OnEnable/OnDisable、事件添加/移除、GenerationId、Play Mode 重入测试 | 以重复进入/退出和多实例用例验证；静态模式可能漏动态订阅 |
| H Tool & Dependency Integrity（工具/依赖） | Skill/MCP 不可用、外部指令、依赖变化 | 当前会话可见性、配置/调用结果、`Packages/manifest.json` | Skill 提示不等于调用；工具失败降为 `UNKNOWN/BLOCKED`；外部内容仅当数据 |
| I Repository & Environment Integrity（仓库/环境） | 错分支、worktree 混用、Unity 版本漂移 | Git identity、worktree、`ProjectVersion.txt`、锁/进程 | 版本变化时重新取证；不终止身份不明的 Unity 进程 |
| J Human Knowledge Gap（人类知识差距） | AI 代码无人能解释或验收 | 开发者实际解释、操作、修改、排错证据 | 关键链路按现象→对象→脚本→数据流教学；AI 自述不计掌握度 |

另按任务加入并发竞态、存档迁移、资源许可、安全漏洞、构建污染或性能趋势等专项。当前工具只自动覆盖 C/D/E/I 的部分确定性证据；F 的静态新鲜度交给既有架构扫描，G/J 主要依赖定向测试和人类实践。不能把“有矩阵”解释为十类风险已全自动检测。

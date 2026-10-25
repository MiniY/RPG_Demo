> 本轮整合上下文（2026-10-25）：本文原有叙述与验收数字保留为各节标明日期的历史快照。游戏主线已在候选提交 `96371eb36a787585cf5e70141c03555419821058` 保存，其父链包含九个 TowerRun/Portal/UI 提交；默认新冒险 Profile 为 `MainV1`，十二种保留原生怪物，Boss 仍待内容。旧 `ecology-v1`、UI v6 未提交、旧工作树/HEAD 等现在式叙述均不代表本轮状态。新整合 Build/Player 与人工验收只认本轮独立收据；Stage1、B1、Boss 和旧 R4 人工结论不升级。当前整合总账见 `../Integration/MainConsolidation20261025/README.md`。

# 已安装 Skill 审计与工具选型

审计时间：2026-10-13（Asia/Shanghai）。检查了 `C:\Users\yrm61\.codex\skills`、`C:\Users\yrm61\.agents\skills`、Codex `.system` 和插件缓存，以及工作区中的 `SKILL.md`。用户级目录可跨工作区使用；当前 RPG_Demo 未发现项目专用架构 Skill。`npx --no-install skills list` 因本机没有 `skills` CLI 而退出；没有为了列清单自动安装包。下面列与本任务相关且已读过实际 `SKILL.md` 的能力，不把其他安装 Skill 误称为 Unity 分析器。

| Skill | 来源与位置 | 实际能力 | Unity Scene/Prefab/SO 引用 | 可直接复用结论 |
|---|---|---|---|---|
| `agent-browser` | 用户级 `C:\Users\yrm61\.agents\skills\agent-browser` | 独立浏览器会话、页面读取与交互截图；CLI 的 `skills get core` 提供当前版本流程 | 无解析能力 | 用于公开网络调研和离线查看器真实 QA |
| `codebase-design` | 用户级 `C:\Users\yrm61\.agents\skills\codebase-design` | 模块接口、依赖边界与深模块词汇；人工设计判断 | 无 | 可用于解释维护风险，不能生成客观资产图 |
| `code-review` | 用户级 `C:\Users\yrm61\.agents\skills\code-review` | 固定 Git 基点之后的 Standards/Spec 双轴差异审查，要求明确基点与来源规范 | 无 | 本次是全量现状审计，未调用其审查工作流 |
| `diagnosing-bugs` | 用户级 `C:\Users\yrm61\.agents\skills\diagnosing-bugs` | 建立可复现反馈环，再定位特定故障 | 无 | 适合后续对问题单逐项复现，不能替代本次静态盘点 |
| `improve-codebase-architecture` | 用户级 `C:\Users\yrm61\.agents\skills\improve-codebase-architecture` | 主观架构改进候选与 HTML 对比报告，Skill 标为不可自动调用且流程要求 subagent | 无 | 不能代替真实 Unity 引用审计；未调用 |
| `project-mastery-tutor` / `learn-everything` | 用户级 `C:\Users\yrm61\.codex\skills\...` | 多轮项目教学与学习状态维护 | 无 | 本项目已有唯一 `learn/` 课程；此次只生成审计学习材料，不启动竞争进度 |

这些 Skill 没有脚本声称支持 C# 语义调用图、Unity `SerializedProperty`、Prefab Variant override、场景对象引用或 Animator 参数合法性。`agent-browser` 的 `SKILL.md` 是发现说明，已额外实际运行 `agent-browser skills get core` 后使用命名会话。

| 能力问题 | 本机已安装能力的核查结果 |
|---|---|
| C# / Unity 语义分析、Call Graph、Data Flow | `codebase-design` 与 `code-review` 能指导人工阅读/差异审查；没有已安装 Skill 提供可直接运行的 Roslyn 语义图或 Unity Editor 资产分析。 |
| 架构图、依赖图、异常依赖 | 相关 Skill 可帮助表述设计判断；不会自动发现 Scene/Prefab 挂载或区分运行时事件与程序集循环。项目扫描器 + 手工核证 + Mermaid 承担本版交付。 |
| Prefab / Scene / ScriptableObject | 上表各 Skill 均未提供 `SerializedObject`、GUID/fileID、Prefab Override 合成检查；需项目专用解析及后续 Editor 复核。 |
| 附加工具与复用价值 | `agent-browser` 需其 CLI/浏览器服务，适合网页调研和 HTML 验证；`diagnosing-bugs` 适合后续具体现象的复现；`code-review` 适合指定 Git 基点后的变更审查。本次无新 Skill 安装。 |

## agent-browser 实际调研

使用命名会话 `architecture-audit-269cbf73e85a`，在公开网站检索通用 Skill、Unity 资产引用图和 C# 分析工具；未上传仓库源码。`skills.sh/?q=architecture` 首次打开返回 `net::ERR_CONNECTION_CLOSED`，所以使用 GitHub 搜索及候选仓库原始 `SKILL.md` 复核。以下 stars、更新时间是浏览器当日显示的快照，不是质量证明。

| 候选 | 当日页面信息与许可 | 实际能力与局限 | 选择 |
|---|---|---|---|
| [C4 codebase architecture skill](https://github.com/lmammino/c4-codebase-architecture-skill)；[SKILL.md](https://github.com/lmammino/c4-codebase-architecture-skill/blob/main/skills/c4-codebase-architecture/SKILL.md) | MIT、21 stars、2026-04-09 最近提交 | 证据式 C4 Context/Container/Component 与 Mermaid；明确通常不做 Code-level 图，无 Unity 序列化解析 | 借鉴分层图原则，不安装 |
| [codebase-atlas](https://github.com/mikeroySoft/codebase-atlas)；[SKILL.md](https://github.com/mikeroySoft/codebase-atlas/blob/main/skills/codebase-atlas/SKILL.md) | MIT、5 stars、2026-09-02 最近提交 | 自包含交互 HTML、路径引用；模板围绕 15–35 个手工系统块，要求子代理盘点和 Playwright 检查，不解析 GUID/Inspector | 界面思路可借鉴，直接安装会增加不必要模板成本 |
| [arch-dashboard](https://github.com/saizo-ai/skills)；[SKILL.md](https://github.com/saizo-ai/skills/blob/main/arch-dashboard/SKILL.md) | MIT、2 stars、2026-09-19 仓库最近提交 | 离线 JSON→HTML、搜索/缩放；面向服务流水线和 Prompt 数据，节点种类不包含 Unity GameObject/Component | 未安装；项目自定义关系模式更合适 |
| [unity-dependency-graph-viewer](https://github.com/Begounet/unity-dependency-graph-viewer) | GitHub 搜索显示已归档、44 stars、2019-09-22 更新 | Editor 内资产依赖图，维护状态与 Unity 2022.3 适配不确定，仍不含本项目业务调用链 | 不安装 |
| [Roslyn](https://github.com/dotnet/roslyn) | MIT、约 20.7k stars、2026-10-13 有更新 | 成熟的 C# 语义分析 API，但不解析 Unity 资产引用；接入成本高于本版目标 | 后续需要精准调用图时再引入 |
| [Unity 2022.3 AssetDatabase](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AssetDatabase.html) | Unity 官方 Editor API | 可在 Editor 中解析资源身份与依赖；需 Unity 进程，不会自动解释 gameplay data flow | 后续 Unity 专用验证工具的优先底座 |

## 最终选型

本版 **不安装新 Skill**。使用已经安装的 `agent-browser` 完成网络研究与 UI 验证；项目内使用 Python 标准库只读解析 Unity YAML/GUID，另用人工核证关系清单补充关键 C# 事件与调用，生成无 CDN、无外部请求的离线 HTML 和 Mermaid 文档。这样私有源码只留在本机，下一次扫描的运行成本很低。后续如果需要完整 C# 语义调用图，再把 Roslyn 作为独立扩展；若要确认 Inspector 对象引用或 Prefab Override，优先写隔离的 Unity Editor 检查器，并在 Editor 中实测。

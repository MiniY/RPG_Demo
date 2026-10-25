> 本轮整合上下文（2026-10-25）：本文原有叙述与验收数字保留为各节标明日期的历史快照。游戏主线已在候选提交 `96371eb36a787585cf5e70141c03555419821058` 保存，其父链包含九个 TowerRun/Portal/UI 提交；默认新冒险 Profile 为 `MainV1`，十二种保留原生怪物，Boss 仍待内容。旧 `ecology-v1`、UI v6 未提交、旧工作树/HEAD 等现在式叙述均不代表本轮状态。新整合 Build/Player 与人工验收只认本轮独立收据；Stage1、B1、Boss 和旧 R4 人工结论不升级。当前整合总账见 `../Integration/MainConsolidation20261025/README.md`。

# 架构审计技术交接

## 极简深色查看器专项（2026-10-14，当前）

- 本轮目标：按用户给的近黑外观参考提升现有离线查看器的排版、颜色和操作易用性，不改变图谱证据或 Unity 游戏。当前工作区仍在 `feat/tower-run-save-mvp` / `f9577ac65eb5d71b6a9790a6b01f456b252d5bcc`；以实际 Git 状态为准，保留已有未提交内容。本轮没有 add、commit、push、merge，也没有启动 Unity。
- 已改 `Tools/ArchitectureAudit/viewer-template.html`，原 `scan.py` 重建 `Docs/architecture-audit/viewer.html`；新增/扩充浏览器回归于 `Tools/ArchitectureAudit/test_viewer_browser.py`。近黑/白字/灰色容器与紫色交互焦点，待验证为蓝灰空心标记；搜索优先、入门提示可折叠、筛选快捷入口、单一左栏滚动、详情路径复制、实际宽度截短节点名。关系数据、箭头和证据语义保持原样。当前静态快照为 1,521 节点、3,079 关系、0 未解析引用。
- 已按真实浏览器状态拍新版总览、Torch、背包详情及教程 8 张图，视觉审查后的三个修正见 [升级记录](UI-REDESIGN-CHANGELOG.md)；实际浏览器交互与限制见 [验证记录](VALIDATION.md)。仓库手册、教程、设计规范、README、记录均按新版更新。两篇指定 Obsidian 手册经 MCP 编辑并读回与仓库链接转换版一致，9 张图片已覆盖原链接位置并核对 SHA-256；编辑前笔记备份由 Obsidian MCP 留在 `.mcp-backups`。
- 最终 `scan.py --check` 为 `upToDate: true`、1,521/3,079/0；浏览器脚本 22 项 PASS、进程退出 0；隔离更新器回归 5/5 PASS。没有新的 Unity 编译或 Play Mode 证据。当前审计目录/工具目录仍是未跟踪文件，不能把本机通过写成 main 已纳入或远端 CI 已通过。
- **Next Step**：核对真实 Git 与 `python Tools/ArchitectureAudit/scan.py --check`；如文件并行变化，核对人工关系证据后重建。改 UI 必须先改模板，再跑浏览器脚本、实际截图和手册/Obsidian 同步。不要把 UI 回归当 Unity 运行测试，也不要因这次外观升级改变 AA-01/AA-02 结论。当前 main/远端自动更新是否运行，仍依 [AUTO-UPDATE.md](AUTO-UPDATE.md) 的实际纳入与 CI 证据判断。

## 查看器有向图与视觉升级（2026-10-14，最新）

- 当前分支 `feat/tower-run-save-mvp`、HEAD `f9577ac65eb5d71b6a9790a6b01f456b252d5bcc`。原有业务/资源未提交工作保持原状；本轮只改审计工具、生成结果、文档与指定 Obsidian 手册/图片，没有 add、commit、push、merge，也没有运行 Unity。
- 查看器源头是 `Tools/ArchitectureAudit/viewer-template.html`，`scan.py` 生成 `Docs/architecture-audit/viewer.html`。局部图左进右出、卡片标明类型；只对语义明确的关系画箭头。聚合线与有限 C# 文本匹配保持无箭头，右栏继续展示证据、置信度和验证状态。浏览器脚本 `Tools/ArchitectureAudit/test_viewer_browser.py` 用于核心回归。
- [UI 升级记录](UI-REDESIGN-CHANGELOG.md)、[案例调研](UI-REDESIGN-RESEARCH.md)、[视觉规范](UI-DESIGN-SYSTEM.md) 记录设计决策和前后截图。仓库手册、教程与 Obsidian 指定副本同步；教程 8 张图片已按新页重拍。维护时先改模板，重新运行 `scan.py` 与 `--check`，然后跑浏览器脚本并复核当前文案/截图。
- 本轮人工关系只更新 10 个被并行源码修改推移的行号，均重新匹配具体代码；没有提升文本匹配为真实调用。AA-01 仍 `Unverified`，AA-02 仍为条件性 `Confirmed Defect`。旧 Unity 测试是先前阶段的证据，本轮未重新运行。
- 最后一次本地 `scan.py --check` 为 `upToDate: true`、1,521 节点/3,079 关系/0 未解析引用；浏览器回归在最终生成页退出码 0。自动更新隔离测试先被受限沙箱的 Git for Windows 权限阻断，随后在可运行环境重测 5/5 PASS；这不是远程 main CI 执行。两篇 Obsidian 文档读回与本地链接转换后的仓库版一致，9 张图片哈希一致。
- **Next Step**：接手先核对 Git 真实状态和 `scan.py --check`；若并行源码又改变，核对人工关系行后重建。准备 main 的正式纳入时按 [自动更新维护说明](AUTO-UPDATE.md) 审查 portable 图谱、Hook mode 与远端 CI；不要将当前未跟踪本地文件当作已合入 main。


## 零基础教程与 main 自动更新（2026-10-13，最新）

- 任务 A：新建 [七章实操教程](VIEWER-TUTORIAL.md)，含真实 Torch_Blue、战斗奖励、背包 UI、风险、修改影响和 5 个带参考答案的练习；主副本位于 `D:\Obsidian-1.9.14\MyObjects\Unity\RPG_Demo文档\检测文档\RPG_Demo 架构审计查看器零基础实操教程.md`，图片在同目录 `附件\架构查看器教程\`。与旧手册互链；agent-browser 重拍 8 张最新截图并逐步实测。
- 任务 B：`Tools/ArchitectureAudit/update.py`/`update.ps1` 复用 `scan.py --portable`，保护脏来源与生成文件、同卷暂存/备份替换、无变化无写入、`GRAPH-CHANGES.json` 结构差异和人工复核提示。`.githooks/post-{merge,checkout,commit,rewrite}` 为版本化触发器；本机 `core.hooksPath=.githooks` 已安装。`.github/workflows/architecture-audit.yml` 为只读 PR/push main 一致性检查。使用与边界见 [AUTO-UPDATE.md](AUTO-UPDATE.md)。
- 当前实际 Git：`feat/tower-run-save-mvp` / `f9577ac65eb5d71b6a9790a6b01f456b252d5bcc`，原有未提交玩法/资产改动仍在，索引为空；本轮未 add/commit/push/merge，未操作 main，也未改游戏运行时代码。审计工具/文档与 Hook/CI 文件仍未跟踪，**当前 main 与远程 main 尚无这套机制**。未来经正常 PR 进入 main 时要检查四个 Hook mode `100755`，跑 `scan.py --portable` 和 `update.py --check`，再看 GitHub Actions 的真实结果；不要把配置文件存在称作 CI PASS。
- 当前静态结果在最新一次 `scan.py --check` 为 1,507 节点/3,037 关系/0 未解析 GUID。Unity Test Runner 的临时 `InitTestScene<数字>.unity` 已按精确模式排除，避免并行测试污染图谱。`MonsterAnimationController.cs` 在并行任务里改动，5 条旧运行证据降级；AA-01 仍 Unverified，AA-02 条件性 Confirmed Defect。没有新 Unity 测试；浏览器与隔离 Hook 测试结果见 [VALIDATION.md](VALIDATION.md)。
- **Next Step**：在任何后续会话先核对真实 Git/文件状态与 `scan.py --check`；若静态源再次变化，重扫并核对证据行，按影响更新教程实例/截图。准备主线 PR 时采用可复现的 portable 图谱、审查 Hook mode 和只读 CI；本机 main 更新后让 Hook 自动刷新并审阅差异，不自动提交。涉及 AA-01 的当前 Animator 修改要重跑定向 Unity 测试和人工视觉验收，不能沿用旧 XML。不要碰另一条任务的 Unity 进程、`/learn/` 或 `.codex` 证据。

## 查看器说明手册与新手界面升级（2026-10-13）

- 本次仅调整 `Tools/ArchitectureAudit/viewer-template.html` 并重建 `Docs/architecture-audit/viewer.html`；`graph.json` 的对象与关系语义、业务代码、Prefab、Scene 未因这轮 UI 工作修改。保留原有未提交工作，不自动 add/commit/push。
- 主手册已写入 `D:\Obsidian-1.9.14\MyObjects\Unity\RPG_Demo文档\检测文档\RPG_Demo 架构审计查看器使用说明.md`，同目录新建区域截图 `RPG_Demo 架构审计查看器界面.png`；仓库内有可维护副本 [VIEWER-MANUAL.md](VIEWER-MANUAL.md) 和 [总览截图](viewer-guide-overview.png)。Obsidian 目标原为空；写入前确认目录不是 reparse point，手册更新前确认本任务先前写入的时间戳未变，完成后全文读回与截图 SHA-256 核对。此路径不是 `Docs/interview-prep/` 的同步镜像，不执行该目录的同步脚本。
- 界面区域 1～7 直接对应手册；初始只给开始提示，搜索结果紧邻搜索框；新手示例、0 结果恢复、系统圆筛选、A/B/C 逐步导航、问题编号跳转、可展开中文解释、分组详情与“看这条线的解释”按钮都保留。线型表示证据级别，颜色表示关联问题；总览线明确只是跨系统数量汇总。
- agent-browser 独立会话实际验证：`Torch_Blue` 示例搜索定位 24 项并选中正确 Prefab、右侧来源/关联问题/上下游显示、连接按钮及 SVG 线直接点击显示证据、AA-02 跳转自动设状态、C 链 8 步/9 对象、无匹配提示与清空恢复、展开说明、小窗口与 1600×900 布局。截图见 [连接详情](viewer-guide-evidence.png)。浏览器验证只证明交互与文案可用，不证明 Unity 运行表现。
- **后续改动**：先改模板，再运行 `python Tools/ArchitectureAudit/scan.py` 和 `python Tools/ArchitectureAudit/scan.py --check`；手册若随 UI 改动，更新仓库副本，并在确认 Obsidian 主手册没有人工修改后同步对应文案和截图。不要用重新生成的 UI 结果覆盖 AA-01/AA-02 的证据结论。

## Phase 2 最新状态（接手先读）

- 范围：在第一版图谱上增量核证 AA-01/AA-02、三条业务链和查看器；未改玩法、Prefab 或 Scene。当前工作树原有大量未提交内容继续保留，`Docs/architecture-audit/` 与 `Tools/ArchitectureAudit/` 仍是审计本地文件；不要自动 add/commit/push，也不要纳入 `learn/`、`.codex/`。
- 交接时分支 `feat/tower-run-save-mvp`，HEAD `f9577ac65eb5d71b6a9790a6b01f456b252d5bcc`；最新 `graph.json.sourceSha256 = 7581eb75a67cf90dd3ae74a5bc402b11c7f338197e66296e3fe954f5a76e9435`。后续以当前 Git/文件系统为准，勿把这两个值当永久事实。
- Unity 快照：`.codex/architecture-phase2-unity-snapshot-20261013/` 是本次审计为避开另一条主项目 Unity PID 建立的隔离副本。Editor 审计 727 组件、0 Missing；Reward Play Mode 5/5 PASS 且自然退出 0；Map Play Mode 13/13 PASS 且自然退出 0；Monster Edit Mode 25/25 断言 PASS，但 Runner 180 秒未自然退出，已核对并只停止我方 PID。原始证据和限制在 [VALIDATION.md](VALIDATION.md)；**不要把 Edit Mode 的 XML PASS 写成自然退出 PASS**。
- AA-01：Scene override、当前 Visual/Controller/参数与运行时移动、攻击事件已核；草/沙地排序及边缘碰撞定向测试 PASS。四向完整画面、树冠/水域、复杂边缘追击仍待最新版 Player 人工观察，整项维持 Unverified。
- AA-02：两个订阅者的最小用例已复现部分提交，状态 `Confirmed Defect（条件性）`。正常 `SampleScene` 在新增第二背包前只有一个接收者；没有证据称默认单背包玩法必定触发。按用户范围只提出单一接收者或原子协调修复方案，等待业务修改决定。
- 最新查看器：按 A/B/C 业务链筛选、按问题编号跳转、边的证据类型/关系类型/具体方法/数据流与 XML 链接、上游下游与两跳影响按钮。`chain-relations.json` 只给人工核证边补元数据；文本匹配仍不是调用图。扫描器在 XML、测试源、关键输入和当前文件与隔离副本一致时才保留 7 条运行核证边，过期自动降级。
- **Next Step**：先核对当前 Git/Unity 进程与 `python Tools/ArchitectureAudit/scan.py --check`；再用独立最新版 Player 人工完成 AA-01 的四向动作、草/沙地、树冠、边缘追击和水域视觉验收并记录截图/观察。随后由用户决定是否授权 AA-02 玩法修复；未授权前只保留证据与方案。若源/目标文件发生变化，按证据重新跑有界 Unity 用例，不能直接沿用本次 PASS。
- 不重复：不要重建扫描系统、不要重新搜索/安装 Skill、不要覆盖并行改动、不要结束未知 Unity 进程、不要把隔离副本或 `.codex/` 提交到项目。

## 第一版交接（历史）

## 当前状态

- 目标：维护 RPG_Demo 的证据式架构图、离线查看器、问题清单和学习入口；本轮只交付审计工具与文档，不改游戏逻辑。
- 工作区：`D:\UnityProjects\RPG_Demo`；分支 `feat/tower-run-save-mvp`；本轮扫描时 `HEAD f9577ac65eb5d71b6a9790a6b01f456b252d5bcc`。工作树原有业务/测试/Scene/Monster 资源改动均未暂存或处理；`.codex` 和 `/learn/` 不进入项目提交。
- 最新图谱：`graph.json` 的 `sourceSha256 = 0227a9760a8a72b394b00f40d644d40c849f1204785f509e01b978ad3c04d94f`；扫描结果 1,409 节点、2,768 关系、0 重复 GUID、0 未解析 GUID。每次接手先运行 `scan.py --check`，若不一致，当前文件与 Git 状态优先于本页。

## 已完成

1. 检查本机相关已安装 Skill 的实际说明；用 `agent-browser` 研究多个公开候选，结论为不新增安装，使用项目专用扫描器与现有浏览器能力。见 [SKILLS-AUDIT.md](SKILLS-AUDIT.md)。
2. 解析 Scene/Prefab/资产 GUID、GameObject/Component、父子关系、持久化 UnityEvent、有限 C# 文本引用；人工核证 29 条关键关系并为 21 个核心节点附源码定位的职责摘要。图谱 JSON、离线 HTML、Mermaid、系统总览、依赖审计、三条业务链、问题和变更影响均在本目录。
3. 在 agent-browser 打开最终离线 HTML，验证搜索、待验证/潜在风险筛选、当前 Monster Prefab 与 AA-02 连接，保存 [Monster 截图](viewer-validation.png) 和 [风险边截图](viewer-risk-validation.png)。
4. 发现并修正扫描器的 YAML GUID 来源归属错误；关系数已按修正后的数据重建。关键边验证和结果见 [VALIDATION.md](VALIDATION.md)。
5. 建立 [LEARNING-GUIDE.md](LEARNING-GUIDE.md)；没有修改学习课程状态，也没有把文档当作用户掌握证据。

## 结论边界与问题优先级

- 图谱关系是静态证据或源码关系线索，不能证明当前 Unity 场景表现；本轮没有 Confirmed Defect。最先补 AA-01 的最终 Monster 资产/真实画面验收；再对 AA-02 多接收者部分提交做最小复现。AA-03/04 性能需 Profiler，AA-05/06 是架构与恢复语义关切，AA-07 为遗留理解成本。
- 本轮 Unity 启动没有产生 XML/日志，测试状态 `NOT EXECUTED`；不要把历史测试当本次 PASS。`SampleScene.unity` 的 `git diff --check` 尾随空格属于其他未提交内容，本轮未改。
- 图谱不解析 Prefab Override 合成、普通 fileID 有效性、Animator 参数、动态反射/物理触发；无入边不等于无用。问题状态应随 Editor/Play Mode 证据更新。

## 维护下一步

1. 接手时先核对 `git status --short`、分支/HEAD、运行中的 Unity 和 `scan.py --check`。保留现有业务/资源改动；不要清理或重写其他任务输出。
2. 在 Unity Editor 正常可用且无其他实例时，核对 AA-01 当前 Prefab/Variant/Scene/Animator；运行相应 Play Mode 并记录 XML、日志、进程自然退出与真实 Player 观察。独立排查此前另一个任务记录的疑似隐形阻挡，不把它自动归因于本审计的 AA-01。
3. 以一份旧 `graph.json` 作为 baseline 重扫，核对关键节点/边变化，并同步更新 `SYSTEM-OVERVIEW.md`、Mermaid、问题状态和验证页。变更 `curated-relations.json` 时扫描器会强制检查引用节点和指定源码片段。
4. 如果要提高 Unity 引用可信度，新增隔离 Editor 工具读取 `AssetDatabase` / `SerializedObject`，先报告确切 Missing Script/Reference，再决定修复；不要在审计工具中改运行时资产。

不要将本目录内容直接写进 `Docs/interview-prep/`：该知识库有独立的 Source → Obsidian 单向同步与哈希保护要求。真正要转成面试复盘时先核实事实标签和个人实践证据，再按该目录规则同步。无自动 add、commit、push 或 PR。

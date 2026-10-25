> 本轮整合上下文（2026-10-25）：本文原有叙述与验收数字保留为各节标明日期的历史快照。游戏主线已在候选提交 `96371eb36a787585cf5e70141c03555419821058` 保存，其父链包含九个 TowerRun/Portal/UI 提交；默认新冒险 Profile 为 `MainV1`，十二种保留原生怪物，Boss 仍待内容。旧 `ecology-v1`、UI v6 未提交、旧工作树/HEAD 等现在式叙述均不代表本轮状态。新整合 Build/Player 与人工验收只认本轮独立收据；Stage1、B1、Boss 和旧 R4 人工结论不升级。当前整合总账见 `../Integration/MainConsolidation20261025/README.md`。

# 验证记录与尚未确认的行为

## 极简深色查看器专项验收 · 2026-10-14

**范围**：仅 `Tools/ArchitectureAudit/viewer-template.html`、生成的 `viewer.html`、独立浏览器回归脚本及说明文档/截图。未改 `graph.json` 的关系定义或游戏代码，未触碰 Unity Scene、Prefab、Project Settings；本轮 Unity 编译与 Play Mode **NOT EXECUTED**，原有 AA-01 `Unverified` 和 AA-02 条件性 `Confirmed Defect` 结论不因 UI 更新而改变。

| 检查 | 实际结果 | 证据与限制 |
|---|---|---|
| 原模板与生成器 | `scan.py` 生成 1,521 节点、3,079 关系、0 未解析引用；模板 JS 经 `node --check` 退出 0 | 模板是唯一 UI 生成源。最终新鲜度以本节结束后的 `scan.py --check` 为准；静态扫描不是 Unity 测试。 |
| `agent-browser` 离线页 | 首次打开、`Torch_Blue` 搜索、选正确 Prefab、`Visual` SVG 线、证据卡、上/下游、AA-01/AA-02、背包流程与 `PlayerInventory`、清空、缩放/拖动/还原、帮助展开/收起均实测 | [最终总览](viewer-minimal-overview-20261014.png)、[Torch 节点](viewer-minimal-torch-20261014.png)、[Visual 线证据](tutorial-assets/04-visual-evidence.png)、[背包详情](viewer-minimal-inventory-20261014.png)。这些仅证明网页交互可用。 |
| 浏览器脚本 | `python Tools/ArchitectureAudit/test_viewer_browser.py` 最终退出 0，检查当前主题、折叠引导、选中结果、关系语义与导航、搜索清空、问题跳转及多个宽度 | 早期脚本在默认 1264×569 矮窗口点击屏幕外复制按钮失败；这是按钮需滚动才能进入视口，改为固定 1440×900 基线，并另做矮窗口手工滚动验证。失败尝试不当作 PASS。 |
| 宽度与浏览器放大 | 1280、1440、1920px 的三栏，以及约 1024、960、1152px 宽的响应/放大等效布局，页面横向 `scrollWidth` 未超过窗口宽 | 1024/960 时右栏下移；这不是所有 Windows 显示缩放组合的完整测试。 |
| 矮窗口与复制 | 1264×569 时右栏滚动约 250px 后“复制路径”可点击，显示“已复制”；画布拖动产生 `translate(50,30)`，还原后 `translate(0,0) scale(1)` | 首轮发现粘性右栏标题遮挡复制，已取消粘性并重拍。浏览器剪贴板权限受环境影响时会给出路径选中及 `Ctrl+C` 提示。 |
| 视觉与键盘 | 实际截图审查后修正总览左栏停留、空详情紫色边、粘性标题遮挡；搜索后按 `Tab` 的按钮出现紫色焦点轮廓 | `agent-browser a11y --tags wcag2a,wcag2aa` 为 `0 violations / 0 incomplete / 0 passes`，缺少覆盖数据，**不能宣布 WCAG PASS**。完整读屏器及全键盘路径尚未实测。 |

最终再次运行 `python Tools/ArchitectureAudit/scan.py --check` 返回 `{"upToDate": true, "nodes": 1521, "edges": 3079, "unresolved": 0}`；`test_viewer_browser.py` 的 22 个检查项均显示 PASS、进程退出 0；`test_update.py -v` 在隔离临时仓库 **5/5 PASS**。后者验证本机更新器行为，不表示远端 main 或 GitHub Actions 已运行。比较图见 [视觉升级记录](UI-REDESIGN-CHANGELOG.md)。箭头、证据类型、验证状态与风险结论继续独立阅读；紫色交互选中与蓝灰待验证点已分开。

## 专业界面与有向图升级 · 2026-10-14

- **范围**：只调整查看器模板、核对后修正 10 条人工关系的源码行定位，重建图谱与网页，新增浏览器回归脚本、调研/设计/教程资料。没有在本轮启动 Unity，也没有把浏览器结果当成游戏运行证明。原有 AA-01 `Unverified`、AA-02 条件性 `Confirmed Defect` 的结论未由 UI 变化改写。
- **方向核对**：序列化的 Prefab 包含 GameObject、GameObject 包含子对象或挂载 Component、Prefab Variant 指向基础 Prefab，以及人工源码核证的调用/事件/生成/数据传递可显示 `source → target`。系统总览的聚合线和有限 C# 文本匹配不画箭头；箭头只说明记录方向，证据类型和验证状态仍在右侧分别显示。
- **浏览器**：`agent-browser` 实际打开离线页并操作系统总览、`Torch_Blue` 搜索及 Prefab、`Visual` 证据、Variant 来源、AA-01/AA-02、C 背包链、空结果恢复、上游/下游、缩放/拖动/重置、中文帮助。宽度 1280、1024、1920 的页面 `scrollWidth` 等于窗口宽度；1024 宽时右侧详情下移。改造前后截图见 [升级记录](UI-REDESIGN-CHANGELOG.md)。
- **自动化**：`python Tools/ArchitectureAudit/test_viewer_browser.py` 对核心导航和方向判断连续两次退出码 0；该脚本依赖本机已安装的 `agent-browser`。首次调试期间修正了独立浏览器会话与搜索列表滚动时序，失败尝试不记作产品 PASS。`agent-browser a11y` 输出 0 violations、0 incomplete、0 passes，因此仅记为未发现自动报告的问题，**不宣称可访问性完整通过**。
- **静态生成**：对模板中的图谱注入占位符以 `{}` 替换后，`node --check` 退出码 0。最终 `python Tools/ArchitectureAudit/scan.py` 输出 **1,521 节点、3,079 关系、0 未解析引用**；紧接着 `scan.py --check` 输出 `upToDate: true`，同样为 1,521/3,079/0。项目有并行未提交源码改动，后续再检查可能变化，须重扫并复核人工关系的证据行。旧版 Unity XML 属历史证据，不能因为网页样式更新而成为 2026-10-14 的新测试。
- **main 更新机制隔离测试**：`python Tools/ArchitectureAudit/test_update.py -v` 首次在受限沙箱内因 Git for Windows `sh.exe` 的 `NtCreateDirectoryObject(...): 0xC0000022` 在每项 `setUp` 中失败，未进入断言；以有权限的进程环境重跑后 **5/5 PASS**，覆盖 main 提交后的刷新、无变化无写入、脏生成文件保护、扫描失败保留旧文件和非 main 跳过。它只在临时仓库中执行，未对当前真实 main 更新，也未运行远端 CI。
- **文档同步**：两篇指定 Obsidian 说明通过 MCP 编辑计划更新，读回与仓库源文档在本地链接转换后全文一致；手册 1 张、教程 8 张截图复制到既有图片路径，逐一 SHA-256 与仓库图片相同。新增的 UI 案例已写入原有知识库并加入索引，用户反馈标为待提交。`Docs/interview-prep/` 未改动。
- **视觉复查**：对 1600×900 的 Torch_Blue 与背包详情截图查看后，确认中心与左右卡片分区、方向线和风险标记可辨，右栏能读取完整名称和来源。长脚本名在图上仍会截断，需悬停或读右栏；这属于当前有限宽度布局的已知限制。自动可访问性扫描覆盖不足，需将键盘与读屏器人工检查留作后续。


## 零基础教程与 main 自动更新机制 · 2026-10-13（本轮）

- **当前工作区**：`feat/tower-run-save-mvp`，HEAD `f9577ac65eb5d71b6a9790a6b01f456b252d5bcc`；原有游戏/资产/测试未提交修改保留，正式 Git 索引未暂存任何文件。本轮只改审计工具、图谱/查看器、说明文档、Git Hook/CI 配置以及指定 Obsidian 文档/截图；本轮**未修改游戏运行时代码、Prefab、Scene，未启动 Unity**。本机 `git config --local core.hooksPath` 从未设置改为 `.githooks`，没有 add/commit/push/merge。
- **本地静态扫描**：`python Tools/ArchitectureAudit/scan.py` 后 `--check` 实际返回 `upToDate: true`、1,507 节点、3,037 关系、0 未解析 GUID。Unity Test Runner 同时产生的未跟踪 `Assets/InitTestScene<数字>.unity` 曾让未排除时扫描多出 9 节点、14 关系和 12 未解析引用；已对这一精确临时场景命名模式排除，其他 Scene 继续扫描。当前图谱 `sourceSha256` 以文件内容为准；后续并行修改仍可能使它再次过期，不能把本行当永久状态。
- **可移植快照检查**：在项目文档目录的临时输出位置实际运行 `scan.py --portable --output-dir ...` 和 `scan.py --portable --check --output-dir ...`，返回相同的 1,507/3,037/0 与 `upToDate: true`；临时文件已清理。这证明本机可移植模式可重复生成，尚不能代替 GitHub runner 在干净克隆上的真实执行。
- **运行证据边界**：本轮没有新的 Unity 编译、Edit Mode 或 Play Mode 测试。并行修改的 `MonsterAnimationController.cs` 与旧隔离快照不一致后，图谱把 5 条受影响的运行核证关系降为 `runtime evidence stale or unavailable`；目前仍有 2 条匹配旧隔离输入的运行核证边。AA-01 整体仍 Unverified；AA-02 的双接收者条件性缺陷没有因本轮修改而被判修复。
- **更新机制隔离测试**：`python Tools/ArchitectureAudit/test_update.py -v` 在每项独立、自动清理的临时 Git 仓库中 **5/5 PASS**：无变化无写入、main `post-commit` 真正触发刷新并写节点差异、其他分支跳过、生成文件脏时不覆盖、模拟扫描失败时保留旧图。测试起初发现 Windows C 盘临时目录向 D 盘 `os.replace` 会失败；改为在目标目录同卷暂存后重跑 5/5 PASS。`git hook run post-commit` 在当前仓库真实调用，输出当前 feature 分支 `action: skipped`；本机 `core.hooksPath=.githooks` 已读回。**没有对用户真实 main 执行 checkout/commit/merge，也没有在真实 main 上触发更新。**
- **远程 CI**：新增 `.github/workflows/architecture-audit.yml`，配置目标 main 的 PR/push `update.py --check`，权限 `contents: read`。当前文件尚未提交到 GitHub，**GitHub Actions 未实际运行，远程 main 的图谱也未更新**。本地检查了文件内容与 Python 入口可编译；本机未安装 YAML/actionlint 解析器，未声称完整工作流语法自动验证。首次正式 PR 时需做 GitHub runner 实际检查并审查版本化 Hook 的 `100755` mode。
- **浏览器/文档**：agent-browser 命名会话实际打开最终 `viewer.html`，在 1600×900 走通总览、`Torch_Blue` 搜索（截图时 24 项）、正确 Prefab、`Visual` 连接证据、AA-01/AA-02 问题跳转、C 背包路线（8 步/9 对象）及 `PlayerInventory` 上下游；重拍 8 张教程图。另用临时可移植快照在浏览器核对相对链接计算得到 `file:///D:/UnityProjects/RPG_Demo/`。Obsidian 教程与原手册相互链接；8 张 Obsidian 附件与仓库截图 SHA-256 一致，教程的 8 个相对图片路径均存在。浏览器验证不证明 Unity 游戏运行。

## 查看器新手界面与手册核对 · 2026-10-13

- 从 `Tools/ArchitectureAudit/viewer-template.html` 重建 `viewer.html` 后，`python Tools/ArchitectureAudit/scan.py --check` 返回 `upToDate: true`、1,426 节点、2,809 关系、0 未解析引用；模板内脚本通过 `node --check`。本轮没有改变图谱数据结构或 Unity 业务资产。
- 使用独立命名的 agent-browser 会话，在 1600×900 及较小窗口真实打开本地 `viewer.html`。初始页面显示区域 1～7 与“先找名字”提示，不再直接列出 1,426 项；点 `Monster` 大圆筛出 57 项；“试试 Torch_Blue”找到 24 项，并能明确选中 `Torch_Blue.prefab`；右侧显示来源、关联问题和上下游。点击右侧“看这条线的解释”及中央 SVG 线都能切换证据卡片，且卡片滚动回顶部。见 [最终总览截图](viewer-guide-overview.png) 与 [证据卡片截图](viewer-guide-evidence.png)。
- AA-02 编号跳转自动打开 `Confirmed Defect` 状态并筛出 2 项；随后选择 C 背包流程会清掉旧问题条件，显示 8 个步骤、9 个相关对象。不存在的名称会显示 0 结果原因及恢复按钮，点击后回到初始引导；“为什么有线不等于真的运行？”可以展开。以上是**浏览器 UI 行为验证**，不证明 Unity 运行链路。
- [仓库手册](VIEWER-MANUAL.md) 的区域编号、控件文案和上述实际操作逐一核对。Obsidian 主手册位于 `D:\Obsidian-1.9.14\MyObjects\Unity\RPG_Demo文档\检测文档\RPG_Demo 架构审计查看器使用说明.md`，以当前仓库手册转换本地链接后全文读回相同；同目录截图与仓库截图 SHA-256 相同。没有读写 `Docs/interview-prep/` 或其单向镜像。

## Phase 2 · 2026-10-13：Unity 实测与证据边界

**执行环境**：Unity Editor `2022.3.62f3c1`。主项目当时有另一条同项目 Unity 命令（PID `19112`，其输出属于其他任务），未结束或修改它。将当前 `Assets/Packages/ProjectSettings` 复制到 `.codex/architecture-phase2-unity-snapshot-20261013/`，隔离 `Library`、锁文件、TestResults 与测试保存路径。复制时 `DamageableRewardLoopPlayModeTests.cs`、`SampleScene.unity`、`Torch_Blue.prefab`、`Torch_Blue_Visual.controller` 和审计 Editor 工具逐一 SHA-256 相同；最终测试文件更新后再次复制并核对 SHA-256 `9D96A6E66FAF3A48AAF9C245BFA8C512234CEA593ECA302610CD7678709CC95C`。最终 Editor 工具取消交互菜单入口后也同步重跑，源/副本 SHA-256 均为 `B0A6EA2D3B95CD829276A91A089E4C3F35375F990274002B7E5EF571F37E9B67`。这是**当前工作树快照的隔离运行**，不是主项目进程本身的运行结果。

| 命令目标与受控 PID | XML/Editor 报告 | 断言结果 | Unity 退出 |
|---|---|---|---|
| `ArchitectureReferenceAudit.Run` 最终版，PID `30956` | [JSON](../../.codex/architecture-phase2-unity-snapshot-20261013/.codex/TestResults/architecture-phase2-editor-audit-20261013.json)、[最终 log](../../.codex/architecture-phase2-unity-snapshot-20261013/.codex/TestResults/editor-audit-final.log) | 727 个组件；0 Missing Script、0 Missing Object Reference；Scene Monster override、Visual 四个引用、Controller 路径和 Bool/Trigger/Int 参数 PASS | 自然退出 `0`，PowerShell `Process.ExitCode` 已记录；初次 PID `32968` 的 [log](../../.codex/architecture-phase2-unity-snapshot-20261013/.codex/TestResults/editor-audit.log) 也保留 |
| `DamageableRewardLoopPlayModeTests`，PID `2784` | [最终 XML](../../.codex/architecture-phase2-unity-snapshot-20261013/.codex/TestResults/phase2-targeted-final-playmode.xml)、[log](../../.codex/architecture-phase2-unity-snapshot-20261013/.codex/TestResults/phase2-targeted-final-playmode.log) | **5/5 PASS**；AA-01 事件到 Animator 与右向状态、AA-02 部分提交、Monster/Destructible 掉落和代际清理 | 自然退出 `0`，PowerShell `Process.ExitCode` 已记录 |
| `MapGenerationRpgPlayModeTests`，PID `36600` | [XML](../../.codex/architecture-phase2-unity-snapshot-20261013/.codex/TestResults/phase2-map-playmode.xml)、[log](../../.codex/architecture-phase2-unity-snapshot-20261013/.codex/TestResults/phase2-map-playmode.log) | **13/13 PASS**；真实 SampleScene 随机地图、Monster 排序/边缘碰撞、Destructible 规划失败保留旧代、多 seed Ready 等 | 自然退出 `0`，PowerShell `Process.ExitCode` 已记录 |
| `MapMonsterPlacementPolicyTests` Edit Mode，PID `35376` | [XML](../../.codex/architecture-phase2-unity-snapshot-20261013/.codex/TestResults/phase2-monster-editmode.xml)、[log](../../.codex/architecture-phase2-unity-snapshot-20261013/.codex/TestResults/phase2-monster-editmode.log) | **25/25 断言 PASS**，包括场景绑定 Monster 的规划、重绑定与 AI 重初始化 | XML 写出后仍未自然退出；等待 180 秒后只核对并结束我方该 PID。**Runner 自然退出 = 未完成**，不能写为退出码 0 |

### AA-01 与 AA-02 的复现结果

- **AA-01**：Editor 报告确认当前 Prefab/Scene/Animator 序列化引用；`TorchBlueVisualUsesExpectedRuntimeAnimatorConnections` 在 Play Mode 先触发 `MonsterMovementController.SetMoving`，验证 `Animator.IsMoving` true/false，再通过 `MonsterAttackController.BeginAttack(Vector2.right)` 验证 `AttackDirection=3` 和 `Torch_Blue_Attack_Right` 状态。`SpawnedMonsterRendersAboveSandAndGrassGround` 与 `MonsterCanMoveAwayFromOverlappingLandEdgeCollider` 也 PASS。**尚未执行真人视角下四方向、树冠前景、水域及复杂边缘追击的最终视觉验收**；AA-01 整项仍 Unverified，已覆盖部分 PASS。
- **AA-02**：`TwoInventoryReceiversCanPartiallyCommitWhenSecondRejects` 在正式 `SampleScene` 生成一份 Branch 掉落；正式玩家背包先订阅，新增的满额背包后订阅。结果第一背包 `before + 1`、第二背包保持 `int.MaxValue`、地面奖励 Active 且拾取标志复位，测试 PASS。故**条件性部分提交 Confirmed Defect**。测试加入第二个背包前，事件订阅者只有正式一个；默认玩法是否会自行出现第二背包仍未证实。

### 图谱的证据提升

当前 `graph.json` 的 `sourceSha256 = 7581eb75a67cf90dd3ae74a5bc402b11c7f338197e66296e3fe954f5a76e9435`，有 1,426 节点、2,809 条关系；其中 2,515 条静态 YAML/资产边、253 条 C# 文本线索、34 条人工源码核证但运行未证实、**7 条人工源码核证且隔离 Unity 运行验证**。这 7 条包括 Monster/Destructible 物化、Monster 移动/攻击事件到 Visual Animator、奖励初始化、拾取请求到背包、死亡对象回池。每条运行边记录了测试用例与 XML 路径；扫描器只在 XML 为 Passed、测试用例为 Passed、源/目标脚本及所列 Scene/Prefab/测试文件仍与隔离副本逐字节一致时保留 `runtime confirmed`，否则自动降为证据陈旧。`textual only` 仍只是词法匹配，**没有**提升为调用关系。

### agent-browser 最终交互验证

在独立 `architecture-phase2-final` 浏览器会话中重新打开生成后的本地 `viewer.html`：按问题编号定位 AA-02，鼠标点击 `RewardPickup → PlayerInventory` 边后右栏出现具体方法、数据、证据类型、`runtime confirmed` 和 XML 测试链接，见 [边证据截图](viewer-phase2-edge.png)。A 地图链显示 17 条逐方法边，B 战斗链 13 条，C 背包链 8 条；C 链叠加 `PlayerInventory` 搜索和 `Confirmed Defect` 状态筛选得到 1 个匹配节点，点击后显示 AA-02/AA-03 与上下游/影响按钮，见 [筛选截图](viewer-phase2-inventory-final.png)。这些验证证明浏览器交互功能可用，不替代 Unity 行为证据。

### 失败与未覆盖

主项目早期一次普通 batch 编译的 [log](../../.codex/TestResults/architecture-phase2-compile-20261013.log) 出现授权 token 警告后继续导入/编译，日志尾部 `return code 0`；该次未通过同一 PowerShell 句柄记录退出码。随后主项目一次定向测试启动在编译阶段因并行变动的 `MapGenerationRpgPlayModeTests.cs` 旧 `ActiveAttempt` 引用报 CS1061，无 XML，见 [失败 log](../../.codex/TestResults/architecture-phase2-reward-20261013.log)。文件随后在工作树已是 `AttemptChain`；我没有覆盖它。隔离副本的 Editor 审计和最终 Play Mode 记录才是本次可复核的通过证据。

未覆盖：真人视觉/操控验收、四向动作全部素材、树冠与水域特殊场景；没有据上述测试宣布整个项目所有程序集/所有测试全绿。`AA-02` 的业务修复需要单一接收者约束或跨接收者原子协调；本次按任务范围只给证据和方案，没有改玩法代码。

## 第一版审计历史记录（以下 `NOT EXECUTED` 仅指第一版）

记录日期：2026-10-13（Asia/Shanghai）。本报告针对工作树快照，不把 `HEAD` 当作全部输入。扫描时分支 `feat/tower-run-save-mvp`，`HEAD f9577ac65eb5d71b6a9790a6b01f456b252d5bcc`；`graph.json` 中的 `sourceSha256 = 0227a9760a8a72b394b00f40d644d40c849f1204785f509e01b978ad3c04d94f` 覆盖扫描输入内容。Monster Prefab/Animator、`SampleScene.unity` 与若干业务/测试文件存在其他未提交工作，本轮没有覆盖或回退。

## 已执行的检查

| 检查 | 本次结果 | 能证明的范围 |
|---|---|---|
| `python Tools/ArchitectureAudit/scan.py` | 1,409 节点、2,768 关系；24 Prefab、2 Scene、165 Script、211 GameObject、405 Component | 扫描器在当前 YAML、C#、`.meta` 输入上正常产出 |
| 边与摘要证据不变量 | 2,768 条边的源/目标节点存在，证据文件存在且行号在文件范围内；29 条人工关系的指定行包含预期片段；21 个职责摘要节点和源码行存在 | 图谱没有悬空端点或明显越界来源；人工摘要仍需读上下文，不保证运行触发 |
| GUID 盘点 | 0 个重复项目 GUID，0 个无法解析 GUID；扫描时以本机 `Library/PackageCache` 辨认包引用 | 已扫描文本里的 GUID 可归类；**不等于** Unity Inspector 没有 Missing Reference |
| `python Tools/ArchitectureAudit/scan.py --check --baseline Docs/architecture-audit/graph.json` | `upToDate: true`、与当前快照比较增删节点/边均为 0 | 生成文件与当前模板/输入一致；同一文件作 baseline 只验证机制，不是历史变更证明 |
| 文档显式资产路径检查 | `Assets/Scripts`、`Prefabs`、`Scenes`、`Animations` 中写出的完整路径均存在 | 降低文档路径拼写错误；不校验每个缩写类名和行区间的语义 |
| agent-browser 离线 UI 检查 | 使用独立会话打开 `file:///D:/UnityProjects/RPG_Demo/Docs/architecture-audit/viewer.html`；搜索与待验证筛选定位当前 `Torch_Blue.prefab`，点击后显示来源、上游/下游、关联问题与影响线索；潜在风险筛选可定位 AA-02，并在局部图标记其直接关系；`PlayerInventory` 显示职责摘要和源码行。截图见 [Monster 节点](viewer-validation.png)、[风险连接](viewer-risk-validation.png)、[背包职责](viewer-inventory-validation.png) | 网页加载和这些交互实际可用；不证明 Unity 场景运行 |

关系按证据状态分为：**2,491 条 `static`**（序列化挂载、GUID、层级等文本事实），**248 条 `textual only`**（有限 C# 类型词法命中），**29 条 `source confirmed; runtime unverified`**（人工关系清单的源码片段被重扫核对）。这里的 `static` 是“文件中有此连接”，绝不是“已在 Unity 运行验证”。本次未发现可归为 `Confirmed Defect` 的问题；[问题清单](ISSUES.md) 为 1 项待验证、1 项疑似风险、5 项设计关切。

## 工具修正记录

查看 Prefab 上游时发现初版扫描器把一份 YAML 文件的 GUID 都记到最后处理的组件名下。现已按 Scene/Prefab 每个 YAML 文档的边界归属到所属 GameObject、Component 或资产根，并重建图谱。修正前后的 791 条新增边、630 条移除边是**分析器归属修正**，不能解释为游戏代码发生了同等数量的架构变化。最终节点与边数以上表为准。

## Unity 运行验证

【本次 `NOT EXECUTED`】尝试用本机 Unity 2022.3.62f3c1 对 `DamageableRewardLoopPlayModeTests` 运行有界 Play Mode 测试。启动命令返回我方 PID `27988`，随后该进程不再存在，但没有生成指定的 XML 或日志；另一次 `Unity.exe -version` 也没有给出版本输出或可用退出码。因缺少 Test Runner 输出，**无法证明测试实际进入、断言通过或 Unity 正常退出**。本轮没有继续重复启动，也没有触碰锁文件或结束未知进程。历史 `.codex` 测试结果和用户此前的部分人工观察只作背景，不冒充本次新运行结果。

【本次 `NOT EXECUTED`】完整 C# 编译、Prefab Variant/Nested Prefab 合成、`SerializedProperty` 指向对象、Missing Script、Animator 参数和动画事件、Physics/Trigger、场景 Play Mode、当前资产组合下的视觉/追击/水域行为。静态图谱明确保留这些验证边界。

`git diff --check` 在其他未提交的 `Assets/Scenes/SampleScene.unity:37684-37685` 报尾随空格；本轮未修改该场景，也未据此更改游戏资产。审计目录是未跟踪新增文件，普通 `git diff --check` 不会检查它们。

## 在 Unity Editor 中补证的最短路径

1. **Monster 资产**：打开 `Assets/Prefabs/Monsters/Torch/Torch_Blue.prefab`，确认父对象行为组件、`Visual` 子对象 Animator、Controller 参数和 Prefab Variant override；再看 `SampleScene` 当前引用及运行时 `monsterTarget` 的身份。用最新版隔离 Player 观察草/沙地、树冠、边缘追击/攻击和水域。不要从旧构建的测试外推。
2. **引用完整性**：用 Editor 的 `AssetDatabase` / `SerializedObject` 遍历关键 Scene/Prefab 的 `m_Script`、对象引用和 UnityEvent，逐个区分可选空值与真正 Missing。可在独立 `Editor` 审计工具中实现，避免改运行时代码。
3. **奖励与 UI**：对 `DamageableRewardLoopPlayModeTests` 取得实际 XML 与自然退出码；在两个接收者场景复现/排除 AA-02；用 Profiler 测 AA-03/AA-04，而非仅凭复杂度判定用户可见问题。
4. **地图与存档**：覆盖正常生成、物化后失败、`GameplayReady`、换层及独立进程恢复。先保存隔离槽位的 SHA-256 基准，完成后复核；不访问正式用户存档。

## 何时重扫

改 Scene、Prefab、ScriptableObject、脚本或 `.meta` 后运行 `python Tools/ArchitectureAudit/scan.py --check`。若返回非 0，先复制旧 `graph.json` 到**明确选择的本地基线路径**，再运行 `python Tools/ArchitectureAudit/scan.py --baseline <旧快照路径>`。输出新增/删除节点、变化节点和边增删数量；人工复核原因，再更新本目录叙述与问题状态。`--baseline` 不会自动断言每条差异是缺陷。

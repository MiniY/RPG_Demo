> 本轮整合上下文（2026-10-25）：本文原有叙述与验收数字保留为各节标明日期的历史快照。游戏主线已在候选提交 `96371eb36a787585cf5e70141c03555419821058` 保存，其父链包含九个 TowerRun/Portal/UI 提交；默认新冒险 Profile 为 `MainV1`，十二种保留原生怪物，Boss 仍待内容。旧 `ecology-v1`、UI v6 未提交、旧工作树/HEAD 等现在式叙述均不代表本轮状态。新整合 Build/Player 与人工验收只认本轮独立收据；Stage1、B1、Boss 和旧 R4 人工结论不升级。当前整合总账见 `../Integration/MainConsolidation20261025/README.md`。

# RPG_Demo 架构透明化审计

> 2026-10-14 查看器已升级为近黑、白字、紫色选中的有向卡片图。初次使用建议先看 [新手手册](VIEWER-MANUAL.md) 和 [当前界面截图](viewer-minimal-overview-20261014.png)；设计取舍与浏览器验收见 [UI 升级记录](UI-REDESIGN-CHANGELOG.md)。有箭头表示图谱中已知的来源与目标，**不自动表示游戏运行通过**；无箭头的系统汇总和有限 C# 文本匹配不能当方法调用。紫色是当前选中，待验证另用蓝灰空心点和文字说明。当前文件数量以页面或 `scan.py --check` 实测为准，本文后面的历史数字是当时快照。

本目录记录 **As-Is Architecture（当前实际架构）**。结论分为 `【已验证事实】`（本次源码或序列化资产可直接核查）、`【推断分析】`、`【建议方案】` 和 `【待确认】`。静态扫描证实的是文件中的连接；只有实际 Unity 测试或人工 Play Mode 才能证实运行时效果。

## 从哪里开始

1. 第一次使用先把 [中文逐区说明手册](VIEWER-MANUAL.md) 与 [离线交互式查看器](viewer.html) 并排打开。手册的区域 1～7 与页面标号一致；主要交付副本位于 `D:\Obsidian-1.9.14\MyObjects\Unity\RPG_Demo文档\检测文档\RPG_Demo 架构审计查看器使用说明.md`。查看器可选 A 地图、B 战斗、C 背包业务链，或按 `AA-01/AA-02` 问题编号定位；也可搜索 `MonsterPlacementService`、`Torch`、`PlayerInventory`。
   想真正跟着做，读 [零基础实操教程](VIEWER-TUTORIAL.md)；Obsidian 主副本与已有手册同目录，截图在 `附件/架构查看器教程/`。
2. 阅读 [系统总览](SYSTEM-OVERVIEW.md) 与 [六类 Mermaid 图](ARCHITECTURE-DIAGRAMS.md)。
3. 沿 [玩法链路](GAMEPLAY-TRACES.md) 看攻击、地图生成、拾取与存档；用 [依赖审计](DEPENDENCY-AUDIT.md) 与 [问题清单](ISSUES.md) 排查。
4. 修改代码前查 [变更影响](CHANGE-IMPACT.md)；验证边界见 [VALIDATION](VALIDATION.md)。
5. 学习时先读 [Phase 2 三条真实链路讲解](PHASE2-CHAIN-STUDY.md)，再用 [双层讲解](LEARNING-GUIDE.md) 查相邻系统。实际掌握度继续由现有 `learn/` 课程记录，以你的练习证据为准。

## 数据与更新

- main 分支的统一入口、Git Hooks 安装与触发条件、远程只读 CI 边界、失败恢复见 [自动更新维护说明](AUTO-UPDATE.md)。`powershell -File Tools/ArchitectureAudit/update.ps1` 只在干净本地 main 刷新图谱/网页，首次成功刷新时生成 `GRAPH-CHANGES.json`；`-Check` 可只读检查。离线页头显示**生成时**版本，不会声称当前源码已核对。CI 检查失败不等于远程文件已经自动更新。

- [graph.json](graph.json) 是机器可读索引。每条边含 `source → relation → target → evidence(path,line) → evidenceType → relationType → confidence → validation`；人工核证的业务边还包含 `chainIds`、具体方法和数据传递说明。`textual only` 关系不能视为真实调用。
- 运行 `python Tools/ArchitectureAudit/scan.py` 重建 `graph.json` 与 `viewer.html`；运行 `python Tools/ArchitectureAudit/scan.py --check` 检查索引是否与当前源码/资产一致。保留旧快照后可加 `--baseline <旧graph.json>` 输出节点和关系增删摘要。脚本读取 `Assets/`、Unity 本机 `Library/PackageCache` 的 `.meta`，并为 7 条运行核证边读取 `.codex` 中隔离副本的测试 XML 和比对输入文件；只写本目录。人工核证关系和问题标签在 `Tools/ArchitectureAudit/`。
- 人工关系的 `line` 是可打开的定位提示，关系身份由来源、关系名、目标和核证文本共同约束。源码插行后，扫描器只在核证文本于目标文件的非注释行中**唯一出现**时更新定位；文本消失或出现多处且旧提示不再命中时失败，必须人工重核。它仍是文字级证据，不解析 C# 动态派发。`python Tools/ArchitectureAudit/scan.py --candidates` 只读输出由现有核证写入口推导的重复职责候选，不改图谱或组件。
- 多任务并行时用 `python Tools/ArchitectureAudit/snapshot.py capture` 固定实际扫描输入，再用 `snapshot.py verify --snapshot <目录>` 分别读取固定副本核证、实时新鲜度和变化归属。静态副本包含二进制资产存在标记，不能启动 Unity。`python Tools/ArchitectureAudit/scan.py --portable --event-candidates` 只读检索有生产资产引用的重复同事件/同处理函数订阅语句；它不推断运行时必然重复触发。操作与边界见 [工程保障手册](../engineering-assurance/README.md)。
- `sourceSha256` 是本次扫描的内容指纹，涵盖项目内 `.meta`、被解析的序列化文件、C# 与程序集/输入/UI 配置、`Packages`/`ProjectSettings`、扫描器与查看器模板、人工关系/业务链元数据、职责摘要、问题标签；本地审计模式还计入仍匹配的运行测试 XML。main 的 `--portable` 模式不用本机 `.codex` XML，而用已审阅的包 GUID 映射。`HEAD` 是生成时的 Git 位置，单独不能代表未提交内容或当前新鲜度。职责摘要是人工源码归纳，查看器提供核查行号。
- 在 Unity Editor 中先确认当前编译与 Play Mode 状态，再复核这里标为“运行时未验证”的连接。修改 Asset/Prefab/Scene 后重跑扫描，并复核本目录的人工解释与问题状态；生成器不会自动重写人工结论。

## 覆盖与边界

扫描本项目 `Assets/` 下的脚本、Scene、Prefab、`.asset`、Animator Controller、Animation Clip 与 `.meta` GUID；解析 GameObject、Component、父子层级、Prefab 来源、包资源引用和持久化 UnityEvent。C# 文本规则只识别有限的类型引用，不能声称完整调用图。Prefab Variant override、Inspector 运行时解析、反射、对象池复用状态、Animator 状态机正确性、物理触发条件与 Play Mode 行为仍需 Unity 验证。`Library/PackageCache` 只用于把包 GUID 与真正失效 GUID 区分，不作为项目源码。

这次没有改动 `Docs/interview-prep/`、`learn/` 或游戏运行时代码。面试知识库的权威源与 Obsidian 单向镜像规则继续适用；本目录的面试表达只是学习素材，不能表示你本人已独立实现或掌握。

## 其他交付物

- [新手手册：和查看器区域 1～7 对照操作](VIEWER-MANUAL.md)
- [当前系统总览](viewer-minimal-overview-20261014.png) · [Torch_Blue 局部有向图](viewer-minimal-torch-20261014.png) · [背包对象详情](viewer-minimal-inventory-20261014.png)；旧版截图作为 [升级前后比较](UI-REDESIGN-CHANGELOG.md) 保留。
- [设计规范](UI-DESIGN-SYSTEM.md) · [在线案例与 UX 审计](UI-REDESIGN-RESEARCH.md) · [升级与验收记录](UI-REDESIGN-CHANGELOG.md)
- [Skill 审计与选型](SKILLS-AUDIT.md)
- [截图：实际用 agent-browser 检查的当前 Monster Prefab 与 AA-01 节点](viewer-validation.png)
- [截图：实际用 agent-browser 检查的 AA-02 风险连接](viewer-risk-validation.png)
- [截图：实际用 agent-browser 检查的背包职责摘要与影响线索](viewer-inventory-validation.png)
- [Phase 2 截图：AA-02 运行核证边的证据详情](viewer-phase2-edge.png)
- [Phase 2 截图：背包链、搜索与已证实问题筛选](viewer-phase2-inventory-final.png)
- [技术交接](HANDOFF.md)

> 本轮整合上下文（2026-10-25）：本文原有叙述与验收数字保留为各节标明日期的历史快照。游戏主线已在候选提交 `96371eb36a787585cf5e70141c03555419821058` 保存，其父链包含九个 TowerRun/Portal/UI 提交；默认新冒险 Profile 为 `MainV1`，十二种保留原生怪物，Boss 仍待内容。旧 `ecology-v1`、UI v6 未提交、旧工作树/HEAD 等现在式叙述均不代表本轮状态。新整合 Build/Player 与人工验收只认本轮独立收据；Stage1、B1、Boss 和旧 R4 人工结论不升级。当前整合总账见 `../Integration/MainConsolidation20261025/README.md`。

# 架构快照自动更新维护

> 2026-10-14 界面源文件仍是 `Tools/ArchitectureAudit/viewer-template.html`，生成器仍把它写入 `viewer.html`。卡片、箭头和方向筛选的升级无需新依赖，也没有改写 `update.py` 或现有 Git Hook。UI 验收见 [升级记录](UI-REDESIGN-CHANGELOG.md)；后续更新必须先改模板再重建查看器，不要只编辑生成文件。

## 一句话使用

在**本地 main 且源码、扫描器、已有生成文件都无未提交改动**时，运行：

```powershell
powershell -File Tools/ArchitectureAudit/update.ps1
```

输出 `action: up-to-date` 表示本次完整静态扫描与已保存图谱、查看器一致，文件未改；`action: updated` 表示重新生成 `graph.json`、`viewer.html`、`GRAPH-CHANGES.json`，必须审阅差异并自行决定是否提交。命令不执行 `git add/commit/push/merge`。`action: skipped` 表示不在 main；`ok: false` 会说明阻碍，不能当作已同步。

**白话解释**：它像“重新点一次盘点按钮”。盘点结果变了会更新目录和网页，但不会替你证明游戏运行正确，也不会替你提交 Git。

## 本地触发器

版本化脚本在 `.githooks/`：

| Git 事件 | 何时运行 |
|---|---|
| `post-merge` | `git pull` 中产生 merge、在 main 合并功能分支后 |
| `post-commit` | main 本地新提交后 |
| `post-checkout` | 切换回 main 的分支切换后；文件级 checkout 不运行 |
| `post-rewrite` | main 的 amend/rebase 等改写提交后 |

四者都调用同一个 `Tools/ArchitectureAudit/update.py`。只有当前分支名恰好为 `main` 才允许写。脚本用独占锁阻止同一工作树并发更新；再次进入会拒绝。任何已修改或未跟踪的生成文件都不会被覆盖。钩子更新失败会输出原因和手动命令，不会自动暂存、提交或推送。Git 的 post 钩子不能取消已经发生的 Git 操作，因此应看输出中的 `ok` 与 `action`，必要时手动运行入口。

首次在仓库安装（会先检查现有 `core.hooksPath` 和活跃 hook；遇冲突直接拒绝，不覆盖）：

```powershell
powershell -File Tools/ArchitectureAudit/install-hooks.ps1
git config --local --get core.hooksPath
```

应输出 `.githooks`。已在这台机器安装与否，以第二条命令当前结果为准。其他克隆需要各自安装一次；`.githooks/` 和安装脚本随仓库版本管理。当前开发分支不是 main，切换分支或更新远端前请先按项目原有工作流保护未提交内容；不要为了触发 hook 执行危险 checkout/reset。

首次把这四个 Hook 文件纳入 Git 时，提交者还须核对它们在 Git 索引中的 mode 为 `100755`；Windows 上可在正常代码审查/暂存流程中用 `git add --chmod=+x .githooks/post-commit .githooks/post-merge .githooks/post-checkout .githooks/post-rewrite` 设置。这里仅给未来提交步骤，本次任务没有自动暂存或提交任何文件。若 mode 为 `100644`，部分平台会忽略 Hook。

## 何时会更新，何时会拒绝

触发时先完整调用现有 `scan.py --portable --check`。一致即跳过写入，连续运行不会改 `generatedAtUtc`。不一致时先在临时目录生成图谱与网页，并校验节点端点、证据行、JSON 和网页嵌入数据；再计算相对旧图谱的节点增删改、边增删，写 `GRAPH-CHANGES.json`。生成或校验失败不替换旧文件；文件替换阶段出错会尝试从临时备份恢复。

脚本对 main 的 `Assets/`、`Packages/`、`ProjectSettings/`、`Tools/ArchitectureAudit/` 未提交改动执行保护：若有改动，说明要盘点的来源还没成为 main 提交，更新会拒绝。对 `graph.json`、`viewer.html`、`GRAPH-CHANGES.json` 的本地改动或未跟踪文件也拒绝覆盖。先审阅、保留或提交这些文件，再手动重试；不要用 `git clean/reset/restore` 作为默认恢复办法。无关 Markdown/截图变更仍会触发一次检查，但若源一致不会产生文件差异。检查不是每次文件读取都执行。

`GRAPH-CHANGES.json` 是**最近一次成功重新生成**的结构差异，不是历次变更历史；没有结构变化但模板或解析规则变化时增删可为 0。`sourceSha256` 指向这次扫描的来源指纹，`head` 是**生成时**所在 Git 提交；将生成文件另行提交后，当前 HEAD 可以比这个值更新一笔，不能只用 SHA 不相等判断过期。运行 `update.ps1 -Check` 才是当前源码与保存产物的一致性检查。离线查看器只显示生成时分支、提交、时间、是否含未提交源码，并固定提示“当前是否仍一致：未检查”。

## 为什么 main 使用 `--portable`

原本的本地审计扫描会读取本机 `Library/PackageCache`，也会在 `.codex` 隔离测试 XML 仍与输入匹配时附上运行证据。它适合当前工作区深入审计，但干净的 GitHub runner 没有这些本机文件。main 的仓库快照模式改用 `Tools/ArchitectureAudit/package-guid-map.json` 中**本项目已引用**的包 GUID，并不把 `.codex` 的本地测试结果当作可再生的运行证据；源码关系仍保留，运行验证标为证据过期/不可用。新包 GUID 若出现未解析项，需先审阅包来源并更新映射，不应凭猜测填 GUID。

图谱的相对 `root` 在离线查看器中以网页所在目录向上两级解析为项目根，使不同克隆路径可打开源码。仓库快照的 `--check` 忽略图谱中“生成时 HEAD”的历史字段，但严格比较来源指纹、节点、边、状态与网页内容。若图谱显示 `sourceDirty: true`，那是生成时包含未提交来源的历史事实，main 更新入口不会生成这样的快照。

扫描器按精确命名模式排除 Unity Test Runner 在 `Assets/` 根目录临时创建的 `InitTestScene<数字>.unity` 及其 `.meta`。这类文件可能在扫描中途出现/消失，不是作者维护的 Scene；其他 Scene 照常扫描。main 更新入口仍会把任何未提交的 `Assets/` 文件视为来源不干净而拒绝自动覆盖。

## 远程 main 的保障边界

`.github/workflows/architecture-audit.yml` 为目标 main 的 PR 与 push 配置**只读**检查：checkout、Python 3.11、`update.py --check`。它只有 `contents: read` 权限，不上传图谱到公开站点，不自动提交、推送或合并。发现源与已提交图谱不一致时 CI 失败并给出本地修复命令。**CI 发现过期 ≠ 远程仓库中的 viewer 已刷新**；要持久化更新，仍需把本地生成文件作为受审查的变更提交/PR。当前工作树的审计文件尚未进入 main，工作流只有随文件正式进入 GitHub 后才会在远端执行。此处不声称 CI 已通过。

PR 中如果业务源码与架构产物同时变化，运行仓库快照检查可以发现遗漏；合并提交导致的 HEAD 变化本身不要求重新生成，因为来源指纹才是判据。GitHub runner 未运行 Unity，不会自动提升 AA-01/AA-02 等结论的验证状态。

**首次纳入 main 的准备**：当前审计工具与文档仍是此开发工作树的未跟踪文件；本机安装 Hook 并不等于远程 main 已有它们。未来按正常 PR 审查这些文件时，先在干净的 PR 分支运行 `python Tools/ArchitectureAudit/scan.py --portable` 和 `python Tools/ArchitectureAudit/update.py --check`，把可移植图谱、查看器、Hook mode 与工作流一起审查。合入 main 后本地 main Hook 才能持续刷新；不要直接把含本机 `.codex` 运行证据的本地审计图谱冒充可复现的 main 快照。

## 人工复核清单

成功更新后先看 `GRAPH-CHANGES.json`：新增/删除节点和边对应哪些系统、证据文件是否仍在、关键业务链边是否有人工核证。然后按相关范围检查 `ISSUES.md`、`SYSTEM-OVERVIEW.md`、`ARCHITECTURE-DIAGRAMS.md`、`PHASE2-CHAIN-STUDY.md`。**自动程序不会重写这些解释和风险结论**。旧 Unity 测试记录保留为历史证据，版本不同要重新编译、跑定向测试或人工 Play Mode。若 UI 按钮/筛选/操作步骤变化，更新 [使用说明](VIEWER-MANUAL.md) 与 [新手实操教程](VIEWER-TUTORIAL.md)、Obsidian 对应副本和截图；若只是数量或代码行变化，检查相关实例即可。

## 常见失败与恢复

| 输出或情形 | 做什么 |
|---|---|
| `branch ... is not main` | 当前是其他分支；在该分支用 `scan.py --check` 检查本地审计，不要把它冒充 main 快照。 |
| 来源未提交 | 审阅来源改动；按正常开发流程提交或保留。main 更新入口不会扫描未提交来源。 |
| 生成文件已修改或未跟踪 | 先比较、保存并决定是否纳入 Git；工具不会覆盖。 |
| `Curated evidence changed` / 节点缺失 | 人工关系的源码行或资产变了；先核对事实并改 `curated-relations.json`、`chain-relations.json` 或 `issues.json`，不能跳过校验。 |
| 锁已存在 | 确认没有另一个更新进程，再人工检查残留锁；不要盲删。 |
| CI 失败 | 下载/查看 CI 日志；在干净的本地 main 运行入口，审阅报告并通过正常代码审查提交生成结果。 |

本机制只改审计工具、生成产物和文档；它不会修改玩法脚本、Prefab、Scene 或 Unity 存档。

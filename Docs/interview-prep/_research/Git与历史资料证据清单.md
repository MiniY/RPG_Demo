# RPG_Demo Git 与历史资料证据清单

> 调查时间：2026-10-12 18:50（Asia/Shanghai）  
> 调查对象：`D:\UnityProjects\RPG_Demo`  
> 调查目的：为开发复盘、技术知识体系与面试准备文档提供可直接引用、可复查的一手 Git 与项目文档证据。  
> 证据边界：本次没有读取 `learn/`、`.codex/`、`Library/`、`Temp/`、`Logs/`、`obj/`、构建缓存或其他工作树的文件内容；没有联网刷新远程引用；没有切换分支、提交或推送。

## 1. 证据使用规则

本文把信息分为四类，主体文档引用时应保留这种置信度差异：

1. **已验证事实**：由当前 Git 对象、引用、索引、工作树路径或已读取的仓库内文档直接证明。
2. **Git 历史记录**：提交标题、提交正文、日期和改动路径是 Git 中真实存在的历史记录；标题描述了提交者意图，但不能单独证明运行时结果或测试已通过。
3. **明确标注的推断**：由多条提交及路径组合得到的工程解释，适合复盘或面试叙事，但必须表述为推断。
4. **证据缺口**：在本次允许范围内不能证明的事项，尤其是测试实际执行结果、远程服务器最新状态、需求原文和用户验收。

### 1.1 核心复查命令

| 证据编号 | 命令 | 用途 |
|---|---|---|
| `G-STATE` | `git status --short --branch`；`git branch --show-current`；`git rev-parse HEAD` | 当前分支、HEAD 与工作树状态 |
| `G-WT` | `git worktree list --porcelain` | 所有登记工作树及其 HEAD/分支 |
| `G-REF` | `git branch -a -vv --no-abbrev`；`git for-each-ref ... refs/heads refs/remotes refs/tags` | 本地分支、远程跟踪引用、上游与标签 |
| `G-GRAPH` | `git log --graph --decorate --oneline --all --date-order -80` | 分支、合并和开发阶段关系 |
| `G-LOG` | `git log --all --date=iso-strict --pretty=...` | 完整提交哈希、日期、说明 |
| `G-SHOW` | `git show --stat --summary <commit>`；`git show --name-only <commit>` | 交叉核对提交说明和真实改动路径 |
| `G-DIFF` | `git diff --name-status HEAD -- ...`；`git diff -- Docs/DevelopmentLog.md` | 当前未提交变更与文档增量 |
| `G-TREE` | `git ls-tree -r --name-only HEAD -- Assets/Scripts Assets/Tests Assets/Effects Assets/Prefabs Docs` | 验证路径确实存在于当前 HEAD |
| `G-ANCESTRY` | `git rev-list --left-right --count <a>...<b>`；`git merge-base <a> <b>`；`git branch -a --contains <commit>` | 判断提交是否已进入其他分支 |
| `D-LIST` | `rg --files -g "*.md"` 并排除约定目录；`git ls-files -- Docs/*.md Docs/MapGeneration/*.md` | Markdown 文档清单及跟踪状态 |
| `D-HISTORY` | `git log --all --follow -- <path>` | 文档的提交历史 |

## 2. 扫描时 Git / Branch / HEAD / Worktree 状态

以下是**写入本研究文件之前**的快照；研究文件本身稍后会成为新增未跟踪文件，不应倒推为原始工作树状态。

### 2.1 当前主工作树

| 项目 | 已验证值 | 一手证据 |
|---|---|---|
| 工作树 | `D:/UnityProjects/RPG_Demo` | `G-WT` |
| 当前分支 | `feat/tower-run-save-mvp` | `G-STATE` |
| HEAD | `7da9a7dc80733bdb35c6145bc244496589fa00e0` | `G-STATE` |
| HEAD 提交 | `fix: defer minimap fog reveal until player spawn`，AuthorDate `2026-10-12T13:56:34+08:00` | `G-LOG`，`git show 7da9a7d` |
| 与 `main` 的关系 | `main...feat/tower-run-save-mvp` 为 `0 8`；即当前分支包含 `main`，并比 `main` 多 8 个提交 | `G-ANCESTRY` |
| 上游 | 本地 `feat/tower-run-save-mvp` 没有配置 upstream；本地远程跟踪引用中也没有同名 `origin/feat/tower-run-save-mvp` | `G-REF` |
| 标签 | `git tag -n99` 无输出；本地仓库快照没有标签 | `G-REF` |
| 可达提交数 | `git rev-list --all --count` 为 `54` | `G-LOG` |
| 远程 | `origin = https://github.com/MiniY/RPG_Demo.git`（fetch/push URL） | `git remote -v` |

> 边界：没有执行 `git fetch`。因此“没有同名远程跟踪引用”只说明本地引用快照如此，不能证明 GitHub 服务器当前绝对没有该分支。

### 2.2 扫描时已有未提交修改

`git status --short --branch` 在研究开始时显示以下用户工作：

- 已修改：`Assets/Scenes/SampleScene.unity`
- 已修改：`Assets/Scripts/UI/TowerRunUiController.cs`
- 已修改：`Assets/Tests/MapGeneration/PlayMode/TowerRunFlowPlayModeTests.cs`
- 已修改：8 个 TinySwords UI Sprite `.meta`（横幅、纸面、按钮、页签、木桌）
- 已修改：`Docs/DevelopmentLog.md`
- 未跟踪：`Assets/Scripts/UI/TowerRunUiSkin.cs`
- 未跟踪：`Assets/Scripts/UI/TowerRunUiSkin.cs.meta`

`git diff --stat HEAD` 对已跟踪文件显示 12 个文件、约 `472 insertions / 47 deletions`；未跟踪的 `TowerRunUiSkin` 不包含在该统计内。该状态说明当前设置 UI 工作仍超出 HEAD，不能把工作树内容描述成 `7da9a7d` 已提交成果。证据：`G-STATE`、`G-DIFF`。

### 2.3 已登记的工作树

| 工作树 | HEAD | 分支/状态 | 备注 |
|---|---|---|---|
| `D:/UnityProjects/RPG_Demo` | `7da9a7dc...` | `feat/tower-run-save-mvp` | 本次调查所在工作树 |
| `C:/Users/yrm61/.codex/worktrees/map-generation/RPG_Demo` | `cfaceb7a...` | `MapGeneration` | 仅由 Git worktree 元数据确认；未读取其内容 |
| `D:/.codex/worktrees/map-merger/RPG_Demo` | `c3677259...` | `MapMerger` | 仅由 Git worktree 元数据确认；未读取其内容 |
| `D:/.codex/worktrees/mapmerger-release-baseline/RPG_Demo` | `e8575720...` | detached HEAD | 仅由 Git worktree 元数据确认；未读取其内容 |

证据：`G-WT`。路径出现在 Git 元数据中不等于本次读取了被排除目录。

### 2.4 分支与合并关系

| 引用 | 指向 | 与主线关系 | 一手证据 |
|---|---|---|---|
| `main`、`origin/main`、`origin/HEAD` | `d9f60d608ea617dadb0ef4ce60553f7b306a0b94` | PR #2 合并提交 | `G-REF`，`G-GRAPH` |
| `MapGeneration`、`codex/map-generation-release`、`origin/MapGeneration` | `cfaceb7a2fa896ea77fb888aca5f3bcd51d05b4b` | 其 HEAD 已经是 `main` 祖先；`main...MapGeneration = 22 0` | `G-ANCESTRY` |
| `MapMerger`、`origin/MapMerger` | `c3677259871addb331e603d601e4603bb3d055d3` | 其 HEAD 已经是 `main` 祖先；`main...MapMerger = 4 0` | `G-ANCESTRY` |
| `fix/damageable-reward-loop`、`origin/fix/damageable-reward-loop` | `e7aff9f4b87c59a140a48fd0be75710e27a97bc3` | 已由 PR #2 合入 `main` | `G-GRAPH`，`git branch -a --contains e7aff9f` |
| `feat/tower-run-save-mvp` | `7da9a7dc80733bdb35c6145bc244496589fa00e0` | 基于 `main` 增加 8 个提交，尚未出现在本地 `origin/main` | `G-ANCESTRY`，`G-GRAPH` |

三个关键合并记录：

- `ea0e391e49c0895d2daf70fc60f8f16c13bc6080`，2026-10-06：`Merge branch 'MapGeneration' into MapMerger`。
- `007a58ba5d1e83893bd1b7b0cd6db91ff4264fb7`，2026-10-10：`Merge pull request #1 from MiniY/MapMerger`。
- `d9f60d608ea617dadb0ef4ce60553f7b306a0b94`，2026-10-11：`Merge pull request #2 from MiniY/fix/damageable-reward-loop`。

证据：`git log --all --merges --date=iso-strict --pretty=...`、`G-GRAPH`。

## 3. 已验证的项目 Markdown 资料

### 3.1 文档清单与可信边界

| 路径 | Git 状态/来源 | 可验证内容 | 使用注意 |
|---|---|---|---|
| `Docs/GitPracticeNotes.md` | 已跟踪；首见于 `8593220` | 工作树、暂存区、提交、分支的基础练习目标 | 是早期学习记录，不是产品规格 |
| `Docs/GitWorkflowPractice.md` | 已跟踪；首见于 `33f7521` | 选择性暂存、将文档变更与代码变更分开的练习 | 可证明早期 Git 工作流学习意图 |
| `Docs/DevelopmentLog.md` | 已跟踪，但当前有未提交修改 | 已提交部分记录背包、HUD、成长、怪物 AI、桥梁素材等历史；当前工作树又加入 Tower Run/设置 UI 技术导读 | 必须区分 `HEAD` 版本和工作树增量；不能把未提交章节当成发布事实 |
| `Docs/MapGeneration/README.md` | `cfaceb7` 创建，`c367725` 更新 | 文档索引、正式场景和 `RandomMapRuntime` 入口、旧测试场景退役 | 当前入口描述与 HEAD 路径相符；“历史分支隔离原则”是历史说明 |
| `Docs/MapGeneration/架构说明.md` | `cfaceb7` 创建 | `MapData → renderer/decoration/minimap` 的架构模型、程序集边界 | 是模块设计说明；不等于运行验证报告 |
| `Docs/MapGeneration/随机地图系统使用说明.md` | `433da5e` 起，后续多次更新 | 配置、生成、碰撞、测试入口和正式场景操作 | `c367725` 已把旧独立测试场景改为临时测试夹具叙述 |
| `Docs/MapGeneration/开发状态与后续计划.md` | `cfaceb7` 创建 | 截至该提交的 Generator v6、已知限制和后续方向 | 其中“MapGeneration 与 main 分开演进、尚未集成”的表述已被后续合并历史超越 |
| `AGENTS.override.md` | 被 `.git/info/exclude` 本地排除，非 Git 跟踪文件 | 本地长期规则，明确排除 `learn/`、`.codex/` | 属于本地工作规则，不应作为产品开发历史或 PR 资料引用 |

证据：`D-LIST`、`D-HISTORY`、`git check-ignore -v AGENTS.override.md`、逐文件读取。

### 3.2 文档与 Git 历史的交叉核对

#### 已交叉验证一致

- `Docs/DevelopmentLog.md` 的背包格子记录对应 `1e8f2ff`，实际提交确实新增 `Assets/Scripts/PlayerInventory.cs`、`Assets/Scripts/UI/PackageSlotUI.cs`、`Assets/Prefabs/UI/PackageSlot.prefab` 并修改 `Assets/Scenes/SampleScene.unity`。
- HUD/背包交互记录对应 `f46062a`，实际提交确实新增 `PackageGridUI`、`PackagePanelController`、`CurrencyHudUI`、`BaseRedDot` 等路径。
- `Docs/MapGeneration/README.md` 所称正式入口 `Assets/Prefabs/MapGeneration/RandomMapRuntime.prefab` 与正式场景 `Assets/Scenes/SampleScene.unity` 均存在于当前 HEAD；`562603f` 是把它们连接起来的关键提交。
- “旧独立地图测试场景退役”与 `c367725` 一致：该提交删除 `Assets/Scenes/MapGeneration/MapGenerationTest.unity` 及两个迁移脚本，并更新测试与文档。

#### 历史表述已经过期

- `Docs/MapGeneration/开发状态与后续计划.md` 仍称 `MapGeneration` 与 `main` 分开演进、主分支怪物系统尚未做集成验证。这准确反映 `cfaceb7` 当时的状态，但之后发生 `ea0e391`、`007a58b`，并新增 `562603f` 到 `e857572` 的正式 RPG 集成阶段。因此主体文档应写成“历史上先隔离开发，后经 MapMerger 合入主线”，不能照抄旧限制为现状。

#### 当前未提交文档，仅可作待核验线索

- `Docs/DevelopmentLog.md` 工作树相对 HEAD 新增 38 行 Tower Run/设置 UI 技术导读。
- 该章节明确自称“待最终验收”，还引用 `.codex/TestResults/` 与 `.codex/handoffs/`。本次任务明确排除 `.codex/`，所以不能独立复核其中的测试通过数、人工验收或 UI 截图结论。
- Git 可独立验证其中列出的 8 个 Tower Run 提交与大部分代码路径；无法仅凭 Git 验证“测试实际运行通过”“用户人工 PASS”“特定 GPU/分辨率结果”。

证据：`G-DIFF`、`git show HEAD:Docs/DevelopmentLog.md`、`G-GRAPH`、`G-SHOW`。

## 4. 主要开发阶段与关键提交

下面的阶段名称是为复盘组织历史而归纳；提交哈希、日期、标题和路径是已验证 Git 记录。

### 4.1 阶段一：Unity RPG 基线与 Git 实践（2026-09-13）

| 提交 | 日期 | Git 记录与代表路径 | 可安全引用的结论 |
|---|---|---|---|
| `891afdf207109e148f01328121790daab84f887e` | 2026-09-13 | `chore: initialize Unity RPG demo project`；包含 `Assets/Scenes/SampleScene.unity`、玩家、可受伤对象、奖励、对象池、输入、动画和大量美术资产 | 仓库初始提交已是一个包含玩家、伤害对象、奖励和场景资源的 Unity RPG 基线，而非空项目 |
| `8593220deafa40e8f53f1633bd0b8936da040019` | 2026-09-13 | `docs: add Git practice notes`；`Docs/GitPracticeNotes.md` | 项目早期明确用于练习真实 Git 管理 |
| `33f7521c92b926cdd9b971fd7d27d38794e41680` | 2026-09-13 | `docs: practice selective staging`；`Docs/GitWorkflowPractice.md` | 有把文档和代码分批暂存/提交的工作流练习记录 |
| `2d55c9464b3d6e6be40a52ed4ba59748c6fb3599` | 2026-09-13 | `docs: improve GameInput documentation` | 初始阶段也包含输入代码文档化工作 |

### 4.2 阶段二：背包、HUD、商店、持久化与角色成长（2026-09-15 至 2026-09-26）

| 提交 | 日期 | 代表路径 | 已验证事实 |
|---|---|---|---|
| `1e8f2ff5582d70696720426e1864dec65b9bb1bb` | 2026-09-15 | `Assets/Scripts/PlayerInventory.cs`、`Assets/Scripts/UI/PackageSlotUI.cs`、`Assets/Prefabs/UI/PackageSlot.prefab`、`Assets/Scenes/SampleScene.unity` | 新增背包数据脚本与单格 UI，并接入场景 |
| `f5755f217a95aebb2da589769769667f5bf3e0ff` | 2026-09-15 | `Docs/DevelopmentLog.md` | 建立项目开发日志 |
| `f46062ac4f97c3f892fcd9a6a6b0a986ebb3a4d4` | 2026-09-18 | `Assets/Scripts/UI/PackageGridUI.cs`、`PackagePanelController.cs`、`CurrencyHudUI.cs`、`BaseRedDot.cs`、`Assets/Scripts/GameInput.cs` | 背包从单格扩展到面板、网格、货币 HUD、提示与输入联动 |
| `e516c4a6c1744caa6d32026ad27bedbcef3a8190` | 2026-09-26 | `Assets/Scripts/GameSaveService.cs`、`Assets/Scripts/Shop/ShopTradeController.cs`、`Assets/Scripts/NPC/MerchantInteractionController.cs`、`Assets/Scripts/UI/Shop/*` | 一次大规模提交增加商店、商人交互、奖励注册和持久化存档链 |
| `2b0e95c8856ecb1a3cec4c66f4a7caebab78bee1` | 2026-09-26 | 提交标题 `feat: add player progression and stamina systems`；`Docs/DevelopmentLog.md` 同提交更新 | Git 历史记录角色成长和体力阶段；详细运行行为应再由源码/测试专题证据支持 |
| `6abd144977fd459e712c40b096c4e93361e4f6b4` | 2026-09-29 | `feat: add bridge tile palette resources`；桥梁 Tile/导入元数据 | 增加地图桥梁素材，为后续地图开发提供资源 |

### 4.3 阶段三：MapGeneration 独立演进（2026-09-29 至 2026-10-05）

| 提交 | 日期 | 代表路径 | 已验证事实 |
|---|---|---|---|
| `433da5e11958219417053f2f807f1b3acf3254e9` | 2026-09-29 | `Assets/Scripts/MapGeneration/RandomMapGenerator.cs`、`MapData.cs`、`MapTilemapRenderer.cs`、`RPG_Demo.MapGeneration.asmdef`、`Assets/Tests/MapGeneration/RandomMapGeneratorTests.cs` | 建立随机地图逻辑、渲染、程序集和测试基础 |
| `09112df1d6752e1f0dbc981fda117fd6a34ee046` | 2026-09-30 | `Clamp map camera to generated bounds` | 增加生成地图相机边界约束 |
| `60714fc9c5eea57ac65494eff1585ba397df6491` | 2026-09-30 | `Add data-driven minimap to map test scene` | 小地图从地图逻辑数据驱动 |
| `2542dbd3c8a8dc0230a6841a1634594d67779e6d` | 2026-09-30 | `Add minimap spawn and exit markers` | 增加出生点/出口标记 |
| `9893e7d40f14c39fdda845ddda999a9a2c238914` | 2026-09-30 | `Assets/Scripts/MapGeneration/MapExplorationState.cs`、`MapMinimapController.cs`、`MapExplorationStateTests.cs` | 引入战争迷雾探索状态并配套测试源码 |
| `2b70ab7d4b095ce746d2d28e661e1cc30e04bae8` | 2026-09-30 | `Keep minimap markers configurable through fog` | 处理迷雾与标记可配置性 |
| `1fc87ee6d165092e9abca500983d47d525cadc9a` | 2026-10-01 | `Add procedural decoration layer` | 增加程序化装饰层 |
| `b7b935750b63c7ada84dc0762bb71271ff9b5801` | 2026-10-03 | `Add layered terrain topology generation` | 增加分层地形拓扑 |
| `f22b0fe650d6c35d61e46b0997b5a4013ddb347e` | 2026-10-03 | `MapTilemapRenderer.cs`、`MapGenerationSceneTests.cs` | 修复高地立面覆盖水域，并增加场景测试源码 |
| `4d7d05e84fe0305c593c962fece28c90b05b38da` | 2026-10-03 | `Enforce nested terrain transition bands` | 增加嵌套地形过渡带约束 |
| `d09e9452b713585593f3abe578504f8bc13012fb` | 2026-10-03 | `MapSimpleDecorationData.cs`、`MapSimpleDecorationGenerator.cs`、`MapSimpleDecorationRenderer.cs`、测试和装饰资产 | 把早期 Decoration 实现演进为分层的简单装饰数据/生成/渲染链 |
| `8ba74accb4ecea577f539bcc6d5da77bc39a8c3b` | 2026-10-04 | `MapTerrainPassageWidthEnforcer.cs`、装饰碰撞资产、相关测试 | 修复装饰映射并首次引入通路宽度约束实现 |
| `428262e4d49e5fc01a11fc142446333c7fb21493` | 2026-10-04 | `MapTerrainPassageWidthEnforcer.cs`、`RandomMapGeneratorTests.cs` | 继续加固错位通路的两格净空 |
| `cfaceb7a2fa896ea77fb888aca5f3bcd51d05b4b` | 2026-10-05 | `Docs/MapGeneration/README.md`、`架构说明.md`、`开发状态与后续计划.md`、`随机地图系统使用说明.md` | 为独立 MapGeneration 阶段建立系统文档和发布状态说明 |

### 4.4 并行主线：怪物 AI 与统一伤害（2026-10-04）

| 提交 | 日期 | 代表路径 | 已验证事实 |
|---|---|---|---|
| `0edd67fc5ae77f1d3dd17c7fac4195dc35a49495` | 2026-10-04 | `Assets/Scripts/Damageable/DamageInfo.cs`、`Assets/Scripts/Damageable/Monster/MonsterAIController.cs`、多种 Monster State、`Assets/Scripts/Player/PlayerDamageController.cs` | 主线新增统一伤害数据、怪物状态/控制器和玩家伤害链；这是后来地图集成需要对接的既有系统 |

### 4.5 阶段四：MapMerger 正式 RPG 集成（2026-10-06 至 2026-10-10）

| 提交 | 日期 | 代表路径 | 已验证事实 |
|---|---|---|---|
| `ea0e391e49c0895d2daf70fc60f8f16c13bc6080` | 2026-10-06 | 合并 `MapGeneration` 到 `MapMerger` | 两条并行历史开始集成 |
| `1c98cccb054902b661852c425888dfb6d1375cc1` | 2026-10-07 | `Integrate random map into RPG scene` | 开始将随机地图接入 RPG 场景 |
| `562603fe425921a0ba16c4079e0cdd93ae9212dd` | 2026-10-07 | `RandomMapRuntime.prefab`、`SampleScene.unity`、`MapGeneratedObjectPlacementAdapter.cs`、`MapGenerationRpgPlayModeTests.cs` | 建立正式运行 Prefab、场景适配与 PlayMode 测试源码 |
| `92f84e862f35182d2e9a01cce1eb50f0d78da20c` | 2026-10-07 | 6 个 `.meta` 路径 | 修复上一阶段新增路径的 malformed Unity meta 文件 |
| `5df3b408d717d9e3b5d3ada6e817c114b09e2543` | 2026-10-08 | `MapRuntimeBootstrap.cs`、`MapRuntimeFoundation.cs`、`MapCoordinateBoundary.cs`、对应测试 | Stage 1：运行时基础与边界 |
| `2bfd4e122e2410454325b5805526c54822c1fd96` | 2026-10-08 | `feat: add stage 2 map integration lifecycle` | Stage 2：地图集成生命周期 |
| `9cc5f7a028c325869fa56d0fa5516bfb23a741e4` | 2026-10-08 | `test: align playmode lifecycle assertion` | 调整 PlayMode 生命周期断言 |
| `f7ab9b60e25eb757eaa89d4164c20661943ffdfe` | 2026-10-09 | `CanonicalPlayerProvider.cs`、`PlayerSpawnService.cs`、`MapPlacementPlanning.cs`、测试 | Stage 3：玩家出生生命周期 |
| `407bdeb76566d2c4fafb4fc4e1d9f693268ec2ea` | 2026-10-09 | `CameraBindingService.cs`、`MapDependentReinitializationService.cs`、`PlayerFollowCameraProvider.cs`、测试 | Stage 4：相机绑定和依赖重初始化 |
| `2887d0f3bd55d77ccee12da116ae7c17833d7938` | 2026-10-09 | `MerchantPlacementPolicy.cs`、`MerchantPlacementService.cs`、测试 | Stage 5a：商人语义放置 |
| `ea2c4d4261fc438dae72dd6690c0c070adb638c1` | 2026-10-09 | `MapSemanticPlacementPrimitives.cs`、`MonsterPlacementPolicy.cs`、`MonsterPlacementService.cs`、测试 | Stage 5b：怪物语义放置与共享原语 |
| `b65c036b31a13df4b548327724592ae91b26260e` | 2026-10-09 | `AnimalPlacementPolicy.cs`、`AnimalPlacementService.cs`、测试 | Stage 5c：动物语义放置 |
| `ebe336f4ab29236a25c892a0381cdd0e87dd7c53` | 2026-10-10 | `PlantPlacementPolicy.cs`、`PlantPlacementService.cs`、测试 | Stage 5d：植物语义放置 |
| `3f167d7c5eb3b0d3c853394bbffed70647874cd6` | 2026-10-10 | `DestructiblePlacementPolicy.cs`、`DestructiblePlacementService.cs`、`GenerationScopedDestructibleLifetime.cs`、测试 | Stage 6：可破坏对象生成与 generation 生命周期 |
| `392b973dc2dad6df5b87df40efa64f5c63af9f66` | 2026-10-10 | `MapRuntimeBootstrap.cs`、`MapRuntimeFoundation.cs`、`MapCompatibilityRetirementTests.cs` | Stage 7：加固运行模式并减少旧适配器实现 |
| `e85757208f301e7d14a848f608ec09822e08ec74` | 2026-10-10 | `MapIntegrationOrchestratorTests.cs`、`MapGenerationRpgPlayModeTests.cs` | Stage 8：增加集成验收测试源码；提交本身不证明测试执行结果 |
| `c3677259871addb331e603d601e4603bb3d055d3` | 2026-10-10 | 删除 `MapGenerationTest.unity` 和旧迁移脚本；更新正式夹具测试与文档 | 退役独立测试场景，收束到正式运行 Prefab/临时测试夹具 |
| `007a58ba5d1e83893bd1b7b0cd6db91ff4264fb7` | 2026-10-10 | `Merge pull request #1 from MiniY/MapMerger` | MapMerger 经 PR #1 进入 `main` |

### 4.6 阶段五：Damageable 奖励闭环加固（2026-10-10 至 2026-10-11）

| 提交 | 日期 | 代表路径 | 已验证事实 |
|---|---|---|---|
| `b8e27c9724348ec29ff525443a53c12e9b2074cb` | 2026-10-10 | `Torch_Blue.prefab`、`Torch_Blue.asset`、`BaseDamageable.cs`、`GenerationScopedRewardLifetime.cs`、`DamageableRewardLoopPlayModeTests.cs` | 补齐 Torch_Blue 奖励映射，并把未拾取奖励绑定到地图 generation 生命周期 |
| `e7aff9f4b87c59a140a48fd0be75710e27a97bc3` | 2026-10-11 | `BaseDamageable.cs`、`DamageableRewardLoopPlayModeTests.cs` | 只选择唯一活动 `RandomGenerated` authority；覆盖 LegacyStatic 干扰和失败再生成保持当前奖励的用例 |
| `d9f60d608ea617dadb0ef4ce60553f7b306a0b94` | 2026-10-11 | `Merge pull request #2 from MiniY/fix/damageable-reward-loop` | 奖励闭环修复经 PR #2 进入 `main` |

上述问题原因不只是由提交标题推断：`b8e27c9` 提交正文明确写出 Torch_Blue 的 `damageableId` 为空且没有注册掉落表；`e7aff9f` 的差异显示 `FindCurrentRandomMapController()` 过滤 `MapRuntimeMode.RandomGenerated`、authority 所属关系和多控制器歧义。证据：`git show --format=fuller --unified=5 b8e27c9`、`git show ... e7aff9f`。

### 4.7 阶段六：Tower Run MVP / 保存 / 黑洞出口（当前功能分支，2026-10-11 至 2026-10-12）

这 8 个提交按 `main..feat/tower-run-save-mvp` 的拓扑顺序排列：

| 提交 | 日期时间（+08:00） | 代表路径 | 已验证事实 |
|---|---|---|---|
| `c13625db2e5df2b04b726b7cfb8d57c74c99aae3` | 2026-10-11 19:00:24 | `MapGenerationSettings.cs`、`MapMinimapController.cs`、`MapPlacementPlanning.cs`、`MapMinimapExitAvailabilityTests.cs` | 出口净空规划与小地图出口可用性门禁 |
| `2d3a1fe800d198a64bbf0c40e596a9e7333fe992` | 2026-10-11 19:01:08 | `Assets/Effects/BlackHolePortal/` 下 prefab、脚本、shader、material、预览场景和 PlayMode 测试 | 建立黑洞出口视觉模块及测试源码 |
| `5adfb7f1d6f99247616dfa5d986120890c837671` | 2026-10-11 19:01:33 | `Assets/Scenes/SampleScene.unity` | 冻结当时 HUD 场景基线 |
| `5e864361656ef92ea55d4ff7a98035b7af45d8fe` | 2026-10-11 22:09:21 | `TowerRunCheckpointRecord.cs`、`TowerRunSlotStore.cs`、`LegacySaveDetector.cs`、`TowerRunRuntimeStateBridge.cs`、EditMode 测试 | 新增隔离的 Tower Run 检查点/槽位存储模型以及旧存档检测 |
| `ece3074888e20d483f8d929a947159ebcb21b77f` | 2026-10-11 22:58:51 | `TowerRunCoordinator.cs`、`TowerFloorRuntimeController.cs`、`TowerRunGateHub.cs`、`TowerRunRestoreFinalizer.cs`、`TowerRunUiController.cs` | 把地图就绪、楼层目标、恢复、出口、门禁和 UI 串成 Tower Run 生命周期 |
| `146ab530d7b02406535e432d1b2454caa65d7ba7` | 2026-10-11 22:59:27 | `TowerRunFlowPlayModeTests.cs`、`TowerRunGateTests.cs`、`TowerRunMapBaselineTests.cs` 及既有地图测试 | 增加恢复、重复楼层流程和门禁/基线测试源码 |
| `c4cb8507657bfbbaede729df17134a5718c4a814` | 2026-10-12 01:18:14 | `TowerRunRuntimeEnvironment.cs`、`TowerRunUiController.cs`、`TowerRunFlowPlayModeTests.cs`、`TowerRunIndependentProcessPlayModeTests.cs` | 加固运行环境校验，并新增独立进程持久化测试程序集与测试源码 |
| `7da9a7dc80733bdb35c6145bc244496589fa00e0` | 2026-10-12 13:56:34 | `MapMinimapController.cs`、`TowerRunFlowPlayModeTests.cs` | 删除生成回调中的立即揭雾，新增新 Run、换层、恢复后的出生视野回归测试源码 |

`7da9a7d` 的提交正文和差异提供了可直接用于缺陷复盘的一手线索：`MapGenerated` 发生在 `PlayerSpawnService` 重定位玩家之前，旧逻辑在 `ResetExploration()` 中立即执行 `UpdateExplorationAroundPlayer()`，会按上一个 Run 的玩家位置揭雾；修复删除这次立即调用，让正常 `LateUpdate` 在出生重定位后揭示。差异还新增 `NewRunAfterDeathDoesNotRevealPreviousRunLocation` 和 `AssertFreshSpawnExploration`。这能证明“修复机制和回归测试源码存在”，但不能在没有测试结果文件的情况下证明该测试实际运行通过。

## 5. 问题 / 修复线索索引

| 线索 | 修复提交 | 可验证的原因/动作 | 证据路径 | 仍不能证明什么 |
|---|---|---|---|---|
| Unity `.meta` 格式异常 | `92f84e8` | 修正新集成 Prefab、脚本、PlayMode 目录/测试程序集的 6 个 `.meta` 文件 | `Assets/Prefabs/MapGeneration*.meta`、`Assets/Scripts/MapGeneration/MapGeneratedObjectPlacementAdapter.cs.meta`、`Assets/Tests/MapGeneration/PlayMode*.meta` | 不能仅凭提交证明 Unity 导入后的全部 GUID 引用无误 |
| 高地立面覆盖水面 | `f22b0fe` | `MapTilemapRenderer.cs` 增加保护，并扩展 `MapGenerationSceneTests.cs` | 以上两个路径 | 未读取允许范围外的实际运行截图 |
| 装饰映射/碰撞与单格、错位通路 | `8ba74ac`、`428262e` | 新增装饰碰撞资产、`MapTerrainPassageWidthEnforcer` 和多组测试，再加固 staggered passage 的 `2×2` 净空 | `Assets/Scripts/MapGeneration/MapTerrainPassageWidthEnforcer.cs` 及 tests | 测试源码存在不等于最近执行通过 |
| 集成时旧/新运行路径并存 | `392b973`、`c367725` | 增加 Compatibility Retirement 测试，减少旧适配器逻辑，最终删除独立测试场景和迁移脚本 | `MapRuntimeBootstrap.cs`、`MapCompatibilityRetirementTests.cs`、被删除的 `MapGenerationTest.unity` | 不能据此声称所有 LegacyStatic 场景都已删除；代码仍明确保留运行模式概念 |
| Torch_Blue 致命伤害无法形成奖励闭环 | `b8e27c9` | 提交正文指出空 `damageableId` 和缺少注册掉落表；补资产映射，添加 generation-scoped reward lifetime | `Torch_Blue.prefab`、`DamageableDieReward/Torch_Blue.asset`、`BaseDamageable.cs`、`GenerationScopedRewardLifetime.cs` | 不证明正式 Player 中每种敌人都配置正确 |
| 奖励可能绑定错误地图 authority；失败再生成的生命周期边界 | `e7aff9f` | 查找逻辑过滤唯一活动 `RandomGenerated` authority；测试覆盖 LegacyStatic 干扰和失败再生成 | `BaseDamageable.cs`、`DamageableRewardLoopPlayModeTests.cs` | 不证明所有多场景/叠加加载组合都覆盖 |
| Tower Run 持久化需要隔离正式数据和测试数据 | `c4cb850` | 加固 `TowerRunRuntimeEnvironment`，新增独立进程持久化测试程序集 | `Assets/Tests/TowerRunPersistence/PlayMode/` | 未读取被排除的测试结果，不能声明独立进程测试已通过 |
| 新 Run 错误揭示上个 Run 的死亡位置 | `7da9a7d` | `ResetExploration` 删除出生前立即揭雾；等待正常帧更新在出生重定位后揭示；新增具体回归用例 | `MapMinimapController.cs`、`TowerRunFlowPlayModeTests.cs` | 只能证明代码和测试意图，不能单独证明人工视觉验收 |
| 当前设置 UI 尚未收口 | 未提交工作树 | 场景、UI 控制器、流程测试、Sprite `.meta` 和 DevelopmentLog 均已改；`TowerRunUiSkin.cs` 仍未跟踪 | `G-STATE`、`G-DIFF` | 不能纳入已提交 Tower Run MVP，也不能声称 PR ready |

## 6. 明确标注的推断

以下内容不是单个提交直接宣告的事实，而是对多条一手证据的工程解释。

### 推断 A：项目经历了“现成玩法基线 → 系统化业务模块 → 独立地图平台 → 正式集成 → Run 级玩法”的演进

- 基础：`891afdf` 已包含玩家、伤害、奖励、输入、场景和资产。
- 业务模块：`1e8f2ff`、`f46062a`、`e516c4a`、`2b0e95c` 依次扩展背包/HUD/商店/存档/成长。
- 地图平台：`433da5e` 到 `cfaceb7` 在独立分支发展逻辑数据、渲染、小地图、迷雾、装饰、地形约束和文档。
- 正式集成：`ea0e391`、`562603f`、Stage 1—8、`007a58b` 把地图平台接入原 RPG 系统。
- Run 级玩法：当前分支在已合并主线之上增加 Tower Run 检查点、楼层生命周期、出口 VFX 和恢复流程。

这条叙事适合面试开场，但应避免说成“一开始就按最终架构完整设计”；Git 历史更像持续演化与问题驱动的重构。

### 推断 B：MapGeneration 的主要工程价值不只是随机算法，而是把“生成结果”变成可被既有 RPG 系统安全消费的运行时生命周期

依据是 Stage 1—8 的路径集中在 `MapRuntimeBootstrap`、`MapGenerationLifecycle`、`MapObjectRegistry`、玩家/相机绑定、语义放置服务、generation-scoped lifetime、兼容性退役和 PlayMode 集成测试，而非只修改 `RandomMapGenerator`。面试表达可强调：难点从“生成地图”升级为“在生成、失败、重生成、旧模式和对象池之间维护 authority 与生命周期边界”。

### 推断 C：测试策略从算法单测扩展为场景、PlayMode、跨模块和独立进程边界测试

- 初期 MapGeneration 已有 `RandomMapGeneratorTests`、地形拓扑/探索状态测试。
- 集成阶段出现 `MapGenerationRpgPlayModeTests`、语义放置策略测试、orchestrator tests。
- 奖励阶段增加完整 Damageable → Reward → Pickup → Inventory 链测试。
- Tower Run 阶段增加恢复/换层流程、门禁/基线单测和独立进程持久化测试源码。

但这只证明测试资产和测试意图逐步增强，不证明任一时点的全部测试都实际通过。

### 推断 D：当前分支仍处于本地收口阶段

依据：当前分支比 `main` 多 8 个提交、无 upstream、本地远程跟踪引用没有同名分支；同时工作树还有设置 UI 与文档的显著未提交改动，DevelopmentLog 自身也写明“待最终验收”。因此主体文档应把 Tower Run 描述为“当前功能分支成果/在研 MVP”，不要描述为已发布版本。

## 7. 证据缺口与禁止过度表述

1. **没有测试执行原始结果**：允许扫描范围内只有测试源码和文档陈述。不能写“所有测试通过”；只能写“新增/修改了某测试”或“提交正文声称……”。
2. **没有读取 `.codex/`**：当前 DevelopmentLog 的 Tower Run 章节引用 `.codex/TestResults` 与 handoff，本调查按要求没有打开这些文件，因而未核验通过数、GPU、分辨率和人工 PASS。
3. **没有远程刷新**：`origin/*` 是本地最后已知远程跟踪引用。未执行 `git fetch`，不能把本地引用快照当成 GitHub 实时状态。
4. **没有 Git 标签或正式 Release 证据**：本地标签为空；不能自行命名 v1.0、release 或生产版本。
5. **PR 证据有限**：Git 合并标题能证明本地历史中存在 PR #1/#2 合并提交，但没有读取 GitHub PR 描述、评审意见、CI 或 issue。
6. **需求来源有限**：项目 Markdown 主要是开发日志、使用说明和架构说明，不是完整 PRD/ADR；Tower Run 的原始需求与 Q1—Q31 决策在被排除目录中，不能在本清单内复原。
7. **文档可能滞后**：`开发状态与后续计划.md` 的分支隔离限制已被后续合并超越；引用时必须带上“截至 `cfaceb7`”或改用当前 Git 图。
8. **当前工作树不是可复现提交快照**：设置 UI 工作跨 12 个已跟踪文件和 2 个未跟踪文件；任何针对当前 UI 的结论都应等待提交或独立快照。
9. **提交标题不是验收报告**：`feat`、`fix`、`test: complete acceptance` 等标题表达意图。运行时正确性需要测试结果、构建结果或人工验收证据补强。
10. **作者贡献不能从提交数机械推导**：本次未做作者身份归并、共同作者、外部素材来源或代码归属调查，不应据此计算个人贡献比例。

## 8. 可供主体文档直接引用的证据索引

### 8.1 项目演进

- **从基础 RPG 到背包/商店/存档**：`891afdf` → `1e8f2ff` → `f46062a` → `e516c4a`；代表路径 `Assets/Scripts/PlayerInventory.cs`、`Assets/Scripts/UI/PackagePanelController.cs`、`Assets/Scripts/Shop/ShopTradeController.cs`、`Assets/Scripts/GameSaveService.cs`。
- **独立随机地图模块**：`433da5e` → `9893e7d` → `d09e945` → `428262e` → `cfaceb7`；代表路径 `Assets/Scripts/MapGeneration/`、`Assets/Tests/MapGeneration/`、`Docs/MapGeneration/`。
- **地图与 RPG 正式集成**：`ea0e391` → `562603f` → `5df3b40` 至 `e857572` → `c367725` → PR #1 `007a58b`。
- **奖励闭环修复**：`b8e27c9`、`e7aff9f` → PR #2 `d9f60d6`。
- **Tower Run 当前分支**：`c13625d` 至 `7da9a7d` 共 8 个提交；代表路径 `Assets/Scripts/TowerRun/`、`Assets/Scripts/TowerRunCoordinator.cs`、`Assets/Scripts/TowerFloorRuntimeController.cs`、`Assets/Effects/BlackHolePortal/`、`Assets/Tests/TowerRun*`。

### 8.2 可讲的工程难题

- **确定性地图与表现解耦**：`Docs/MapGeneration/架构说明.md` + `433da5e` + `d09e945`。
- **通路对角色碰撞体的净空保证**：`8ba74ac` + `428262e` + `MapTerrainPassageWidthEnforcer.cs`。
- **从独立 Demo 到正式场景集成**：`562603f` + Stage 1—8 + `c367725`。
- **运行模式与 authority 选择**：`392b973` + `e7aff9f`。
- **对象池奖励与地图 generation 生命周期**：`b8e27c9` + `e7aff9f` + `GenerationScopedRewardLifetime.cs`。
- **检查点存档与楼层事务边界**：`5e86436` + `ece3074` + `TowerRunSlotStore.cs` + `TowerRunCoordinator.cs`。
- **事件时序导致的迷雾缺陷**：`7da9a7d` + `MapMinimapController.cs` + `TowerRunFlowPlayModeTests.cs`。

### 8.3 面试表述模板（带证据限定）

> 项目最初已有基础 Unity RPG 场景和战斗/奖励对象。随后通过多个提交逐步补齐背包、HUD、商店、持久化与角色成长。随机地图先在 `MapGeneration` 分支独立演进，形成逻辑数据、渲染、碰撞、装饰、小地图和测试边界；之后在 `MapMerger` 中按 Stage 1—8 接入正式 RPG 场景、玩家/相机和语义对象生命周期，并通过 PR #1 合入主线。主线随后修复 Damageable 奖励的配置和 generation 生命周期问题，经 PR #2 合并。当前 `feat/tower-run-save-mvp` 又在主线之上增加 8 个提交，构建楼层、检查点槽位、黑洞出口、恢复和门禁流程；设置 UI 仍有未提交改动，因此应描述为在研功能分支，而不是已发布版本。

可复查链：`G-GRAPH`、`G-ANCESTRY`、上述完整提交哈希及代表路径。

## 9. 后续补证建议

若主体文档要把“测试通过”“性能”“视觉质量”“用户验收”写成事实，需要另外收集并标注：

1. 当前提交对应的 Unity Editor/Player 版本与构建日志。
2. EditMode、PlayMode、独立进程持久化测试的原始 XML/日志及执行命令。
3. GitHub PR #1/#2 的描述、评审与 CI（若允许联网或使用 GitHub connector）。
4. Tower Run 功能分支未来的提交、远程分支或 PR 状态。
5. 当前未提交设置 UI 的视觉验收截图、目标分辨率和用户确认。
6. 对 `Docs/MapGeneration/开发状态与后续计划.md` 做现状更新，保留历史背景但移除已过期的“尚未集成”表述。

在补证前，主体文档应优先使用本清单中的“已验证事实”和“Git 历史记录”，并把推断和验收状态明确写出。

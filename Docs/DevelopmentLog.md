# RPG_Demo Development Log（开发日志）

> 本文档记录 `RPG_Demo` 的主要开发提交，方便回顾每次提交做了什么、影响了哪些系统、是否已经推送到远程仓库。

## 2026-09-15

### Add inventory package slot UI（新增背包格子界面）

- Commit Hash（提交编号）：`1e8f2ff`
- Branch（分支）：`main（主分支）`
- Remote（远程仓库）：`origin/main（远程主分支）`
- Push Status（推送状态）：已推送

#### Changed Files（改动文件）

- `Assets/Prefabs/UI/PackageSlot.prefab（背包格子预制体）`
- `Assets/Prefabs/UI/PackageSlot.prefab.meta（背包格子预制体元数据）`
- `Assets/Scenes/SampleScene.unity（示例场景）`
- `Assets/ScriptObjects/RewardSOs/Gold.asset（金币奖励配置）`
- `Assets/Scripts/PlayerInventory.cs（玩家背包脚本）`
- `Assets/Scripts/PlayerInventory.cs.meta（玩家背包脚本元数据）`
- `Assets/Scripts/UI/PackageSlotUI.cs（背包格子界面脚本）`
- `Assets/Scripts/UI/PackageSlotUI.cs.meta（背包格子界面脚本元数据）`

#### Summary（内容总结）

- 新增 PlayerInventory（玩家背包）逻辑，用于保存玩家获得的奖励物品。
- 新增 PackageSlotUI（背包格子界面）逻辑，用于显示单个背包格子的图标、数量和选中状态。
- 新增 PackageSlot Prefab（背包格子预制体），作为背包界面中可复用的 UI 单元。
- 更新 Gold RewardSO（金币奖励配置），让金币奖励可以参与背包显示或拾取流程。
- 更新 SampleScene（示例场景），接入背包 UI 相关对象与引用。

#### Impact（影响范围）

- 影响背包系统、奖励拾取显示、金币奖励配置和示例场景 UI。
- 为后续 Inventory UI（背包界面）、Reward Pickup（奖励拾取）、Item Stack（物品堆叠）功能打基础。

#### Verification（验证结果）

- 本地提交成功。
- 远程推送成功。
- 本地 `main（主分支）` 与远程 `origin/main（远程主分支）` 已同步。

## 2026-09-17

### Improve package panel HUD interaction（完善背包面板和 HUD 交互）

- Commit Hash（提交编号）：本条记录随本提交一起生成，具体编号见 Git 历史记录
- Branch（分支）：`main（主分支）`
- Remote（远程仓库）：`origin/main（远程主分支）`
- Push Status（推送状态）：随本提交推送

#### Changed Files（改动文件）

- `Assets/Scripts/UI/BaseRedDot.cs（通用红点脚本）`
- `Assets/Scripts/UI/CurrencyHudUI.cs（货币 HUD 界面脚本）`
- `Assets/Scripts/UI/PackageGridUI.cs（背包网格界面脚本）`
- `Assets/Scripts/UI/PackageHudButtonUI.cs（背包 HUD 按钮脚本）`
- `Assets/Scripts/UI/PackagePanelController.cs（背包面板控制器脚本）`
- `Assets/Scripts/UI/PackageRewardTooltipUI.cs（背包奖励提示框脚本）`
- `Assets/Scripts/UI/PackageSlotUI.cs（背包格子界面脚本）`
- `Assets/Scripts/GameInput.cs（游戏输入管理脚本）`
- `Assets/GameControls.inputactions（输入动作配置）`
- `Assets/GameControls.cs（输入动作生成代码）`
- `Assets/Scripts/Player/PlayerAction.cs（玩家行为脚本）`
- `Assets/Scripts/RewardPickup.cs（奖励拾取脚本）`
- `Assets/Scripts/ScriptableObject/RewardSO.cs（奖励数据脚本）`
- `Assets/ScriptObjects/RewardSOs/Gold.asset（金币奖励配置）`
- `Assets/Prefabs/UI/PackageSlot.prefab（背包格子预制体）`
- `Assets/Scenes/SampleScene.unity（示例场景）`
- `Assets/Font/MyTMP.asset（TextMeshPro 字体资源）`
- `Assets/_Resources/_Font/character.txt（字体字符表）`

#### Summary（内容总结）

- 新增 `BaseRedDot（通用红点）`，为 HUD 图标提供统一的红点显示和隐藏接口。
- 新增 `CurrencyHudUI（货币 HUD 界面）`，实时显示玩家当前金币数量。
- 新增 `PackageHudButtonUI（背包 HUD 按钮）`，支持点击打开或关闭背包，并根据金币数量显示红点提醒。
- 新增 `PackagePanelController（背包面板控制器）`，支持通过 Tab（背包快捷键）打开或关闭背包，并在背包打开时锁定 `Gameplay Input（玩法输入）`。
- 新增 `PackageGridUI（背包网格界面）`，根据 `PlayerInventory（玩家背包）` 动态生成背包格子，并处理格子选中和提示框显示。
- 新增 `PackageRewardTooltipUI（背包奖励提示框）`，显示奖励名称和说明，并将提示框限制在背包背景范围内。
- 扩展 `PackageSlotUI（背包格子界面）`，支持点击选中、鼠标悬浮延迟显示提示框和鼠标移出隐藏提示框。
- 扩展 `GameInput（游戏输入管理）`，加入背包输入事件、攻击输入事件和玩法输入启用状态。
- 调整 `RewardPickup（奖励拾取）` 和 `RewardSO（奖励数据）`，让奖励拾取数量进入背包系统，并支持奖励说明显示。
- 更新场景、UI 预制体和 TextMeshPro 字体资源，接入背包面板、金币显示、红点和提示框。

#### Impact（影响范围）

- 背包系统从“显示格子”扩展为“打开、关闭、选中、查看详情和红点提醒”的 UI 闭环。
- 打开背包时会锁定移动、攻击和控制切换等玩法输入，避免操作 UI 时角色继续行动。
- 金币奖励拾取后会同步更新玩家背包、货币 HUD 和背包红点。
- 为后续物品提示框、商店界面和任务奖励系统打基础。

#### Verification（验证结果）

- 已检查主要脚本逻辑和工作区改动范围。
- 本地编译检查通过。
- 本条记录随本次提交一起推送到远程仓库。

## 2026-10-12 — Tower Run MVP 与设置 UI v4 → v8：技术导读（设置视觉已验收，MVP 待最终验收）

> 本节依据 `feat/tower-run-save-mvp@7da9a7dc80733bdb35c6145bc244496589fa00e0` 的源码、Git 提交和 `.codex/TestResults/` 原始证据增量记录。设置 UI v4 当时仍是**未提交工作树改动**，尚未获得用户最终视觉确认；该阶段已推进至 v8 并获得视觉验收。以下不是整个 Tower Run MVP 的最终验收、发布或 PR 就绪声明。Q1—Q31 的原始决策不在此重写，详见本地 `.codex/handoffs/player-death-run-save-q19.md`。

### 需求、方案与边界

- 目标是闭合“新手关卡 → 第 1 层 → 第 2 层”的最小可玩链：打败本 generation 的全部目标 Monster 才解锁黑洞出口及小地图标记，走近不自动换层，玩家在范围内主动按 `F`；死亡可从三个正式槽位恢复或开始新 Run。
- 保存的是**本层入口检查点**，不是随时保存当前战斗状态。进入楼层后的生命、物品或怪物变化不写回该入口候选；离层时先完整冻结可携带的玩家/背包/商店状态，再开始下一层操作。这使重试和恢复有明确边界。
- 旧 `save.json` 保留但退出本 MVP 的正式保存链；三个新槽位使用 `Application.persistentDataPath/tower-run-slots`。音频偏好使用 `PlayerPrefs`。测试在初始化正式场景前由 `TowerRunRuntimeEnvironment.ConfigureTestIsolation` 改指本地隔离目录/偏好后端，不能拿测试目录或隔离 Player 的结果替代真实正式槽位观察。
- `TowerRunCoordinator` 用操作 ID、generation ID、staged record、状态和可组合门禁约束新 Run、恢复与换层；失败时保持门禁及可重试的 staged 数据，成功完成所有 finalizer 并收到 `GameplayReady` 后才把 staged 数据发布为当前 Run、入口候选并放开玩法。地图静态基线签名用于恢复时核对生成结果；`ConfigurationIdentity` 是显式维护的兼容身份，不是受保护地图配置资产的内容哈希。

### 运行时对象、调用与数据流

1. `Assets/Scenes/SampleScene.unity` 放置主 HUD `Canvas`、`SettingButton`，并引用 `Assets/Prefabs/MapGeneration/RandomMapRuntime.prefab`。`GameSession.BindSceneObjects` 调用 `TowerRunCoordinator.BindScene`；协调者取得地图、玩家、背包、商店、受伤组件，订阅 `GameplayReady`、出口与死亡事件，再由 `TowerRunUiController.EnsureForScene` 绑定 UI。主 Canvas 与地图运行 prefab 应在 Unity Inspector 中结合序列化引用查看；不要把动态设置节点误认为 Scene 中预先放好的静态 UI。
2. `Assets/Scripts/TowerRun/TowerRunCheckpointRecord.cs` 定义 Run ID、楼层、生成请求/静态签名、属性、背包和商店快照及 `DeepClone`。`TowerRunRuntimeStateBridge.TryCapture/TryApply` 将运行状态与记录互转；应用失败不得发布可玩状态。`TowerRunCoordinator.BeginNewRun/BeginRestoreSlot/HandleExitUseRequested` 分别产生新基线、读取槽位深拷贝、或冻结下一层携带数据，然后由 `BeginOperation` 发出 `MapGenerationRequest`。
3. 地图生成后，`TowerRunRestoreFinalizer.TryFinalizeBeforeReady` 转交协调者核对 generation/seed/attempt/静态签名并应用玩家状态；`TowerFloorRuntimeController.TryFinalizeBeforeReady` 从当前 registry 只收集 role 为目标 Monster 的活动 `BaseDamageable`，绑定死亡事件、放置黑洞 prefab、锁定小地图出口。`GameplayReady` 后才进入 Playing。非目标对象不计入目标集合；目标为空则拒绝就绪。
4. 目标全败后，`TowerFloorRuntimeController.HandleTargetDefeated` 同步解锁世界出口与小地图标记。`HandleInteractPressed` 检查门禁、玩家存活、距离及当前 generation；协调者先捕获离层数据，`TryAcceptExitTransition` 再将出口置为 Transitioning 并隐藏，避免同一出口反复接受换层。黑洞视觉资源在 `Assets/Effects/BlackHolePortal/Prefabs/VerticalBlackHolePortal.prefab`、`Scripts/BlackHolePortalEffect.cs`、相关 shader/material；视觉 prefab 本身不拥有换层事务。
5. `Assets/Scripts/TowerRun/TowerRunSlotStore.cs` 的 `ReadSlot/TryCommit` 分类空、有效、损坏、不兼容、不可用；写入候选先经兼容检查、临时文件回读验证，再移动或替换正式槽，最后回读核验。覆盖时使用读取时的 confirmation token 防止在确认期间无声替换已变化的记录；结果不确定时返回需核验状态，不把内存事务成功伪报为正式磁盘保存。`Assets/Scripts/TowerRun/LegacySaveDetector.cs` 只检测旧档，不迁移或删除它。
6. `Assets/Scripts/TowerRunGateHub.cs` / `TowerRunGate.cs` 管理 startup、death、operation、settings 等独立 owner 的玩法和世界暂停 lease。死亡时协调者重置旧攻击/受击过渡状态并关闭背包/商店；恢复最终化还会同步玩家受伤状态。任何一个 owner 仍持有门禁时，另一个 UI 的关闭都不能提前解除暂停。

### 小地图迷雾缺陷：实际原因而非推测

`Assets/Scripts/MapGeneration/MapMinimapController.cs` 在每次 `RenderMap` 时创建新的 `MapExplorationState` 并重置 fog `Texture2D` 像素。曾出现“第三次死亡后开始新 Run，旧死亡地点仍被揭开”的真实人工失败；定向探针发现 `MapGenerated` 回调执行时玩家仍在上一地图格 `(62,62)`，之后 `PlayerSpawnService.TrySpawn` 才把玩家移动到新出生格 `(32,32)`。旧代码在重置迷雾的回调里立即按旧位置揭示 37 格，于是视觉上像继承旧探索状态。`7da9a7d` 删除这次过早揭雾，改由正常 `LateUpdate` 在出生重定位后按玩家当前位置揭示；同 Run 移动仍继续探索，每次生成仍按入口世界规则重建探索，而非建立跨 Run 迷雾持久化。修复前定向用例 0/1、修复后 1/1，扩展流程 4/4、Unity 正常退出；修复后的独立 Player 新 Run 迷雾由用户实际观察 PASS。证据见 `.codex/TestResults/tower-fog-sentinel-investigation-2026-10-12.md`、`tower-fog-flow-regression.xml` 与 `tower-fog-isolated-player-build-7da9a7d.md`。同一时期 H0033 弹窗的进程归属/根因未被证实，不能归因于 Unity 或游戏断言。

### 设置、音频与“古卷账簿”v4 → v8（v8 视觉已验收）

- `Assets/Scripts/UI/TowerRunUiController.cs` 是正式 Player 使用的**运行时构建者**。`Awake → BuildUi` 在 HUD Canvas 上创建流程覆盖层、设置木框/纸面、音频页、存档页、三槽卡片、覆盖确认弹窗和楼层标签；`HookExistingSettingsButton` 将现有 HUD 按钮连到与 Esc 同一窗口。`Update` 对覆盖确认、设置、背包、商店实施关闭优先级。`OpenSettings/CloseSettings` 获取/释放 settings 门禁并停止试听，未复制保存业务系统。
- `Assets/Scripts/UI/TowerRunUiSkin.cs` 是小型 Scene-owned 视觉配置：八个 Sprite 字段及少量墨色、按钮/切片应用函数。`SampleScene.unity` 的**主 Canvas** 序列化这些引用；`EnsureForScene` 优先找带 `TowerRunUiSkin` 的名为 `Canvas` 的 HUD，防止误将窗口建到小地图动态 Canvas 上。木框 `WoodTable_Slots`、纸面 `Carved_9Slides`、横向卡片 `Carved_3Slides`、标题 `Banner_Horizontal`、选中页签 `Ribbon_Blue_3Slides`、普通/覆盖/禁用的蓝/红/灰按钮来自既有 TinySwords 素材。卡片采用较矮的 `Carved_3Slides`，不是压扁九宫格纸面。
- 八张 Sprite `.meta` 为 Point 过滤，并按实际 Sprite border 设置木框 32、纸面 48、卡片左右 48、页签/按钮左右 64 像素（完整 L/B/R/T 见 `.codex/TestResults/settings-ui-visual-v4-20261012.md`）。`TowerRunUiSkin.ApplySprite` 仅在 border 非零时使用 `Image.Type.Sliced`，否则保留 Simple；标题小横幅保纵横比。样式仍回退到纯色，避免引用失效时窗口完全不可见。没有迁移 TMP；沿用 Unity 2022.3 可用的 `LegacyRuntime.ttf`，这也修复了早先 `Arial.ttf` 在该版本抛异常的实际缺陷。
- `ShowAudioPage/ShowSavePage` 只切换页签内容；`BeginSaveRequest/SelectSaveSlot/CommitSlot` 调用协调者现有保存接口，`ConfirmOverwrite/CancelOverwrite` 处理弹窗。音量/静音经 `TowerRunRuntimeEnvironment.Preferences` 保存，`ApplyAudio` 设置 `AudioListener.volume`；`PlayPreviewSound` 用独立 `AudioSource` 播放短合成音。存档页 `ScrollRect + RectMask2D` 包住可增长的消息和卡片，关闭按钮留在固定布局区；`ResizeSettingsFrame` 按 Canvas 尺寸限制外框。
- v4 的真实隔离 Player 截图、素材映射与 1280×720/1920×1080 检查见 `.codex/TestResults/settings-ui-visual-v4-20261012.md`。最终流程回归 `settings-ui-v4-flow-regression.xml` 为 5/5 PASS、Unity exit 0，独立 v4 Player 构建 exit 0。该结果证明实现与两种实际尺寸的初步画面，但**当时用户尚未最终批准 v4 视觉，也未授权提交 UI**；极端比例/更小窗口未执行。不要把早期概念图当正式渲染截图。
- 用户随后批准 A—F 最小收口：保留古卷账簿风格和全局 `CanvasScaler`，将音频控件收成居中的紧凑组并为滑块手柄、静音框加入像素风内芯；标题装饰缩到 88×84；覆盖弹窗扩为最多 720×480、标题 30、正文/按钮 22，并以 0.82 遮罩和独立 `CanvasGroup` 阻断底层交互。存档提示在最低宽度显式分行，避免中文句号孤立成行。
- 槽位时间仅在 UI 层经严格 ISO 8601 `"O"` 解析后转换为玩家本机 `yyyy-MM-dd HH:mm`；解析失败显示“保存时间未知”。存档内 `savedAtUtc`、confirmation token、覆盖事务、并发保护和格式均未改变。最终 `TowerRunFlowPlayModeTests` 6/6 PASS、Unity exit 0；取消覆盖前后隔离槽位文件 SHA-256 相同。
- v6 独立 Player 构建成功，并在真实客户端 `1024×576`、`1280×720`、`1920×1080` 各采集音频页、三槽页和覆盖弹窗，共 9 张截图；最小分辨率下文字、按钮与三个槽位完整显示，底层“关闭”位置在弹窗开启时不可操作，取消后恢复。完整证据见 `.codex/TestResults/settings-ui-v6-visual-closure-20261012.md`。**在 v6 阶段，用户最终视觉确认与提交授权尚未收到**，因此当时的工作树继续保持未提交。
- v6 美术方向获认可后，用户只要求文字可读性收口。v7 将主/次墨色收紧到 `#30231B` / `#4A3629`，选中页签改为 `#FFF3DB`；设置标题由 34 调为 38，其他关键设置文字由 22 调为 24，并使用现有 dynamic `LegacyRuntime.ttf` 的 `Bold`。标题、页签、按钮与槽位仅增加一个 `(1,-1)`、约 35% Alpha 的单向 Shadow，不使用 Outline，也未替换字体或迁移 TMP。新增可读性用例先在 v6 上 0/1 FAIL，修正后 1/1 PASS；最终 `TowerRunFlowPlayModeTests` 7/7 PASS，v7 Player 构建成功。真实 Player 检查覆盖 1024×576 音频/存档/弹窗和 1920×1080 存档页，未见新增裁切、遮挡、溢出或按钮交互回归；证据见 `.codex/TestResults/settings-ui-v7-text-readability-20261012.md`。**在 v7 阶段，仍待用户最终视觉确认，未获提交授权。**
- 用户补充指出 v6/v7 的“音频”“存档”“试听”“关闭”虽然几何居中，实际字形视觉上仍偏下。定位确认这些 Text 已为 `MiddleCenter`、全拉伸 Anchor、中心 Pivot 和零 offset；偏差来自 `LegacyRuntime.ttf` 行框/基线与中文字形墨迹边界不重合，并叠加 Tiny Swords 按钮下沿和 Ribbon 尾部的不对称视觉重量。v8 只为这四个短标签引入集中常量 `ShortButtonLabelVisualOffsetY = 3f`，沿 Unity UI 正 Y 上移，按钮 Sprite、父 RectTransform、尺寸、位置、点击范围、事件和全部长文字布局不变。新用例先在 v7 上 0/1 FAIL，修正后 1/1 PASS；强化后逐一触发四个按钮 Hover、切换页签并确认长按钮仍为零偏移。最终 `TowerRunFlowPlayModeTests` 8/8 PASS。独立 v8 Player 与真实帧采集 Player 均构建成功；Standalone 截图覆盖 1024×576、1280×720、1920×1080 的音频/试听 Hover/存档/覆盖弹窗，未见文字跳动、裁切、遮挡或异常换行。证据见 `.codex/TestResults/settings-ui-v8-short-button-optical-center-20261012.md`。**当前 `VISUAL_ACCEPTANCE = PASS WITH MINOR ISSUES`：用户已接受 v8 作为 RPG_Demo 设置界面的正式 MVP 视觉版本，文字可读性与短按钮视觉居中已获确认；1024×576 下部分长文本略显粗重，列为非阻断美术改进项。本轮设置 UI 视觉改造正式收口，不继续迭代；已有自动化与实际 Player 验收结果继续有效。整体 Tower Run MVP 尚未通过最终审核，`READY_FOR_PR_REVIEW = NO`。**

### 验证、版本与仍待收口

- 可追溯本地提交：`c13625d` 出口净空/小地图门禁；`2d3a1fe` 黑洞视觉；`5adfb7f` HUD 场景基线；`5e86436` 检查点/三槽；`ece3074` Run 生命周期/UI；`146ab53` 恢复/爬塔测试；`c4cb850` Unity 运行修复；`7da9a7d` 迷雾时序修复。完整 SHA、执行命令及异常过程保留在 `.codex/handoffs/player-death-run-save-q19.md` 与 `.codex/TestResults/`，Git 保留源码差异。
- `c4cb850` 的最终工程证据：TowerRun EditMode 21/21 **断言通过但 runner 退出挂起、非 exit 0**；MapGeneration EditMode 216/216、PlayMode 16/16、黑洞 GPU 1/1、独立进程写入与读取各 1/1，后五项均 exit 0。GPU 结果只覆盖 D3D11/RTX 4060。v4 UI 的 5/5 是当时未提交工作树的针对性回归，不应冒充 `c4cb850` 或旧 HEAD 的测试。
- 人工已报告 PASS：新手关卡至第 2 层路线、目标击败/出口解锁/主动 F、槽位保存与覆盖/恢复、输入优先级及两次死亡恢复；迷雾修复后新 Run、设置输入、黑洞视觉和亲耳试听四项也获用户 PASS。仍缺独立明确观察的四项：非目标对象不阻塞、换层重复 F 不并发、出口实际净空、第三次死亡后新 Run 对三个旧正式槽位逐槽不变。槽位恢复绑定和覆盖确认并发窗口的 Spec 审核风险仍待收口。`READY_FOR_PR_REVIEW = NO`；文档归档或清理临时文件不会把它自动改成 YES。

## 2026-10-04

### Implement monster AI and damage reaction system（实现怪物 AI 与伤害受击系统）

- Commit Hash（提交编号）：本条记录随本提交一起生成，具体编号见 Git 历史记录
- Branch（分支）：`main（主分支）`
- Remote（远程仓库）：`origin/main（远程主分支）`
- Push Status（推送状态）：随本提交推送

#### Changed Files（改动文件）

- `Assets/Scripts/Damageable/DamageInfo.cs（伤害信息数据）` 及其 `.meta` 元数据
- `Assets/Scripts/Damageable/BaseDamageable.cs（可受伤对象基类）`
- `Assets/Scripts/Damageable/Animal/AnimalHurtController.cs（动物受击控制器）`
- `Assets/Scripts/Damageable/Plant/PlantBehaviorController.cs（植物行为控制器）`
- `Assets/Scripts/Damageable/Monster/BaseMonster.cs（怪物基类）`
- `Assets/Scripts/Damageable/Monster/BaseMonsterState.cs（怪物状态基类）` 及其 `.meta` 元数据
- `Assets/Scripts/Damageable/Monster/MonsterStateType.cs（怪物状态类型）` 及其 `.meta` 元数据
- `Assets/Scripts/Damageable/Monster/MonsterAIController.cs（怪物 AI 控制器）` 及其 `.meta` 元数据
- `Assets/Scripts/Damageable/Monster/MonsterPerceptionController.cs（怪物感知控制器）` 及其 `.meta` 元数据
- `Assets/Scripts/Damageable/Monster/MonsterMovementController.cs（怪物移动控制器）` 及其 `.meta` 元数据
- `Assets/Scripts/Damageable/Monster/MonsterAttackController.cs（怪物攻击控制器）` 及其 `.meta` 元数据
- `Assets/Scripts/Damageable/Monster/MonsterAnimationController.cs（怪物动画控制器）`
- `Assets/Scripts/Damageable/Monster/MonsterHurtController.cs（怪物受击控制器）`
- `Assets/Scripts/Damageable/Monster/MonsterPatrolState.cs（怪物巡逻状态）`、`MonsterChaseState.cs（怪物追击状态）`、`MonsterSearchState.cs（怪物搜索状态）`、`MonsterReturnState.cs（怪物返回状态）`、`MonsterAttackState.cs（怪物攻击状态）` 及其 `.meta` 元数据
- `Assets/Scripts/Player/PlayerDamageController.cs（玩家造成伤害控制器）` 及其 `.meta` 元数据
- `Assets/Scripts/Player/PlayerHurtController.cs（玩家受击控制器）` 及其 `.meta` 元数据
- `Assets/Scripts/Player/PlayerAction.cs（玩家行为脚本）`
- `Assets/Prefabs/Monsters/Torch_Blue.prefab（蓝色火炬敌人预制体）` 及其 `.meta` 元数据
- `Assets/Prefabs/Monsters/HappySheep.prefab（快乐绵羊预制体）`
- `Assets/Prefabs/Players/Warrior_Blue.prefab（蓝色战士玩家预制体）`
- `Assets/Scenes/SampleScene.unity（示例场景）`
- `Assets/Animations/Monsters（怪物动画资源目录）` 及其 `.meta` 元数据
- `Assets/_Resources/HomeMadeResources（自制资源目录）` 及其 `.meta` 元数据

#### Summary（内容总结）

- 新增 `DamageInfo（伤害信息）`，统一描述伤害数值、伤害来源及后续受击处理所需的数据，减少玩家、怪物和可破坏对象之间重复定义伤害参数的情况。
- 扩展 `BaseDamageable（可受伤对象基类）`，并更新动物、植物、怪物和玩家相关控制器，使不同类型对象可以接入统一的 Damageable（可受伤对象）处理流程。
- 新增 `MonsterAIController（怪物 AI 控制器）` 和 `MonsterStateType（怪物状态类型）`，以状态机（State Machine，状态机）组织 Patrol（巡逻）、Chase（追击）、Search（搜索）、Return（返回）和 Attack（攻击）等行为状态。
- 将怪物行为职责拆分为 `MonsterPerceptionController（感知控制器）`、`MonsterMovementController（移动控制器）`、`MonsterAttackController（攻击控制器）` 和 `MonsterAnimationController（动画控制器）`，让行为决策、移动执行、攻击执行和动画表现彼此独立，降低耦合度。
- 新增 `PlayerDamageController（玩家造成伤害控制器）` 和 `PlayerHurtController（玩家受击控制器）`，更新 `PlayerAction（玩家行为）`，接入玩家攻击、伤害判定和受击反馈流程。
- 更新 `HappySheep（快乐绵羊）`、`Warrior_Blue（蓝色战士）` 和 `SampleScene（示例场景）` 的组件、引用和行为配置，并新增 `Torch_Blue（蓝色火炬敌人）` 预制体及怪物动画资源。

#### Impact（影响范围）

- 影响玩家战斗、怪物 AI、动物受击、植物行为、伤害传递、受击反馈、敌人预制体和示例场景配置。
- 怪物的 Animation Control（动画控制逻辑）与 Behavior Control（行为控制逻辑）通过控制器职责分离，后续可以独立调整动画状态和 AI 决策，减少互相修改造成的回归风险。
- 状态机为后续扩展警戒、受击硬直、死亡、技能和更复杂的敌人行为提供了统一扩展位置。

#### Verification（验证结果）

- 已读取并核对工作区状态，确认本次改动包含 C# 脚本、动画资源、预制体和场景配置。
- `learn/tilemap-minimap/course-state.md（学习进度记录）` 及 `learn/（学习记录目录）` 保持在工作区中，不纳入本次提交。
- 本次尚未在 Unity 编辑器中运行场景或执行 Play Mode（播放模式）测试；提交前仅进行 Git 文件范围核对。
- 本条记录随本次提交一起推送到远程仓库。

## 2026-09-26

### Add merchant shop and persistent save system（新增商人商店与持久化存档系统）

- Commit Hash（提交编号）：本条记录随本提交一起生成，具体编号见 Git 历史记录
- Branch（分支）：`main（主分支）`
- Remote（远程仓库）：`origin/main（远程主分支）`
- Push Status（推送状态）：随本提交推送

#### Changed Files（改动文件）

- `Assets/Scripts/NPC/MerchantInteractionController.cs（商人交互控制器）`
- `Assets/Scripts/Shop/ShopCatalogSO.cs（商店目录配置）`
- `Assets/Scripts/Shop/ShopTradeController.cs（商店交易控制器）`
- `Assets/Scripts/UI/Shop/ShopGridUI.cs（商店商品网格界面）`
- `Assets/Scripts/UI/Shop/ShopModeTabButtonUI.cs（商店分类标签按钮界面）`
- `Assets/Scripts/UI/Shop/ShopPanelController.cs（商店面板控制器）`
- `Assets/Scripts/UI/Shop/ShopPurchaseFlyEffect.cs（购买物品飞入效果）`
- `Assets/Scripts/UI/Shop/ShopSlotUI.cs（商店商品格子界面）`
- `Assets/Scripts/GameSaveService.cs（游戏存档服务）`
- `Assets/Scripts/ScriptableObject/RewardRegistrySO.cs（奖励注册表配置）`
- `Assets/Scripts/PlayerInventory.cs（玩家背包）`
- `Assets/Scripts/ScriptableObject/RewardSO.cs（奖励数据）`
- `Assets/Scripts/RewardPickup.cs（奖励拾取）`
- `Assets/Scripts/GameInput.cs（游戏输入管理）`
- `Assets/Scripts/Player/PlayerAction.cs（玩家行为）`
- `Assets/ScriptObjects/RewardRegistry.asset（奖励注册表数据）`
- `Assets/ScriptObjects/Shop/ShopCatalog_Merchant.asset（商人商品目录）`
- `Assets/Prefabs/NPC/Merchant_Pawn.prefab（商人角色预制体）`
- `Assets/Prefabs/UI/ShopSlot.prefab（商店商品格子预制体）`
- `Assets/Animations/MerchantSign（商人交互标识动画资源）`
- `Assets/Animations/MerchantVisual（商人角色动画资源）`
- `Assets/GameControls.inputactions（输入动作配置）` 与 `Assets/GameControls.cs（输入动作生成代码）`
- `Assets/Scenes/SampleScene.unity（示例场景）`、`Assets/Prefabs/Players/Warrior_Blue.prefab（玩家角色预制体）`
- 奖励数据、TextMeshPro 字体字符表及 UI 图标资源

#### Summary（内容总结）

- 新增商人交互逻辑：玩家靠近商人后可按 F（交互键）打开商店；交互行为脚本与商人待机、头顶交互标识动画资源分开管理，减少行为逻辑与动画表现之间的耦合。
- 新增商店目录配置与购买流程，支持食品、武器、技能、材料和其他分类，以及价格、库存、单次购买数量和稳定商品编号等配置。
- 商店交易会校验商品、金币、库存和背包状态；失败时恢复交易前状态，并以存档写入成功作为确认交易的条件。
- 新增商店分类标签、商品网格、库存与售罄状态、商品提示、错误提示和购买飞入背包效果。打开商店时锁定玩法输入，按 Escape（退出键）关闭商店。
- 新增版本化 JSON（JavaScript 对象表示法）存档，将背包和商店库存写入本地存档，并使用正式文件、备份文件和临时文件管理写入与恢复。
- 新增 RewardRegistrySO（奖励注册表配置），用稳定的 RewardId（奖励唯一编号）将存档中的奖励记录映射回 Unity 奖励资源。
- 扩展背包的存档加载、数量增减和交易快照回滚；拾取物品只有在背包确认成功保存后才会从场景中回收。
- 更新输入动作：将原 Controls（控制动作）和 Attack（攻击动作）调整为 Interact（交互动作）和 Battle（战斗动作），并加入 F（交互键）交互绑定。
- 当前商人目录仅配置一个食品商品；武器和技能分类尚无已配置商品。本次实现的是购买流程，尚未实现出售流程。

#### Impact（影响范围）

- 影响商人交互、商店购买、背包与奖励、输入管理、游戏存档、示例场景及相关 UI 和美术资源。
- 背包拾取与商店交易都接入统一存档服务，减少奖励、金币和库存状态在异常或重新启动后不一致的风险。
- 商店界面打开时暂停玩家玩法输入，避免 UI 操作期间角色继续移动或攻击。

#### Verification（验证结果）

- 已检查本次新增商店、商人、存档与奖励注册相关文件，并核对工作区新增、修改和删除内容。
- `dotnet build Assembly-CSharp.csproj --no-restore` 和 `dotnet build Assembly-CSharp-Editor.csproj --no-restore` 分别构建通过：两个程序集成功生成，0 个警告，0 个错误。
- `dotnet build RPG_Demo.sln --no-restore` 在解决方案包装层返回失败，但没有输出编译错误；两个实际 Unity 项目分别构建成功。
- 尚未在 Unity 编辑器中运行场景或执行 Play Mode（播放模式）测试。
- 本条记录随本次提交一起推送到远程仓库。

## 2026-09-26

### Add player progression and stamina system（新增玩家成长与体力系统）

- Commit Hash（提交编号）：本条记录随本提交一起生成，具体编号见 Git 历史记录
- Branch（分支）：`main（主分支）`
- Remote（远程仓库）：`origin/main（远程主分支）`
- Push Status（推送状态）：随本提交推送

#### Changed Files（改动文件）

- `Assets/Scripts/GameSession.cs（游戏会话）`
- `Assets/Scripts/PlayerStatsRuntime.cs（玩家属性运行时数据）`
- `Assets/Scripts/PlayerProgressionService.cs（玩家成长服务）`
- `Assets/Scripts/PlayerStaminaController.cs（玩家体力控制器）`
- `Assets/Scripts/UI/PlayerStatsUpgradeUI.cs（玩家属性升级界面）`
- `Assets/Scripts/UI/PlayerStatusPanelUI.cs（玩家状态面板界面）`
- `Assets/Scripts/GameSaveService.cs（游戏存档服务）`
- `Assets/Scripts/GameInput.cs（游戏输入管理）`
- `Assets/Scripts/Player/PlayerAction.cs（玩家行为）`
- `Assets/Scenes/SampleScene.unity（示例场景）`
- `Assets/Prefabs/UI/PackageSlot.prefab（背包格子预制体）`

#### Summary（内容总结）

- 新增 GameSession（游戏会话）单例，在首个场景加载前自动创建，并通过 DontDestroyOnLoad（跨场景不销毁）保存玩家属性和成长服务。
- 新增 PlayerStatsRuntime（玩家属性运行时数据），统一管理生命值、魔法值和体力值的最大值、当前值、范围限制、变更通知和属性快照。
- 新增 PlayerProgressionService（玩家成长服务），支持通过金币增加或减少生命值、魔法值和体力值上限。默认每个属性点消耗或返还 10 金币，且交易写入失败时会恢复金币和属性快照。
- 新增 PlayerStatsUpgradeUI（玩家属性升级界面），为生命值、魔法值和体力值提供加点、减点按钮，显示“当前值 / 最大值”，并在金币不足、达到上限或保存失败时显示原因。
- 重构 PlayerStatusPanelUI（玩家状态面板界面），从独立管理体力条改为只负责显示生命值、魔法值和体力值三个状态条及对应文本，属性修改交由运行时数据和成长服务处理。
- 新增 PlayerStaminaController（玩家体力控制器），支持冲刺时按时间消耗体力，停止冲刺或攻击后经过恢复延迟自动恢复体力；默认冲刺消耗速率为每秒 2 点、恢复速率为每秒 5 点、恢复延迟为 1 秒。
- 更新 PlayerAction（玩家行为），普通攻击开始时取消冲刺状态，并按配置消耗体力；体力不足时不会播放攻击流程或造成伤害，成功消耗后立即保存运行时状态。
- 更新 GameInput（游戏输入管理），新增 CancelControlState（取消加速状态）接口，在界面禁用玩法输入或开始攻击时清理左 Ctrl（加速键）的切换状态。
- 扩展 GameSaveService（游戏存档服务），将存档版本升级到 v2，新增玩家名称和生命、魔法、体力属性字段，并为旧版本存档补充默认属性、规范化数值和自动迁移保存逻辑。
- 更新 SampleScene（示例场景）中的角色面板、状态文本、状态条和按钮绑定，并调整 PackageSlot（背包格子）文本自动缩放及悬浮提示延迟配置。

#### Impact（影响范围）

- 影响玩家状态 HUD（游戏主界面）、角色属性面板、攻击和冲刺行为、金币背包、统一存档及场景初始化流程。
- 玩家属性数据与 UI（用户界面）显示、属性成长交易、攻击/冲刺消耗之间通过事件和服务接口协作，减少 UI 直接修改核心属性数据造成的耦合。
- 旧版商店和背包存档仍可迁移到 v2 格式；新增的玩家属性会使用默认值初始化，不会因旧存档缺少字段而读取失败。

#### Verification（验证结果）

- 已读取并核对本次新增脚本、既有脚本差异、场景配置和预制体配置。
- `dotnet build Assembly-CSharp.csproj --no-restore` 构建通过：0 个警告，0 个错误。
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore` 构建通过：0 个警告，0 个错误。
- `git diff --check` 仅报告 Unity 场景序列化文件中的两个空字段行尾空白，没有发现 C# 代码格式错误。
- 尚未在 Unity 编辑器中运行场景或执行 Play Mode（播放模式）测试。
- 本条记录随本次提交一起推送到远程仓库。

## 2026-09-29

### Add bridge tiles and update sprite import data（新增桥梁瓦片并更新精灵导入数据）

- Commit Hash（提交编号）：本条记录随本提交一起生成，具体编号见 Git 历史记录
- Branch（分支）：`main（主分支）`
- Remote（远程仓库）：`origin/main（远程主分支）`
- Push Status（推送状态）：随本提交推送

#### Changed Files（改动文件）

- `Assets/Sprites/GrassTiles.prefab（草地瓦片调色板预制体）`
- `Assets/Sprites/GrassTiles/Bridge_All_*.asset（桥梁瓦片资源）`
- `Assets/Sprites/GrassTiles/Bridge_All_*.asset.meta（桥梁瓦片资源元数据）`
- `Assets/_Resources/TinySwords/Terrain/Bridge/Bridge_All.png.meta（桥梁精灵导入配置）`
- `Assets/_Resources/TinySwords/Resources/Gold Mine/GoldMine_Active.png.meta（金矿激活状态精灵导入配置）`
- `Assets/_Resources/TinySwords/Resources/Gold Mine/GoldMine_Destroyed.png.meta（金矿摧毁状态精灵导入配置）`
- `Assets/_Resources/TinySwords/Resources/Gold Mine/GoldMine_Inactive.png.meta（金矿未激活状态精灵导入配置）`

#### Summary（内容总结）

- 新增 12 个 Bridge_All（桥梁合集）瓦片资源，按 4 行 3 列命名为 `Bridge_All_0_0` 到 `Bridge_All_3_2`，用于后续 Tilemap（瓦片地图）场景绘制。
- 更新 GrassTiles（草地瓦片调色板）预制体，将新桥梁瓦片纳入已有瓦片绘制资源集合。
- 更新 Bridge_All.png（桥梁合集精灵图）的 Unity 导入元数据，使桥梁精灵切片和瓦片资源引用保持一致。
- 更新 GoldMine_Active、GoldMine_Destroyed 和 GoldMine_Inactive（金矿三种状态）精灵导入元数据，为后续资源采集或地图物件表现保持导入配置一致。
- 保留 `learn/tilemap-minimap/course-state.md（学习进度记录）` 在工作区中，不纳入本次提交。

#### Impact（影响范围）

- 影响 Tilemap（瓦片地图）绘制资源、草地瓦片调色板、桥梁地图元素和金矿地图物件的 Sprite Import Settings（精灵导入设置）。
- 为后续在场景中绘制桥梁通路、制作地图连通区域或接入小地图/地形系统打基础。
- 本次主要是 Unity 资源和元数据更新，没有新增或修改 C# 运行时代码。

#### Verification（验证结果）

- 已核对工作区状态，确认本次 Unity 资源变更包含桥梁瓦片资源、草地瓦片预制体以及桥梁/金矿精灵导入元数据。
- 本次未修改 C# 脚本，因此未重新执行 C# 编译检查。
- 尚未在 Unity 编辑器中运行场景或执行 Tile Palette（瓦片调色板）绘制验证。
- 本条记录随本次提交一起推送到远程仓库。

> 本文记录前序字体落地与精确清档实施。随后商店正式发布纳入必要字体代码／内容配置，字体像素保持本地私有恢复；当前发布与成品验收见 [商店正式集成记录](../Releases/ShopUI-V35-FormalIntegration-20261105.md)。本文历史验证保持原版本范围。

# 主工程统一 MyTMP 与旧地图测试档清理（2026-11-05）

本轮已实际修改 `D:/UnityProjects/RPG_Demo` 的 main 工作区，完成精确清档、统一字体、真实 Editor Play 新冒险保存，以及两个独立新 Editor 进程恢复。用户人工视觉验收仍为 **NOT_RUN / PENDING_USER**；本记录不代表商店整体、交易、背包交互或完整系统验收通过。

唯一原始证据根：`D:/UnityProjects/RPG_Demo/Artifacts/ShopUiFontUpgrade/ShopUI-V35-UnifiedMyTMP-20261030-133415/MainFontAndMapSave-20261104-235001`（下称 Evidence）。机器回执见 `Evidence/final-verification.json`，跨进程逐字节比较见 `Evidence/world-and-save-comparison.json`。本次不提交、推送、合并或迁移整店。

已按本轮明确授权执行 main 必要修改；前序 `FontProtocol-20261104-225035/DESIGN.md` 仅作为调查背景。最初 main 为 `5316ea63521fbf63fe88c9068e4518b85ca97afb`、496 dirty、暂存区空；当前身份和完整保全结果以 final-verification 为准，历史方案与旧 Gate 状态不替代本轮实测。

**清理的数据与归属依据**

真实 main Editor 探针返回 company/product=`DefaultCompany/RPG_Demo`，`Application.persistentDataPath=C:/Users/yrm61/AppData/LocalLow/DefaultCompany/RPG_Demo`。普通入口使用 `TowerRunRuntimeEnvironment.SlotRoot` 的默认 `tower-run-slots`，未安装测试存储 override，未影子化 company/product。源码 `TowerRunSlotStore.ReadSlot` 只扫描 1–3 号固定路径；缺主文件但保留 `.backup` 会使槽位不可用。因此处理主文件和其恢复备份，逐一确认 `.tmp` 也不存在。

旧测试槽位根：`C:/Users/yrm61/AppData/LocalLow/DefaultCompany/RPG_Demo/tower-run-slots`。下列五个旧 `schema1/default/TRB2` 文件已清理；清理前是 2026-10-12/13 的测试记录。

| 文件 | 原 SHA-256 |
|---|---|
| tower-run-slot-1.json | `1c727195fb530e682330ed77a96dbb8c40f7e78bcaa55fc9eee3ecda9ba1e549` |
| tower-run-slot-1.json.backup | `2a32fdf18182abc1c1038256ef69bd2f6392b0e6a9aebedadcaaf4737f7c3c0d` |
| tower-run-slot-2.json | `b4d7d4cf34c3ac4fef02863e73a55fecfe8b3eb8ce0057583921ed7b64cbda2a` |
| tower-run-slot-3.json | `e3f3741d1de384570e8a80eed11af0842d8978d02d182022701dd046fbb9bee3` |
| tower-run-slot-3.json.backup | `24882a4182209e740fb93b4071ea64feedf6cdebd3c1c37ceab85ebb862328e2` |

备份在 `Evidence/Backups/MapPlayTest/files/`，逐字节恢复读回副本在同级 `RestoreCheck/`，完整原路径、大小、SHA、时间及 run/profile 在 `Backups/MapPlayTest/manifest.json`。备份位于项目 Artifacts 内，运行时不扫描。`cleanup.json` 绑定通过的真实 Editor 探针 Run，并记录五个精确删除路径；清理前确认无 Unity/Player 写入者，重新核验原件和两份备份 SHA。

无槽位索引文件或持久化“当前槽位”键需要删除：选择及 pending overwrite 是 `TowerRunUiController` 的内存状态。保留 `save.json`、`save.backup.json`、`shop_stock_*.json`、Player.log 和全部 PlayerPrefs 键。当前 `save.json` 只供 LegacySaveDetector 非阻塞通知使用，不是地图恢复输入。收尾再次确认上述无关文件 SHA 完全一致；Player 的18项偏好原值不变，Editor业务设置不变。Unity 自己的会话计数和会话 ID 随真实 Play 正常变化，见 `preferences-after.json`，未回写伪装成未运行。

如需回退数据，先关闭相关运行程序、另行备份当前新槽位，再从 manifest 的 `backup` 路径恢复所需精确文件。不要直接用旧备份覆盖本轮新档。已验证的是备份字节可恢复；本轮不承诺在当前内容下重新播放已明确清除的旧测试档。

**统一字体及仍有效的内容职责**

来源是已准备好的商店工作树 `D:/.codex/worktrees/shop-ui-night-run/RPG_Demo/Assets/Font/MyTMP.asset`。只复制这个资产的已验收字节，保留 main 原 `.meta`。未重新烘焙、未升级 Unity/TMP、未修改字符源表，未新增第二份业务字体。

| 身份 | 实际值 |
|---|---|
| 原 main 字体 SHA | `0cf1e34120a47968ed4417bfb31f8ba892e72d3864ce48c7260d1a61a3c3a48a` |
| 当前 main / 来源字体 SHA | `d369760a46cfd85e4af65dac61fec167bc7548dadbcb3ece7363b09b12ef7503` |
| 保持的 GUID | `18feaf95d90fd2545a36fb4b573dbe0a` |
| 保持的 .meta SHA | `9914efe8c23c18667a168632d9e9be9898349b11f6229ba8378f4214a02e5980` |
| 覆盖 | 原368字全部包含于新版388字，缺失0；388glyph、1张静态Atlas、材质与Atlas引用真实加载有效 |

旧字体字节在 `Evidence/Backups/Project/Assets/Font/MyTMP.asset` 和原 Before 中保留，不作为运行时 `.bytes` 打包。现有 UI 按原 GUID 自动引用这一个资产。真实版本为 Unity2022.3.62f3c1、TMP3.0.7、uGUI1.0.0、Test Framework1.1.33；manifest/package lock未修改。

新建 `CandidateContent_I1_MyTMPV1.json`，1220条pin中只更新 MyTMP 一行，其他1219条及元数据保持；新内容为 `content-r1:a4d222a9bf0aa3952ea231ed097e746a6348ff2e571a7a193ea7a61a55f4daac`。`Recipe_MyTMPV1.asset` 只改变资源名称和清单引用。普通 SampleScene 通过已有配方解析器优先使用新版本，恢复则按明确记录的旧身份选择原配方。

旧 MainV1 manifest `c55fb3b4…9802`、旧 VNext manifest `c1b30f16…9ec9` 及旧配方、历史 Seal 和验证材料保留原字节。被清除的是更早的 default/TRB2 测试档；这不等于获得删除所有 MainV1/旧VNext内容职责的授权。

本轮不实施前案的 Host schema3、额外显示政策 JSON 或旧字体运行时 TextAsset。沿用 schema2，只给现有 Host 增加精确旧 VNext 身份和当前内容差异集合：旧 MainV1/旧VNext 如实为1195原字节+25差异；当前新字体内容为1196原字节+24差异；两集合必须只相差同一个 MyTMP 路径且其余行的原/现SHA完全一致。未知路径、未知SHA、混用身份、缺依赖或额外编译输入继续拒绝。

最终真实封存 `map-main-mytmp-unified-20261105-f03`，694成员/330源码输入，implementation digest=`25a7fbc7267a1eb09845f54794d22b985422d5c3af8dac4b0c0f504507ec068e`。先绑定独立 Standards/Spec 静态审阅，再由现有 sealer 写 Stamp/Host；整组源文件和字体 raw SHA 在 Editor 再次核验。原清单未改写，不关闭检查，不把25项差异说成原字节一致。新封存回执在原 Authoring/VersionedExtension/HostSeal-* 目录，早期f01/f02及失败保留。

**实际变更清单（路径相对主工程根）**

| 路径 | 必要原因 |
|---|---|
| Assets/Font/MyTMP.asset | 复用已准备的新版字节，原meta不变 |
| Assets/MapGeneration/MapVNext/I1/Resources/CandidateContent_I1_MyTMPV1.json | 新字体内容身份，只有字体pin更新 |
| Assets/MapGeneration/MapVNext/I1/Resources/CandidateContent_I1_MyTMPV1.json.meta | 新清单唯一Unity身份 |
| Assets/MapGeneration/MapVNext/I1/Recipe_MyTMPV1.asset | 复用原数值，引用新清单 |
| Assets/MapGeneration/MapVNext/I1/Recipe_MyTMPV1.asset.meta | 新配方唯一Unity身份 |
| Assets/Scenes/SampleScene.unity | 配方目录注册当前与精确旧版本 |
| Assets/Scenes/MapVNext/I1/I1Game.unity | 原I1入口引用当前配方并保留旧配方 |
| Assets/Scenes/MapVNext/I1/Runtime/MapVNextI1Bootstrap.cs | 沿用同一显式内容目录 |
| Assets/Scripts/MapGeneration/SpatialVersion/MapHostCompatibility.cs | 严格选择25/24差异集合并校验唯一字体差 |
| Assets/Scripts/MapGeneration/SpatialVersion/MapHostBuildStamp.cs | sealer真实生成f03编译身份 |
| Assets/MapGeneration/MapVNext/I1/Resources/MapVNextI1Host.json | sealer真实生成完整受审组 |
| Assets/Editor/MapVNext/MapVNextI1Build.cs | 对当前和保留内容分别检查，不混报数量 |
| Assets/Editor/MapAcceptance/AssetNameWarningAudit.cs | 显式main字体/路径/引用只读探针 |
| Assets/Tests/MapVNext/EditMode/MapVNextI1CompatibilityTests.cs | 字体差异集合及身份混用正反例 |
| Assets/Tests/MapVNext/PlayMode/MapVNextFormalDefaultTests.cs | 限显式环境/真实main的保存重开与GameView采集 |
| Tools/MapVNext/seal_i1_host.py | 复用现有封存，绑定真实dirty基线和逐输入审阅；当前内容参数必填 |
| Tools/MapVNext/test_i1_seal.py | 基线delta允许/拒绝检查 |

另新增本说明，只向 `RPG-Map-Generation.md` 和本地 `.codex/HANDOFF.md` 追加本轮真实记录，保留原其他内容。可审阅的逐文件本任务差异见 Evidence 的 task-only.diff；相对本轮备份/实际 source-baseline，而非把全部既有 dirty 纳入。未更改 TowerFloorRuntimeController、MerchantPlacementService、TowerRunCoordinator、TowerRunSlotStore、地图算法/数值或交易/背包逻辑。

| 功能 | 旧实现 | 本次实现 | 关系 | 原因 |
|---|---|---|---|---|
| 地图存储 | TowerRunRuntimeEnvironment / SlotStore固定槽 | 根据实际入口精确备份/清理5文件 | Direct Reuse | 无第二存档权威或全局清理 |
| 字体 | 原MyTMP及GUID引用 | 同一资产新版已准备字节 | Extension | 保持全部现有引用 |
| 内容选择 | 现有配方有序目录/expected content解析 | 注册新配方与原精确旧身份 | Extension | 新冒险当前内容，旧职责不冒名 |
| Host | schema2有限组/实际源校验 | 精确25/24集合、真实新Seal | Extension | 字体变化需有真实版本证据 |
| 历史内容 | MainV1/旧VNext清单和配方 | 原字节保留 | Intentional Compatibility | 仍有有效入口/证据职责 |
| 验证 | 原Guard/Supervisor/测试程序集 | 有界main探针与新会话断言 | Extension | 不建设第二运行器/门禁 |

**实际验证与边界**

| 层级 | 结果与原始证据 |
|---|---|
| 静态/来源 | 字体与商店来源逐字节一致、原meta保持、368包含于388；696审阅输入精确绑定，最终两轴open0；694Host成员与Stamp逐SHA一致 |
| 离线检查 | unit-sealer：11/11；缺少current-content参数的真实CLI反例在IO前拒绝，Stamp/Host字节未变 |
| 实际Editor加载/编译 | Probe-04，Run `8bc00c01eef54a36ba5471d7a025ca14`，probe.json无异常；自然0、unforced、Job/双锁/Guard FREE |
| 兼容正反例 | EditMode-04，Run `94123b3e09c5416bbb3bde92d8767455`，NUnit4/4；自然0、unforced、Job/双锁/Guard FREE |
| 清档后首次Play | ActualMain-New-01，Run `e82897db5204476f85621fa7f5434d07`，1/1；三槽Empty、原按钮开始新冒险、Playing、真实槽1保存成功；自然0/unforced |
| 独立新进程恢复 | ActualMain-Restore-Batch-01，Run `7a0d208204b7407cb9550392820a9778`，1/1；真实main原槽位、同run/content、完整World逐字节一致，存档未被恢复过程改写；自然0/unforced |
| 真实截图 | ActualMain-Restore-01，Run `c73a47debed74dae8807a71169de039e`，恢复/采图断言1/1，八张PNG；主进程自然0，但自有后代超出根进程生命周期，Supervisor回收，完整生命周期仍FAILED，不升级为PASS |
| 助手画面检查 | 八张原图已实际查看，所采样MyTMP文字缺字符0、材质引用有效，未见明显方块/缺字/粉色材质；不等于整套UI布局验收 |
| 人工视觉/交互 | 本轮人工视觉NOT_RUN，未新增实体键盘、交易/背包玩法或全系统验收；旧商店已确认范围不扩大 |

完整World SHA为 `930b1511aab6908a18bcbc3db4193c2d42616a8aab9d870d822b4d6f7691c297`，三进程逐字节相同。新冒险 runId=`23ca5b9d-263a-4c7a-bd5f-58669e1c724b`；槽位1 SHA=`64cb1ffa9c788e093fafd560b9800256edc16a4a2359f259f06b8f024a6e745d`。`world-and-save-comparison.json` 关联全部报告、磁盘文件及原始导出。

截图：`Evidence/ActualMain-Restore-01/` 下 `startup/hud/package/existing-main-shop-{1920x1080,1366x768}.png`。每张另有 `-texts.json`，记录活动TMP组件（不把每个活动组件都说成可见）；启动文字本身部分使用原uGUI Text。背包和商店沿现有开关打开，无购买或物品注入，商店是main原有效界面。本轮没有将V3.3整店迁入main。

失败原始证据保留：首个沙箱前检UNKNOWN且未启动；首次封存缺父证据目录且未写产品；Probe-02编译因不支持NUnit特性退出1；Probe-03探针字段名错误退出2；EditMode-01/02缺fixture根、03路径过长，均3/4。已分别修正非必要特性、实际TMP字段和测试适配参数/短路径。后续通过不改写这些失败。全部已启动Run的自有残留均清零、双锁释放，最终四树Guard FREE；不宣称非batch辅助进程退出问题已修复。

当前dirty工作区架构预览为 `Evidence/Architecture-current/viewer.html`，标识实际本机未提交状态；它不替代正式已发布main的干净快照。本轮不额外构建Windows Player，不重做多Seed地图、MainV1全量恢复、交易或系统回归；字体/清档目标以真实main Editor证据交付。

**下次操作与正常/异常标准**

在 `D:/UnityProjects/RPG_Demo` 打开 `Assets/Scenes/SampleScene.unity` 后进入Play，应出现“冒险”选择：槽位1是本轮验证创建的新内容档，槽位2、3为空；旧测试档不会再从 `.backup` 或 `.tmp` 回来。可点“开始新冒险”进入当前新内容，也可选槽位1恢复刚验证的新冒险。HUD、背包和现有商店继续按原GUID使用新版MyTMP。

正常：进入Playing、没有字体缺字/粉色材质或内容/Host不一致错误。异常：旧default/TRB2档重新出现、当前字体SHA/Host错误、恢复失败或新Console异常。发生异常保留新日志和具体画面，不自动覆盖新档、清空全部persistentDataPath或删Prefs。本轮已完成实施；后续按用户新反馈处理，不自动Git发布、整店集成或删除其他历史版本。

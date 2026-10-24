<!-- ECOLOGY-SUBSYSTEM-COMMIT-20261024-START -->
# 生态分布子系统本地保存版本

全项目整合保持 `PAUSED_FOR_ECOLOGY_SUBSYSTEM_COMMIT_REVIEW`，旧R3/R4流水线保持STOPPED。本轮仅做生态分布子系统及直接依赖的精确本地保存，不合入main、不写远端、不Build/Player或发布。

本地分支 `codex/ecology-subsystem-20261024`，工作树 `D:/.codex/worktrees/ecology-subsystem/RPG_Demo`，父基线 `f9577ac65eb5d71b6a9790a6b01f456b252d5bcc`；真正main仍 `d9f60d608ea617dadb0ef4ce60553f7b306a0b94`。确切提交SHA由Git日志及 [提交回执](D:/UnityProjects/RPG_Demo/Docs/Integration/EcologySubsystemCommit/Evidence/commit-receipt.json) 查询，避免在提交中自引用自己的SHA。

当前内容 `content-r1:c55fb3b4b0362e8bb59e84f4624fbb22c2f9ef96f3dcd5f61481a92dab9c9802`，1220 pins；原R3/R4 manifest原字节保持。原工作目录3497输入、61路径、三正式槽/两侧档、受验快照与暂停候选均保留。原M00149/50仍FAIL；其生态池失败已在本分支最终E010原生12类型/24activation门禁复验，不修改旧证据。

必要业务验证及来源、精确文件SHA/GUID、采用/排除、保护与回退见 [独立审查报告](D:/UnityProjects/RPG_Demo/Docs/Integration/EcologySubsystemCommit/SUBSYSTEM_COMMIT_REVIEW.md)。E00727/27、E005 writer1/1、E008独占reader1/1、E01013/13均自然exit0、无force/残留、双锁释放。E00310/10断言/自然root0但child grace强制，E004 EditMode15/15断言但退出TIMEOUT；原FAIL/TIMEOUT保留，不作为完整PASS。后者是未解决的Editor退出问题，非已发现生态断言错误；本地功能保存以真实入口/布局/运动/安全/换层/原生池/精确及跨进程保存的有效门禁为依据，main整合时仍需单独处置该工具链问题。

T13/B1 HIGH_RISK、Boss CONTENT_PENDING、全Seed/长期性能/本版本人工体验NOT_RUN保持。历史R4人工4/4只限其原版本。所有旧阶段自动推进语句只作历史，本任务完成即停止，恢复全项目须用户明确授权。
<!-- ECOLOGY-SUBSYSTEM-COMMIT-20261024-END -->

# 最新状态｜R4 四项人工验收 PASS；主工程集成待批准

R4_MANUAL_ACCEPTANCE=PASS_4_OF_4，来源是用户实际体验 ReviewR401 的 **USER_CONFIRMED**：岩壁碰撞、植被位置、水中泡沫、阴影瓦片均 PASS。R4_INTEGRATION=PENDING；RELEASE=NOT_AUTHORIZED。此结论优先于下方旧“待复验/FAIL”交付快照，旧R3失败与原始证据按历史保留，不能改写旧Run。原六项人工PASS只限原范围；不声称六项又在R4人工复验。T13/B1仍HIGH_RISK，Boss仍CONTENT_PENDING；VIS-F1=A/VIS-S1=A和其他既定规则保持。

本轮只做归档和只读审计：Main原3457输入保持，R4原受验3490输入保持＋2个独立Editor截图测试；Main/R4有54文件差异（新增33/修改21/删除0），无Scene/Prefab/动画/Packages/ProjectSettings差异。已知R3冻结清单有15个共享脚本与R4不同，R3→R4精确恢复尚未验证，须在集成前处理，不自动扩展退役规则。

阅读：[本次人工确认](DECISION_ROUNDS/2026-10-24-r4-human-acceptance-integration-audit.md) · [受控集成预审方案](R4_CONTROLLED_INTEGRATION_PLAN.md) · [当前HANDOFF](HANDOFF.md)。停止状态 **R4_HUMAN_ACCEPTED_INTEGRATION_APPROVAL_PENDING**。本轮未写游戏代码/资源/配置、未运行Unity/Player/测试/Build、无Git写。下方连续实施授权和旧暂停/运行状态均按当时历史阅读，本轮边界仅归档＋只读预审，等待用户批准具体方案。

---

# 最新交付｜R4 自动候选完成，等待统一人工复验

实际入口：[完整验收报告](C06_C07_ACCEPTANCE_REPORT.md)；本地HTML/Windows启动器/多Seed实际图和105段分范围视频位于 D:/UnityProjects/RPG_Demo/Artifacts/MapAcceptance/NightRunC06C07-20261023/ReviewR401/index.html。当前BuildR405：Run286普通版、Run287开发版4固定Seed完整通过且13项核心字段逐项一致；Run288当前游戏源码完整50/50。另Run289独立Editor严格阴影补验1/1，887动画格固定后，两组阴影投影范围外像素差0。所有作业自然0、非强制清理、无残留、双锁释放/postFREE。

这不是同一次51/51，也不是人工通过。原六项人工PASS按原范围保留；岩壁与植被仍人工FAIL，泡沫和阴影等待用户体验。VIS-F1=A、VIS-S1=A与原玩法/T13-B1不变，B1仍有限观察HIGH_RISK。原始FAIL/TIMEOUT、失败包、入口和帧保留。旧Main R3生产输入/包/档保全，只有显式隔离R4候选；无Git写或正式标准推广。主受验3490输入保持，仅新增两个Editor测试文件不入Player；当前工作输入3492。精确接续以[HANDOFF](HANDOFF.md)为准，下方历史按当时范围阅读。

---

# 当前进度索引｜2026-10-24T02:20:39.171981+08:00

用户已明确恢复，当前事实以[HANDOFF](HANDOFF.md)和[无人值守执行日志](UNATTENDED_EXECUTION_LOG.md)为准。Run268当时47/47全门槛；Run271两版Windows构建成功；Run272实机第二Seed因静态角色截断声明楼梯而失败，原件保留。Run273/274诊断后R4增加完整脚底的所有声明楼梯可达/双向资格(DELEGATED_DECISION R4-STAIR-ACCESS-001)，不改Seed/预算/原脚底/B1；Run275新回归断言1/1，退出尚未完成，整体未通过。新修复需完整回归、新Build与新Player，不将旧包称最终成果。原六人工PASS范围、岩壁和植被人工FAIL、新视觉待复验全部保持。下方历史状态按当时范围阅读。

---

# RESUMED_BY_USER｜2026-10-24 R4 持续整改中

用户已明确恢复并授予D0-D2自主审阅/实施/阶段衔接；不再以昨日暂停阻止执行。VIS-F1=A、VIS-S1=A保留，原六人工PASS仅原范围，岩壁/植被人工FAIL与最终人工RETEST_PENDING不变。实际进展见[接续日志](UNATTENDED_EXECUTION_LOG.md)和[HANDOFF](HANDOFF.md)，本段优先于下方暂停/早期未实施历史。

R4显式独立工程补旧Mountain实际立面、动物完整轮廓候选资格，并在新recipe显式排除不足minimumGroup的孤立组件；旧主R3生产输入/包/档保全，无Git写。Run264八Seed均正常Ready、非零装饰/每图5植物/违规0，人口计划=实例=Registry。Run266完整32阶梯/并行高度断言PASS但后代强制清理，生命周期未通过。Run267合并46/47，256邻接mask/视觉清理、R4新手至3层人数/新槽精确恢复/B1/Barrel已通过；1个旧配方fixture身份错误已定位，待Run268。Run267自然2/非强制清理PASS/无残留/双锁释放/postFREE，不冒充整体通过。新R4 Windows包仍尚未产生；原始失败均保留。

---

# PAUSED_BY_USER｜2026-10-23 安全暂停（最新状态）

用户要使用电脑打游戏，当前仅完成收尾与保全，禁止新增 Unity、测试、构建、Player、采集或开发；不是任务完成、失败或取消。VIS-F1=A、VIS-S1=A 和既有自主决策保留，连续实施须等用户明确恢复。原六项人工 PASS 仅保留原范围；岩壁碰撞、植被仍为人工 FAIL，泡沫/阴影人工验收 PENDING。没有新的 R4 Windows MVP。

2026-10-23T19:33:33.771980+08:00 实测：Unity/Player/本任务作业均无活动进程（查询器自身单列排除）；主工程与 R4 的原 Guard 均 FREE，全机 heavy 锁非阻塞探测 FREE，探测自有句柄已释放。最后 Run258 自然退出0，cleanup PASS、forced=false、无残留、项目与全机锁释放，无需取消。Run259 在启动前被自动审批审查因本次暂停拒绝，目录不存在，状态 NOT_STARTED_PAUSE_REVIEW_REJECTION；不得当作测试失败或补启动。

真正停止点、全部待办、受控恢复命令与保全回执看 [HANDOFF](HANDOFF.md)；逐 Run 事实看 [C06/C07 实施记录](C06_C07_IMPLEMENTATION.md)。进程/锁证据为 Evidence/C06C07Implementation-20261023/SafePause-20261023/process-lock-state.json。下方所有“继续/正在运行/待确认”是保留历史，以本段为准。

---

# C06C07 当前实施｜已复现遗漏的旧Mountain立面，R4显式候选继续

VIS-F1=A、VIS-S1=A已确认；从高地碰撞优先实际实施。当前Run255规则平台PASS、旧Mountain立面四家族实际原Move反例FAIL；具体证据与R3保全/R4独立工作位置见[C06/C07实施记录](C06_C07_IMPLEMENTATION.md)。不把局部根因当全方向已修，不把视觉确认当测试通过。当前OPEN_READY=0、旧档/Git/Guard保护保持；原六人验PASS与岩壁/植被FAIL不改。下方旧检查点为历史。

---

# NIGHT-RUN-C06C07-20261023｜课程已实读，项目视觉方案待审核

课程与本地事实见[唯一课程实证笔记](NIGHT_RUN_C06_C07_VIDEO_STUDY.md)，五图/技术合同/本轮全部可决项见[项目方案](NIGHT_RUN_C06_C07_VISUAL_PROPOSAL.md)及[离线图页](D:/UnityProjects/RPG_Demo/Artifacts/MapAcceptance/NightRunC06C07-20261023/DesignPreview01/index.html)。两集已实际播放、自动字幕全文私人保存、关键画面核对；不是逐帧完整视频审阅。图片均DESIGN_PREVIEW，未改游戏输入、未启动Unity/Player、无Git写。下方带日期的旧状态保留为历史，不覆盖本段。

当前OPEN_READY=2：VIS-F1泡沫范围、VIS-S1阴影类型/强度；推荐不是用户选择。原P7外部内容读取障碍已解除，精确岸线语义处于RULE_PROPOSED / VISUAL_APPROVAL_PENDING，不再要求用户提供MP4或逐帧解说。岩壁/植被维持FAIL_USER_REPORTED，原六项人工PASS按既有范围保留。正式新视觉采用等待附件§8要求的看图确认。

---

## R3 最终交付｜2026-10-23

AUTOMATED_CANDIDATE_DELIVERED_HUMAN_REVIEW_PENDING。当前最新为BuildR308 / ReviewR301，不再使用下方中间包或Review09当最新交付。[完整整改验收报告](MANUAL_REMEDIATION_ACCEPTANCE_REPORT.md)给实际Windows入口、34张实机图、13段视频、人口/Barrel时序/多Seed/退役影响和所有限制。Run250两版构建与Run251/252完整两版实机均自然0、非强制清理PASS/无残留/双锁释放/postFREE；源3457稳定，四固定图跨包一致。Run238生产55PASS/1历史N/A，旧FAIL和其他N/A仍保留。

最终保护读回AfterRun252Final PASS：六原档/保护输入/五共享工具/私有归档保持，260计划退役文件均已移出，无额外GUID漂移，index语义同HEAD/staged空。原P7样式WAITING_REFERENCE_DETAILS、B1 HIGH_RISK及人验PENDING不变；无正式发行/Git写。下方阶段进度均为保留的历史快照，以本段与新报告为准。

## AUTONOMOUS-CONTINUATION-20261023｜当前持续规则

用户已追加本任务范围内D0调查/D1普通工程/D2设计取舍自主决定与连续实施授权；仅D3危险/重大已确认规则冲突/越界暂停受影响操作。实际使用正式grill-me-game，规则唯一入口为[自主决策协议](AUTONOMOUS_DECISION_POLICY.md)，新增受托选择记DELEGATED_DECISION，不能冒充用户确认。原十项与入口A仍是用户明确确认。水沫精确P7保持REFERENCE_REQUIRED，允许有依据的隔离工程候选，不阻断其余整改。Git禁写/旧档/共享Guard与Supervisor/保护输入不变，最终人验PENDING、不自行正式发行。

实际进展唯一入口为[本轮执行记录](MANUAL_REMEDIATION_EXECUTION.md)，当前接续看[HANDOFF](HANDOFF.md)。每个阶段分别记录实现、运行、生命周期和人验；下方旧快照保留为历史，不据此重复已完成工作。退役兼容范围及受托决定看自主协议AUTO-005。

# RPG_Demo 生态分区与实体布置

## MANUAL-ENTRY-A-CONFIRMED-20261022｜入口A已由用户明确批准

用户本聊天明确选择A并追加完整授权：本地整改工作稿的普通“开始新冒险”切到本轮独立新身份Units生态配方；不覆盖/冒充旧terrain-v2-xiaosu-v1。先资源备份、引用清理、入口适配，再安全移出专用素材；旧四怪物配方档明确拒绝，原档和旧Build保留。新手/第一关/至少一个后层必须真实核验每区目标/实例/Registry。继续Barrel、岩壁和植被，不逐项等待；本地入口切换不代表发布或最终人验通过。OPEN_READY=0。

新增身份暂定ecology-zoning-units-remediation-v3（工程命名），新配置和内容摘要另建。原Units候选/旧档身份也不能原地改写：其保留清单包含旧动画、Prefab和18项算法源码，必须通过版本化输入/兼容分支保护，不能更新旧哈希伪称旧版。新Barrel资产使用独立版本，旧原生Barrel资产只为旧12种身份兼容保留；四购买怪物仍完整移出新版活动输入。旧路径不可用时明确拒绝，不套新配方。


## MANUAL-REMEDIATION-RESPONSE-001｜十项已确认，连续实施已恢复

用户已明确回答十项，详见[正式答复与单次结算合同](DECISION_ROUNDS/2026-10-22-manual-remediation-response-001.md)。当前OPEN_READY=0；不重复已答问题。只伤玩家＋提前击杀不爆炸使正常连锁分支不适用，不新增独立点燃通道。原死亡/地面掉落/拾取/清场目标链可唯一复用，无新增奖励归属问题。三秒三有效帧一次播放、保护/返回优先、同高程静态阻挡已确认。

当前先取得未调参正常新手/第一关Profile、计划/生成/Registry联表，再修根因并落实群体/楼层政策。四购买素材退出新版活动输入前必须私有归档/引用审计/旧版拒绝保护，保留共享成果及全部旧证据。水沫WAITING_REFERENCE_DETAILS仅暂停该切片。后续经原Guard/Supervisor实测与修复，最终候选统一人验；无Git写、无正式存档覆盖、不改受保护Settings/玩家Collider。下方旧调查门禁和未答表为历史，以本次用户答复和最新执行记录为准。


## MANUAL-REMEDIATION-20261022-R1｜当前整改入口

用户已人工体验并报告六类问题，现阶段为调查＋grill决策，禁止修改正式游戏/资源/配置、删除素材、切默认、启动Unity/Player或执行Git写。原Build09自动结果/旧FAIL/TIMEOUT保留，不能再把“尚未人工体验”当当前状态。

本轮调查、十项可决政策、技术契约与证据缺口统一见[人工验收整改第001轮](MANUAL_ACCEPTANCE_REMEDIATION.md)。本轮新明确方向为四购买怪物及专用素材退出活动Assets/正式Build输入，删除前私有备份与反向依赖审计；旧“只排除生成资格且活动Assets保留”已被此目标取代，尚未实际删除。旧档政策、人口体验细化及Barrel新攻击政策仍OPEN；27项既有玩法与T13有限观测语义保持，只有本轮明确选项被批准后才调整相应合同。后文带日期的交付/旧授权记录保留为历史，不覆盖本段当前门禁。


2026-10-22T20:07:58+08:00：Build09显式Windows候选和实际对照已交付，人工统一验收待完成。项目文档为权威源，Obsidian为单向阅读副本；旧失败与阶段原件继续保留。

先看[真实画面、3组旧新对照与14段视频](file:///D:/UnityProjects/RPG_Demo/Artifacts/MapAcceptance/EcologyUnitsCandidate-20261022/Review09/index.html)，体验使用[经过包SHA校验和原Supervisor的普通版入口](file:///D:/UnityProjects/RPG_Demo/Artifacts/MapAcceptance/EcologyUnitsCandidate-20261022/Review09/Open-Units-Candidate.ps1)。脚本的`-VerifyOnly`只查包、不启动Player；正常体验不启用测试探针或测试槽覆盖。

| 内容 | 当前事实 / 阅读入口 |
| --- | --- |
| 完整交付、Run、版本、安排和限制 | [P6_EXECUTION](P6_EXECUTION.md) |
| B1与修订P0.V | Run40完整定向门槛通过，最新Run202合并81/81；[P0V_EXECUTION](P0V_EXECUTION.md) |
| 实际两版Windows | Build09两包Succeeded；Run205/206完整实际Player PASS，均自然0/非强制清理/无残留/双锁释放/postFREE |
| 新候选 | 原Torch/TNT/Barrel十二类型；四固定入口跨构建地形/布局/类型/版本/e一致，各原槽恢复和三家族攻防/F完成 |
| 四怪物 | 已完成成果、资产、GUID、旧配方/旧档保留；新增风格适配延期；[保留/退役影响](PRESERVATION_AND_RETIREMENT_IMPACT.md) |
| 配置与恢复 | [P5_EXECUTION](P5_EXECUTION.md)；精确保留版本，不以新候选替代缺失旧版本，正式六档保持 |
| 原槽额外Editor数据回归 | Run203 XML22/22，但Editor退出TIMEOUT300，自有清理PASS；整轮不记PASS |
| 玩法与技术合同 | [DESIGN](DESIGN.md) / [DECISIONS](DECISIONS.md) / [TECHNICAL_DESIGN](TECHNICAL_DESIGN.md) / [TECHNICAL_DECISIONS](TECHNICAL_DECISIONS.md) |
| 真实动作与跨层 | [P4_EXECUTION](P4_EXECUTION.md) |
| 接续、依赖和已保留风险 | [HANDOFF](HANDOFF.md) / [ACCEPTANCE](ACCEPTANCE.md) / [IMPLEMENTATION_PLAN](IMPLEMENTATION_PLAN.md) |
| 讨论和逐轮证据 | [DISCUSSION_LOG](DISCUSSION_LOG.md) / [EVIDENCE](EVIDENCE.md) |

T13仍HIGH_RISK，不可观测求解内部短暂接触漏检UNKNOWN；观察到的完整脚底接触和可信扫掠继续保护并永久取消旧击。原严格Run20/23 FAIL保留。人工体验、自然手感、美术采用均PENDING，新候选缺少真实兼容Boss，正式默认terrain-v2-xiaosu-v1不推广。14段视频是披露过安排的受控实机，不冒充全层自然击杀；180帧窗口不当长期性能。

最后源指纹`90d7c3d0997dc4a7c1f46d131de695da16de7d8d0044112998063ff04a4bbce1`，3630构建输入逐SHA保持；六旧档、1872旧meta、四购买Gameplay Prefab/meta、保护输入、共享工具和索引语义保持。无Git写，当前无活动Unity/Player；当前可做工作已交付，不自动发起正式推广或永久退役。

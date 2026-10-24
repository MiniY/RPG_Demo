# 最新状态｜R4 四项人工验收 PASS；主工程集成待批准

R4_MANUAL_ACCEPTANCE=PASS_4_OF_4，来源是用户实际体验 ReviewR401 的 **USER_CONFIRMED**：岩壁碰撞、植被位置、水中泡沫、阴影瓦片均 PASS。R4_INTEGRATION=PENDING；RELEASE=NOT_AUTHORIZED。此结论优先于下方旧“待复验/FAIL”交付快照，旧R3失败与原始证据按历史保留，不能改写旧Run。原六项人工PASS只限原范围；不声称六项又在R4人工复验。T13/B1仍HIGH_RISK，Boss仍CONTENT_PENDING；VIS-F1=A/VIS-S1=A和其他既定规则保持。

本轮只做归档和只读审计：Main原3457输入保持，R4原受验3490输入保持＋2个独立Editor截图测试；Main/R4有54文件差异（新增33/修改21/删除0），无Scene/Prefab/动画/Packages/ProjectSettings差异。已知R3冻结清单有15个共享脚本与R4不同，R3→R4精确恢复尚未验证，须在集成前处理，不自动扩展退役规则。

阅读：[本次人工确认](DECISION_ROUNDS/2026-10-24-r4-human-acceptance-integration-audit.md) · [受控集成预审方案](R4_CONTROLLED_INTEGRATION_PLAN.md) · [当前HANDOFF](HANDOFF.md)。停止状态 **R4_HUMAN_ACCEPTED_INTEGRATION_APPROVAL_PENDING**。本轮未写游戏代码/资源/配置、未运行Unity/Player/测试/Build、无Git写。下方连续实施授权和旧暂停/运行状态均按当时历史阅读，本轮边界仅归档＋只读预审，等待用户批准具体方案。

---

# 最新交付｜R4 自动候选完成，等待统一人工复验

实际入口：[完整验收报告](C06_C07_ACCEPTANCE_REPORT.md)；本地HTML/Windows启动器/多Seed实际图和105段分范围视频位于 D:/UnityProjects/RPG_Demo/Artifacts/MapAcceptance/NightRunC06C07-20261023/ReviewR401/index.html。当前BuildR405：Run286普通版、Run287开发版4固定Seed完整通过且13项核心字段逐项一致；Run288当前游戏源码完整50/50。另Run289独立Editor严格阴影补验1/1，887动画格固定后，两组阴影投影范围外像素差0。所有作业自然0、非强制清理、无残留、双锁释放/postFREE。

这不是同一次51/51，也不是人工通过。原六项人工PASS按原范围保留；岩壁与植被仍人工FAIL，泡沫和阴影等待用户体验。VIS-F1=A、VIS-S1=A与原玩法/T13-B1不变，B1仍有限观察HIGH_RISK。原始FAIL/TIMEOUT、失败包、入口和帧保留。旧Main R3生产输入/包/档保全，只有显式隔离R4候选；无Git写或正式标准推广。主受验3490输入保持，仅新增两个Editor测试文件不入Player；当前工作输入3492。精确接续以[HANDOFF](HANDOFF.md)为准，下方历史按当时范围阅读。

---

## AUTONOMOUS-CONTINUATION-20261023｜当前持续规则

用户已追加本任务范围内D0调查/D1普通工程/D2设计取舍自主决定与连续实施授权；仅D3危险/重大已确认规则冲突/越界暂停受影响操作。实际使用正式grill-me-game，规则唯一入口为[自主决策协议](AUTONOMOUS_DECISION_POLICY.md)，新增受托选择记DELEGATED_DECISION，不能冒充用户确认。原十项与入口A仍是用户明确确认。水沫精确P7保持REFERENCE_REQUIRED，允许有依据的隔离工程候选，不阻断其余整改。Git禁写/旧档/共享Guard与Supervisor/保护输入不变，最终人验PENDING、不自行正式发行。

实际进展唯一入口为[本轮执行记录](MANUAL_REMEDIATION_EXECUTION.md)，当前接续看[HANDOFF](HANDOFF.md)。每个阶段分别记录实现、运行、生命周期和人验；下方旧快照保留为历史，不据此重复已完成工作。退役兼容范围及受托决定看自主协议AUTO-005。

# 生态子系统技术设计修订基线 v0.2

## MANUAL-ENTRY-A-CONFIRMED-20261022｜入口A已由用户明确批准

用户本聊天明确选择A并追加完整授权：本地整改工作稿的普通“开始新冒险”切到本轮独立新身份Units生态配方；不覆盖/冒充旧terrain-v2-xiaosu-v1。先资源备份、引用清理、入口适配，再安全移出专用素材；旧四怪物配方档明确拒绝，原档和旧Build保留。新手/第一关/至少一个后层必须真实核验每区目标/实例/Registry。继续Barrel、岩壁和植被，不逐项等待；本地入口切换不代表发布或最终人验通过。OPEN_READY=0。

新增身份暂定ecology-zoning-units-remediation-v3（工程命名），新配置和内容摘要另建。原Units候选/旧档身份也不能原地改写：其保留清单包含旧动画、Prefab和18项算法源码，必须通过版本化输入/兼容分支保护，不能更新旧哈希伪称旧版。新Barrel资产使用独立版本，旧原生Barrel资产只为旧12种身份兼容保留；四购买怪物仍完整移出新版活动输入。旧路径不可用时明确拒绝，不套新配方。


## MANUAL-REMEDIATION-RESPONSE-001｜十项已确认，连续实施已恢复

用户已明确回答十项，详见[正式答复与单次结算合同](DECISION_ROUNDS/2026-10-22-manual-remediation-response-001.md)。当前OPEN_READY=0；不重复已答问题。只伤玩家＋提前击杀不爆炸使正常连锁分支不适用，不新增独立点燃通道。原死亡/地面掉落/拾取/清场目标链可唯一复用，无新增奖励归属问题。三秒三有效帧一次播放、保护/返回优先、同高程静态阻挡已确认。

当前先取得未调参正常新手/第一关Profile、计划/生成/Registry联表，再修根因并落实群体/楼层政策。四购买素材退出新版活动输入前必须私有归档/引用审计/旧版拒绝保护，保留共享成果及全部旧证据。水沫WAITING_REFERENCE_DETAILS仅暂停该切片。后续经原Guard/Supervisor实测与修复，最终候选统一人验；无Git写、无正式存档覆盖、不改受保护Settings/玩家Collider。下方旧调查门禁和未答表为历史，以本次用户答复和最新执行记录为准。


## MANUAL-REMEDIATION-20261022-R1｜当前整改入口

用户已人工体验并报告六类问题，现阶段为调查＋grill决策，禁止修改正式游戏/资源/配置、删除素材、切默认、启动Unity/Player或执行Git写。原Build09自动结果/旧FAIL/TIMEOUT保留，不能再把“尚未人工体验”当当前状态。

本轮调查、十项可决政策、技术契约与证据缺口统一见[人工验收整改第001轮](MANUAL_ACCEPTANCE_REMEDIATION.md)。本轮新明确方向为四购买怪物及专用素材退出活动Assets/正式Build输入，删除前私有备份与反向依赖审计；旧“只排除生成资格且活动Assets保留”已被此目标取代，尚未实际删除。旧档政策、人口体验细化及Barrel新攻击政策仍OPEN；27项既有玩法与T13有限观测语义保持，只有本轮明确选项被批准后才调整相应合同。后文带日期的交付/旧授权记录保留为历史，不覆盖本段当前门禁。


2026-10-22T20:07:58+08:00 现行状态：已确认27项玩法/13项技术方向保持，仅Q12.2/T05/T13保护历史按用户明确批准的B1有限观测语义增补。原严格Run20/23三接触FAIL和原代码保留；实际观察到的完整脚底接触及可信实际扫掠继续永久取消旧攻击。Run40修订P0.V为45/45、3504输入稳定、自然exit0、非强制清理/双锁释放/postFREE；完整门槛按Run40-full-gate-reconciled通过，另披露Git索引字节漂移原因UNKNOWN：2955条内容与同一HEAD一致、暂存空，首审NOT_PASSED原件保留。T13仍HIGH_RISK，不可观测碰撞内部接触漏检率UNKNOWN，人工验收PENDING，不直接上线。20261021旧四怪物P1 Windows视觉候选构建=Succeeded、定向Player=PASS：三Seed24楼梯/47原始动作帧/4次真实池复用；阅读入口Artifacts/MapAcceptance/EcologyP1-20261021/Review/index.html。Build02绑定较早P1源，完整生态须P6最后重建。Run51正式TRB2旧档只读副本隔离恢复15/15/0Skip，六旧档保持；无Git写、不改共享Guard/Supervisor，不切正式新默认。 后续实测：修订完整P0.V由Run40通过，严格Run20/23 FAIL保留；最新Run202合并81/81、0Skip。Build09开发/普通Windows及Run205/206完整Player均自然0、非强制清理/无残留/双锁释放/postFREE；四固定入口原地形/静态几何/布局/类型/版本/e跨构建一致，均完成原槽精确恢复与三家族原输入攻防/死亡回池/实际F。新显式12种Profile ecology-zoning-units-float32-candidate-v2，无Boss/ART_REVIEW_PENDING；四怪物已完成成果/资产/GUID/旧配方和旧档保留，新增风格适配延期，正式默认terrain-v2-xiaosu-v1未推广。原版66数值样本跨构建16差异，独立数值版本0地形/装饰差异；旧R1含义不重写。查询优化经2520扫掠精确对照及B1全组回归；新Player四图180帧窗口P50约3.84ms、P95约3.86～11.61ms，不当长期性能或人验。另Run203原槽EditMode断言22/22但退出TIMEOUT300、自有强制清理PASS，生命周期失败原样保留。T13仍HIGH_RISK，不可观测内部短暂接触漏检UNKNOWN，人工PENDING。交付14实际视频/3旧新对照/正常黑线图与绑定，最终Guard FREE；六旧档/1872旧meta/保护输入/共享工具/索引语义保持，无Git写。

## 0. 先知道现在到了哪里

大白话：现有工程已经会造地形、安排实体、切换地图、保存楼层入口。新任务是在这些结构上增加“谁的地盘、安全范围、居民归属和有限外出”，并把内部做法讲清。不能把旧地形块改个名字就当生态领地，也不能因为之前写过实施计划就认为技术选型已经获得确认。

当前分支 feat/tower-run-save-mvp，HEAD f9577ac65eb5d71b6a9790a6b01f456b252d5bcc。3485个运行输入文件与上一轮收尾聚合SHA一致，索引SHA一致；新 MapEcologyLayout / Planner / RuntimeQuery 在 Assets/Scripts 中没有实现。四怪物尺寸与双动画职责仍未修复。已有dirty全部保留，没有回滚。详见 [技术证据增补](EVIDENCE.md#技术审查增补2026-10-21) 和 Evidence/technical-review-initial-state.json。

上一次设计审查仅调查/文档，没有新增实现或运行；其3485输入指纹是历史基线。现在已按新的明确授权新增首个P0.V攻击链切片及夹具，未改场景、Prefab、原GUID、物理配置或Git索引。初轮CIM阻断已保留；本次同一探针经正常审批在普通宿主上下文成功，原Guard FREE并经Supervisor真实Run01～03，最新四例4/4/自然exit0/cleanupPASS。仅首个攻击链切片通过，完整运动/T13仍未验证，不凭缺锁或四例宣称整个系统PASS。

## 1. 已有覆盖差距与本次补充

这里的“已有”是补充前文档/源码状态。补齐文档不会把静态论证变成运行PASS；EXISTS_AND_EXPLAINED只表示既往内容本身足够明确，不表示用户已经批准技术选项。

| 主题 | 补充前状态 | 已有位置、代码接点 | 本次补充与剩余 |
|---|---|---|---|
| 27项玩法及入口恢复范围 | EXISTS_AND_EXPLAINED | DECISIONS；DESIGN §1–5 | 原规则保留，不重问 |
| 单一地形权威与生态附属计划 | PARTIAL | DESIGN §6/9；IMPLEMENTATION_PLAN §2–3；MapRuntimeFoundation:95 | §2–3字段、冻结/失效；T01-A已确认 |
| MapEcologyLayout等新模块 | EXISTS_NOT_EXPLAINED；实现MISSING | IMPLEMENTATION_PLAN §3提案；Assets/Scripts无类 | 完整Interface合同；不把提案写成代码 |
| 存储、序列化及稳定身份 | MISSING | 旧稿只列概念；SlotStore:668/797 | §2数据字典、DTO、缓存与身份 |
| 分区算法及类型/体型互依 | PARTIAL | DESIGN §6“多源生长＋噪声” | §4候选、队列、反例；T02-A/T03-A |
| 保护缓冲及玩家入区几何 | PARTIAL | DESIGN §2；移动/Hurt各入口 | §4/7；T04-A/T05-A |
| 配额、取整与放置容量 | PARTIAL | Profile:103旧全图公式；Policy:199 | §5手算、偏置、实际容量；T06-A |
| 追击/返回、寻路更新成本 | PARTIAL / CONFLICT | 旧FSM再追击、受伤跳Chase与新规则冲突 | §6状态/距离/缓存；T07-A/T08-A |
| 攻击永久取消与池复用 | PARTIAL | PlayerAction:235/284；AttackState:54/96 | §7锁目标事实、历史时序；T09-A |
| 随机域、摘要、配置版本 | PARTIAL | StableScore；旧TRB2/TRB3 | §8；T10-A |
| 入口检查点数据表达 | PARTIAL | DESIGN §5；CheckpointRecord:128 | §8恢复顺序与兼容；T11-A |
| 提交与失败可见性 | EXISTS_NOT_EXPLAINED | DESIGN §4已不承诺提交后回滚；Orchestrator:55 | §9完整事件时序；T12-A |
| 尺寸/动画修复 | 实现MISSING | EVIDENCE F12/F13；双脚本仍在 | §10列普通工程路径，最终足迹未定 |
| 成本、边界、验证 | PARTIAL | ACCEPTANCE已有目标，无测量 | §11–12；全部NOT_RUN/NOT_MEASURED |
| 现有旧生态代码 | IMPLEMENTED_NOT_REVIEWED（作为本次新技术审查对象） | Population/Registry/FSM/存档既有实现 | 已核对其事实；不把旧功能当新领地实现 |

## 2. 四种数据，四种寿命

大白话：配置像“造图说明书”；布局像“这张图的座位表”；运行状态像“这只怪现在在干什么”；入口存档像“下次按哪份说明书重开本层”。怪物挨打不能改说明书，对象回池也不能删掉地图的领地事实。

- 配置：版本化生态类型、Prefab/颜色映射、足迹版本、密度/预算/商人资格。规划读取，不被AI修改。
- 已接受布局：依附同代MapData的只读结果。候选期间只有Planner能写；验证、冻结后一次发布。换代整体失效，GameObject不是其存储形式。
- 运行状态：每次激活私有，AI/移动/攻击分别通过约定入口写；不能写回配置或布局。
- 持久化与缓存：存档只保入口身份及选定恢复数据；节点邻接、区域成员索引、回家距离场、黑线可重建。缓存带完整版本键，失效后重建，不能自行生成另一份领地。

下表是提案字段，不是现有类型已经拥有这些字段。为便于阅读，写入者P=Planner，C=原MapRuntimeContext/提交链，R=原运行控制器，S=原槽位存储。读者以实际职责写明。“保存=身份”指入口配方/manifest；已选T11-A不保存布局正文，表中布局字段从精确配方重建。原B正文方案仅历史比较、不适用。

### 2.1 配方、地图与空间

| 字段 | 类型/结构 | 含义 | 来源 | 写入者 | 读取者 | 生命周期 | 默认/非法值 | 是否保存 | 不变量 |
|---|---|---|---|---|---|---|---|---|---|
| RequestedSeed / TerrainAttempt | int / int≥0 | 地形请求与实际尝试 | 现Generation record | 原协调器 | 生成/恢复 | 本层入口 | attempt<0非法 | 已有 | 生态重排不改 |
| GenerationId | Guid | 本次运行地图代次 | 原Orchestrator新Guid | C | Registry/回调 | 一次提交 | Empty不可GameplayReady | 否 | 不进稳定布局摘要 |
| GeneratorVersion / ConfigurationIdentity / ProfileId | int/string/string | 原生成路由 | 原记录/版本路由 | 原协调器 | 生成/兼容 | 版本/入口 | 空身份非法 | 已有 | 旧分支不重定向 |
| EcologyRecipeVersion / ConfigDigest / ContentMapVersion / FootprintVersion | int/string/string/string | 算法、配置内容、类型映射、体量合同 | 新版本化配置 | P读入后冻结 | 规划/恢复 | 配方/入口 | 未知版本拒绝 | 身份 | 同名改内容不能冒充同配方 |
| TerrainBaseline / EcologyDigest | string/string | 重建核对值 | 原TRB + 新规范编码 | P/C | 恢复/诊断 | 一份布局 | 空/不符拒绝Ready | 身份 | 不含InstanceID/日志RunId |
| EcologyAttempt | int [0,B) | 同地形第几次候选 | 有界内部循环 | P | 签名/恢复/诊断 | 候选→接受 | -1未接受 | 身份 | 与地形attempt不同 |
| RunId / RequestId | string/Guid | 日志与操作关联 | 原流程/诊断 | 原协调器 | 报告 | 请求 | 空可诊断但不得当稳定ID | 原RunId按旧合同 | 不决定分区 |
| NodeId | int [0,W×H) | 逻辑格的本图索引 | i=(y-Origin.y)W+x-Origin.x | P | 全部空间查询 | 地图 | -1无节点 | 否（入口重建） | 与MapData同尺寸/Origin |
| Terrain / Elevation / StairEdge | 原MapCell/楼梯记录 | 可走/高程/跨层事实 | MapData唯一权威 | 原地形生成 | P/R | 地图 | 越界、非显式跨层非法 | 原基线，非生态复制 | 四邻、同高或原合法梯边 |
| WorldMapping | 原MapCoordinateBoundary | 逻辑格↔世界 | 实际Tilemap | 原投影 | 生成/移动/画线 | 当前投影 | 转换失败拒绝 | 否 | 不重复加Origin，不以z造第二地图 |
| RegionByNode | int[W×H] | 节点所属区域 | P生长结果 | P冻结 | AI/分配/画线 | 布局 | -1非生态节点 | 否（入口重建） | 一个合格节点至多一个区 |
| ProtectionFlags | byte[W×H] | 核心/缓冲等资格 | 安全规划 | P冻结 | 移动/战斗 | 布局 | 0不保护；未知bits非法 | 否（入口重建） | 资格不同于occupancy |
| StaticClearance / TraversalEdges | 派生数组/邻接索引 | 类型合法中心/边 | 地形+静态碰撞+足迹 | 查询构建器 | P/R | 缓存版本 | 不可达=-1/false | 否 | 从权威重建，禁止反写地形 |

MapData当前内部仍是[,]，无需为新生态重写。生态运行时推荐线性数组：64×64共4096槽，int区域索引约16KiB、byte标记约4KiB（不含对象头）。稀疏Dictionary对极稀疏大图才更省槽位，但此处增加哈希和分配；这是E类选择，不让用户审批数组行序。数组只存逻辑数据，不直接把[,]或Dictionary交给JsonUtility。

### 2.2 区域、类型与居民

| 字段 | 类型/结构 | 含义 | 来源 | 写入者 | 读取者 | 生命周期 | 默认/非法值 | 是否保存 | 不变量 |
|---|---|---|---|---|---|---|---|---|---|
| RegionId / Kind | 稳定int / enum | 普通、Boss、安全商人、安全休息、中立 | 种子稳定排序与规划 | P | 所有消费者 | 布局 | -1/未知非法 | 否（入口重建） | 两安全；Boss0/1；种类不由画线决定 |
| Members / Neighbours | int[] / 去重邻接表 | 反向索引/真实相邻 | RegionByNode+合法边派生 | 构建器 | 面积/采样/调试 | 缓存 | 重复/跨代非法 | 否，重建 | 区域成员与正向映射一致 |
| EcotypeId | 稳定string | 种类＋颜色 | 明确版本映射 | P选择 | Pool/AI/恢复 | 配方/布局 | 空只允许非居民区 | 否（入口重建） | 不用Prefab显示名猜颜色 |
| PrefabRef / AssetGuid | Unity引用 / 字符串映射 | 运行资源/作者身份 | 配置引用与受控内容映射 | 资产作者 | Pool/内容验证 | 内容版本 | 丢引用失败 | 存内容ID，不存运行引用 | Player中用配置引用解析；GUID不等于InstanceID |
| EffectiveArea / LegalCandidateCount | int / int | 合法活动节点/可放点数 | 指定足迹图 | P | 配额 | 一次候选 | <0非法 | 否（入口重建） | 不是W×H；候选数≠可同时摆放量 |
| ConstructiveCapacity / TargetCount | int / int | 已构造可行放置下界/目标 | 间距+占位打包结果/配额 | P | 实例化/验收 | 布局 | target>capacity非法 | 否（入口重建） | 不把困难的最大打包问题称精确容量 |
| SafeCore / SafeBuffer | flags+RegionId | 保护语义来源 | T04算法 | P | 保护查询 | 布局 | 与另一安全连通/接触不合法 | 否（入口重建） | 保护=主体并缓冲 |
| Spawn/Exit/MerchantClearance | 原Reservation及用途 | 出生/出口/交互的保留空间 | 原静态规划+商人计划 | 原Plan/P | 其他布置 | Plan | 冲突拒绝 | 原基线；新布局重建 | 不能把安全全区当实体占位 |
| ResidentId / HomeRegionId | string / int | 初始居民与永久原领地 | 区域稳定ID+居民序号 | P冻结 | Registry/AI/清场 | 本层布局 | 空/不存在区域非法 | 否（入口重建） | 临时跨区不改home |
| InitialNode / InitialPose | int / 原投影位置 | 出场点与朝向输入 | 合法采样 | P | Population | 布局→激活 | 不可达/足迹相交非法 | 否（入口重建） | 不用当前Transform回写 |
| MerchantId / CatalogId | merchant.main / string | 唯一商人/既有商品目录 | 现商人/ShopCatalog | P/原商店 | 初始化/入口库存 | 布局/入口 | 重复商人非法 | 原库存合同 | 商人不入Monster击杀集 |

规范表示候选T01选择发布位置；无论选哪种，都推荐RegionByNode作为成员归属规范表示，Members/Neighbours/黑线从它重建。冻结时交叉核对每个区域面积、连通、成员覆盖和邻接；消费者只能读取。外部只拿IReadOnlyList不等于内部不可变，必须停止TryAdd或复制后封闭写口。无需一张“第二份生态地形表”。

### 2.3 运行身份、许可、攻击

| 字段 | 类型/结构 | 含义 | 来源 | 写入者 | 读取者 | 生命周期 | 默认/非法值 | 是否保存 | 不变量 |
|---|---|---|---|---|---|---|---|---|---|
| GenerationId+ResidentId+ActivationLease | Guid/string/ulong | 某居民本次池激活 | Plan绑定+单调激活序号 | Population/激活入口 | AI/回调 | 一次激活 | lease0未绑定 | 否 | 同GameObject复用后身份不同 |
| BindingReady / Retired / Defeated | bool | 可输出行为/失效/首次死亡 | 原生命周期扩展 | R | AI/伤害/清场 | 激活 | 初始未就绪 | 否 | 未绑定不移动/攻击；退休不等于击败 |
| HomeRegionId / HomeNode | int/int | 原地盘与合法归返点 | Plan | 激活入口一次写 | AI/移动 | 激活 | 丢home失败 | 否，入口可重建 | 不因过境或挨打改写 |
| PermitId / DepartedAt / ReturnLatched | ulong/double/bool | 同次外出身份、开始时间、返程锁存 | 第一次出原领地 | 许可裁决入口 | FSM/移动/攻击 | 一次完整外出 | 无许可禁止继续外追 | 否 | 状态变化不续期 |
| OutsideElapsed / HomeDistance | 秒/图步或世界单位 | 持续外出时长/原领地距离 | 世界时钟/T07 | 许可查询 | 行为/诊断 | 一次外出 | 不可达=∞ | 否 | 单位不混用；真实回领地前不清 |
| ReturnRetryCount / LastProgress | int/秒+位置 | 有限堵塞诊断 | 位移结果 | R | Return | 本次返回 | 超限失败，非死亡 | 否 | 不瞬移、不重置生命 |
| AttackId / Status | ulong / Pending,Invalid,Consumed | 一次攻击 | 起手 | 原攻击控制器 | 延迟结算 | 起手→取消/结算 | 0无攻击 | 否 | Invalid不可回Pending |
| CastStartOrder / BoundTargetLease | ulong/激活身份 | 起手先后、目标约束 | 起手 | 攻击控制器 | 命中 | 一次攻击 | 已选T09-A冻结目标身份 | 否 | 不用帧号代替同帧先后 |
| ProtectionNow / LastTransitionOrder | bool/ulong | 当前保护及最近进出历史 | 统一位置/保护入口 | 空间规则记录器 | 攻击 | 激活 | 未初始化不可Ready | 否 | 同帧进出仍留下历史 |
| PendingDamage / RequiredCompletedStep / TerminalReason | 本代独立请求 / int / enum | 到期后排队、所需物理结果、取消或故障原因 | 原到期与step协议 | 本代队列唯一消费 | 原伤害接收者 | 入队→一次终态 | 未满足step不消费；Unknown非保护 | 否 | 新起手不覆盖；先终态后回调 |
| PathCacheKey | generation/footprint/policy/blockerRev/goal | 路径缓存有效范围 | 当前权威版本 | 查询构建器 | 移动 | 按需 | 任一不符失效 | 否 | 调试开关不进键 |
| DefeatFact | 原OnDefeated+代次/居民身份 | 一次真实死亡 | BaseDamageable | 原伤害死亡链 | Population/原楼层清场 | 本层 | 旧lease拒绝 | 不存战斗中途 | Drop一次，Count一次，回池非Count |

稳定Resident/Region ID能跨入口重建；GenerationId、lease、attack只负责本次运行隔离。Unity对象引用、GetInstanceID、Transform、池队列位置均不能作为跨会话身份。资源GUID适合编辑期映射核查，Player按版本配置引用取资源；不要求运行时用AssetDatabase找GUID。

### 2.4 序列化、Interface与失效

当前TowerRunSlotStore确用JsonUtility，无另一个Dictionary序列化库。Unity2022.3默认序列化不直接支持多维/锯齿数组、Dictionary、嵌套容器；持久化用Serializable普通记录＋一维数组/记录包装，反序列化后再建查找表。[Unity序列化规则](https://docs.unity3d.com/2022.3/Documentation/Manual/script-Serialization.html)、[JsonUtility.ToJson](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/JsonUtility.ToJson.html)。

候选小Interface（接口，包括时序、失败、成本合同）：
- PlanEcology(MapData、静态基底、版本配置、生态上下文) → FrozenLayout或结构化Failure；纯数据、不实例化、不写active/正式档。
- QueryMove(binding、from、to、shape、purpose) → Legal/截断位置/原因；普通移动和Hurt同入口，不合法不偷偷穿过。
- Begin/ResolveAttack(身份、目标合同、起手序号) → 单次资格；攻击控制器保留唯一扣血调用。
- BuildDerivedQueries(acceptedPlan、版本键) → 索引；换代/体量/静态阻挡版本变化即失效。

Seam（可替换实现接入点）放在原BuildPlacementPlan、原实际MovePosition之前、原延迟扣血之前；既有Population/Registry/存档服务仍接管其本职。不加第二FSM、全局逐帧总管或万能规则DSL。

### 2.5 REV-F：冻结深度与激活就绪

大白话：只给调用方一个“只读接口”还不够，若底下还是同一个数组，它仍可能被别人改掉。新生态接受后，Planner的草稿和运行中的地图必须彻底断开可写别名。

当前真实可写暴露：MapPlacementPlan.Reservations返回内部List，TryAdd始终开放；Reservation.OccupiedCells、Request.FootprintOffsets返回内部数组，Occupancy.Owners返回实际Dictionary（MapPlacementPlanning:9–22、29–43、49–51、80–99）。目前没有深冻结。
未来新分支合同：
1. 可变Builder仅规划持有；接受时逐层复制规范数组、每个记录的嵌套数组/集合，所有索引由复制结果重建。
2. 新生态Plan封闭TryAdd/Release等写口，输出不泄露可还原为底层可变数组/List/Dictionary的视图；调用方只通过查询或值复制读取。
3. Builder和输入数组随后改变必须不影响已接受快照。冻结不能只是设置IsFrozen但仍泄露源数组。
4. 商人/植物/动物/可破坏物及所有布置先完成，最终联合校验后才深冻结整个新Plan，不能冻结后再让后续服务TryAdd。原Registry就绪核对同代完整reservation/居民/商人身份，实际实例绑定与清场目标齐全，再使BindingReady。冻结布局与运行Registry的可变实例登记分开。
5. Pool拿到实例、OnEnable、登记成功、BindingReady分属不同时刻；未就绪/失效实体不得输出移动/攻击或进入错误代次计数。
6. 首次真实死亡由原BaseDamageable一次Drop/OnDefeated、原楼层一次Count；清理/卡住/退休非击败。伤害请求先Consumed再调用可重入的原接收者。
旧无生态分支保留原调用合同；新分支的深冻结/别名攻击用纯数据断言，激活门禁需Unity集成验证。本轮均未实现/未运行。

## 3. 当前真实调用链

源码位置均相对本项目；完整静态证据和行号见EVIDENCE增补。

| 链 | 当前实际顺序 | 新职责接点与错误边界 |
|---|---|---|
| 地图 | Coordinator → Orchestrator → GeneratePending → BuildPlacementPlan → Validate → CommitPending → retire旧Registry → Project → Materialize → SpawnPlayer → Reinitialize → GameplayReady | 新生态在同pendingMap规划；原提交之后仍有失败可能 |
| 人口 | Plan清共享planned → 每实体TryAdd → committedPlan实例化 → 订阅死亡/绑定Registry → 延后地图重初始化 | 多次生态候选改局部数据，接受才发布；Spawn/OnEnable不是身份就绪屏障 |
| 寻路 | 每怪目标格改变等触发MapElevationRuntimeQuery BFS → 中心格路径 → Rigidbody2D.Cast → MovePosition | 中心图不含足迹；平坦图现HasElevation fallback不能复用为生态豁免 |
| 玩家近战 | 选最近BaseDamageable → 起手捕获一个target → WaitForSeconds → 活动/同层/存活 → TakeDamage | 包含静物；当前不重查距离，不能称旧代码已覆盖全命中条件 |
| 怪物近战 | 起手只锁方向 → Tick读取CurrentTarget → 前摇到时范围/朝向/同层检查 → Player.TakeDamage | 单目标，但不是固定起手对象；T09公开选择 |
| 入口恢复 | JsonUtility读档与兼容验 → 固定seed/profile/attempt生成 → 比静态baseline → GameplayReady后入口candidate → 保存clone | 不抓取当前逐只血量/位置；找不到配方/摘要不符走原错误合同 |


## 4. 空间规划：先有能走的路，再分地盘

大白话：隔着悬崖看起来近，不代表能走到。算法只沿现MapData允许的边长地盘；噪声可以让边缘不那么整齐，不能把悬崖变成路。角色能站在一个点，也不代表整个身体能从拐角过去。

### 4.1 图、距离与候选比较

令N=W×H，V=可遍历节点数，E≤4V为候选有向边，S=楼梯数，K=区域数，C=合法放置候选数，M=居民数，F=足迹覆盖格数，P=不同足迹类别数，A=生态尝试数。预建楼梯查表O(S)，遍历每个格并调用同一CanTraverse语义生成邻接O(N+E+S)。否则现异高边扫描楼梯，最坏有O(E×S)，不能漏计。

同一节点图可有不同合法子图：玩家移动脚底、Torch与修复后Boss未必能通过相同通道。当前玩家直径0.6的solid脚底与1.222宽的trigger hurtbox职责不同。规划使用实际静态障碍与指定足迹的swept segment合法边；动态怪物不写回永久地形，只在实际移动时Cast/等待。

| 算法 | 实际步骤与适用性 | 成本与反例 |
|---|---|---|
| 既有地形块再切分 | 同Terrain同高连通块→连通区域生长；复用候选 | 楼梯两侧可能被人为分开，长草地一整块过大；不能直接等同领地 |
| 多源BFS（Breadth-First Search，广度优先搜索） | 种子同时入队，沿统一代价合法边按层生长；T02-A | 建图后O(V+E)，空间O(V+K)；边界偏路径距离形状，不能宣称噪声加权边 |
| 多源Dijkstra | 正整数边代价=基础值+固定噪声软成本，最小堆生长；T02-B | 二叉堆O((V+E)log V)，空间O(V+E)上界；权重必须非负，本提案严格>0；噪声幅度太大出现细长领地 |
| 平面Voronoi+裁剪 | 欧氏最近种子→地形裁剪→分裂检查/连通修复 | O(KV)朴素距离＋修复；隔崖同区、裁剪碎片需额外协调，本项目不推荐 |

[Boost原算法合同](https://www.boost.org/doc/libs/latest/libs/graph/doc/html/graph/algorithms/shortest_paths/dijkstra_shortest_paths.html)说明非负边权Dijkstra与单位边权BFS的适用性；这里只借算法前提，不安装Boost。上述复杂度是本提案容器分析，不拿库内部实现的复杂度代替本项目。

已选T02-A。首版噪声仅接普通种子软偏好，具体整数空间场见§4.1.1；不接BFS队列代价、等距抢格或人口密度，不承诺平滑疏密已经完成。若用户选择B，则量化噪声为正整数代价，堆键用(cost,regionId,nodeId)，避免浮点平局漂移。两者都不允许后处理悄悄剪断原来连通的区。

种子数由可用面积/目标区域面积给初值，再受最小群体与可构造容量约束。不是N除某常数就保证可行。顺序：
1. 原静态Plan保留出生/出口/梯口；不改地形。
2. 商人安全核心覆盖现SpawnCell，联合找到商人交互位置；休息核心从玩家真实可达候选选。
3. 按T04加缓冲；按T05的玩家几何检验两保护集合不接触、不经可达保护节点连成一路。必须存在经过非保护空间的实际可走往返路径。
4. 低概率Boss资格先判；抽中才在可容体型位置选0/1种子，不中不留Boss洞。
5. 普通领地种子稳定排序（候选固定得分、逻辑格ID、稳定区域ID），在剩余图生长。无法供居民合法通行的连接带可留中立，不自动安全。
6. 类型与足迹按T03协调，群体/预算按§5计算，联合实体与通道验证。
7. 有限修复只允许确定的连通合并/减K/重选种子。任何修复后重新验连通、身份、人口与保护；不能随意把孤格划走当“平滑”。

BFS伪代码（说明性，无生产代码）：

    for seed in stableSeeds:
        owner[seed.node] = seed.region
        distance[seed.node] = 0
        enqueue(seed.node)
    while queue not empty:
        u = dequeue()
        for v in legalNeighbours(u) in recipeFixedOrder:
            if v is unassigned:
                owner[v] = owner[u]
                parent[v] = u
                distance[v] = distance[u] + 1
                enqueue(v)
            # 同层竞争由初始种子顺序+固定邻居/队列顺序确定
    verify every owned node has a parent chain to its own seed
    verify after any permitted repair, not only before repair

不要将“固定顺序”写成依赖Dictionary迭代顺序。当前语义规划邻居right/up/left/down，而运行BFS是up/right/down/left；旧代码保持，新配方单独固定，不用统一旧顺序来破坏老布局。

### 4.1.1 REV-A：首版噪声只接普通领地种子偏好

大白话：BFS仍按一格一格沿路扩张。首版空间噪声只在合法候选之间影响“种子偏向哪里”，不修改边价、不直接改变人口密度，也不宣传已经实现平滑疏密。稳定散列用于产生格点值和平局；相邻位置经过插值才构成空间连续的场，两者不是一回事。

选定最小接点：在硬资格、可行面积和种子间真实通路间距均满足后，用版本化二维整数值噪声作为种子软偏好。Boss/安全硬资格不被噪声推翻；当前不把噪声接入等距抢格、人数公式或后处理。
- 坐标为MapData局部格坐标(x-Origin.x,y-Origin.y)，Origin已进入地形身份，禁止重复世界平移。格点间距L初值8逻辑格，频率1/L格⁻¹；不是世界像素。
- 每个格点角值h∈[0,1023]，来自固定整数评分，输入包括domain=eco.seed-field.v1、地形实际seed/attempt、ecologyAttempt、recipe/config身份、格点整数坐标；不用Unity全局Random或当前时间。
- 用双线性整数插值得q∈[0,1023]。L≤256、乘积checked long；频率、取值范围、量化和端序进入recipe。此场连续插值后量化，不宣称导数光滑或Perlin。
- 首个普通种子在合格候选按q降序，再按独立种子域稳定分数/NodeId选。后续候选先满足最小间距，用 score=4×最小合法图间距+floor(3q/1023)，取最大；q最多贡献3分，不能压过一图步的4分差。K/硬资格仍按原合同。每次更新最近种子距离的成本另计O(K(V+E))。
- 开关仅供未来定向对照：off时q软项=0且首种子用同一基础稳定评分；它是影响布局的配置项，必须入ConfigDigest，和纯显示黑线开关严格区分。本轮没有新增开关或配置。

    ix=localX/L; fx=localX%L; iy=localY/L; fy=localY%L
    h00,h10,h01,h11 = StableScore(seedFieldDomain, latticeCorners) & 1023
    numerator = (L-fx)*(L-fy)*h00 + fx*(L-fy)*h10
              + (L-fx)*fy*h01 + fx*fy*h11
    q = numerator / (L*L)  # 整数除法；不改变BFS边价

说明用手工3×3网格，节点y×3+x，无阻挡，L=2，四角值h00=h01=0、h10=h11=512，三行采样均为[0,256,512]。这里固定基础平局为NodeId小者优先，需2种子：首种子2；最远合格候选6为第二种子。队列初始[2(R0,d0),6(R1,d0)]；固定右/上/左/下：
出2加入5、1；出6加入7、3；出5加入8、4；出1加入0。后续首次占领得R0={0,1,2,4,5,8}，R1={3,6,7}。每点parent属于同区、最终连回种子。此例无安全/体型约束，仅解释接点与FIFO，不是运行产物。
同层首次占领仍由稳定种子顺序和FIFO决定，不为q重新排序队列；没有偷偷改成Dijkstra。合并/减区后重验连通。未来对照固定同地形及其他配置、仅变seed-field开关，记录q/种子/最终分区/失败原因；手算不记运行PASS。

### 4.1.2 REV-B：候选几何来自本次输入，不来自当前场景

大白话：规划新图时，场景里可能还站着旧地图。Planner必须拿“这次新图的尺寸图纸”判断，不能拿旧墙做Cast。

当前缺口是真实的：MapData和原静态预留可读，但尚无完整的候选碰撞几何清单。MapElevationRuntimeQuery:19–26只读LastGeneratedMap；MapStage1ProjectionPlan:53–89含Unity渲染资源，Prepare还创建Sprite，均不能冒充纯规划查询。当前规划禁止调用Physics2D/Collider/Rigidbody.Cast读取场景，也不先造一整张候选世界；保持T12-A。

未来最小数据适配是本次recipe派生的CandidateGeometry描述（概念名，不新建地图权威）：
1. 地表/高程/显式梯边来自同一候选MapData；节点与世界地面投影使用原MapCoordinateBoundary的同一转换合同。传入可重建投影参数，不读取旧图的LastGeneratedMap。
2. 不可走格的Grid型碰撞按实际Tilemap格形状/布局变换派生；生成崖壁按MapElevationProjector:669–720的同一邻边判断、厚度、偏移和梯边排除规则构造窄矩形。未来提取共享纯几何计算供Planner和原Projector消费，避免手抄第二套规则；是否生产路线真的启用该投影策略必须由recipe固定。
3. 装饰/商人/静物碰撞来自版本化资源形状记录及本次计划的姿态/scale；原占位格和CollisionOffsets是资格/保留空间，不能自动当真实Collider外轮廓。DecorationCollisionMarker是Sprite型且源meta启用fallbackPhysicsShape，空physicsShape数组不代表没有轮廓。
4. 需要真实导入轮廓的资源，在未来授权的Editor准备步骤由Sprite.GetPhysicsShape等引擎读取，连同源资源身份、导入设置、局部变换和内容摘要生成可供Player直接使用的版本记录。当前没运行Editor，所以真实轮廓尚NOT_EXTRACTED；不能编造数字。已明确的Box/Circle/Capsule可从资源与最终足迹合同生成基本形状。
5. 纯数据查询对圆/胶囊/盒/多边形做形状相交与移动段扫掠，按高程表面和合法梯边判断支持；纯几何不调用物理场景。未支持的形状/缺轮廓/不一致transform报UnsupportedOrMissingGeometry，不以中心格或AABB近似放行并声称完整足迹已证明。
6. 接受结果随Plan绑定GeometryManifest/ProjectionPolicy/Footprint版本。投影后以真实Collider核对形状、变换、阻挡关系与规划清单；不符则T12就绪失败，清本代、保原档、不GameplayReady。

上述记录是候选MapData与资源配置的派生输入，不复制terrain权威、不建立新PhysicsScene或通用几何框架。原MapFootColliderCandidate只是fixture/验收适配，不能把其额外斜边假设为生产图已有碰撞。

Cast源形状事实已核：官方Rigidbody2D.Cast会查询该刚体附属的全部Collider形状，contactFilter过滤命中结果，不是源形状白名单。当前PlayerHurt:208–212优先用PlayerColliderRoles.MovementCollider.Cast，正式玩家已绑定solid脚底；只有缺roles才fallback body.Cast，不能误报玩家现链路必然混入身体Trigger。MonsterMovement:261用body.Cast，所查Torch/四新怪根仅solid形状，未见这批资产的同刚体双形状实证；未来新生态绑定时仍须验证明确源形状，缺失则准备失败，不用useTriggers=false冒充源过滤。[Unity Rigidbody2D.Cast](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Rigidbody2D.Cast.html)

验证反例：旧图仍在/新场景无Collider时候选结果不变；非零Origin、Grid变换、异高/楼梯与对应生产投影；装饰Sprite轮廓与Collider对照。所有几何提取/运行对照仍NOT_RUN，此合同补齐不等于已有候选几何实现。

### 4.2 类型与体型怎样避免互相打架

T03-A推荐“先候选地盘→列可行类型→按固定权重选→实际打包→有限协调”：在当前地盘对各候选足迹求合法子图/放置点，只有能形成群体的类型进抽签。选中后实际放置不足就有限合并/减区或进入下一生态attempt，不能任意降到低于群体底线。接受前必须在该Ecotype足迹的合法中心与扫掠边子图上验证：所有InitialNode、合法Home归返点属于同一连通分量，巡逻只取该分量的候选；地盘在点图连通不能替代这个检查。最坏需对每区/足迹类型处理成员；总遍历量可按P×V组织共享clearance，避免K×P次全图无差别重算。

T03-B“先抽类型再按它的足迹长区”：类型比例更直接，但大体型/稀有类型可能反复占到狭地；需构建类型专属可行图、稳定安排种子并争地。没有随机一次就保证配额的承诺。两种都能满足一领地一类型；不是让类型名称反向改地图。

### 4.3 缓冲与接触语义

2026-10-22实施澄清（已有完整脚底合同的实现，不新增玩法选择）：Stopping Node（可停靠点）由原完整solid脚底和真实扫掠图认证；Surface Mask（地面保护集合）记录这些实际脚底覆盖的地面单元。原Warrior脚底radius=.3、offsetY=-.25，放在单位格中心时下沿越过本格.05；因此地面集合要覆盖下方承载这部分脚底的格。该边缘格的中心可能放不下整个身体，可停靠资格继续由脚底图单独判断。两集合仍使用同一MapData/Origin/NodeId，没有另一份地形来源。安全主体与缓冲先沿认证图选停靠范围，再生成完整脚底的表面覆盖；缓冲步数沿真实可往返边传播。

两保护集合接受前，以原玩家完整几何脚底检查扩张后的集合仍不相交，防止“中心看似分离，脚底仍可一路蹭着保护”的情况；保护几何使用原形状，物理Polygon的接触余量只用于静态碰撞净空。源码接点为MapCandidateCollisionGeometry.Safety.cs；Run76平面原生夹具通过但退出TIMEOUT，Run77三个实际Seed资格/无保护往返通路通过并自然exit0。具体面积/路线与边界见P2_EXECUTION；普通领地、其他角色碰撞联合、Scene商人覆盖及整套质量验收仍待实施验证。普通领地的InitialNode/巡逻/合法HomePose还须同体型连通，并以完整脚底位于原领地为条件。

T04-A推荐“沿真实通路扩张的缓冲＋同可行走表面几何检查”。保护规范集合由节点/表面单元决定；怪物身体与移动段对这一集合做完整几何相交检查。隔崖同屏相近不无故扩到另一层；楼梯作为真实边传播。T04-B采用同可行走表面按几何半径膨胀，再做连通/路径核验；范围视觉较圆，但隔墙邻面可能被保护，范围与通路步数不一致。绝不直接在屏幕投影上画圆当规则。

- 悬崖两侧：A不越无梯边扩张；B若同高空间几何近，可能连带覆盖隔壁地面。不同高程面必须分开，楼梯连接按合同处理。
- 楼梯口：A可沿梯边扩张，需覆盖实际过渡扫掠；B若只测平面圆会漏或误连，须同样用真实通路复核。
- 对角接触：四邻不连并不代表两个保护多边形不碰角；额外用保护几何闭集接触和玩家足迹可走图检验，触边按保护计，epsilon仅数值容差。
- 狭道：缓冲如果封住唯一危险通道，两安全区可能经保护连通或切断商人/出口路线；拒绝该候选，不缩角色/开隐形路。

T05-A已确认：solid移动脚底任意接触即保护，完整离开才退出。B锚点方案已NOT_APPLICABLE，保留候选比较供历史追溯。Monster一直执行完整地面活动足迹禁入，双方攻击的保护范围始终是主体并缓冲。玩家身体trigger不作为移动脚底偷换。A会更早得到保护，B更贴“脚点越线”；两者成本接近，但边缘体验不同，该选择已确认。

高程不只是拿整数相等筛掉一切：楼梯过渡段由原通行合同判断其表面支持。实际几何、非零Origin、Grid变换、闭边接触、斜向扫过角落均列运行验证；文档不宣称已经解决引擎碰撞精度。

### 4.4 同地形有限重排与失败分类

推荐工程初始边界：配置化最大生态attempt B=8、每attempt最多2次连通修复；这些是待测保守初值，不是生产成功率承诺。预算计数必须写诊断，非法配置（≤0、溢出、缺内容）直接失败，不把它当随机不走运。

    terrain = originalGenerate(requestedSeed, terrainAttempt)
    staticBase = buildOriginalStaticReservations(terrain)
    for e in 0..B-1:
        candidate = freshPlanFromStaticBase(staticBase, sameGeneration)
        input = (sameTerrain, recipe, configDigest, footprintVersion,
                 fixedMapContext, e)
        layout = buildSafetyRegionsAndResidents(input, candidate)
        if layout has failure:
            if reason is InvalidRecipe/UnknownContent/MissingGeometry/NumericRangeExceeded:
                return FatalConfigurationFailure(reason)
            record(e, phase, reason, counts)
            continue
        if finalValidate(layout, candidate):
            freeze(candidate, layout)
            publishAcceptedCandidateOnce()
            return Accepted(ecologyAttempt=e)
    return EcologyBudgetExhausted(allReasons)  # 不调用外层换地形重试

固定：地形内容、成功terrainAttempt、GenerationId、出生/出口、配置内容、楼层资格上下文。变化：由e派生的安全候选/种子/类型/布置稳定评分。每次全新临时Plan，不能只复制occupancy后沿用前次LastPlan/planned。恢复可先按记录成功e重建，并核对该配方规定的选择过程；不能“找一个能过的新e”替换存档。

错误分InvalidRecipe/UnknownContent、InsufficientSafeSpace、ProtectedConnection、NoQualifiedMerchant、NoConnectedTerritory、InsufficientConstructiveCapacity、CriticalRouteBlocked、EcologyBudgetExhausted。具体枚举名属E。失败在原失败页停住，NewRun/FloorTransition手动重试才可新图；Restore保留记录身份。有限终止并不保证可行。

## 5. 人口、放点与商人

大白话：先算“想放几只”，再证明“真的摆得下”。安全地、悬崖、狭道和大身体会让同样面积的两个区容量很不一样。小地图预算不足时先少开地盘，让剩下的地盘仍有群体，而不是每区只剩一只。

### 5.1 配额与说明用手算

每普通区i：有效活动面积Ai、类型密度ρi（只/合法活动格）、最低群体gi、可构造容量ci、目标上限ui。
需求di = min(ci, ui, max(gi, floor(Ai×ρi)))；先要求ui≥gi，否则InvalidRecipe；如果ci<gi，该领地不可接受，应合并/减K/重排。对密度使用整数分子/分母配置，避免浮点恰好过阈值。

全图预算Bordinary先扣符合资格且抽中的1Boss预算；必须Bordinary≥Σgi，否则按确定策略减少普通区数再重算。T06-A按各区剩余需求比例分整数名额，最大余数分配，余数同分用seed派生稳定排名；T06-B逐轮均衡轮转，每轮每区最多加一个，跳过已满区。A更保留面积/密度比例，B更均衡小区。二者都不按配置列表前缀截断，也不保证每种必有一块。

**说明用，不是生产参数**：三普通区A=(20,30,50)，同ρ=1/5，g=(2,2,2)，c≥(4,6,10)，u不约束；原始需求d=(4,6,10)，全图普通预算13。
先给最低(2,2,2)，余7，剩余需求(2,4,8)，总14。比例份额=(1,2,4)，结果(3,4,6)。
若预算14，余8，份额=(1.143,2.286,4.571)，先floor=(1,2,4)，剩1给第三区，结果(3,4,7)。
若第三区实际构造容量只有5，需求变(4,6,5)后重新分配；不能把少的名额随手给列表第一项。若某区容量1<g=2，不能把它降为1仍报群体满足。
B轮转在预算13时可得(4,5,4)，更平均，较弱保留大区更多人数的倾向；面积仍影响需求上限，不承诺每一张图都严格随面积增长。

“面积正相关”仅在类型/足迹、其他约束及未触全图预算/容量截断时成立。全图预算、其他区需求或类型同时变化时，个别区可变少。这是算法耦合事实，不把Z-07无条件解读成全局严格单调。

### 5.1.1 REV-F：整数分配、零需求与容量回流

前置：g≥1、u≥g、密度分子≥0/分母>0、预算/计数/面积在约定上界内，否则InvalidRecipe；c<g是本候选容量不足，有限合并/减区/重排。容量c必须是一组同时相容位置的已构造数量，不是C个逐点合法候选数。

    di=min(ci,ui,max(gi,floor(checked(long(Ai)*densityNumerator)/denominator)))
    base=checked_sum(gi); need_i=di-gi; total=checked_sum(need_i)
    require ordinaryBudget>=base   # 否则先减少领地，重新构造
    r=min(ordinaryBudget-base,total); n_i=gi
    if total==0: return n_i        # 不除零，不强花未用预算
    if r==total: return d_i        # 预算过剩不超过需求
    product_i=checked(long(r)*need_i)
    extra_i=product_i/total; remainder_i=product_i%total
    n_i+=extra_i
    left=r-sum(extra_i)
    for region in (remainder降序, stableBudgetScore, RegionId):
        if left==0: break
        if n_i<di: n_i++; left--

乘积/求和使用checked long并限制可表达输入；溢出为NumericRangeExceeded/InvalidRecipe，不wrap、不饱和后冒充成功。余数稳定得分碰撞再用RegionId，禁列表前缀偏置。
若后续商人/阻挡/间距联合校验使容量证明失效，重构c与相容点组，把未用名额按剩余需求回流；每次有界重排/回流受原attempt/pass总预算约束，不能无限互抢。终态逐区g≤n≤min(c,u)，总数≤普通预算，每居民对应实际相容点，清场目标与最终居民一致。
说明例补充：三地区d=(4,6,10)、g=2、B=30只给(4,6,10)；d=g且total=0保留最低，预算可留空；u=1<g=2是非法配方，不套min得到1后接受。全图耦合下不承诺无条件单调。当前旧Profile已有maximum≥requiredMinimum校验，不能把旧全图公式当新区域合同已实现。

### 5.2 放点算法与真实容量

复用原MapCellOccupancy和spacing验证，加入实际footprint与完整通道合法性。
- 先生成所有合法候选（C≤V），按域分离的稳定分数排序O(C log C)；依次尝试到人数或预算耗尽。每点最多检查其足迹F与近邻空间桶，稠密最坏仍可退化O(M)，不能承诺空间哈希总O(1)。
- 给每次放置最大候选检查数C，所有候选总遍历次数/验证BFS次数受attempt预算约束。无无限拒绝采样。
- 原拒绝采样在稀疏时简单、密集时命中率骤降；完整候选排序更容易终止/复现。Poisson-disc类更均匀，但复杂地形/楼梯仍需同样合法性，首版没有必要引入新采样框架。
- 以实际构造出的相容位置数量作为可证容量下界，不求最大独立集。放入会阻挡通路的Destructible时保留到出口/各Monster的通达验证；普通怪动态互相挡路不直接变永久阻挡。
- 摆放不足但≥群体最低，回流预算后最终联合校验，可接受低于原始需求；低于群体底线则减区/重排。Boss若抽中不能用“0只的Boss区”冒充成功。

原Policy中每候选可能扫描reservations，Destructible还可能逐候选BFS；新人口增多时必须共享静态图/clearance/候选，记录实际验证次数，不能套用旧最多5怪的性能结论。

### 5.3 唯一商人及未来输入

地图上下文只包含这次确实用到且可持久化重建的floor、已知地图标签/地表统计、入口配方版本。当前资格→候选类型→权重/保底→商人区内位置→现有ShopCatalog初始化。当前只有merchant.main；尚不存在的地图功能不伪造值，不造DSL、多商人经济或新实体框架。

Merchant位置与交互净空先于占满居民通路；库存仍由ShopTradeController/TowerRunRuntimeStateBridge保存，不写入生态类型配置。Monster Mushroom是Monster角色；原可破坏蘑菇是Destructible，按稳定类型/Prefab/Role映射防重复，不能凭名字归类。


## 6. 一只怪的状态、许可与移动

大白话：怪物可以为了追人离开自己的地盘，但“现在还在追”“正在挨打”“正在反击”都不能重新发一张出门证。返程已经开始，就带着原来的期限回去；堵住回不去是故障，不能算它死了，也不能偷偷满血或瞬移。

### 6.1 现有FSM与旁路

当前正式状态只有Patrol、Search、Chase、Attack、Return。Hurt是旁路控制器，死亡由BaseDamageable事件终止AI，回池由生命周期处理；下表按这一事实设计，不声称现已有Hurt/Death枚举。

| 当前阶段→下一阶段 | 触发 | 移动/表现处理 | 读写数据 | 外出累计 | 失败/约束 |
|---|---|---|---|---|---|
| 激活→Patrol | 最终位置、代次、home、lease绑定且重初始化完成 | 绑定前无行为输出，后恢复表现 | 写BindingReady/Home一次 | 尚无 | Pool.OnEnable不是完成屏障 |
| Patrol→Search | 发现合法目标 | 停普通移动、确认视线 | 当前目标，原home不变 | 如仍有外出则继续 | 保护内目标不能引出有效攻击 |
| Search→Chase | 持续可见且许可允许 | 跟随合法路径 | 实际足迹任一部分首次越界才建permit | 同次只建一次 | 无合法出路则不追 |
| Chase→Attack | 现有范围/视线/朝向规则 | 停走，起手现有动画 | 新AttackId | 继续 | 不因此暂停外出时钟 |
| Attack→Chase/Return | 本次命中/取消/结束 | 原冷却，不返还耗费、不加新技能 | Consumed/Invalid | 继续 | 超距/到期/ReturnLatched优先Return |
| 任一活动→Return | 到外出阈值、失去资格、目标无效 | 取消不允许继续的旧攻击，计划返程 | ReturnLatched=true，保留permit | 继续到真实回归 | 看到新目标也不续追 |
| Return内有限反击→Return | 已有近身攻击可用，且不会突破返程合同 | 原近战短阶段；不切成自由追击循环 | 新攻击但原Return锁存/permit不变 | 继续 | 预算不足/无进度则不再反击；不无限停留 |
| Return→Patrol | 完整足迹回到原领地合法归返点，确认实际到达 | 停、恢复巡逻 | 结束本permit | 此次结束 | 不能用!hasDestination当到达 |
| 活动↔Hurt旁路 | 真实受击/恢复 | 锁普通移动，统一合法扫掠截断外力 | 不改home/permit/ReturnLatched | 继续 | Hurt结束回返程意图，不一律Chase |
| 活动→死亡→回池 | 首次血归零→原延迟池回收 | 停AI/攻击；单一表现owner | defeated一次，lease失效 | 终止 | Drop/Count一次；禁用非击败 |
| 任意→退休/换代 | 旧地图失效 | 取消回调、解除订阅、幂等释放 | BindingReady=false | 终止 | 不计清场，不伪造死亡 |

返回是否可反击仍遵守Q20B；用于实现的单次反击阶段上限/进度检测是E/T，不增加新的反击技能或回血。外出到期意味着立即执行返程，不意味着期限一到自动已经抵达；返程运动仍占外出时间，必须记录超时和堵塞。故障超过有限重试则保持合法停留并报FAIL/诊断，不能假称永远可达。

世界时钟沿用现语义：外出起点用缩放世界时间，elapsed=worldNow-departedAt，不是每状态一个倒计时。设置页现同时WorldPause+输入门禁；背包/商店只有输入门禁，不自动停世界。timeScale=0暂停物理步与WaitForSeconds，Update的所有逻辑并非自动不执行。[Unity timeScale](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Time-timeScale.html)、[WaitForSeconds](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/WaitForSeconds.html)。不改设置暂停体验，不把现有丢视线宽限计时当外出计时。

### 6.2 距离与缓存T07-A/T08-A

T07-A推荐怪物完整足迹合法图上“当前位置到原领地合法节点集合的最短路径步数”；领地内为0，绕崖需计真实路，跨普通/Boss/中立边界不重置。单位是图步，不是屏幕像素或累计行程。若选T07-B，保留近似现home点直线半径，并另验存在合法返回路径；成本低、旧参数易沿用，但大领地边缘会提前受限、隔崖看着近却绕远。两者都不能越保护/时间/返程约束。

推荐T08-A：按(GenerationId,HomeRegionId,FootprintClass,StaticBlockerVersion,PolicyVersion)构建回家多源BFS距离/下一步场；同领地同体型共享。动态怪阻挡用实际Cast与有限等待，不每帧重建字段。追玩家仍复用按目标格缓存的路径，增加足迹/安全/版本键。T08-B：每怪按需BFS同时求距离/回路，简单且省共享缓存，但M多且目标跨格时成本重复。两种都受重算预算约束，查询未完成时停在合法位置，不允许先越界后纠正。

每次实际移动前检查：代次/绑定→目的（巡逻、追击、返回、Hurt）→路径段完整足迹→禁入/地形/梯口→物理Cast→只应用合法截断。Hurt原直接MovePosition必须接同一入口。平坦图也查生态，不能沿旧HasElevation=false fallback放行。对于斜向移位，扫掠整个形状，不只检查四邻终点；足迹图边与世界段同合同。

静态阻挡增删使相关图失效；动态暂堵不改MapData。大体型没有通路时规划不可把它放入需穿越该窄口的居民集合。返回遇堵有限再算/等待，区分Arrived、Blocked、NoPath、Retired；现!hasDestination混合停止/成功须拆结果。这些枚举/容器细节属E。

### 6.2.1 REV-C：统一在家、离家、零距离和返程完成

大白话：怪物只要一部分脚底出了自己的地盘，就开始这次外出；回来必须整只脚底真正落回合法活动区。停住、格子编号在家、路程表某格为0，都不能单独宣布“回家”。

设R为原领地地面集合，F为最终足迹，C为该居民活动连通分量：
H={v∈C | v的完整F合法且包含于R，能作为巡逻/返回停留点}。
H非空；初始点与巡逻目标从同一H/C合同产生，禁止另用出生点直线圈近似。共享键必须含能确定C的布局身份。
HomePose(p)=实际完整solid足迹包含于R、姿态静态合法、且属于同C可认证地连接H的连续位置域。领地边界闭集内接触可仍在家，首次有任何实际足迹部分位于R外才建permit；不是等整只全出去。安全边界触碰就保护是另一语义，不混淆两种边界。
- 首次实际!HomePose→发一次permit并记departedAt。意图/被挡住的请求不发。事件边界由T13实际运动证据确认，未知不伪造许可。
- 真正HomePose成立→HomeDistance=0、Return完成、结束本permit/ReturnLatched，才恢复Patrol。HomeNode只作可选停留目标，不另要求回原出生点。
- 表中D[containingCell]=0但实际半脚在外时，运行查询不能返回0。先做HomePose；非HomePose的实际位置作为临时查询节点q，只连接所在格及四邻可认证合法中心，连接段必须完整扫掠且不跨非法格边，每条成本1；q恰好是现有非Home节点中心时直接用该节点D。返回min(1+D[v])或不可达，这是扩展离散图上的最短步数，不宣称连续世界长度。超过局部范围的异常跳位不能用一个连接步缩短。扫掠过程、节点距离及连续实际pose须一致验证，避免边缘来回切状态。
- 许可elapsed、ReturnLatched、attack/lease仍是每只怪私有，不随共享距离表变化。动态怪物暂堵不改静态表。

底层MapData.CanTraverse:196–221已证双向；新的足迹/梯口边尚无代码，不能据底图替它证明双向。统一按反向合法边从H传播：

    for h in stable(H): D[h]=0; Next[h]=none; enqueue(h)
    while queue:
        v=dequeue()
        for u in stable(Predecessors(v)):  # ForwardEdge(u,v)必须合法
            if unseen(u):
                D[u]=D[v]+1; Next[u]=v; enqueue(u)

若足迹边实证对称，Predecessors复用同一邻接；否则反向索引只为计算到家的距离，不新增单向楼梯玩法。对Next路径逐边验证能正向到H。地图/布局/足迹/静态阻挡/策略变化失效；怪挪动不触发全图重建。真实连续位置与节点connector、楼梯接触数值仍NOT_RUN。

### 6.3 分散巡逻的最小实现

每领地/体型先缓存合法候选。每次到达或明确失败后，以(ResidentId,巡逻序号)稳定/运行随机域抽一个不同于当前点、路径有效的点；停留时间在到达时抽一次，Hurt不每帧重抽目标。每只怪保存很短的最近访问环（初值4个点，单位节点，测覆盖/重复率后调整）。先按ResidentId分散初始偏好，不加全区共享访问账或预约调度器；实际碰撞重叠时局部避让/重选。若实测仍集中，再用证据判断需要临时预约，不预造Boids。

候选抽取失败有次数上限，回到合法停留/有限重试。路线必须在本领地合法图内，不借中立带绕出去当普通巡逻。运行巡逻可不跨会话复演，不消耗静态布局随机域。

## 7. 一次攻击怎样永远取消旧伤害

大白话：命中时双方都在外面，不能证明“这段时间从没进去过”。B1明确以实际观察到的完整脚底接触和已证明的实际扫掠留下历史；碰撞内部未观察到的短暂进出不再承诺全部捕获，也不因此普遍拒伤。已经记录的有效进入仍永久取消旧击；退出后可以新起一击，旧击始终无效。

### 7.1 目标合同与历史候选

当前玩家起手锁一个BaseDamageable；怪物起手只锁方向，命中读CurrentTarget；双方目前单目标，无已发现的多目标/投射物业务链。T09-A已确认在新生态分支让怪物也冻结起手目标身份，方向仍按原规则；目标切换须下一次起手才生效。其代价是改变当前“前摇中换目标仍可打新目标”的技术行为，方向已确认，不能当旧代码已实现事实。

T09-B已NOT_APPLICABLE，以下只保留原候选比较：若保留怪物命中时取目标，必须同时保留起手观察对象的失效记录，并对实际命中对象查询整个攻击期间的保护历史。不能在回调临时给新目标取一个“当前修订号”作为起手快照，否则漏掉此前入区。没有新增多目标攻击，只是补齐既有late binding（延迟绑定）合同。

历史存储推荐：每实体ProtectionNow＋LastTransitionOrder（进、出都更新）、起手CastStartOrder、激活身份、起手双方修订快照，以及攻击单调Invalid状态。Order是同图单调事件序号，不是Time.frameCount，同帧也可排先后。
- 固定目标时比较起手修订和当前修订：进又出也不等。
- 延迟目标时，只要LastTransitionOrder>CastStartOrder或当前仍保护，就说明攻击区间与保护相交（含起手前已进入、起手后才退出的情形）；起手观察对象也不得已经使本次攻击Invalid。
- 比较进入修订号单独不足以支持“命中时换成起手时已在安全区、后来退出的对象”，必须记退出/区间历史。
- 令牌事件订阅可立即标Invalid，但晚确定目标仍需历史。仅用事件而无记录不能补订阅前的历史。首版不引入统一新战斗框架；这些数据附着原控制器。

### 7.2 REV-D / T13-B：高风险的具体定序协议（现行 B1 观测语义）

方向B及B1有限观测语义已明确批准；风险HIGH_RISK，已有Run30/Run36定向通过，但完整P0.V未通过。原v0.2严格版本字节保存在Evidence/P0V/AdoptedV02，原合同FAIL保留。不得因风险自动改A，不把玩家Dynamic改Kinematic，不手动驱动全局Physics2D。

v0.2实施前接点快照（保留来源与旧行号；当前opt-in实现及真实定序见P0V_EXECUTION）：
- Physics2DSettings.asset:41 m_SimulationMode=0，TimeManager:6–7 fixed=.02、maximum=.33333334；scoped Scripts未找到改simulationMode/Simulate。序列化设置对应自动FixedUpdate模拟，运行实值仍需记录。
- PlayerAction:64–74、125–138在FixedUpdate直接MovePosition；PlayerHurt:76–78、180–198也在FixedUpdate，末步234–244当场解锁，二者没有显式先后，末步可被普通请求覆盖是静态风险，不是已复现bug。
- MonsterMovement:62–65/174在FixedUpdate提交，MonsterHurt:113–130却在WaitForFixedUpdate协程后直接提交。现元数据相关executionOrder为0/未设置，无现成早/晚屏障。
- 玩家到期来自WaitForSeconds续体（PlayerAction:284–311），怪到期来自AI.Update Tick（AttackState:62–73）。新攻击输入的实际InputSystem回调相位也要在最小实验记录，不能凭组件排列猜。
Unity自动FixedUpdate模式在MonoBehaviour.FixedUpdate后模拟；MovePosition在后续物理更新生效，同一步多次调用只有最后一次有效，实际碰撞可阻止到达。[SimulationMode2D.FixedUpdate](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/SimulationMode2D.FixedUpdate.html)、[MovePosition](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Rigidbody2D.MovePosition.html)。WaitForFixedUpdate不等于可取得完整物理轨迹。[执行顺序](https://docs.unity3d.com/2022.3/Documentation/Manual/ExecutionOrder.html)

**最小接入图（已在显式opt-in P0.V实现，正式新生态尚未广泛接入）：**

    原输入/AI.Update起手 → 原攻击记录（固定双方lease/方向/修订）
             原前摇/WaitForSeconds到期 → 本代PendingDamage队列
                                              ↓
    FixedUpdate早接点(-900，早于本轮移动生产者的0)
      → 封闭上个Physics步骤全部参与身体的历史
      → 补齐保护事件/修订 → 消费到达requiredCompletedStep的请求
      → 本步普通/Hurt意图采集(现脚本0；新分支不再各自MovePosition)
      → FixedUpdate晚提交点(+900)
      → 每刚体仲裁唯一最终命令、完整足迹/合法段、Cast截断
      → 一次MovePosition → Unity自动Physics(n)
      → 下一早接点读取Physics(n)实际反馈

两个执行次序值是代码属性/现框架接点的拟定初值，不是本轮改ProjectSettings。早/晚两个小适配共享同一代次上下文；不造另一套物理系统或第二地图权威。先查现有顺序占用并测试真实调用次序。生态MonsterHurt数值推进必须改走意图采集，不允许WaitForFixedUpdate旧协程在晚提交之后再次MovePosition；旧Profile保留原链。PlayerHurt最后一步的所有权仍属于Hurt，锁释放只影响下个step，普通意图不能覆盖。若受击由本次伤害消费产生，只能影响新一步，不能回写已封闭历史。

**明确等哪一步：**
例Physics(n)完成后，Update/协程在原本会扣血的时刻到期：
1. 起手与入队前同步已确定完成的物理历史，封闭到n；同步不得在模拟未完成时伪称完成。
2. 请求记录requiredCompletedStep=n+1，要求核对至少一次原到期后的物理结果。
3. FixedUpdate(n+1)早接点只看到Physics(n)，不消费；本步允许实际移动/进入保护。
4. FixedUpdate(n+2)早接点封闭Physics(n+1)，发布保护事件，再消费该请求。
现两条到期路径属于上述渲染Update/续体情形；若实际相位不同，必须用已记录phase/step映射落实，不通用地取“最新计数”猜完成。输入起手相位同样必须能同步当时已生效的历史，否则最小实验不通过。开局无前步时从BindingReady实际初始pose建立step0基线，不能拿未初始化位置。
延期可能接近1～2个fixed间隔，当前设置的算术量级约20～40ms；这是排队相位估计，不是实测上下限，长帧/暂停可更久，WaitForSeconds本身也有帧量化。体验上**原本已经到期、尚未真正结算的窗口内，玩家再进入保护会取消旧击**；不增加额外无敌时间或其他反制规则。

**位移证据与轨迹边界：**
每步记录Requested→Selected→CastAllowed→Submitted→PhysicsObserved；前四项都不是已经走过的路径。只保存最终命令，覆盖/落败意图不产生Enter。
Cast是场景几何查询，首尾相同不是任意动态接触完整路径的证明。可证明类别至少分：
- 受控直线/静态截断，源形状正确、结果及接触与证明合同一致；
- 动态接触/滑动/挤压：以实际完整脚底pose的原生形状几何取得正向接触，保存保护修订；实际观察点在外侧且没有可信扫掠时，按已批准B1记录ContactObservedOutsideB1，不宣称从未进入，不仅因此拒伤。不能用请求或首尾连线补造内部路径；
- 身份/Lease失效、真实观察丢失、未受控非接触位置改变、闭合后改写、几何/查询失败等仍是独立故障，拒相关pending，单列FaultRejected。B1只取消“正常接触内部不可观测”这一项拒伤理由，不取消其他故障。
跨步历史不能只用渲染帧首尾；同一步多意图不是多段实际轨迹。所有参与身体先统一封闭，再扣任何血，并在每次消费前复核当时实际完整脚底。Run35独立原生相交见证揭示Transform滞后，当前几何中心用原生Collider.bounds与实际Rigidbody2D位置，不以旧Transform冒充实际pose。所有已观察接触及已证明扫掠的Enter/Exit都保存；不承诺未观察的求解内部短暂接触全不漏，漏检率UNKNOWN。正常接触持续吞伤或已观察保护失效仍判FAIL，不能归入已接受例外。

**请求、资格与生命周期：**
本代生态runtime队列拥有每个不可变PendingDamage：G、双方logical/lease、AttackId、起手保护修订、固定方向、NominalDueWorld、OriginalDueCallbackWorld、EnqueueOrder、requiredCompletedStep、原到期资格证据。状态Pending→Consumed/Cancelled/FaultRejected单向；先终态再调用原TakeDamage。
- 新攻击不覆盖已入队旧请求。验证AttackId查对应攻击记录，不要求它等于角色最新AttackId。
- 玩家下一起手仍可取消旧的“尚在等前摇”的协程，沿原规则；已经入队的独立请求只由其明确取消原因结束。怪冷却/下一起手沿原计时，不因队列尚未消费而偷偷重置。
- 起手冻结目标与方向、检查当时原起手条件及保护；原到期读取原资格，失败不入有效伤害。实际结算再验代次/lease/存活/保护历史和该攻击本来具有的命中条件：怪物原有同层/距离/朝向继续验；玩家原链没有延迟距离/朝向复核，不借本轮补出新限制。同层/存活按原规则保留并在结算核验。
- 取消保留明确原因（保护/死亡/前摇取消/退休/代次/租用结束/资格失效/轨迹故障），保护不可逆。新起手不复活旧击，也不把角色永久禁攻。
- 真WorldPause只等待，恢复后再过完整物理核对；只锁输入的背包/商店不自动暂停队列。换层/离开、死亡、回池令相关请求失效，旧代整队列退休；已Consumed不回滚。
- 动画/耗费/冷却由原攻击链负责；队列不再发一份攻击、不延长返回反击，不恢复外出许可。

本适配只接新生态Profile opt-in；旧Profile对照是最小实验必选项。源码当前没有队列/最终移动仲裁/历史闭合机制。实验范围与事前判据见ACCEPTANCE的V0-T13，本轮全部NOT_RUN；只有真实正常碰撞和时序证据通过后才考虑广泛接入。

### 7.3 说明时序与伪代码

例：G=77，骷髅resident=r2.0、lease=6，玩家lease=3，攻击A=18。以下“序号”是说明用。

| 事件 | order | 保护状态/历史 | A18 |
|---|---:|---|---|
| 双方在外，起手 | 100 | 记录玩家LastTransition=90、怪=80；CastStart=100 | Pending |
| 玩家实际移动进入保护 | 101 | player=true，LastTransition=101 | Invalid（或命中比较必拒绝） |
| 同帧玩家离开 | 102 | player=false，LastTransition=102 | 仍Invalid |
| 延迟请求到达/屏障结算 | 103 | 双方现在都在外；102≠90且>100 | 不扣血，不恢复 |
| 下一次合法起手A19 | 104 | 新记录102/80，新AttackId | 新Pending |
| 旧A18又回调 | 105 | 查A18自身已Invalid（不因有A19便自动拒绝其他仍Pending的旧击） | 拒绝 |
| 骷髅回池再次启用lease=7 | 106 | A18仍携lease=6 | 拒绝；不命中新激活 |

    beginAttack(caster, observedTarget):
        synchronizeProtectionHistory()
        require bindingReady, currentGeneration, alive, outsideProtection
        require existing initiation rules
        id = nextAttackId()
        context = (generation, casterLease, targetContract,
                   observedTargetLease, startOrder, revisions, Pending)

    onAppliedMovement(entity, orderedActualSegments):
        for each protection transition crossed:
            update ProtectionNow and LastTransitionOrder
            invalidate subscribed pending contexts monotonically

    resolveDueDamage(context):
        synchronizeAtSelectedBarrier()
        reject if status != Pending or any captured identity retired
        target = frozenTarget  # T09-A已确认
        reject if either captured party invalidated this attack
        reject if target lease not current
        reject if either protected now or history overlaps attack interval
        reject if original target rules fail
        # 玩家旧路径未验距离；本项不擅自增加追踪/命中范围玩法
        mark context Consumed before invoking receiver
        call original single damage receiver exactly once

进入只影响尚未结算的本次攻击，不退还体力/冷却，不回滚已扣血；表现可按原动画完成或安全中断，但动画事件不能重新产生伤害。ReturnLatched和外出累计独立于动画/攻击状态。BaseDamageable仍负责血量/首次死亡；不把安全规则广加到所有Damageable，导致安全区Destructible/Animal等内容被意外变成无敌。

**保证与前提分开**：在身份、完整位置历史和所选顺序合同都成立时，旧一击不能因退出复活，重复回调不二次伤害。当前工程尚无这些新记录/屏障；同帧、撞墙、高速、池复用、多个物理步与取消后动画均待真实集成验证。

## 8. 确定性、摘要与入口存档

大白话：Seed只是一串生成输入中的一个。算法版本、配置内容、怪物体型、商人资格上下文变了，同Seed也可能换图。我们只要求入口布局与初始居民能重建，不要求上一次怎么追、怎么掉落、每帧物理碰撞完全重演。

### 8.1 当前事实与T10

现地形System.Random(seed)＋Mathf.PerlinNoise，高程/装饰另有域；语义放置已有固定uint StableScore。运行巡逻/掉落使用UnityEngine.Random。现代码没有跨Unity/.NET/平台固定PRNG与浮点承诺。[Microsoft Random说明](https://learn.microsoft.com/en-us/dotnet/api/system.random)明确不保证跨.NET大版本相同序列；[Unity InitState](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Random.InitState.html)说明种子与随机状态，不为本项目的全部输入兼容背书。

T10-A推荐新生态静态选择复用版本化稳定整数评分：安全、种子、类型、人数余数平局、位置、商人各有domain标签；Hash输入精确含seed、terrainAttempt、ecologyAttempt、recipeVersion、上下文摘要、稳定ID、node。uint溢出语义固定unchecked；字符串UTF-8/Ordinal；显式长度前缀，禁止GetHashCode/InstanceID；评分碰撞用稳定ID/NodeId打破。不改旧地形RNG。
T10-B每域独立System.Random，保存算法环境身份并保留兼容构建/实现；循环调用顺序也属于配方。少写评分组合，但插入一次抽数会改变同域后续结果；升级运行时仍需回归，不能只存初始seed承诺跨版本。

域分离减少无意联动，但全局预算与商人占位确实会有意影响怪物结果。调试画线完全不取随机数。不承诺“商人任何变化都绝不影响怪物”。

### 8.2 规范摘要与旧分支

当前TRB2/TRB3是32位FNV式静态摘要，不是SHA256；槽位JSON覆盖token的SHA256是另一用途。新配方建议独立EcologyDigest=SHA256(canonical bytes)，保留旧terrain baseline原字节顺序；摘要只用于稳定核验，不是防篡改认证。

编码：带格式版本的UTF-8/固定端序整数与长度前缀；配置内容按稳定字段/类型ID排序；区域按RegionId；归属数组按NodeId；居民按ResidentId，包含home/type/initialNode/足迹版本/预算；安全flags、生态attempt、上下文及terrain baseline都纳入。邻接/黑线可重建，不重复纳入；必要时独立验证缓存。禁止依赖JSON缩进、Dictionary顺序、当前Guid、运行Transform或投影GetInstanceID。

配置同名不同内容时发布新内容版本/保留旧配方，不悄悄更新旧身份。找不到精确旧配置或不符摘要沿旧失败合同停止。足迹修复会改变新生态版本；旧四怪物资源修复虽不改Seed布局，仍需旧四怪物通行/攻击回归，不能泛称运行完全不变。

### 8.3 T11入口表示与恢复步骤

已选A：保存精确配方/内容身份、上下文、ecologyAttempt、EcologyDigest及计数manifest；保留所需旧配方/配置，重建布局后核对。存档小，最贴原入口检查点；代价是不能删除旧配方再期待旧档能生成。
B已NOT_APPLICABLE，仅保留候选比较：另保存已接受生态布局DTO（区域归属、安全、类型、居民初始点）并核对同一地形基线；生态不必再抽布局，但配置/Prefab/地形版本仍要可用；档更大、schema迁移与重复内容校验更多。它不保存逐帧位置/血量，也不取消地形重建依赖。

入口扩展字段：
- 继承原schema/runId/floor/player/inventory/shop/generatorVersion/configidentity/profile/seed/terrainAttempt/staticBaseline；
- 新分支版本标识、生态recipe/config/content/footprint身份、固定地图上下文、ecologyAttempt、EcologyDigest、region/resident/merchant计数manifest；
- 本次A没有LayoutDto正文；B不适用。激活lease、当前AI、血/追击/attack/history/path缓存都不存。
- 必需字段必须由原兼容策略检查原始JSON，不能让JsonUtility补0后误以为完整。新schema路由显式添加；旧档没有生态字段走原旧分支，不补成新配方。

恢复顺序：
1. 原SlotStore读JSON/必需字段/schema/兼容；拿到入口clone。
2. 精确路由旧/新Profile；解析原地形版本与新生态内容；未知即显式失败。
3. 用保存seed/terrainAttempt重建地形并核对旧静态合同；不换seed。
4. 按已选A的精确生态输入与e重建，校验连通、类型、预算、足迹/地形一致性，再比EcologyDigest与manifest。不读取LayoutDto正文，不在缺版本时改走B。
5. 再走原提交/实例化/绑定/商店入口库存应用；生成新运行GenerationId/lease。
6. 原GameplayReady后才替换currentRecord/entranceCandidate并解除门禁。任何失败不保存新身份、不允许回旧层继续玩。

旧default/TRB2、ecology-v1、terrain-v2、terrain-v2-xiaosu-v1实际路由继续保留。新Profile只候选ecology-zoning-v1，未写配置。代码与原槽位.tmp/.backup机制复用，不再造存档服务。


### 8.4 REV-E：精确旧配方解析与真实内容保留

大白话：档里写着“c1版本”不会把被覆盖的c1变回来。要支持旧入口，项目里必须真正留着那份配置、布局算法和所需资源，并且读档能精确选到它。

初次技术审查时状态MISSING（保留的历史事实；现行实现/验证见P5_EXECUTION与P3_EXECUTION）：PopulationService:53/79–97固定读一个profile；Runtime Prefab:1356引用单一MapEcologyProfile.asset（GUID f1f1ff9e10e45b944ad6f47399bead4d）。Profile:136–142只在XiaoSu路由追加物种；构建器仍修改同一资产。CheckpointRecord:129–148和MapGenerationRequest:7–26没有精确生态版本字段，Coordinator:495–507也未向生成链传这些绑定。已有字符串Profile兼容不等于精确内容保留。

T11-A保留，新增实现合同，不偷换T11-B：
- 解析负责人放在原MapGenerationController的配置解析入口；输入是入口记录的Profile、RecipeVersion、ConfigDigest、ContentMapVersion、FootprintVersion，输出一次解析后不可变的ResolvedRecipe绑定。Population只读此绑定，不自行取当前默认profile代替。
- 使用项目显式登记的有限版本表/序列化直接资源引用，不做热更新/插件/下载框架。Coordinator选择当前默认版本仅用于新Run/新楼层；Restore严格按档中组合键查表，原地形分支同时保留。
- Config条目绑定数值、类型资格/顺序/权重、噪声/算法/规范编码版本；内容映射绑定Ecotype（种类＋颜色）→精确Prefab与影响初始构成/交互的依赖；Footprint记录绑定P1后最终Collider/offset/scale/导入与投影几何。运行不能用GUID动态寻找“最新资源”。
- 同名数值变化新ConfigDigest；映射/被登记资源内容变化新ContentMapVersion；Collider/PPU/pivot/scale/通行或接触语义变化新Footprint/geometry身份，必要时算法RecipeVersion也递增。同GUID不豁免内容变化。
- 首版P1完成后，在首次新生态入口可保存之前登记并冻结r1/c1/m1/f1及所需真实资源。未来v2若改这些资源，保留v1实际资产及传递依赖；只为改变的v2资源创建独立版本引用，未变资源可共享且不可覆写。不重建现有GUID。保留对应旧算法/编码分支，不能只有旧资产名。
- 默认从v1推进v2必须先验证v1条目/依赖仍可解析；若旧资源已经被外部覆盖，报ContentMismatch并保护正式档，不能称此旧版本“已支持”。缺版本失败是错误处理，不是兼容通过。
- 老default/ecology-v1/terrain-v2/terrain-v2-xiaosu-v1继续原路线，现旧档没有精确生态摘要，不能替它编造历史内容版本。P1修旧四怪后旧路线实际碰撞/攻击回归单列，不宣称完全无影响。
- A的依据是衔接入口重建和范围控制；不是声称DTO大到不能保存。区域int数组16KiB只是算术估计，整份DTO/JSON字节数NOT_MEASURED；版本保留的资产、代码分支和维护成本必须如实记账。

必须跑的未来升级链（当前NOT_RUN）：
登记并保留v1(r1,c1,m1,f1) → 默认v1造层/保存S1 → 新资源发布v2并将新入口默认切v2 → 验证新入口用v2 → Restore S1精确解析v1 → 地形/生态重建比摘要manifest → 原服务绑定/库存入口应用 → Ready。
同时故意移除v1条目、同GUID改足迹、配置同名改数值，必须明确失败且原档SHA不变。只做当前版保存立即读回，不能替代该升级用例。此机制目前尚无代码，必须先落地/验收才可声称支持新生态旧入口。

## 9. 候选、提交、实例化与失败

大白话：草稿失败可以直接丢掉；已经切换地图后再出错，则要停在失败界面并清理新对象。两者不是同一种“撤销”。商人已经出现、怪还没绑好时不能让玩家开始操作，也不能先把半成品身份写入正式入口档。

### 9.1 当前真实时序和T12

当前原Orchestrator会在投影前CommitPending，旧Registry随后退休；投影失败走PostCommitFailure，无全链回滚。Registry.Retire只清登记/占位，Population的RetireActiveInstances只清本服务实例。别把它们写成整个世界的事务。

T12-A推荐保留这条逻辑提交顺序，加完整的GameplayReady验收屏障：新生态、唯一商人计划、居民目标集合与同代身份先纯数据联合验证；实际投影/所有服务实例化/绑定/计数/入口应用完成后才开放游戏和替换入口记录。提交后失败各服务幂等清理并保留门禁，原存档身份保持不变；不承诺恢复旧层。
T12-B改成全参与者准备后再切active的两阶段发布：先在pending上下文构建投影/实例/Registry绑定，全部就绪才切换。失败清pending比较直观，但原服务广泛读activeMap，需pending查询/隐藏激活适配，改动、内存峰值、兼容回归显著更多。也不是数据库事务，Unity外部副作用仍须明确收尾。

推荐A的事件表：

| 阶段 | 权威/可见性 | 可写数据 | 失败处置 |
|---|---|---|---|
| 请求开始 | 输入/世界门禁生效 | staged recipe | 保留旧入口正式档 |
| 纯数据候选 | pending Map/Plan，玩家不可操作 | 局部candidate | 丢弃候选，不污染active |
| 联合校验/冻结 | 同地形接受一个e | 冻结布局/居民/商人/占位/摘要 | 有界重排/明确失败 |
| CommitPending | 原active Map+Plan+Registry切换 | 同代发布，不再改布局 | 失败不虚称恢复旧active |
| 投影/实例化 | 新图仍门禁 | 各服务自己的实例登记 | 本代已造实例按owner清理，保留失败诊断 |
| 绑定/重初始化 | 完整home/lease，AI尚不能输出 | 运行私有状态 | 不把OnEnable早触发当成功 |
| 清场目标核对 | 从同代Registry Monster居民投影成原目标集 | 原楼层唯一目标集合 | 数量、ID、实例不全则失败 |
| GameplayReady | 身份/服务完整一致，之后才放操作 | currentRecord/entranceCandidate | Ready前不发布新入口 |
| 后续入口保存 | 复制已就绪入口candidate | 原SlotStore.tmp/backup | 原存储失败合同 |

清场使用布局的预期居民集合与Registry一一核对后交原TowerFloorRuntimeController；不是再维护第二份实时击杀权威。原BaseDamageable先置defeated、奖励一次、发OnDefeated；原楼层集合一次性计数；Population订阅负责释放；表现不扣血/不发击败。归返、卡住、禁用或租用失效都不能减少剩余目标数。

## 10. 体量修复、表现与黑线

四怪物修复仍属于工程工作，不另提玩法审批。当前根/Visual scale=1，Slime/Mushroom脚底0.1758×0.12、Skeleton0.293×0.12、Boss0.3047×0.12；旧Idle像素测量与同哈希资源相符，但本轮没有重跑像素工具或实际镜头。最终尺度NOT_VERIFIED。

恢复开发后先每类稳定体量，统一脚底、受击体、近战距离、影子/血条；不每帧按alpha缩放、不改玩家/相机掩盖问题、不重建GUID。体量版本确定后重建clearance/容量。专用表现负责四怪物Hurt/Death/复用恢复，共享AI/朝向/攻击适配避免再次禁用其Death Animator；通用旧怪表现兼容保持。Slime无Attack/Hurt动作，不补新能力；BossFly/MushroomJump仍为现地面表现。

黑线只读已接受归属数组：
1. 遍历每个节点的四条格边；邻区不同/地图边缘则产边，规范键(min端点,max端点,表面层/归属对)去重。
2. 跨高程不把共享屏幕投影当同一条世界边；依据原MapCoordinateBoundary/投影合同求端点，楼梯过渡另核正确表面。
3. 可合并共线段作缓存，但节点归属仍为规则真相，折线不是碰撞墙。
4. 缓存键为布局摘要+投影版本/变换；换代重建，开关仅显隐。复杂度O(N)，段数O(N)上界。
5. Editor/Development条件内Inspector默认与热键；发行构建不含可打开的入口。开关不抽随机、不改AI/碰撞/存档，不吃正常游戏输入。

## 11. 四条端到端说明实例

下例**说明用手工候选**：7×5、Origin=(0,0)，NodeId=y×7+x，所有格平坦可走；为算得清，使用点足迹与一图步缓冲。它不能证明真实玩家/怪物能在这个小尺寸里摆下，也不是生产参数或算法运行产物。

### 11.1 地图→领地→居民

白话：先给出生与休息留保护，再把能活动的地面分给普通居民；人数在纯数据阶段定好，最后才从池里取对象。

- 商人安全R0：core={7}，buffer={0,8,14}，merchant.main示例落在8附近的合法交互安排（本例忽略实际商人形状）；出生7不改。
- 休息R1：core={27}，buffer={20,26,34}。
- 普通R2：成员示例{9,10,11,16,17,18}，Ecotype=skeleton.default。
- 普通R3：成员示例{21,22,23,28,29,30}，Ecotype=torch.blue。
- 其他为中立R4，本例无Boss。保护两组件分开；7→8→9→10→11→12→13→20→27经过危险/中立格。
- 数组局部：RegionByNode[7]=R0，[9]=R2，[21]=R3，[27]=R1；ProtectionFlags[8]=Buffer，[9]=0。
- 布置记录：(r2.0,home=R2,start=9)、(r2.1,R2,18)、(r3.0,R3,21)、(r3.1,R3,30)，此处人数4只作记录示范，实际需求算法见§5手算。
- 小队列推演：仅演示在扣除保护后的图上，从9和21开始的一次BFS扩张。初始Q=[9(R2,d0),21(R3,d0)]；出9按右上左下加入10、16、2，8被保护排除；出21加入22、28，14被保护排除。此时Q=[10,16,2,22,28]。继续生长/容量协调可能改变本手工候选，不能把这两步冒充上面完整记录的计算证明。
- Planner输出冻结记录→原PlacementPlan接受→Commit→Population取得Prefab并设置最终点/身份→地图重初始化→Registry核4个居民与唯一商人→Ready。只有最后这些阶段实例化。

### 11.2 一只骷髅的活动

白话：r2.0认的是R2，路过R3或Boss地盘不“搬家”。它可以防身一下，但出门证仍是同一张。

注册G77/lease6/homeR2/start9，Patrol在R2合法点间换目标。发现玩家→Search→Chase；实际足迹任一部分首次越出R2时建立permit5、departedAt=12.0秒。到16.0秒跨中立/另一普通/Boss边界，仍permit5、homeR2，外出4秒，不给新许可。到期或失去资格ReturnLatched=true；受到伤害只走合法Hurt，不重发permit；近身反击用新AttackId，但return锁存及elapsed继续。完整足迹真实回R2合法返点才结束permit5，不能以移动被Stop假装到了。死亡则BaseDamageable发一次defeat/drop，原楼层计一次；随后回池lease失效，无返程满血。

若路径被动态物体堵住，合法等待/有限重算；耗尽记FAIL并保留故障，不发defeat。世界真正暂停时16.0秒冻结；只开背包禁输入而世界不停时，elapsed仍增长。

### 11.3 进入又离开的攻击

白话：A18的记录写着“起手时玩家保护修订90”，之后哪怕又回外面，90已经变102，旧票就作废。

完整逐时刻见§7.3。已选T09-A冻结起手目标，前摇中CurrentTarget改变不改变本次对象；旧目标失效就取消本次，不转打新对象。T13选定的排序决定“哪一步实际位移先于这次扣血”；已经结算后才进保护不回滚。

### 11.4 入口保存→退出→恢复

白话：保存的是“这一层刚开始时”，下次重造同样领地与初始居民，战斗重新开始。

新入口记录示例：(profile=ecology-zoning-v1, requestedSeed=42, terrainAttempt=1, ecologyAttempt=2, recipe=1, configDigest=c1, footprint=f1, context={floor:1}, ecologyDigest=h1)。当前运行G77不保存。Ready后原入口candidate保留，槽位保存其clone；退出后Restore按原身份与版本生成。新运行G99、lease重新编号，但RegionId/ResidentId/初始点与h1相同。人物/背包/商店按入口快照恢复；不恢复r2.0上次剩几滴血、追到哪里。缺c1或h1不符则原失败页，不偷用新配置/新Seed。T11-B已不适用，本例不存LayoutDto；缺精确旧配方不得以正文快照绕过。

## 12. 成本、验证与未决事项

### 12.1 成本账，全部NOT_MEASURED

| 工作 | 时间/空间上界与次数 | 需要实际记录 |
|---|---|---|
| 地图派生图/楼梯索引 | O(N+E+S)，空间O(N+E+S) | W/H、V/E/S、静态阻挡、构建分配 |
| 各足迹clearance | 直接格覆盖O(P×N×F)，边扫掠还计每边shape检测 | F、每类合法格/边、耗时；不是常数操作 |
| BFS或加权生长 | 每attempt O(V+E)或O((V+E)log V) | A、K、修复pass、拒绝原因 |
| 稳定种子挑选 | 若逐种子更新全图距离，最多O(K(V+E)) | 不把“多源BFS一次”漏掉择种子成本 |
| 类型/容量/采样 | 每attempt共享P图＋候选排序ΣO(Ci log Ci)，间距最坏O(M²) | 候选数、每种footprint、放点尝试 |
| 通路保留 | 每次阻挡候选校验O(V+E)，最多D次/attempt | D及总搜索节点数，避免M×C隐含放大 |
| 回家字段T08-A | H个(领地,体型)键，每键O(V+E)/O(V) | H、内存峰值、失效次数 |
| 历史备选T08-B（NOT_APPLICABLE）；实际追目标路径另计 | 每次O(V+E)/O(V)，总Q次 | Q/帧、目标跨格触发、分配/GC |
| 位移与保护 | 每活动实体每物理步局部足迹/段查询+Cast；不全图扫 | 活动M、固定步数、Cast满槽/实际交点数 |
| 攻击历史 | 固定目标O(1)修订比较；事件取消O相关pending数 | pending总数、队列长度、过期清理 |
| 黑线/摘要 | 换图O(N+M+K)基础遍历，排序另计 | 构建/显隐分配；每帧不重算 |
| 存档（已选A） | 按manifest/身份大小；B额外布局成本仅NOT_APPLICABLE比较 | JSON字节数、读写、重建、错误分布 |

当前64×64只是资产默认规模，实际有效V与M要测。示例若H=10，int距离字段约160KiB数据，另有下一步/键/对象头；这是算术估算，不是Profiler测量。缓存键数量受K与P组合约束，采用按需有上限缓存，峰值先记录再调。无测量前不引入DOTS/多线程。

小参数首轮工程初值：生态attempt8、局部修复2、巡逻历史4、每怪重算冷却先以现运动/物理步为依据并记录，不凭空给生产毫秒承诺；静态候选一次完整遍历，动态阻塞重试使用有限次数/秒配置。值必须注明单位、上下界、取值原因，调参不改变保护/返回/群体底线。

### 12.2 验证矩阵与能证明什么

全部新项NOT_RUN，性能NOT_MEASURED。ACCEPTANCE追加技术编号，未来授权后执行：

| 决策 | 反例/验证入口 | 能证明/不能代替 |
|---|---|---|
| T01 | 双索引不符、跨代布局、冻结后写；Plan与Registry一致 | 纯数据权威合同；不替代真实激活顺序 |
| T02-A/T03-A | 隔崖、双梯、碎片、窄口、大体型、修复断区 | 图/足迹连通与终止；不保证每Seed可行 |
| T04-A/T05-A | 对角碰角、梯口、完整脚底、外到外高速穿越 | 几何分类；仍需真实Collider/物理步 |
| T06-A | 取整余数、预算不足、容量不足、类型顺序打乱 | 分配不变量/偏置；不证明群体观感 |
| T07-A/T08-A | 长U路、Home点远边、动态堵塞、目标每格移动 | 距离/许可与成本上界；真实性能需Profiler |
| T09/T13 | 起手→入→出→命中，起手换目标，同帧/多物理步、撞墙、回池/死亡/换图 | 历史/身份/顺序；文档时序不是运行证明 |
| T10/T11-A | 同输入摘要、debug开关、配置同名改值、旧profile/未知schema/缺字段 | 静态复现及兼容；不要求逐帧战斗回放 |
| T12-A | 候选失败、Registry填充/投影/半实例化失败、商人/目标集不完整 | 门禁和新入口不半发布；不宣称全链回滚 |
| 表现/黑线 | 四怪同镜头/足迹、Death/池复用、相同Seed开关图、发行入口不可开 | 人工体验/真实显示；不升级原地形阶段验收 |

纯数据测试查算法与DTO往返；Unity集成查Collider、生命周期、脚本顺序和实例绑定；Profiler/长跑查耗时/内存/GC/失败率；人工Player验收查画面、群体感、追返体验。四者分别留证，不互相替代。本轮不写生产测试、不建新工程、不跑原型/微基准。

### 12.3 当前确认、依赖与停止点

用户已明确确认T01～T12=A、T13=B。原27玩法及5项不适用不变，未选技术候选已在台账标NOT_APPLICABLE，保留比较与原问题快照。REV-A～F的普通合同已补，当前没有需要用户重新选择的核心方向冲突；T13真实轨迹与物理定序、候选几何一致性、精确旧内容解析是实施/运行前提，尚未验证，不伪装成已存在。

本稿v0.2已由用户明确采用为带验证前提的实施依据；授权P0/P0.V及完整门槛通过后自动P1～P6。T13 HIGH_RISK / RUNTIME_NOT_VERIFIED。当前首个攻击链四例真实PASS且自然exit0，CIM准入已恢复；真实运动与完整矩阵尚未实现/验证，继续逐片实施。具体事实见P0V_EXECUTION，事前判据在ACCEPTANCE；几何与内容版本仍按对应前置验证，不因当前授权自动通过。

最终技术基线明确确认后按原连续交付目标恢复，普通实现不新增逐步审批；Unity运行准入、受保护资源与Git范围仍按真实授权，不由本稿扩大。新证据若暴露实质冲突，仅回到相关T题，不推翻其余方向或重开玩法问卷。

## 13. Run23 后的唯一 T13 合同取舍（用户已批准 B1，先定向验证）

**2026-10-21 生效增补：** 用户在选择框明确选择第2项，随后直接要求执行 `DECISION_ROUNDS/t13-b1-bounded-validation-response.md`。下文保留提出时的 PROPOSED 背景；当前 B1 已 CONFIRMED_FOR_BOUNDED_VALIDATION / HIGH_RISK / NOT_PASSED。原严格合同下 Run20/Run23 FAIL 不回填 PASS；Run20 源码与本轮变更前源码分别保存在既有备份和 Evidence/T13B1-20261021/BeforeB1。

本变更仅修订 Q12.2、T05、T13 和 V0-06 中“保护历史观测覆盖”的部分：任何**实际观察到**的完整移动实体脚底任意部分接触主体/缓冲必须保护，已证明的实际扫掠和多物理步历史继续有效；同一步求解内部、实际观察点都在外侧且无可信扫掠的短暂进出不再承诺全部捕获。它不是“已证明从未进入”。连续轨迹缺独立真值，漏检率 UNKNOWN，不以未观测到漏检报告零漏检。安全主体/缓冲移动禁入、追击/返回、身体类型、自动物理及全局设置不变。

新运行前登记 B1 判据：原三场景不删、不挪保护区/缩碰撞体/改碰撞层；普通接触仅因内部过程不明不普遍吞伤害；实际保护见证永久取消，消费前复核；无接触且非受控位移、闭合观察后位置改写、身份失效/查询失败仍分别故障。伤害同时记录真实 HP/OnDamaged、攻击终态、观察源/位置/扫掠前提与延期。任何可复现的正常边界保护失效都要修复或回审，不能归入 B1 例外。

### 13.1 已完成的原合同内修复

当前 `EcologyMotionBody` 可以在实际物理接触回调和已完成步的观察点，以实际完整脚底的零长度形状查询取得“确实与保护相交”的正向见证。保存该步见证与绑定的保护修订；旧攻击即使随后退出也不能恢复。不是用提交点、首尾连线或碰撞事件本身假造进入。

新增反例由独立原生 `Physics2D.Distance` 对保护矩形查询体核对实际相交，Run22 先取得 `FaultRejected` 而非保护取消的 RED，Run23 得到 `ProtectionCancelled`、HP 不变的 PASS。原三项正常碰撞反例的方法文本保持不变；Run23 仍分别有 20/20 个 Unknown，不能宣布正常贴身伤害已修好。Run21 的初始重叠夹具错误与 Run22 的超时收尾也原样保留。

### 13.2 当前缺什么证据

三个原反例的保护矩形、完整脚底与实际坐标详见 `P0V_EXECUTION.md` 本轮调查。实际位置与提交点不同，说明碰撞求解确实修正了运动；读到的实际观察点在外面，不足以证明整步从未进入。不能把这一信息缺口换名为保护取消，也不能为消掉 Unknown 而放行伤害。

候选筛选结果：实际脚底正向见证已实现并验证；空保护集合排除本来就有，不记作新成果；空间包围排除尚无可用于这些近边界接触的、覆盖全部移动/接触修正的可靠边界。`maxTranslationSpeed`、`maxLinearCorrection` 和迭代次数不足以直接宣称某个公式覆盖本版本整步全部求解，故未实现拍脑袋包围盒。公开 `MovePosition`、`Cast`、`Collision2D` 文档没有提供内部完整连续轨迹保证。这里是“尚无已证明满足原合同的方法”，不是“所有 B 实现不可能”。

### 13.3 一份最小候选：T13-B1，改变保护历史的定义

**白话：** 保留现在的正常物理碰撞，但把保护历史明确限定为“真实观察到的完整脚底接触，加上已经能证明的实际扫掠”。接触步如果所有实际观察点都在外面、其内部轨迹没有取得证明，不再仅因此拒绝本次伤害。代价是引擎同一步内部短暂进去又出来、且所有观察点恰好都在外面的接触，可能不再取消旧攻击。

这是一条真实安全语义变化，已获用户明确批准先定向实现与验证。原严格合同失败快照不改；原T13-B的排队方向和其余玩法、HIGH_RISK保持，现行V0-06按本增补解释。不能命名为“通过原严格合同”。

| 方面 | T13-B1 的具体影响 |
| --- | --- |
| 正常接触 | 只因碰撞轨迹不可还原而发生的拒伤可取消；仍须真实 HP 恰好扣一次验证 |
| 安全边界 | 观察到实际脚底接触必取消；未观察到的同一步接触进出可能漏取消，是必须明确接受的损失 |
| 高速外到外 | 已证明的无接触直线扫掠仍保留；碰撞中的高速进出不承诺全部捕获 |
| 手感与物理 | 不改 Dynamic/Kinematic、CCD、步长、求解器、碰撞层、移动接口或额外等待；伤害资格变化仍需实测 |
| 身份与旧链路 | Generation/Lease/AttackId、死亡/回池失效、保护修订永久取消不变；未绑定旧链路不受此候选改变 |
| 修改范围 | 仅接触步的历史资格分类及相应验收定义；不得传播到区域禁入、追击许可、返回或存档合同 |
| 回归范围 | 原三例保留为旧合同失败快照；新增 B1 明确判据，补真实 AI 前摇、进入退出、碰撞高速、异常位移与旧 Profile；其他 V0 项仍逐片完成 |

批准前曾建议保留严格合同；用户现已明确接受本节披露的观测覆盖损失，故继续B1定向实现。该决定不等于验证通过，真实缺陷仍修复；完整修订门槛通过后沿原授权自动继续。T13-A本身不补齐缺失运动信息，仍不适用。

该用户级判断已经完成，不再重复询问。普通血条、脚底参数、素材动作映射和测试组织由工程调查处理；只有新出现且超出本次有限批准的实质变化才回审。


## 14. P3 已实现结构与验证边界（2026-10-22）

这是实施事实和工程细节，不增加第28项玩法或第14项用户技术决定。P0.V/B1仍按第13节；不可观测的碰撞内部接触漏检率UNKNOWN，T13保持HIGH_RISK。下表不是正式新Profile/Ready已经接通的声明。

| 实际位置 | 数据与单位 | 当前职责与边界 |
|---|---|---|
| EcologyHomeNavigationCache | 接受的布局、静态几何、区域/完整脚底类、静态版本 | 同Home共享合法节点/反向归家场；巡逻目标场和追击目标场各最多16份；静态变化的新版本接入正在验证 |
| EcologyHomeNavigation | Home地面、合格活动分量、全域安全禁入图、D/Next | 实际完整脚底+可认证局部连接判Home；不能只用身体所在格或D=0判归家 |
| EcologyResidentBinding | Guid GenerationId、字符串ResidentId/TypeId、HomeRegionId、lease内Excursion | inactive预绑；Ready前无AI；显式旧路交接才释放，普通故障不转旧写法；重绑导航须保留原许可/HP/租约 |
| EcologyExcursionState | Episode整数、DepartedAt/Elapsed为缩放世界秒、ReturnLatched、一次反击预算 | 正式默认6图步/16秒；只有实际观察或可信实际扫掠出Home才建episode；意图不发许可；暂停世界冻结 |
| EcologyMotionBody | 物理步号、Normal/Hurt意图、原生脚底、一次提交与实际观察 | 原Cast保留skin后，对该前缀做完整脚底地面/安全/范围检查，只提交合法前缀；B1历史仍看实际结果 |
| 原MonsterAI/Patrol/Chase/Attack/Return | 原感知与近战、共享图路径、有限等待 | 继续使用原状态机；返回锁存阻止再追，真实OnDamaged/Hurt不能换许可；一次既有反击取原前摇+冷却，至多2秒 |
| 原MonsterMovement | Idle/Moving/Arrived/Blocked/NoPath/Retired | 新路由可区分结果；Return完成不依赖旧!hasDestination，而由实际观察确认完整Home、无该步已提交意图、零位移和零速度 |
| EcologyCombatBinding | P0.V现有long代次/居民句柄及Lease | 当前属于原队列内部身份；正式生产接入仍须与MapRuntimeContext的Guid/逻辑ID逐一绑定并在Ready前验证，不能把两种字段类型假称已经相同 |

实际观察顺序：物理步完成→读取原生脚底与body→B1保护历史→若可信实际直线扫掠穿出Home，记录真实区间见证→按实际末位姿更新外出时钟→只有完整Home且该步没有已提交运动意图、实际零位移零速度才结束许可。仍有受击意图时的零速度顶点不算停靠。未知轨迹不凭请求位置补出一个事件。

连续Home距离先查完整HomePose，成立为0。否则精确合法格中心读原D/Next；其他连续位置作为临时节点，只尝试所在格和四邻中心，逐段查地形邻接、完整脚底地面/保护和静态碰撞，取1+D最小值。这个1是离散连接成本，不是像素或世界米数。

整段范围证书使用同一个Home锚定D：沿途每个根位置须能合法连到D小于上限的局部锚点。连接线族组成三角形，连同完整脚底构成Minkowski Sum（闵可夫斯基和，即整只脚沿这些连接线铺过的区域）；检查其内部静态障碍和地面/安全边界，不只查三条边。没有一张证书覆盖整段时做深度最多12的有界细分，只放行已证明的连续前缀。它不宣称所有合法位移都能无损放行，后续仍需测多Seed/复杂路径及实际成本。

暂堵不改永久地形或静态图：实际Cast继续挡实体，返回按真实归家进展记账，1.25缩放秒一轮、最多3轮无进展后明确FAIL，保持演员活动且可受伤/击杀，不回血、不瞬移、不自动消失。反击或受伤不能重置这套进展账。可破坏静态物死亡则应产生静态几何版本，更新共享字段；动物/怪/玩家暂时占路不能写成永久墙。接受时的不可变布局摘要和入口存档身份不跟着死亡改变。

验证入口为P3_EXECUTION.md和Evidence/P0V/RunNN-results.xml/RunNN-Supervisor.json/输入清单。Run122走通实际原感知、追出、锁存返回、一次原反击起手与实际扣血、真实OnDamaged/Hurt、归家；Run124验证受击顶点不清许可并回归原FSM；Run125验证实体堵路的有限FAIL及真实致死接收器。静态版本核正在Run126以后验证；原Population/Registry/Ready事务、正式新Profile、版本内容保留、最终Windows交付尚未完成。


## 2026-10-22T13:15:45+08:00 ART-AMENDMENT-20261022-V2：保全与候选改道

P2联合规划与实际原生几何Run95通过；P3正式Coordinator/原人口/Registry/Ready/玩家共享Barrier/旧路由恢复Run139通过，三种子46居民5520完整Home观察及实际静物死亡自动更新Run140通过。固定未筛选64地形联合规划Run141为60接受/4拒绝，不当64图运行；真实Boss种子20994032有14居民/1680Home观察。Run142混合前置失败已仅修自有测试Scene卸载；Run143/144功能46/46但原2秒子树收尾失败保留。Run145=9a2c212daef74dcdbd255599d35b55ad，同46/46、3565输入稳定、自然exit0、原Supervisor已有单次30秒树退出宽限、非强制清理PASS/无残留/双锁释放/postFREE；共享工具与物理未改。ART-AMENDMENT-20261022-V2已在无活动作业安全检查点生效：保留四怪物所有已完成成果及旧身份/旧档，暂停新增美术适配(DEFERRED_BY_ART_SCOPE_CHANGE)；制作仅含现场核实的Torch/TNT/Barrel十二类型的显式隔离候选，四怪物仅从该候选生成资格排除。无合法同风格Boss，候选Boss内容待定且无Boss，通用Boss算法/旧配方继续。当前正式默认terrain-v2-xiaosu-v1未切；从此暂缓进一步正式入口/内容推广，P6只交候选。ART_REVIEW_PENDING不阻断独立共用开发；P3剩余/P4-P6及T11仍未完，T13 HIGH_RISK/不可观测漏检UNKNOWN/人验PENDING。

执行附件已原字节归档 DECISION_ROUNDS/rpg-monster-art-preservation-and-retirement-v2.md；另外两附件标为参考，不充当当前Run事实。检查点、四个精确Prefab GUID/直接依赖及成对增量副本在 Evidence/P3/ArtAmendment20261022/checkpoint.json。原可摧毁蘑菇不在排除名单；原MapEcologyProfile与旧ZoningV1均保留。既有P1 Build02媒体只证明其绑定的旧输入，不充当新候选运行。新增候选内容、足迹、实例、Registry/目标、死亡/回池和发布依赖须实测；包内依赖以BuildReport为准。四怪物旧兼容仍验，未完成美术适配不改PASS。

当前仅有一段尚未编译的楼梯测试草稿，已原字节保全；它不是已完成实现，须完成或按本次候选范围改接后再启动。B1合同、27玩法/13技术方向不重问，原严格FAIL不覆写。


## 2026-10-22 数值版本隔离与查询等价修复

Run197/Build08两个数值诊断包成功；Run198开发与Run199普通均自然exit0、非强制清理/无残留/双锁释放/postFREE，保护回读保持。66种子原算法跨构建有16个原地形差异；隔离Float32实现原地形0差异、装饰0差异、异常0。详细四种子数组一致；旧613580675原结果有81地形格/151高程格和楼梯差异。实际IL及编译rsp支持独立程序集固定中间float存储有效，但不外推所有平台/未来编译器。

新增显式Profile ecology-zoning-units-float32-candidate-v2，config=95a43108674307dd147f2ecf54311c34e8699934e310a9147090d788a28acea9，content=content-r1:274886eeb5f5ec582673200c25b09966e574b2e8660708b176f2bc563f27cb9f。它仍只含原12种Torch/TNT/Barrel、无Boss。原MapGenerationController保留唯一生成/规划/Commit权；新的ScriptableObject只提供无运行态数值策略，实际依赖/源码/编译配置均纳入新有限清单。旧ZoningV1与UnitsCandidateV1的资产、内容清单和生成实现原样保留；不重写旧R1含义，不迁移旧槽，不让新策略替代缺失旧版本。旧R1跨开发/普通构建重现能力已知有差异，严格摘要不匹配时拒绝并保留原档，不能声称已修好旧R1所有跨构建档。正式默认仍terrain-v2-xiaosu-v1；只有独立候选Build extra define选择新Profile。

Run200=ea201b157f584030b4c9105ff2ea7173，3/3、0Skip、3630输入稳定、自然0/非强制清理/双锁释放/postFREE：新候选三真实Ready/完整Home巡逻/静物死亡刷新/原目标集，以及e1原槽保存和一次精确恢复。原算法测试副本另以2520次真实Circle/Box、正负倍率/offset、静止/斜向/长扫掠、96矩形对照原精确区间，未知形状仍拒绝。

随后优化仅将原生脚底读取和保守扫掠包围盒一次性准备，复用到同查询各区域；完整圆角/矩形窄相公式和区间合并保持。包围盒数值guard只扩大待精查候选，不扩大实际保护区。Run201=9c2ad9968051437fb89b5d8f6cadd10a，32/32、0Skip、3630输入稳定、自然0/非强制清理/双锁释放/postFREE，包含完整B1伤害组、原算法区间精确对照、新候选三图/e1。还不是性能PASS，需新源Player实测。Run202扩展合并回归正在执行，旧/新12种和旧Boss跨选择恢复、缺失旧版本不替换、原Retry/换层及TRB2只读恢复均纳入。

## 2026-10-23 人工整改 R3：共用真实投影表面

此节为AUTO-006受托实现，原B1合同及旧严格失败不变。remediationProjectedSurfaces默认false，仅独立Settings_R3开启。每个上层南边实际绘制的Tile生成一条Face命令（高程、实际低格、原Tile/Sprite）。MapElevationProjector按该命令画Face并将Sprite导入物理轮廓变成同Grid空间静态Polygon；MapCandidateCollisionGeometry追加完全同一组世界轮廓与原接触半径，原逻辑高低窄边和楼梯开口继续保留。旧Stage1与旧设置不接入，不改玩家Collider/缩放、不整墙固定移动。

MapRemediationSurfacePlan以字典和集合存同代Face/楼梯表面；地图格遍历O(width*height)，轮廓O(总顶点)，根部资格查集合、四邻水和有限楼梯。装饰在保留生成器输出后按锚点/全部Ground部件/CollisionOffsets同高程支撑筛选，树冠悬垂不删除；场景Plant原候选排序之前增加表面资格，原植物所有者/预约/Registry不变。H0排除实际既存水沫投影格；高层顶面盖住底层水沫，不误删其干燥地面。P7精确方向/直角规则仍WAITING_REFERENCE_DETAILS。

RemediationRuntimeAudit独立读取已经提交并实际渲染的Face/Stairs/Foam及实际Registry，不复用资格函数，逐个根部核对；零违规同时要求合法装饰非零。Run226真实红灯、Run227断言绿但退出超时、Run228完整自然退出绿分开保留。新R3内容/配置如实冻结，不继承旧R1/R2身份或e1数值；早期未验收R3隔离槽不伪称跨源码版本精确恢复。原六正式档只读保全。


## 2026-10-24 R4 原生楼梯候选资格（自主设计决定）

详见[定向事实、取舍、数据和伪代码](DECISION_ROUNDS/2026-10-24-native-stair-platform.md)。R4-STAIR-ACCESS-001与R4-NATIVE-PLATFORM-001均为用户已授权D2范围的DELEGATED_DECISION，不冒称逐项用户确认；原Seed/原脚底/物理/8次生态预算/B1不变。Run279三个定向入口完整通过，当前Run280全50例仍待断言及收尾，旧包BuildR403不当最终候选。原六人工PASS范围与岩壁、植被人工FAIL保留；本阶段无人为变更验收结论。


## R4有条件集成的来源兼容补充

旧档来源快照与当前Host/真实Player编译图/SDK原字节双重校验，见[R4兼容来源合同](R4_COMPATIBILITY_SOURCE_CONTRACT.md)。这是用户连续授权内的D2兼容接缝，旧清单和旧档不改，不新增玩法确认。当前阶段和未完成门禁以[R4集成报告](R4_INTEGRATION_REPORT.md)为准，不能用静态方案或旧R4人验冒充新集成包通过。

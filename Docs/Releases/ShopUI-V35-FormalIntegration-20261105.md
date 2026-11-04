# 商店 V3.5 正式 SampleScene 集成

正式场景继续使用 `Assets/Scenes/SampleScene.unity`，商店 UI 更新为已完成的卷轴、木质商品区、详情和真实商人 Idle 表现。交易、背包、库存、商人交互和地图／存档数据权威沿用当前主工程。

## 使用与私有资源前提

本机主工程保留完整资源。公开 Git 只含新增美术与统一 MyTMP 的引用身份、校验清单及恢复工具。首次克隆后先按 [私有资源恢复说明](../../Tools/ShopUI/README.md) 恢复17个精确资源，再打开Unity 2022.3.62f3c1。MyTMP原GUID和meta保留；字体本体仅退出当前Git跟踪、本地新版保留。没有修改历史提交、重烘焙字体、另建业务字体或再次清档。当前正式槽位1与Prefs继续保护。

进入新冒险，WASD靠近商人，F开店；点商品购买，Esc／关闭按钮关店，Tab看背包。人工检查完整外观、四个按钮字的内框居中、详情排版、商人Idle/横幅、单击单次购买和关店恢复输入。本轮最终人工状态为 `PENDING_USER`，原V3.3视觉、V3.4功能确认保留各自版本范围。

## 范围与复用

| 功能 | 旧实现 | 本次实现 | 关系 | 原因 |
|---|---|---|---|---|
| 商人交互 | MerchantInteractionController → GameInput/F | 原事件链打开正式场景ShopPanelController | Direct Reuse | 不增加第二个交互权威 |
| 交易、金币、库存、背包 | ShopTradeController / PlayerInventory / PackageGridUI | 保持业务实现与ADR-03；只更新UI呈现绑定 | Direct Reuse | 防止重复成交及状态来源分裂 |
| 详情与分类 | ShopGridUI、ShopSlotUI、ShopModeTabButtonUI | 增加ShopDetailsUI、ShopUiText和视觉组件，保留原分类语义 | Extension | 复用既有目录和商品数据 |
| 商人头像与横幅 | 实际Merchant prefab的Animator/SpriteRenderer | ShopMerchantPresentationUI读取真实来源，隔离预览相机和RT | Necessary New Module | 原UI没有持续Idle展示，组件不拥有商人业务状态 |
| 字体与内容 | 原MyTMP GUID、schema2 Host、旧内容manifest | 采用此前已落地当前字体身份与配方；旧manifest不改，按精确组重新封存 | Intentional Compatibility | 统一字体时保持内容校验与现有旧Profile职责 |
| 隔离候选封存／执行 | seal_i1_host / run_i1 → Guard → Supervisor | 显式绑定snapshot/materialization/真实根；私有Editor命名空间和可选测试宏 | Extension | 不借父仓库Git身份，不另建运行基础设施 |
| 私有资源 | 原项目许可证边界 | 精确manifest/meta/GUID+本地资源包恢复 | Necessary New Module | 缺少适用于本次17项资源的恢复入口，其他怪物工具不纳入 |
| 架构与验收报告 | 原scan/snapshot及deliver_readable字段/Links/tree | 公共图谱以opaque节点表示私有资源；中文离线Shop报告 | Extension | 静态证据不能冒充像素／运行／人工结论 |

迁移从73项清单开始：69新增和4个UI源码，正式场景UI143→310个块（新增167），415个非UI块和4组交易/背包业务绑定保留原字节。四个辅助地图场景保留；没有集成Shop演示场景、储备怪物、未知字符表或其他dirty。

原输入Manifest共1220项：保留旧字节，原内容身份严格25项受审差异；当前统一字体身份24项差异。原MainV1与旧VNext清单/配方保留有效职责，未实施schema3或旧字体运行时`.bytes`。新Host/Stamp绑定完整845项（335编译输入），内容/font/Host检查继续有效。

## 测试方式与边界

测试使用独立Editor公司/产品、私有槽位与设置。商店探针真实经过InputSystem → GameInput → merchant范围/事件，购买经过EventSystem射线命中 → 原交易系统。测试接近商人的摆位复用既有合法SetupFoot，四风格演员明确是隔离夹具，不声称实体键盘行走或自然随机分布。普通Windows包不带RPG_SHOP_INTEGRATION_TESTS；截图专用变体显式带该宏，两者源码Host一致，二进制和数据命名空间分别记录。

原正式默认五项回归还依赖本机历史MainV1固定测试副本，SHA `ecfef6c8fadcaa5177a3a0157563d4dcf21441e8d6263ef24e9729a65943105b`，只复制到派生Artifacts，不能把缺少本机夹具的新克隆当作已运行这项测试。正常游戏与新的保存流程不依赖该测试副本。

最终结果、真实Run/Build/源码身份、失败索引、Windows完整文件树和截图绑定见本机 `Artifacts/ShopUiIntegration/ShopUI-V35-20261105-020020/`。原始运行日志、付费像素和验收包不随Git发布。

## 本轮已取得的候选技术证据

| 范围与验证主体 | 结果 | Run ID / 身份 |
|---|---|---|
| 独立Standards与Spec静态审阅 | r04各869/869逐SHA、0开放问题；仅静态资格 | snapshot `55c992bcb0b3d4b7ceefdccbdcd59eb7e65f53de20c77d3db136279f504dec81` |
| Host／内容EditMode正反例 | 4/4；测试代码在g03/g04之间不变，自然0 | `4ea8abe5673a4129946b65876ac8ed8b` |
| g04正式商店PlayMode | 1/1；F/merchant各1，3笔购买+售罄，分类详情/背包/ADR清理，四Idle/横幅 | `7fbb6daaa7174235b7dd493585c8e21c` |
| g04原正式默认5项PlayMode | 5/5；普通按钮、新旧保存重开、随机重试与严格拒绝 | `30fcdfd8e7c74fe3b03de03b2e9c1f1c` |
| g04显式SHOP_REVIEW Windows构建 | 成功，0 errors；原25/当前24差异与完整源检查均有效 | `0f432b9381054faab234e49a1f30dbd9` |
| 1920×1080真实Player自动流程 | 完成，15张原始帧；非实体键盘人工结论 | `4dd303509bae4b2dba877b28b7324fa9` |
| 1366×768真实Player自动流程 | 完成，15张原始帧；非实体键盘人工结论 | `92d7bebbe7f34483a78d99959ec1645f` |
| 本轮真实新档→独立新Player恢复→实际保存重开 | 三份完整World原字节相同，SHA `bfd9a7be964822c5c75c4f1adc6adfb9f537fd6ca25158c648e104c9ec1b8f0a` | `f097d162e83947f483e91dd0ddb1ef87` |
| 精确Git索引检出＋17私有资源恢复 | 845输入及含Stamp的336个编译文件原SHA匹配，未借用主目录dirty | 本机任务 `staged-validation.json` |
| 工具／私有恢复／静态快照／中文报告语义 | 分别31/8/16/6项通过；不替代运行和人工 | 本机任务 `evidence/*unit*.log` |

上述有效Unity／Player运行均自然退出0、无forced或残留，Job清理与双锁释放通过。Build中的旧TMP动态fallback缓存变化复用原已独审精确字节对和恢复规则，原字节恢复且完整Build输入匹配；没有给新字体哈希临时放行。

失败保持原样：g01缺少21项尚未导入的UPM元数据时封存拒绝；t35-e01因编排未止错误启动，已自有Supervisor取消，不计验收。实际UPM导入后21pins原SHA匹配；g02拒绝派生空Worker目录重新生成的meta，精确备份并仅移除该空目录/meta后才封存。首商店F夹具失败后改为复用既有合法SetupFoot/明确按键时序，由r04新Run证明，未改产品输入/交易。t35-f01缺本地旧Profile夹具；补齐原副本后，过宽Formal筛选t35-f02带入旧诊断并被自有取消，最后五个完整方法名的t35-f03通过。取消记录始终为CANCELLED/FAIL，不升级为自然退出通过。

Git通用空白检查原始返回2：314处均为Unity序列化空标量的标准 `key: ` 行，逐行限定扩展名与空标量正则后无其他项，保留原验证字节；没有豁免C#/Python/文档空白问题。原始检查及精确资格记录见本机 `whitespace-qualification.json`。此为序列化格式判断，不影响内容、字体或Host断言。

最终普通Windows构建、真实主目录复验、源码提交/远端HEAD/CI及交付入口由本机任务最终回执记录；以上各行只陈述已经完成的候选证据，不能凭它们宣称整个dirty目录或人工验收全部通过。

## 正式主目录及真实冷克隆补验

main真实SampleScene商店Play为1/1，Run `d3021506a89f4814a02db2622b723c08`，自然0/unforced/残留0/双锁释放；测试前后正式存储文件及18项Player/5项Editor偏好原值一致，临时设置精确恢复。

源码普通提交 `7621439fe6f14698171ec518f006281106494848` 后建立真实干净Git冷克隆，无Library，先恢复17私有资源后Git仍clean、845源组原SHA一致。冷导入普通Windows Build Run `1bb112c5b9984784869062092bca54f5` 成功，0 errors，`extraScriptingDefines=[]`、`shopIntegrationTests=false`，源输入/输出树资格及自然收尾通过。

普通二进制的两个真实进程 Run `6906bb39973d4243b9352473233d057d`、`853710a72cb14713b81dc5975d9b5917` 从本轮实际新档副本出发，真实保存后第二新进程恢复、再次保存重开；四份完整World均与前述新冒险原件bfd9…b8f0a一致。正常入口仍使用baked影子company/product与原选择器，未调用I1私有启动能力或商店测试宏。最终可玩包是普通二进制，截图来自同一g04源组的显式Review变体，两者分别标识。最终main若仅追加文档/便携GUID映射/生成图谱，产品输入继续逐SHA匹配，无须据文档提交再伪称新构建。

架构表补齐本次实际 `com.unity.ugui@1.0.0` 的LayoutElement与HorizontalLayoutGroup两条原包GUID；以原.meta和脚本校验值登记来源，解决新增布局组件在便携扫描中的8条重复未解析记录，不过滤未知项或更改运行时组件。正式图谱从包含该映射的干净已提交源码重新生成；主目录其他dirty与人工PENDING_USER边界保持。

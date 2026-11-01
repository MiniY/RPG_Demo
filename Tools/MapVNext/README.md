# MapVNext 报告入口

`run.py` / `run_r3.py` 负责既有 Guard → Supervisor 的 Unity 采集，不为说明改版重跑。`deliver_readable.py` 是采集之后的中文报告生成与交付检查入口，继承原 `deliver.py` 的案例/素材组装与 `timeline.py` 的原图 + frames.csv 回放绑定；原一次性作者脚本和历史 r3d 产物保留，不再直接手改 index.html。

首次或再生成（从本工作树根目录执行；原始证据只读，输出为独立目录）：

```powershell
python Tools/MapVNext/deliver_readable.py generate --source Artifacts/MapAcceptance/MapVNextStairs-20261028-r3-ReviewFix --legacy Artifacts/MapAcceptance/MapVNextStairs-20261028 --output Artifacts/MapAcceptance/MapVNextStairs-r3d-readable-v2-basic-visual-20261029 --input D:/Download/rpg-demo-map-vnext-basic-visual-acceptance-addendum.md
python Tools/MapVNext/test_readable_delivery.py Artifacts/MapAcceptance/MapVNextStairs-r3d-readable-v2-basic-visual-20261029/Checks
python Tools/MapVNext/deliver_readable.py check Artifacts/MapAcceptance/MapVNextStairs-r3d-readable-v2-basic-visual-20261029
```

中文标题、操作和有来源的分组反馈来自 `readable_delivery.json`。它是展示元数据，读取既有案例与 P0 资产绑定，不拥有高程或内容版本；原业务 manifest、MapData 和角色行为不改。更换实际 Build/需求时另建修订与有依据的反馈记录，更新案例预算；本轮 60/12 不是未来任务的固定预算。生成器拒绝写入旧证据目录和无标记的其他非空目录；再次执行同一修订不会重复追加卡片。

HTML/CSS/JS 模板与 Markdown 共用同一份说明。检查涵盖中文字段、素材分类、分项人验、Build/Run、完整 Player 文件树、原媒体哈希、相对链接与回放帧；Python 测试只证明这些可检查的契约。实际阅读、PNG 显示、筛选与回放必须另在本机浏览器预览，保留截图与结果；不能称为用户觉得易懂。

长期规范唯一入口为 [地图可视化验收交付规范](../../Docs/MapGeneration/地图可视化验收交付规范.md)。本工具暂接入地图候选工作树；主项目只有规范与指令链接，工具尚未同步，主项目不得声称已执行不存在的相对命令。需要使用此工具时明确工作树并按上述命令运行；后续任务缺少检查入口应先补齐，不能静默跳过。

## 2026-10-29 基础视觉确认追加

当前报告修订 `r3d-readable-v2-basic-visual`，Player 仍为 r3d。原 v1 包与 ZIP 保留原字节；配置用路径与 SHA 绑定旧人工反馈及来源附件，生成器保留历史根字段，在 events 追加新确认，current 提供最新的分项汇总。同步时间不是用户试玩时间；不制造逐例人验。

用户确认中文材料可理解性、当前专项基本楼梯—高地连接与按高程配色视觉。人工操作无新增 PASS，随机布局本专项尚未完成交付/验收，P1 不关闭、P2 不启动。generation-scope.md 与首页由同一配置记录素材/基础连接/受控矩阵/随机生成/正式集成层次，以及实际 Fixture 调用与少量随机参数。工具不启动 Unity/Player。旧字体范围 FAIL 仍以历史原件链接保留。

<!-- P2-RANDOM-DELIVERY-20261029 -->
## P2 随机布局候选（2026-10-29）

P1 代表性操作已由用户确认，P2 独立授权有效；上方“P2 未启动”仅属旧报告日期。旧 v1/v2/r3d 原目录与生成路径保留。本轮默认随机入口为 `MapVNextRandomLayout.Generate → MapData → 同一 VNextProjection/StairGeometry/正式玩家`；不通过 Fixture 选整张地图。

所有实际 Unity/Player 依旧 `run_r3.py → unity_guard.run → 共享 Supervisor`。下面命令只作可复现入口，`--id` 每次用新名称；不重跑已经通过且输入未变的 Run：

```powershell
python Tools/MapVNext/run_r3.py p2prepare --id p2-prepare-example-next-01 --timeout 600
python Tools/MapVNext/run_r3.py p2play --id p2-contracts-example-next-01 --timeout 600
python Tools/MapVNext/run_r3.py p2build --id p2-build-example-next-01 --timeout 1200
python Tools/MapVNext/run_r3.py p2player --id p2-pilot-example-next-01 --batch-phase pilot --timeout 1500
python Tools/MapVNext/run_r3.py p2player --id p2-full-example-next-01 --batch-phase full --timeout 3600
python Tools/MapVNext/run_r3.py p2player --id p2-replay-example-next-01 --batch-phase determinism --timeout 180
```

先 8 基础连接与 3 小样；检查实际画面和物理后，才执行冻结的 24 Seed、首 12 成功图全部声明平台往返，再以 3 个独立新进程各重放预定 3 Seed。原始失败/拒绝分母保留。Build-rN 已存在 Player 时拒绝覆盖；改运行代码则使用新候选修订并重新 Build，不拿新源码配旧 EXE。

真实运行完成后，在 `Artifacts/MapAcceptance/MapVNextP2Random-20261029/release.json` 指定实际 Build 目录和经过核验的 pilot/full/contracts/replays Run ID；不得预填 PASS。原入口路由到 `deliver_p2.py` 读取实际 JSON/CSV/PNG，旧 generate/check 行为保持：

```powershell
python Tools/MapVNext/deliver_readable.py p2generate --source Artifacts/MapAcceptance/MapVNextP2Random-20261029 --output Artifacts/MapAcceptance/MapVNextP2Random-20261029/Delivery-r1
python Tools/MapVNext/deliver_readable.py p2check Artifacts/MapAcceptance/MapVNextP2Random-20261029/Delivery-r1
python Tools/MapVNext/deliver_readable.py p2portable Artifacts/MapAcceptance/MapVNextP2Random-20261029/Delivery-r1 --zip Artifacts/MapAcceptance/MapVNextP2Random-20261029-r1-Portable.zip --extract Artifacts/MapAcceptance/MapVNextP2Random-20261029/PortableCheck-r1
```

便携包检查使用新 ZIP/新解压目录，CRC、文件树、Player、图片解码与相对链接分别核验。浏览器真实打开/筛选/回放另留回执；使用者理解和人工审图仍由用户反馈。原始实际截图不编辑为示意图，辅助统计不冒充真实游戏画面。不加入商店 v3.2、字体改版、旧失败二进制或付费美术源文件。


## 南侧岩壁占位返修（R18）

当前修订实际Build/Run以新的 release-cliff-r7.json 为准；原 release.json/Delivery-r1 不覆盖。新的空间规则权威在根 RPG-Map-Generation.md R18。数据/输入/源码前态与精确差异在 Artifacts/MapAcceptance/MapVNextP2Random-20261029/CliffExclusion-r2/Authoring（目录名是本任务初始修订，不是最终Build版本）。

`run_r3.py p2player --batch-phase cliff --id <唯一名称>` 仍只适配原Guard/Supervisor，固定42/21193/7727/314159；单一当前完整Player执行90组墙边方向试验与32次独立探针请求。运行C/F2/O检查点、诊断与放置展示。移动实际采样保持真实输入→PlayerAction→Rigidbody2D，独立Python检查读取实际CSV/多边形，不据布尔字段自证。

`deliver_readable.py p2generate --source Artifacts/MapAcceptance/MapVNextP2Random-20261029 --release release-cliff-r7.json --output Artifacts/MapAcceptance/MapVNextP2Random-20261029/Preview-r7` 经原P2生成入口调用cliff_report；后者增加同镜头的明确旧碰撞诊断重现、原始r1源码红测、实测岩壁/放置、Build绑定的各对象占地合同示意、原24Seed新分母、全实体消费者与生产接入缺口。该入口只选实际成功Run，新Build人工仍NOT_RUN。相同命令正常再生成，不手改产物。

`p2check <目录>` 保留原Player/媒体/链接/旧回归，新增岩壁方向/样本、放置激活、源/Build绑定、实际轨迹与岩壁区域的独立距离检查；`p2portable` 用新ZIP和新解压路径验证全树。浏览器另按实际卡片/帧数检查比较图、占地示意、筛选、GIF与回放；`test_cliff_report.py` 为有限负例/独立几何单元测试，测试夹具不是游戏证据。`cliff_motion.py` 的岩壁容差.002，CSV数值舍入额外.000002，不改变游戏判据。

旧薄边界含.035预留净空，Box2D可在该缓冲带发生微小接触；与真正岩壁区域分开记录，不放宽岩面。绿色诊断点是.30占地查询的合法格心；可达批次使用.30+.01的导航净空。正式生态/池/恢复消费者仍NOT_INTEGRATED，主项目规则仅待审补丁；禁止默认切换或Git写。


## I1 正式流程集成（2026-10-30，当前隔离树）

I1 独立正式场景和新内容版本在 `D:/.codex/worktrees/map-vnext-integration-i1/RPG_Demo`。这是候选集成，不是 main 默认。Unity 入口为 `run_i1.py`，仍经原 Guard→Supervisor；精确受审源码组由 `seal_i1_host.py` 离线封存。未知字体变化保留前后原件及当前文件，Build 阻断；不会凭清缓存标志覆盖未知变化。

报告正常入口复用 `deliver_readable.py i1generate / i1check / i1portable`，转到 `deliver_i1.py`。`release-i1.json` 只能在实际 Run 完成后登记当前 Build 和 Run；`generate --source <I1证据根> --release <release-i1.json> --output <新交付目录>`；`i1check <目录>`；`i1portable <目录> --zip <新ZIP> --extract <新解压目录>`。

它保留全部固定24输入（拒绝不计可玩）、真实同Build原图及带输入/Generation的采样回放，独立Python距离测量沿用 cliff_motion 几何函数；不创建第二份运行时地形事实。完整Player文件树与来源SHA逐项绑定。`test_i1_delivery.py` 是说明/身份/缺失/未知混用的有限正反例，不能替代 Unity、实际 Player 或浏览器。生成后必须按正常入口再生成核对，实开离线页面、图片、筛选、回放，最后新ZIP/解压全树验证。

旧版本人工反馈不扩大为I1人工通过。共享原manifest严格1220项=1197原字节+23批准差异，兼容整组校验另列；实际K以最终封存回执为准。旧档使用副本，原件不读写，未授权迁移；旧内容几何与新版R18分开。历史失败不覆盖。最终交付后等待用户验收，MERGE_READY=NO，无Git写入。

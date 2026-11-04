# Play 重试报错与 Player 标签重复登记修复

普通 `SampleScene` 的“开始新冒险”可能先拒绝若干不合格地图候选，再成功生成地图。此前每次中间拒绝都立即输出 Error，导致一次成功的新冒险也在 Console 中留下红色记录。本修复只改变错误报告时机，并清理自定义标签列表中重复登记的内置 `Player` 标签。

## 最终行为

- 自动 VNext 随机操作沿原循环尝试最多 12 个输入。`VNextFormalLayoutRejected` 和 `EcologyBudgetExhaustedOnFixedTerrain` 的中间拒绝保留原诊断与 `VNEXT_RANDOM_RETRY` 普通日志；成功后不留下这些中间 Error。
- 12 个输入全部拒绝时，输出一条最终 Error，继续保持 `Failure`、非 `GameplayReady`。
- 显式 Seed、固定输入队列、手动 Retry、Restore、配置错误和异常保留原即时报告路径。
- `ProjectSettings/TagManager.asset` 只删除自定义 tags 下重复的 `Player` 一行。Unity 内置标签、对象上的 Player 标签、Damageable 和全部层/排序配置保留。

这不提高地图候选通过率，也不保证任意随机操作都成功。R01–R18、生成算法与参数、地图资源、配方及存档格式均无变化。

## 复用与权威

| 功能 | 旧实现 | 本次实现 | 关系 | 原因 |
|---|---|---|---|---|
| 地图候选与失败诊断 | 原生成器、`MapRuntimeContext` | 原样保留 | Direct Reuse | 继续拒绝不合格地图 |
| 随机重试与错误报告 | `TowerRunCoordinator`、`MapGenerationController` | 同一循环和预算，参数显式传递延迟策略，耗尽后报告 | Extension | 操作所有者判断最终失败，无新增共享开关或第二份状态 |
| 新旧地图兼容 | 原 Host 封存与内容身份 | 原工具生成本次精确源码组的 Host/Stamp | Extension | 源码与兼容性声明匹配，原内容 manifest 不变 |
| Player 标签 | 内置与自定义同名条目并存 | 复用内置标签，删除自定义重复项 | Direct Reuse | 无需重复声明内置标签 |

保留 `GenerateMap()`、单参数 `TryGenerateMap()` 与公开 `RetryFailedOperation()` 的原入口，没有替换数据权威或让两份生成器同时运行。

## 已有验证与证据边界

| 验证主体与范围 | 实际结果 | Run ID |
|---|---|---|
| 修改前报错反馈检查 | FAIL：仅应有一次故意指定坏 Seed 的 Error，实际有 3 条 | `5ba4a23322744d4d86b7476dae639b19` |
| 修复后五项 PlayMode | 5/5，普通按钮、用户五 Seed 序列、12 次耗尽、显式错误/配置早退及新旧存档恢复 | `36c80b31f5404658ab623b21210cb30a` |
| 独立重跑原反馈检查 | 1/1，额外的两条中间 Error 消失，显式坏 Seed 仍报告 | `75aed872500d4db4892cfa6a1d8ffb5a` |
| 既有隔离 Editor 标签审计 | 8/8 custom checks；内置 Player 条目为 1，赋值与 CompareTag 正常，原重复消息消失 | `198198eb8dc84b70a4f9af9eda8c583a` |

上述三个最终运行均自然退出 0、无强制回收、无自有残留，Job 清理及两把执行锁释放通过。PlayMode 使用影子 company/product 和私有测试槽位，退出后临时设置精确恢复；标签验证使用逐字节相同的修正配置，验证工作树原配置已恢复，未进入 Play。正式原档未读写。

用户原五 Seed 序列为 `-367763121 → -1644553391 → -93201598 → 887863686 → 666268681`。新测试经过实际生成和重试循环，检查最终 Seed、Ready 与保存重开一致。TestRunner 继续拒绝未预期 Error，没有动态吞掉随机候选 Error 的监听。

源码组 `map-play-error-fix-20261104-e02` 含 684 项受审输入；独立 Standards/Spec 审阅各 0 开放问题。旧内容 manifest 仍为 `c55fb3b4b0362e8bb59e84f4624fbb22c2f9ef96f3dcd5f61481a92dab9c9802`，原严格检查仍为 1196 项原字节与 24 项批准差异，无缺失；这不是修改原内容身份。

原始证据仅保留在本机忽略的 `Artifacts/MapAcceptance/PlayErrorFix-20261104/`、`Artifacts/PlayerTagFix-20261104/` 及 I1 `VersionedRuns/peg04`、`peg02`，不随 Git 分发。旧 `peg01` 的 240 秒 TIMEOUT、`peg03` 的 2/5（缺少历史副本夹具、测试误拒普通 Log）均保留，修正夹具/断言后使用新 Run 验证；没有把旧失败改标通过。跨进程运行依赖既有验收入口和私有旧档夹具，不能把缺失本地证据当作新克隆已运行通过。

通用 `assure.py` 测试汇总维持 PARTIAL：它的整工作树指纹包含临时影子设置，退出后这些字节恢复；标签审计也没有 NUnit XML。原始 XML、custom audit、自然退出和产品输入逐哈希核对分别记录，不声明当前整个 dirty 工程全部通过。

## 发布范围与人工复验

本次只发布两项已完成修复、对应测试、必要 Host/Stamp 和本说明及架构快照；不包含商店、字体字符表、储备怪物及其他未完成工作。架构图从干净的已提交源生成，不能把它视作其他本地 dirty 内容的验收。

这属于不改变地图行为的内部诊断/配置修复，未重建 Windows Player 或重新采集视觉媒体。旧 Player 仍代表旧版本。用户当前已打开的 Editor 缓存未在本轮重启验证；正常重开项目后读取修正配置，清空旧 Console 再检查新消息。本次人工验收没有新增 PASS。

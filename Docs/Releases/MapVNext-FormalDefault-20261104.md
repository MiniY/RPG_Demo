# 正式新版地图入口

用户 2026-11-04 明确批准新版地图取代 MainV1 作为正式普通新游戏默认。正式场景保持 SampleScene，普通按钮沿用 TowerRunCoordinator；场景直接注册已验收 VNext 配方和必要的既有出口预制体。地形算法、五色色序、楼梯模板、0.30 脚圆、岩壁禁入和实体预算沿用已验收实现。

旧版默认可从 GitHub 历史提交 998c337433c968d6db0abde2557c02cb2649edb7 及其父链查看。当前普通新冒险统一使用 VNext；存档恢复按记录身份解码，旧档内容不被改成 VNext。旧 schema 下一层与重试使用原基线。

普通随机新游戏及无固定输入的后续层有限复用原重试，最多 12 个输入，只对两个已知提交前规划拒绝执行。显式 Seed、固定换层输入和存档恢复沿用精确输入语义。原失败和所有重试日志保留。

验证和交付入口为本地 Artifacts/MapAcceptance/MapVNext-FormalDefault-20261104/START-HERE.md。本轮构建、真实运行和跨进程恢复已完成，实际结果见下表与该目录原始回执；本次 Build 人工视觉复验待用户完成。

## 本轮自动验证

| 检查 | 实际结果 | Run ID |
| --- | --- | --- |
| 实际主目录 Editor Play，普通新游戏按钮及保存重开 | 1 个用例通过，自然退出 0 | 78025bc620d44dadb025844bcf90336b |
| 候选完整产品逻辑的 Play 回归，严格 Seed、拒绝归属与旧档边界 | d02 的 3 个用例通过，自然退出 0；d03 仅补同代 World 诊断 | e1b86f85112a455f9039bf0c63e529b6 |
| d03 启动与有限整组兼容正反例 | 11 个用例通过，自然退出 0 | 634d22dff5f1493e80496f345103011c |
| d03 普通 DEFAULT Windows 构建 | 0 错误、2 警告，自然退出 0 | 47626380f8ab4896a046b0df0de2b5e6 |
| 普通按钮与实际 Seed 21193、8675309、7727 | 4 组进入新版，保存重开；真实楼梯输入和媒体 | 5a78d30541dc4ae8b55113baa7fbebd1 |
| 本次实际新版进度的新进程恢复 | GameplayReady、再次保存与重开，完整世界与上一进程逐字节一致 | 2a5e4d28ed8144a2aaae5c3fce5b128e |
| 原 MainV1 存档副本恢复 | GameplayReady、保存重开，完整世界逐字节等于原 c01 基准 | 621818438de84bbeb7abadab97dcfc2d |
| 普通构建请求专用 I1 能力的负例 | 早期拒绝，自然退出 2，原始 FAILED 保留 | 9adde97e3f984a71a7a972fcaa6d8d33 |

NUnit XML 中 asserts=0 按原值保留，表格统计用例数。所有最终自有 Job 清理通过、无残留、项目锁和全机锁释放。多 Seed Player 完成检查后曾在失焦状态等待，激活本次自有窗口后恢复退出，自然退出 0；该现象根因仍为 UNKNOWN，保留完整原时间线。没有据此改写旧导航或历史退出失败的根因。

两路独立代码审阅最终均 0 开放项。源码、场景和必要依赖绑定 d03 的 684 项整组 HostSeal。原 manifest SHA 仍为 c55fb3b4b0362e8bb59e84f4624fbb22c2f9ef96f3dcd5f61481a92dab9c9802，严格检查为 1196 项原字节一致、24 项受控差异、0 缺失；新增差异是正式 SampleScene，原始清单不重写。

## 复用关系

| 功能 | 旧实现 | 本次实现 | 关系 | 原因 |
| --- | --- | --- | --- | --- |
| 普通新游戏入口 | TowerRunCoordinator.BeginNewRun | 同一按钮与协调器默认选择 VNext | Extension | 使主项目普通开局使用已验收地图 |
| 精确配方注册 | ConfigureRetainedEcologyRecipes | 场景直接序列化引用，经既有方法校验 | Extension | 普通主场景可获得同一套已验收输入 |
| 地形与空间规则 | MapVNextFormalTerrain、MapSpatialContext 及原消费者 | 直接复用原算法、五色、楼梯、岩壁与 0.30 脚圆 | Direct Reuse | 已人工验收的地图行为继续使用 |
| 随机输入拒绝处理 | RetryFailedOperation | 有限调用同一重试，仅接受新产生的已知提交前拒绝 | Extension | 普通新游戏一次操作即可筛选合法地图 |
| 旧档恢复 | 原版本路由和兼容校验 | 按存档身份恢复，旧 schema 下一层保留原默认 | Intentional Compatibility | 保护已有进度，避免自动换身份 |
| 出口激活 | 已验收 ExitPortal_Inactive | 正式场景使用同一预制体与 finalizer | Direct Reuse | 复用物化前定位和激活规则 |

当前完整试玩包、真实媒体、输入轨迹和离线 HTML 位于本地 Artifacts/MapAcceptance/MapVNext-FormalDefault-20261104。该目录是生成交付物，源码仓库提供构建入口。新 Build 人工视觉复验为 PENDING_FOR_THIS_BUILD，原 a41/r3 人工通过结论保留原范围。

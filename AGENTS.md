# RPG_Demo 项目指令

## 随机地图现行规则入口

涉及随机地图生成、地形/高程、楼梯/坡道、噪声/Seed、水域、地图生态/实体布局或地图视觉（含 Sprite、Tilemap、Collider）的任务，开发前必须读取 [RPG-Map-Generation.md](RPG-Map-Generation.md) 并核对其引用的专项证据。任务完成后在同一轮按该文件的更新协议记录规则与算法参数、实际实现/实验/失败、测试和截图、Git/源码变化及待用户批准的决定；修改已确认规则须有用户批准并保留历史。

## Map Visual Acceptance Delivery Contract

凡修改随机地图、地形、地图生态 Population、地图对象碰撞或可达性、出生与安全区、出口与传送门、地图重建或小地图可见结果，交付前必须读取并执行 [地图可视化验收交付规范](Docs/MapGeneration/地图可视化验收交付规范.md)。该规范要求受影响测试、可运行 Windows MVP、真实运行截图、涉及动态表现时的录屏、可复现数据与离线验收报告。仅文档修改或不改变地图行为的内部整理，按规范中的简化分支验收。

继续任务先核对 `.codex/HANDOFF.md` 与当前 Git/进程状态，以当前状态为准。保留既有未提交工作及其他适用的 Skill 路由、`grill-me` 和 Git 安全指令。本规范不授权切分支、提交、推送、合并、覆盖正式存档或改动受保护的 `Assets/MapGeneration/MapGenerationSettings_Default.asset`。`/learn/` 与 `.codex/` 为本地状态，不纳入 Git 交付。

## 工程保障入口

开发、修复、重构或集成任务开始前，按 [工程保障手册](Docs/engineering-assurance/README.md) 的 L0–L3 分级执行相称的现有能力检索和任务前检查；跨模块或核心状态变更须核对权威数据所有者。完成时核对修改范围、原始测试证据与交接版本，分别报告自动验证、独立审阅和人工验收。L3 及手册列出的高风险动作须先给出证据、方案和恢复路径，取得真实人工授权后执行。已有全局 Skill 路由、架构查看器和 DataTest 各守原职责。

Codex 任务准备启动 Unity Editor、批处理测试、Build 或导入验证时，先按 [Unity 项目并发门禁](Docs/engineering-assurance/UNITY-CONCURRENCY-GUARD.md) 核对物理项目和进程占用，并通过现有工程保障目录的统一启动器申请执行权；无法确认占用身份时停止该次 Unity 操作并保留诊断证据。普通只读检查和不冲突的文件编辑不需要 Unity 执行权。

Codex 自动化启动 Unity（含地图验收、EditMode/PlayMode、Performance 和 Player Build）统一经 `unity_guard.py run` → 共享 `unity_supervisor.py`，DataTest 经其固定的共享适配器；执行前读取 [生命周期与 DataTest P0 收尾规范](Docs/engineering-assurance/DATATEST-V2-LIFECYCLE-P0.md)。执行后分别核验 XML、自然退出、Job 回收、自有残留、项目/全机锁及下一次准入，最终结果含 Run ID 和原始日志。纯快照走受控文件 Worker，测试退出超时保留 TIMEOUT；清理身份或结果 UNKNOWN 时阻断后续启动并保留证据。

## Reuse First：地图与玩法代码变更

开发前定位相关功能、类、方法及实际调用入口；优先复用或扩展现有实现。新增模块须说明现有实现为何不适用；架构正确性高于脚本数量。每个运行时事实须有明确数据权威；兼容并存的版本须通过显式路由选择。合理扩展现有功能可直接实施，替换核心架构或改变数据权威须先取得批准。

修改后核对重复调用、重复状态、旧路径有效性及 Unity 资源引用和存档兼容性，不凭“疑似冗余”删除旧实现。交付时附简表 `功能 | 旧实现 | 本次实现 | 关系 | 原因`，关系标为 Direct Reuse、Extension、Necessary New Module、Intentional Compatibility、Suspicious Duplication 或 Superseded / Dead Code；同时给出关键修改文件、Git Diff 和实际调用链证据，并说明疑似重复逻辑是否同次执行、是否争夺数据权威。

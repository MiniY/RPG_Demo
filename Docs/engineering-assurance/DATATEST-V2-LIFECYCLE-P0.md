# Unity 生命周期与 DataTest V2 P0

## 执行入口

所有可控 Codex 自动化 Unity 启动使用 `Tools/EngineeringAssurance/unity_guard.py run`，或 DataTest `Runner/shared_backend.py` 的 Unity 请求适配。两条路径最终进入同一个冻结的 `unity_supervisor.execute`。地图验收、测试、性能及构建执行方法不获得单独启动权限。手动打开的用户 Editor 和外部脚本不受此合作式入口的 OS 强制拦截；Agent 指令不是系统级拦截器。

DataTest 位于 `D:\UnityProjects\RPG_Demo_DataTest`。`Runner/datatest.py snapshot` 是纯文件操作：`snapshot_bridge.py` 只允许固定 Python、固定 `snapshot_worker.py`、精确请求路径和输入 SHA，使用共享 Supervisor 的现有公开命令策略接口；共享 CLI 继续只接受 Unity。Worker 的复制、Hash、Git 查询同步执行，其辅助进程继承同一个不可 breakaway 的 Job。没有第二套进程终止器。

快照保守使用真实源项目路径申请项目 Mutex 和全机重型 Mutex，并保留两次真实进程采样、锁内最终准入和调用者身份校验。因此源 Editor 正在运行或重型任务占用时，快照也可能 BLOCKED。不要将虚构的空项目或模拟准入用于真实快照。

## 完成与异常收尾

1. 运行器记录 Run/Snapshot、真实命令、PID/原生创建时间和调用者身份。Unity 子树在创建时原子加入共享 OS Job；Job 非继承，启用 KILL_ON_JOB_CLOSE。
2. 调用者等待共享 Supervisor 的最终结果；桌面/PowerShell 直接启动父进程的原生句柄被固定，父进程退出触发现有取消文件。调用者死亡由共享引擎的固定句柄检测处理。
3. 断言、根进程退出和 Job 清理分别判定。XML PASS 加退出超时仍是 TIMEOUT。Deadline 从进程恢复/LAUNCHED 后计算，包含启动、导入、测试和退出等待，绝不是 XML 完成后的独立 300 秒。
4. 超时/取消/中断由共享引擎只回收自己的 Job，等待原生根对象发信号、Job 活动列表为空，然后释放项目及全机锁。记录真实退出码、forced 和最终 Guard。
5. Supervisor 无最终结果时保留原始 `result.json=RUNNING`；若适配器此前固定了该根进程对象，则在其原生退出、已绑定的不可继承 Job 关闭契约、两把真实锁重新取得及真实 Guard 双采样全部通过后，另存 `supervisor-death-certificate.json`。外部结果为 `SUPERVISOR_DIED` 失败，明确 Job 对象销毁后活动列表不可查询，使用内核 KILL_ON_CLOSE 契约与根对象退出证据。缺少这些证据、清理 UNKNOWN/FAIL 或响应超时会写 `State/lifecycle-hold.json` 自动阻断后续请求，不能仅凭 PID、进程名、锁文件不存在或一次 Mutex 获取解除。历史原始结果保留。
6. `lifecycle-diagnostic.json` 单独记录测试、退出请求时间（未观测则 null）、自然退出、强制回收、残留、两把锁和 Guard。重复 XML PASS/退出 TIMEOUT 自动关联已有缺陷，默认不重试同条件实验。
7. 纯快照另记录 `snapshot-lifecycle.json`，仅在 Worker 真实退出、Job 清理通过、服务文件锁重新可取得、Manifest 完整发布并复验后报告 PASS。失败 Staging 可保留恢复，不等同仍有活动 Worker。

## 当前证据边界

2026-10-16/17 定向 A/B/C：无测试 `EditorApplication.Exit(0)` 可自然退出；最小 EditMode 和原 Smoke 断言通过但关闭 TIMEOUT。当前两份原生运行读栈显示主线程在 `CoreShutdown → ShutdownJobThreads → ujob_wait_all`，另一个后台 Job 在线程内 `curl_multi_perform → CurlExecutor::CurlExecute` 持续执行。具体 URL、所属服务及 Curl 不完成的底层原因尚未证明。相同 CDN 配置超时也见于历史自然退出成功样本，不能单凭该日志断定根因。

原生采样使用只读非侵入 `-pvr` 和 `qd`，不等同原子 dump 或 Windows Wait Chain Traversal。原始 XML、日志、命令、时间线和退出状态存 DataTest 每次 Run；汇总报告以 `State/latest-report.json` 为入口。自然退出尚未修复时允许受控 TIMEOUT，不得宣称完整测试通过。

P0 的最新真实验收以 DataTest `State/p0-acceptance.json` 和对应离线 HTML 为准；缺失或 NOT_RUN 即未验收。保留旧系统迁移备份、历史失败报告和旧同步 Disabled 状态。

## 本轮已验证和保留限制

- DataTest 43/43 回归、共享工程保障 74/74 回归通过。新适配器的真实 OS Job 隔离用例覆盖复制 Worker 正常完成、复制中调用者退出、启动器退出和 Supervisor 退出；早期失败及错误31的日志保留。
- 真实 Unity `p0-supervisor-hard-death-20261016T161835Z-61ba7b72cfb8` 在 READY 后只终止自有 Supervisor，固定的根对象及采样子树全部退出。原始 Supervisor 结果保留 RUNNING，外部结果 SUPERVISOR_DIED，独立证书 PASS，Guard FREE。即使内核回收的进程退出码为0，也不计自然退出。
- 其后连续两次生产快照 `capture-20261016T162307Z-6b62593711f2`、`capture-20261016T162348Z-7fc40fd52559` 均 PASS：调用者自然返回0、Worker/Job无活动残留、服务/项目/全机锁释放，后次实际准入成功。源3359个业务输入及 Index 不变；主仓仅有获授权的工程文档/指令更新。
- 真实不同项目 contender 被全机锁在启动前拒绝，没有第二个 Unity；同时运行两个真实 Editor 的场景未执行。外部活动进程身份不可读与不同项目识别继续由门禁隔离回归证明 fail closed。
- Supervisor 硬终止的真实验证位于 READY 阶段；本轮未在 CreateProcess API 内部瞬间注入死亡。共享 Supervisor 在创建 Unity Job 前的 Guard 只读查询辅助进程不在该后建 Job 内；在准入阶段强制终止可能留下短暂的查询辅助进程，该窗口未因本补丁宣称覆盖。
- DataTest 已提供聚合生命周期诊断及重复缺陷关联。`unity_guard.py run` 已统一进入同一 Supervisor 并保留原始生命周期报告，但主工程冻结入口尚未增加 DataTest 同格式的 XML 聚合/缺陷关联，属于未覆盖交付项。

因此当前 `DATATEST_V2_READY=PARTIAL`、`LIFECYCLE_PATCH_READY=PARTIAL`。常规受控超时/取消/中断已有自动回收证据，未知清理仍停止后续启动；不能把本次补丁宣称为所有异常窗口的 OS 级彻底治理。

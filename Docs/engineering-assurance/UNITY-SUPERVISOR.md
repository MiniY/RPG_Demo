# Unity Supervisor v1

共享执行器位于 `Tools/EngineeringAssurance/unity_supervisor.py`，复用 `unity_guard.py` 的项目身份、项目 Mutex 和准入。DataTest V2 任务拥有其 `Runner/` 适配与真实 Unity 验收；本工程保障任务拥有共享实现、Windows 非 Unity 回归及本文档。当前实现与测试进度以本文件后续的验收记录为准。

## 固定 JSON 接口

```powershell
python Tools/EngineeringAssurance/unity_supervisor.py identity --pid <调用者PID>
python Tools/EngineeringAssurance/unity_supervisor.py run --request <绝对路径.json> --result <绝对路径.json>
```

`identity` 成功返回 `pid`、`createdAtUtc`（带时区的 ISO 8601）、`creationTime100ns`（字符串）、`imagePath`、`state`。`caller` 使用其中 **`pid` 与 `createdAtUtc`**。无法打开或查询身份时退出 2，不得用枚举记录补造时间。API 依赖 Windows 10 / Server 2016 及以上的创建时 Job 绑定，不支持时拒绝启动。

request v1（必需字段如下；路径均为绝对路径）：

```json
{
  "schemaVersion": 1,
  "runId": "datatest-unique-id",
  "taskId": "datatest-smoke",
  "taskName": "DataTest Smoke",
  "operation": "EditMode",
  "projectPath": "D:/UnityProjects/RPG_Demo_DataTest/Sandbox",
  "command": ["D:/path/to/Unity.exe", "-batchmode", "-projectPath", "D:/UnityProjects/RPG_Demo_DataTest/Sandbox", "-runTests"],
  "timeoutSeconds": 1800,
  "cancelFile": "D:/path/to/run/cancel.request",
  "caller": {"pid": 1234, "createdAtUtc": "2026-10-16T00:00:00+00:00"},
  "stdoutPath": "D:/path/to/run/stdout.log",
  "stderrPath": "D:/path/to/run/stderr.log"
}
```

可选项：`environment` 为合并到继承环境的字符串字典；`cleanupTimeoutSeconds` 默认 30，范围 1–120；`treeExitGraceSeconds` 默认 2，范围 0–30；`snapshotEvidence` 为 `{ "snapshotId": "...", "manifestPath": "绝对路径", "sha256": "64位SHA256" }`，只核对并绑定该 manifest 文件，不代替 DataTest 的快照内容验证。旧 Guard 的 `--snapshot-manifest` 保留其原 `unity_project_snapshot.py` 格式，不要把 V2 格式直接传给旧参数。

result v1 的稳定消费字段：

- `schemaVersion`、`runId`、`taskId`、`projectPath`、`command`、`requestSha256`。后者是 Python `sha256(json.dumps(request, sort_keys=True).encode('utf-8'))`，即默认 JSON 编码参数的规范化内容哈希，并非原文件字节哈希。
- `launched`：是否已成功恢复根进程执行。创建挂起后若 Job 分配失败，仍为 false。
- `state`：`COMPLETED` / `BLOCKED` / `FAILED` / `TIMEOUT` / `CANCELLED` / `CALLER_EXITED` / `INTERRUPTED`。
- `reason`：具体原因；`exitCode` 为根进程 OS 退出码或 null；`naturalExit` 表示根进程在 Supervisor 未发起回收前已退出，不等于测试通过。
- `process`：根进程的 `pid`、`createdAtUtc`、`creationTime100ns`、`imagePath`；`caller`：已核验并持续持有句柄的调用者身份。
- `cleanup`：`status` 为 `PASS` / `FAIL` / `UNKNOWN` / `NOT_REQUIRED`；`forced` 为是否发起 owned Job 回收；`residualPids` 为该 Job 最后核对仍有的成员；`rootWaitResult`、`verifiedAtUtc` 记录原生等待结果与核验时间；`reason`、`errors` 保留失败。**只有 `cleanup.status=PASS` 才能声称本 Job 已无成员且根进程退出。**
- `admission`、`finalAdmission`、`postExitAdmission`：原 Guard 的准入原始结果；`locks`：全机与项目 Mutex 名、获取与释放记录。
- `stdoutPath`、`stderrPath`、`eventsPath`、`startedAtUtc`、`endedAtUtc`、`resultPath`。
- `snapshotEvidenceStatus` 独立记录 manifest 绑定（PASS / FAIL / NOT_REQUIRED）；退出后盘点失败记在 `postExitAdmission`。两者失败不会抹去已取得的 owned Job 清理证据，但会阻止整体成功。`evidenceErrors` 非空也不能成功。

CLI 只有 `state=COMPLETED && exitCode=0 && naturalExit=true && cleanup.status=PASS` 才退出 0，其他退出 2。XML/NUnit、测试数据、截图和快照内容完整性由 Runner 分别验收。超时/取消即便回收成功也不能改为 COMPLETED。result 已存在时拒绝重复运行；stdout、stderr 和事件日志也不能覆盖旧文件。强制终止 Supervisor 时最终 result 可能仍为 RUNNING，此时必须由调用方标记中断，不能引用初始记录为完成。

## 锁与生命周期边界

固定全机锁名为 `Global\RPG_Demo_UnityHeavy_v1`，按全机锁 → 物理项目锁的顺序获取，竞争时立即 BLOCKED。锁内执行双采样准入，并在紧邻进程创建前复查。通过后使用 `PROC_THREAD_ATTRIBUTE_JOB_LIST` 在 `CreateProcessW(CREATE_SUSPENDED)` 创建时就加入带 `KILL_ON_JOB_CLOSE` 的独立 Job；再用 `IsProcessInJob` 核验成员身份，才恢复主线程。这关闭了“创建后、单独 AssignProcessToJobObject 之前 Supervisor 死亡”的窗口。Job 句柄不继承，只允许标准日志句柄继承。Job 绑定或嵌套 Job 限制失败时不恢复，不回退为无 Job 启动。

Windows 支持范围和属性语义见 [Microsoft UpdateProcThreadAttribute](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-updateprocthreadattribute)，Job 子进程与关闭行为见 [Microsoft Job Objects](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects)。

调用者的原生句柄在创建时间核验后保持打开，循环检查其退出信号，避免 PID 复用。超时、cancel 文件出现、调用者退出或可捕获中断时只终止本 Job；等待 Job 无成员、核对根进程信号，再进行退出后准入复查并释放两把锁。Supervisor 非正常终止依赖 Job 关闭的内核回收；调用方必须独立核对残留，不能把没有最终报告当作 cleanup PASS。

有界清理若遭遇原生 API 错误或超时，结果保持 FAIL/UNKNOWN，不宣称回收完成；关闭 Job 是最后的内核回收尝试。此时两把已取得的 Mutex 不显式释放，`locks.*.quarantined=true`、`released=false`；它们保持在 Supervisor 持有线程生命周期内。CLI 失败退出时由 OS 销毁/标记 abandoned；嵌入式 Python 调用者必须停止后续执行，不能把线程结束带来的 Mutex 可获取误当作残留已清。调用方须保留报告，重新取得可靠进程证据。本实现无 force-unlock 命令。对手动 Editor、驱动／内核故障或同权限恶意进程不提供不可绕过保证。

原 `unity_guard.py run` 已成为上述执行器的兼容命令适配，不再保留独立 Popen 生命周期。其成功运行状态改用 v1 的 `COMPLETED`，退出码仍仅成功为 0；保留 `report`、`unityPid`、`unityCreatedAtUtc` 别名便于既有消费者迁移。未接入的新旧脚本副本不会自动升级。

这是已接入调用者之间的合作式协调。手动 Editor、直接 Unity.exe、旧版复制的启动器不受全机 Mutex 强制约束。未知外部 Unity 的 UNKNOWN 语义不改变。具有同等文件权限的 Agent 也能改写请求和报告，本工具不构成独立的人类授权边界。

## 验收记录

2026-10-16 定向回归 43/43 PASS（19 项新 Supervisor + 24 项原 Guard），退出 0，约 9.7 秒。完整工程保障回归 74/74 PASS，退出 0，约 48.2 秒。原始日志为 `Tools/EngineeringAssurance/.local/supervisor-targeted-freeze-20261016.log` 与 `supervisor-full-freeze-20261016.log`；冻结源码、测试与日志 SHA-256 见 `supervisor-v1-delivery-20261016.json`。新增测试使用真实 Windows Python 根进程、子进程与孙进程、真实 Job/Mutex/句柄；准入输入与 Unity 命令策略在 Python 测试接口用专用 fixture 代替，CLI 仍只接收 Unity.exe。没有把 Python fixture 称为真实 Unity。

覆盖自然 exit 0、自然非零退出、根进程退出后的子进程残留、timeout、cancel、调用者退出、Supervisor 被终止、同/不同项目竞争全机锁、原项目锁冲突、最后准入变化、创建时间不符、Job 绑定实际 API 错误、清理句柄失效后不提前放锁、原日志防覆盖、CLI 身份查询与非 Unity 命令拒绝、退出后双锁仍持有、快照 manifest 改变与退出盘点失败独立记录。原始逐项结果、标准输出、标准错误、事件和失败 API 错误码保存在 `.local/supervisor-v1-20261016/`，包含历史 RED 和 GREEN 子目录，不覆盖。

共享组件未启动真实 Unity。DataTest V2 任务负责独立 Sandbox 的真实 Smoke、Unity timeout/cancel/调用者中断、XML、自然退出与残留验收。在其结果到达前 `UNITY_INTEGRATION=NOT_RUN`，不恢复四怪物 Stage02。

21:33（Asia/Shanghai）顺序执行主项目 status 与 diagnose 均为 FREE、退出 0，进程集合为空且 UnityLockfile 为 ABSENT。原始输出为 `.local/supervisor-live-status-20261016.json`、`supervisor-live-diagnose-20261016.json` 及各自 `.exit.txt`。这与旧 PID 诊断属于不同时间点；未修改旧报告、未设置例外，也不能把本次预查询当作未来 Sandbox 的执行权。实际启动仍须获得两把锁和锁内最后准入。

| 功能 | 旧实现 | 本次实现 | 关系 | 原因 |
|---|---|---|---|---|
| 物理项目身份与准入 | unity_guard.canonical / ProjectMutex / admission | Supervisor 调用原实现 | Direct Reuse | 保留 UNKNOWN、三视图与原生生命周期语义 |
| run 命令 | Guard 内独立 Popen 等待 | Guard 仅生成 v1 请求并调用 Supervisor | Superseded / Dead Code | 去掉第二条进程生命周期路径 |
| 全机与 owned tree 管理 | 原来只有项目锁，无安全树回收 | Supervisor 的固定 heavy Mutex 与 Windows Job | Necessary New Module | 现有 Guard 缺少这些已获授权的职责；JSON 接口供所有调用者共用 |

## DataTest V2 真实 Unity 集成回读

截至 2026-10-16 21:51（Asia/Shanghai），DataTest 任务已在三个独立 Sandbox 中顺序完成真实 Unity 2022.3.62f3c1 运行。本工程保障任务随后只读核对三份原始 Supervisor result/request、Smoke NUnit XML、两个 READY 标记和冻结源码哈希，没有再次启动 Unity 或修改冻结源码。以下更新补充上方交付时的 NOT_RUN 历史状态：**`UNITY_INTEGRATION=PARTIAL`，`NATURAL_EXIT=FAIL`，不能宣布 Smoke 整体通过。**

- 来源快照：`snapshot-20261016T133605Z-14cf98b93ec0`。
- [真实验收离线页](D:/UnityProjects/RPG_Demo_DataTest/Runs/lifecycle-acceptance-20261016T135120Z-2d4bd12473d6/index.html) 与 [原始 summary.json](D:/UnityProjects/RPG_Demo_DataTest/Runs/lifecycle-acceptance-20261016T135120Z-2d4bd12473d6/summary.json)。summary SHA-256：`E30771D6813C9D8582A4B9B3C70A632CCFFD0B7613D9109810E3F6A919C3ADD0`。
- 本任务回读记录：`Tools/EngineeringAssurance/.local/supervisor-real-evidence-review-20261016.json`，包含逐项核对、原始 result/XML 路径与 SHA-256。

| 真实场景与 Run ID | 功能/触发证据 | Unity 退出 | owned Job 回收与锁 | 最终结论 |
|---|---|---|---|---|
| Smoke：`run-20261016T133649Z-c7e133cf50a2` | [EditMode.xml](D:/UnityProjects/RPG_Demo_DataTest/Runs/run-20261016T133649Z-c7e133cf50a2/EditMode.xml) 为 1/1 Passed、0 failed、1 个逐例 Passed；验收包 metrics 为 PASS | 300 秒期限后 TIMEOUT，`naturalExit=false`，根退出码 `3221225786` | [result.json](D:/UnityProjects/RPG_Demo_DataTest/Runs/run-20261016T133649Z-c7e133cf50a2/EditMode/result.json)：cleanup PASS、forced=true、rootWaitResult=0、residualPids=[]、postExitAdmission=FREE；两锁 released=true | XML 与探针数据 PASS；自然退出 FAIL；Smoke 整体 TIMEOUT |
| Cancel：`lifecycle-cancel-20261016T134322Z-f18d191bdfac` | `unity-ready.json` 的 PID、projectPath 与本次 owned 根进程及请求相符；READY 后发出取消 | CANCELLED，`naturalExit=false`，根退出码 `3221225786` | [result.json](D:/UnityProjects/RPG_Demo_DataTest/Runs/lifecycle-cancel-20261016T134322Z-f18d191bdfac/Supervisor/result.json)：cleanup PASS、forced=true、rootWaitResult=0、无残留、postExitAdmission=FREE、两锁释放 | 真实取消回收 PASS；不算 NUnit 或自然退出 PASS |
| 调用者中断：`lifecycle-interruption-20261016T134626Z-204c4b56716c` | READY 的 PID、projectPath 匹配；DataTest 任务只结束本次自有 Python caller，Supervisor 持续观察原句柄 | CALLER_EXITED，`naturalExit=false`，根退出码 `3221225786` | [result.json](D:/UnityProjects/RPG_Demo_DataTest/Runs/lifecycle-interruption-20261016T134626Z-204c4b56716c/Supervisor/result.json)：cleanup PASS、forced=true、rootWaitResult=0、无残留、postExitAdmission=FREE、两锁释放 | 真实 caller 中断回收 PASS；不等于 Supervisor 被强制终止的 Unity 实测 |

这些通过证明真实 Unity 的 timeout/cancel/caller-exit 回收已取得证据，不能补造自然退出。**`REAL_SUPERVISOR_HARD_DEATH=NOT_RUN`（真实 Unity）；此前只有隔离 Python 进程的实际测试 PASS。** Player、视觉玩法和人工验收仍未执行或未确认。Guard 的 owner 元数据可能是释放锁之前的快照，锁释放证据以上述本次运行的最终 `Supervisor/result.json` 为准，不从旧 owner 字段推断。

Smoke 原始日志出现 Unity China 配置请求超时、`Leftover web requests after shut down`，目前仅为线索，**退出等待根因 UNKNOWN**。故障已登记为 [BUG-20261016T134541Z-2a6c8aa93efd](D:/UnityProjects/RPG_Demo_DataTest/Defects/BUG-20261016T134541Z-2a6c8aa93efd/defect.md)。下一步是在独立 Sandbox 定向诊断原生退出等待，不改测试断言、不忽略超时、不扩大结束进程范围。

验收包在 21:51 记录主项目与最后 Sandbox Guard 均 FREE；主项目 3359 个业务文件比较、分支、HEAD、Index 均未改变，迁移备份保留且自动同步未恢复。这些是 DataTest 验收包在其记录时刻的结果，本次文档回读没有重跑整个 3359 文件清单或再次申请 Unity 执行权。旧架构门禁仍为 `BLOCKED_STALE_BASELINE`，不因生命周期回收通过改为 PASS。本次不恢复四怪物 Stage02。

## 自然退出缺陷定向调查

[BUG-20261016T134541Z-2a6c8aa93efd 调查记录](BUG-20261016T134541Z-2a6c8aa93efd.md) 保存原始 Smoke、历史成功对照和本轮独立 Sandbox 实验。共享四文件与 DataTest 部署 Hash 保持冻结；本轮由 DataTest 独占 Unity，工程保障侧回读证据，不启动主项目、不修改业务、不恢复 Stage02。

2026-10-16 23:45 的直接 Exit 用例 `exit-diagnostic-exit-20261016T154222Z-3f15ea69248f` 已实际自然退出：退出请求约 1.33 秒后原生句柄发信号，COMPLETED、exit 0、naturalExit=true、forced=false、Job 无残留、两锁释放、独立 Guard=FREE。NUnit NOT_RUN，无调试器采样。这个成功只证明该用例的生命周期完成，原 Smoke 缺陷仍 OPEN，不能据此改报 Smoke PASS。

已实际核对历史自然成功日志也含相同 Unity China CDN timeout；该日志本身不能证明根因。历史原生栈的 `CoreShutdown → ShutdownJobThreads → ujob_wait_all` 与 Curl worker 只是旧运行的直接等待证据，仍需本轮失败现场确认。

2026-10-17 00:06 更新：DataTest 在旧固定部署下完成另外两项有界对照。B 最小同步 EditMode 测试为 XML 1/1 PASS，但 TIMEOUT；C 原 Smoke 为 XML 1/1 PASS、metrics 完整，但仍 TIMEOUT。C 成功采集两份当前非暂停原生栈：主线程持续在 `EditorApplication_CUSTOM_Exit → CoreShutdown → ShutdownJobThreads → ujob_wait_all`，同一 Background Job.Worker 2 持续在 `CurlExecutor::CurlExecute → curl_multi_perform`，两线程用户 CPU 在两次采样间各增长约 6.4 秒。**直接原生关闭阻塞位置已确认；具体 Curl 请求归属及触发不完成的原因仍 UNRESOLVED，未证明安全修复。**

B/C 均在原 300 秒期限后才由自有 Job 回收，naturalExit=false、exitCode=3221225786、cleanup PASS、rootWaitResult=0、无自有活动残留、两锁释放、postExitAdmission=FREE；自然退出均 FAIL。B 观察器曾因读取未写完 XML 而 ParseError，补采又遇 CDB 参数组合不兼容；原始失败保留，修正临时采集器并通过非 Unity 夹具后才执行 C。没有修改冻结 Supervisor 作为“退出修复”。详细命令、原生栈、局限与逐文件 hash 入口见上方缺陷调查记录。本聊天停止新增 Unity 复现，Stage02 保持未运行。

### 2026-10-17 01:13 对象级续查

用户另行授权继续 Curl 请求对象与取消/完成时序调查。DataTest 独占同一固定 Snapshot 的两个新 Sandbox，复用 A/B/C 和双份原生栈，未重复对照、未改变冻结四文件、未改业务或网络/服务。两个新 Run 的 XML 均 1/1 PASS，但自然退出仍 FAIL / TIMEOUT；均到原 300 秒期限后才回收自有 Job，cleanup PASS、forced=true、rootWaitResult=0、无自有残留、两锁释放、postExitAdmission=FREE。

第一项 `curl-request-observation-20261016T164526Z-d31a905bcaea` 的 20 秒 CDB 完整采集 FAILED；已完成的指令片段提供字段证据，全局恢复读成功，迟到字段命令 NOT_RUN。第二项 `curl-object-timeline-20261016T170519Z-b8cebeb1b12e` 按预置三个阶段取得六帧只读样本：XML 后约 1 秒，Unity active=2、libcurl running_handles=1；约 10 秒，active=1、running_handles=0、executor running=1、abort-all=0。实际 CurlExecute 指令允许 active 非空时继续循环，因此等待不再能简单归结为“网络仍在传输”。

持续槽位未通过预期 vtable 校验，实际类型/所有者仍 UNKNOWN；另一槽位仅建立 T-B→easy/父 request 的关联，没有 URL 或精确取消/完成事件。不能据此认定 use-after-free、回调必然丢失或某个 CDN 服务负责。补充类型读请求晚于预定安全截止，NOT_RUN；不放宽截止、不追读旧 PID、不再启动第三个 Unity。

**直接等待机制已确认，深层根因 PARTIAL，修复 NOT_IMPLEMENTED，Unity 自然退出 FAIL，自有回收 PASS。** 下一项有价值的修复前证据是剩余槽位的实际类型与已有所有者 teardown/active 移除/完成交接顺序。详细六帧时间表、原始证据索引、临时诊断器 12/12 与 DataTest 4/4、6/6 回归及有前置条件的最小修复提案，均在上方缺陷调查记录。单元测试与回收通过不代替 Unity 自然退出；Stage02 继续 NOT_RUN。

### 2026-10-17 10:49 T-A 定向续查结果

用户另行授权的一次 `curl-vptr-final-20261017T024048Z-2e0d38389650` 已结束。启动前 seal 22 输入及完整计划，独占原 Snapshot 的新 Sandbox；每 10 秒与 XML 后固定阶段共 29 阶段、58/58 帧，总读量 31,706 bytes / 823 次，launch+270 停止读取。无追加 live 命令、无 CDB/断点/注入，四共享文件继续冻结。

**本次持续项已精确识别**：transport vptr=`Unity+0x3addda8` 为 TransportCurl，parent+0x88 的 vptr=`Unity+0x3adf160` 为 UnityWebRequest，parent+0x18 回指该 transport。RTTI 仍 UNKNOWN，具体发起 Editor 服务未知；不能反填上轮历史 T-A 缺失的 vptr，也不能据析构静态代码认定本次对象已删除。

**新增直接近因证据**：XML+1 的 active=2 实际是两个槽位指向同一个 transport 地址、同一父请求。工程保障与 DataTest 分别解包两帧原始数组复核，字节 SHA 一致。后续 active 变为1、同地址 easy 已 null、libcurl running=0，但 executor running=1，持续至最后预定样本。旧同 build 指令允许 add_handle 返回0/7都追加 active，正常完成只移除一份，再 FinishDoRequest；这提供具体因果解释，但本 Run 未逐调用捕获返回7或重复submit来源。

最小修复落点是现有 native 入队/active 成员资格契约，保证同一 in-flight generation 的 transport/easy 不重复登记、完成/取消恰好一次，保留合法后续 retry/redirect。尚未实施引擎补丁，不修改主业务或 Supervisor，不新增退出管理器。

NUnit 1/1 PASS（0.3772005秒），自然退出 **FAIL**。原300秒期限在10:48:48.590360到达，随后自有Job回收 **PASS**（forced=true、rootWaitResult=0、无自有残留），10:48:51.242674最终 TIMEOUT，两锁释放、postExitAdmission FREE。包装工具宿主exit1与源码TIMEOUT映射2不一致，原始shell退出值缺失，单独保留UNKNOWN；不以报告存在代替正确包装退出。本轮不再Unity，Stage02保持NOT_RUN。根因总体PARTIAL，直接重复槽位状态CONFIRMED；完整证据、修复条件和未观测事件见缺陷调查记录文末。

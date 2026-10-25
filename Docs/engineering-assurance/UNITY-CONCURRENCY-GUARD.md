# Unity 项目并发安全门禁（Phase 2.7）

## 当前可用范围

`Tools/EngineeringAssurance/unity_guard.py` 为**已接入的调用者**提供同一物理 Unity 项目的合作式独占执行权。`run` 现复用单一 [Unity Supervisor](UNITY-SUPERVISOR.md)：固定全机重型 Mutex 加原项目 Mutex、原有准入和 owned Job 生命周期。`owner.json`、事件历史和 HTML 面板只供诊断。手动 Editor、直接 Unity.exe 和未升级的脚本副本仍可绕过合作锁。DataTest V2 的 Runner 适配、原址迁移和真实 Sandbox 验收由 DataTest 任务负责；本共享组件任务不启动 Unity，真实 Unity 集成尚未验证。

**后续真实集成更新（2026-10-16 21:51，Asia/Shanghai）：**DataTest 任务已交付三个独立 Sandbox 的真实结果，详见 [Supervisor 真实集成回读](UNITY-SUPERVISOR.md#datatest-v2-真实-unity-集成回读)。timeout、cancel、caller-exit 的 owned Job 回收均 PASS，无残留且两锁释放；Smoke XML 1/1 与探针数据 PASS，但 300 秒后仍未自然退出，整体 TIMEOUT。因此当前 `UNITY_INTEGRATION=PARTIAL`、`NATURAL_EXIT=FAIL`，并非完整集成 PASS。真实 Unity 的 Supervisor 强制终止实验仍 NOT_RUN，退出等待根因 UNKNOWN，架构旧 BLOCKED 保留；本次只追加文档，不改变冻结源码或启动 Unity。

项目路径先解析符号链接/Junction、规范大小写，再计算锁名。同一个物理目录的不同拼写使用同一锁；不同 Git worktree 是不同物理项目，拥有不同锁。门禁元数据保存在 `Tools/EngineeringAssurance/.local/unity-guard/`，不应提交；如果从另一个工作树里的脚本副本运行，Mutex 仍共享，但该副本的本地元数据未必能显示原任务名称。

### 进程生命周期判定（2026-10-15 修复）

门禁在持有项目 Mutex 时连续采集两次 CIM、Get-Process 与原生 Toolhelp PID 视图，并对并集中的每个 PID 用 `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | SYNCHRONIZE)` 取得同一进程对象的句柄。`GetProcessTimes` 给出该对象的创建时间；`WaitForSingleObject(handle, 0)` 的信号状态决定是否退出；`GetExitCodeProcess` 仅保留诊断值，退出码 `259` 不能单独证明仍在运行。仍活动的实例还须核对原生映像名、两份创建时间、CIM 命令行与 `-projectPath`。句柄和快照之间的 PID 重用、两次采样的异常变化、权限拒绝或原生 API 错误都保持阻断。

| 内部诊断状态 | 证据和外部准入 |
|---|---|
| `LIVE_CONFIRMED` | 原生句柄未发信号且身份完整；目标项目匹配时 `BUSY_EXTERNAL`，其他项目可继续检查 |
| `EXITED_CONFIRMED` | 同一原生句柄已发信号、创建时间一致；可忽略该 PID 的残留 CIM 记录 |
| `LIVE_UNIDENTIFIED` | 句柄确认活动，但命令行、映像或视图不足；`UNKNOWN` |
| `PROCESS_STATE_UNKNOWN` | 打不开句柄、时间不符、疑似 PID 重用或 API 失败；`UNKNOWN` |
| `PROJECT_FREE_VERIFIED` | 两次完整采样无未解释进程、UnityLockfile 未被占用，且项目 Mutex 已由查询或运行器取得；对外 `FREE` |

零线程、零句柄、单次空列表、UnityLockfile 缺失、曾经能拿到 Mutex 或旧 `owner.json` 均不足以证明空闲。`status/diagnose` 只在查询期间持锁；`run` 在**同一把锁内**完成初检、紧邻启动前的末次复检、Unity 子进程等待和退出复核，直至结束才释放。未接入 Mutex 的手动 Editor/旧脚本仍可在末次复检后竞速启动，属于已知剩余风险，不能用 `FREE` 宣称全项目强制独占。

## 已知启动入口和接入状态

| 入口 | 调用链与当前保护 | 本轮接入 |
|---|---|---|
| Codex 显式批处理 | `unity_guard.py run` → 单一 Supervisor → 全机与项目 Mutex → 原准入及最终复查 → 创建时 Job 绑定的挂起进程 → 恢复 → 等待/回收/退出复核 | 隔离 Windows 进程测试；真实 Unity 未执行 |
| DataTest V2 手动 | Runner → v1 JSON 适配 → 同一 Supervisor；物理项目为独立 Sandbox | Runner 适配与真实集成由 DataTest 任务验收，不由本任务代填 PASS |
| 旧 DataTest 自动同步 | 历史 `Run-DataTestHidden.py → Sync-DataTest.ps1 → Invoke-DataTest.ps1` | 用户已批准废弃，停用、备份和原址迁移证据由 DataTest V2 任务维护；不继续修旧脚本 |
| 架构审计 Editor 工具 | `Assets/Editor/ArchitectureAudit/ArchitectureReferenceAudit.cs` 在已启动 Editor 内执行 | 启动它的 Editor 必须由持有者管理；工具本身不启动另一个进程 |
| 地图验收/Build | `Assets/Editor/MapAcceptance/MapAcceptanceBuild.cs` 是 Editor 内入口；外部脚本调用方式需逐一审计 | 尚未证明所有外部启动方接入 |
| 手动 Editor 与旧脚本 | 不经过合作锁 | 依赖独立进程和 UnityLockfile 证据；启动竞态仍可能存在 |

全机 Mutex 只协调已升级的 Supervisor 调用者；同一或不同 Sandbox 均不得并发启动第二个受控重型任务。项目锁仍按物理路径区分。地图 Build、外部旧脚本与 DataTest V2 尚须分别提供真实集成证据，不能宣称所有入口已强制接入。

## 使用

普通只读工作无需锁。需要启动 Unity 的 Codex 任务：

```powershell
python Tools/EngineeringAssurance/unity_guard.py status --project D:\UnityProjects\RPG_Demo
python Tools/EngineeringAssurance/unity_guard.py diagnose --project D:\UnityProjects\RPG_Demo
python Tools/EngineeringAssurance/unity_guard.py dashboard --project D:\UnityProjects\RPG_Demo
```

只有 `status` 返回 `FREE`、相关入口占用已核实且任务本身获准时才考虑运行；`run` 会**再次**拿锁并在锁内核查，所以预查询不是执行权。`dashboard` 输出可离线打开的 HTML 路径，近黑底、白字、紫色强调。页面是生成时刻的状态；再次运行命令才能刷新。`UNKNOWN`、`OWNED`、`BUSY_EXTERNAL` 均停止启动，并保存原始诊断 JSON。

定向 Unity 测试优先先做字节完整的隔离副本；与只供静态扫描的 `Tools/ArchitectureAudit/snapshot.py` 分工明确：

```powershell
python Tools/EngineeringAssurance/unity_project_snapshot.py create --repo D:\UnityProjects\RPG_Demo
python Tools/EngineeringAssurance/unity_project_snapshot.py verify --snapshot <上一步输出的目录>
python Tools/EngineeringAssurance/unity_guard.py run --project <副本的project目录> --task <唯一任务ID> --task-name <任务名称> --operation EditMode --snapshot-manifest <副本的manifest.json> --timeout-seconds 1800 -- <Unity.exe绝对路径> -batchmode -projectPath <副本的project目录> -runTests -testPlatform EditMode -testFilter <测试类> -testResults <XML绝对路径> -logFile <日志绝对路径>
```

副本包含 `Assets`、`Packages`、`ProjectSettings` 的每一个实际文件（包括 `.meta` 和未跟踪文件），不复制 `Library`。manifest 记录 Snapshot ID、来源分支/HEAD、源脏文件、未跟踪文件、每个文件 SHA-256、Unity 版本及时间；`verify` 分别报告副本有效性和实时工作树新鲜度。Unity 运行需另外检查 NUnit、日志完整性、编译、自然退出、回读状态；仅副本通过不能证明玩法通过。新的 `run` 由 Supervisor 回收本次 owned Job；自然退出、强制回收和残留分别记录，超时即便回收成功仍为 TIMEOUT。

## 交接、阻断与恢复

执行权由持锁的启动器持有至命令结束及退出复核。下个任务即使看到前任务的 `RELEASED` 记录，也须重新调用 `status`/`run`；记录不是锁。`OWNED` 时可看任务 ID 与操作；`BUSY_EXTERNAL` 表示外部进程或被占用的 UnityLockfile；`UNKNOWN` 表示身份或查询不完整；`BLOCKED`/`FAILED` 的具体原因在 `.local/unity-guard/<项目键>/runs/*.json` 与 `events.jsonl`。不要删除锁文件接管、清空 Library 或按 PID 猜测并终止进程。

卡死时先记录面板、三种进程视图、原生句柄状态及创建时间、命令行、UnityLockfile、Editor/DataTest 日志及对应任务。`OpenProcess` 拒绝访问时，即使 CIM 显示零线程/零句柄，也保持 `UNKNOWN`；由持有者或用户在具有适当权限的会话中核实身份。Supervisor 只按自己创建并绑定的 Job/原生句柄回收，不按未知 PID 终止进程；异常死亡由非继承 Job 的 KILL_ON_JOB_CLOSE 提供内核回收，仍需调用方核验残留。PID 可复用，旧 `owner.json` 不代表当前身份。禁止嵌套申请等待自己释放；当前实现直接拒绝，不自动排队。

文件编辑冲突属于 `assure.py` 的任务基线和授权范围审计，Unity Mutex 不锁普通 `.cs`/Scene/Prefab 修改，也不能认定改动作者。真实人类批准仍通过聊天/外部信任边界取得；本地 JSON 可由同权限进程改写，不能替代批准。

## 2026-10-15 历史验证边界

模拟测试覆盖抢锁、重复申请、正常与异常退出、超时、残留、损坏元数据、未知外部进程、错误项目路径和面板。现场 `UnityLockfile` 曾为 `HELD`，稍后一次查询为 `ABSENT`，说明单独看锁文件会误判。沙箱内 CIM 查询受限；沙箱外先读到两个命令行不可见的 Unity PID，后又看到 PID 37220 的命令行明确指向主项目 `D:\UnityProjects\RPG_Demo` 的 PlayMode 测试。人工检查可确认外部占用；守门工具因另外两个进程身份不全报告 `UNKNOWN` 并阻断启动，此差别应在诊断中保留。本任务不启动或终止这些进程。DataTest 登录自动任务和旧脚本仍是未接入入口，不能把此模块宣传为全项目强制锁。

末次真实状态原文在 `Tools/EngineeringAssurance/.local/phase27-guard-live-status-escalated.json`；它记录 `state=UNKNOWN`、三份进程证据和 `unityLock=HELD`。此文件是生成时刻的本地证据，后续任务必须重新查询。

### 本次修复后的现场状态

专项隔离回归为 **19/19，通过**，含实际 Windows 活动 Python 进程与“已退出但父进程仍持有句柄”的原生句柄验证；日志在 `Tools/EngineeringAssurance/.local/unity-guard-native-final-20261015.log`。工程保障全套 Python 回归为 **50/50，通过**，日志在同目录的 `unity-guard-native-regression-20261015.log`。L3 preflight 已保存任务基线，但当前全图架构快照过期，`architecture=BLOCKED`；这些结果不能提升为完整项目集成通过。未启动生产 Unity。

PID 36408 的 `WaitForSingleObject` 返回已发信号，原生创建时间与 CIM/Get-Process 一致，诊断为 `EXITED_CONFIRMED`（记录的退出码是 259，不能反向解释为仍活动）。PID 26772 的 `OpenProcess` 返回错误 5，命令行和映像路径仍不可读，诊断为 `PROCESS_STATE_UNKNOWN`。两者的零线程/零句柄值不作为判定依据。当前主项目 `diagnose` 仍为 `UNKNOWN`，`UnityLockfile=ABSENT`；完整原始 JSON 在 `Tools/EngineeringAssurance/.local/unity-guard-native-live-diagnose-elevated-20261015.json`。**`UNITY_INTEGRATION=BLOCKED`，不得恢复 Stage 1.1 的 Unity 操作**。

下一步由持有者/用户在有权限的 Windows 会话中只读核验 PID 26772 的原生创建时间、是否发信号、映像和完整项目路径；如果无法取得可靠证据，继续保持 `UNKNOWN`。核验后重新运行上方 `diagnose`，保存完整 JSON；只有它返回 `FREE` 且 `run` 在持有真实 Mutex 的末次准入也返回 `FREE`，才可由地形任务按既有授权范围启动 Unity。DataTest 和其他未接入入口的并发风险仍需单独处理。本次未执行真实 Unity 集成，不能用隔离进程回归替代。

2026-10-16 专项复核、原始日志和当前限制见 `UNITY-CONCURRENCY-PID-2026-10-16.md`。`OpenProcess` 对 26772 的所有测试权限仍返回错误 5，因此当前 `UNKNOWN` 是有证据的阻断；对 36408 的已发信号句柄不再当作活动进程。历史 PID 在原生 API 报 87 且 Toolhelp 快照也缺失时，可标记 `NOT_FOUND_CONFIRMED`；访问拒绝不能走此路径。DataTest 与主项目的治理脚本仍须进入各自 Git 版本并完成真实集成验证。

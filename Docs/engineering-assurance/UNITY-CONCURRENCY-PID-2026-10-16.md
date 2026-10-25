# Unity Project Concurrency Guard：PID 误判专项诊断（2026-10-16）

## 结论与边界

本轮只修改现有门禁、其回归测试和 DataTest 的 Unity 启动点；没有启动或终止 Unity，没有操作四怪物 Stage02、地图系统、Scene、Prefab、Library 或计划任务。主项目当前 `GUARD_STATE=UNKNOWN`，`READY_FOR_STAGE02_RESUME=NO`。主因是 PID 26772 在当前中等完整性 Windows 令牌下无法取得进程句柄；实际存活状态和项目路径均未证实。PID 36408 则已由原生句柄的发信号状态确认为终止，不构成活动占用。

回归测试后的最终 `status` 和 `diagnose` 原始 JSON 分别位于 `Tools/EngineeringAssurance/.local/unity-guard-status-posttest-20261016.json`、`unity-guard-diagnose-posttest-20261016.json`，独立权限探针为 `pid-rights-20261016.json`；对应 `.exit.txt` 记录 Python 进程退出码。三次输出均是采样时刻的证据，不能证明之后的工作树或进程仍相同。

## 两个 PID 的现场证据

| PID | CIM / Get-Process / Toolhelp | OpenProcess 与 Wait | 创建时间、映像、命令行 | 结论 |
|---|---|---|---|---|
| 36408 | CIM、Toolhelp 仍列出旧 `Unity.exe` 记录；CIM 创建时间 `2026-10-13 12:57:18 UTC` | `0x101000` 和单独 `SYNCHRONIZE` 均能打开；`WaitForSingleObject=0`；`GetExitCodeProcess=259` | 原生创建时间与 CIM 一致；已终止对象的映像查询失败，命令行不可得 | `EXITED_CONFIRMED`。退出码 259 不能推翻句柄发信号的事实 |
| 26772 | 三个视图均列出 `Unity.exe`；CIM 创建时间 `2026-10-14 15:21:56 UTC`、父 PID 40636、Session 1、零线程/零句柄 | 组合查询+等待、单独等待、单独 limited query、普通 query 权限的 `OpenProcess` 均失败，实际错误码 **5**；未得到可等待句柄 | 原生创建时间、可执行文件路径、命令行均不可得；CIM 创建时间不能独立证明当前 PID 身份 | `PROCESS_STATE_UNKNOWN`。不能证明存活、终止或目标项目归属 |

`tasklist` 对两者仍显示 `Unity.exe`，其中 26772 的用户栏为 `N/A`。这只表明枚举记录仍可见；同一枚举也显示了已由句柄证实终止的 36408，因此不能用它单独推断 26772 存活。当前令牌为 Medium Mandatory Level，Administrators 组仅 `deny only`；这与访问拒绝相容，但不证明进程的具体保护机制。没有提升权限、启用调试权限或绕过 DACL。

## 判定修复

原门禁已使用 `OpenProcess`、`GetProcessTimes`、`QueryFullProcessImageNameW`、`WaitForSingleObject`、`GetExitCodeProcess` 和双轮 CIM / Get-Process / Toolhelp 采样；旧版对于 36408 已正确忽略终止对象，对于 26772 已正确保留 `UNKNOWN`。本轮修复的是仍可能造成长期误阻断的两个边界：

1. `OpenProcess` 返回错误 87 且同轮 Toolhelp 没有该 PID，并在双轮采样中仍一致时，分类为 `NOT_FOUND_CONFIRMED`。仅有旧 CIM/Get-Process 记录不再永久阻断；错误 5、快照中仍有 PID、两轮身份变化均不会走此路径。
2. 当查询权限不足但 `SYNCHRONIZE` 句柄已取得时，先保留 `WaitForSingleObject` 的结果。已发信号的进程对象可归类为 `EXITED_HANDLE_ONLY`；未发信号且身份不完整仍阻断。原生探针现在保留各次 `OpenProcess` 的请求权限和实际错误码。

`run` 的原有流程仍是在同一项目 Mutex 内执行初次准入、紧邻 `Popen` 的末次准入、等待退出及退出后检查。本轮没有添加强制解锁、超时放行或跳过查询。`status=FREE` 只是采样时刻的判断，真正启动必须重新走 `run` 或等价的锁内准入。没有统一入口的手动 Editor 仍可能在末次复查后竞速启动。

## DataTest 接入

登录任务的实际命令保持为 `D:\develop\Python-3.11.9\pythonw.exe "D:\UnityProjects\RPG_Demo_DataTest\Tools\DataTest\Run-DataTestHidden.py" --mode AUTO`。其链路仍是 `Run-DataTestHidden.py` → `Sync-DataTest.ps1 -AutoSmoke` → 有更新时 `Invoke-DataTest.ps1 -Profile Smoke -SkipSync` → `Process.Start(Unity.exe)`。

`Invoke-DataTest.ps1` 现在在同步完成后，从主项目现有 `unity_guard.py mutex-name` 获取**DataTest 物理项目**的同一 Windows Mutex 名，持有至所有 Unity 测试与报告完成；调用 `admit-under-lock` 作初次、每个 Unity 启动前和退出后的检查。准入阻断时写入 `unity-guard-*.json`；Mutex 已被占用时在摘要中记录原因，不会绕过门禁启动 Unity。摘要还标注 `unityGuardAdmission`。原有 DataTest 同步互斥锁、`run.lock`、NUnit/XML/资源限制/超时/退出判定保留。旧 `-AllowStaleUnityPid` 的零句柄例外已改为明确阻断，因为它没有进程生存期证明。

接入代码目前位于未提交的 DataTest 工作树改动。DataTest 现有“运行前必须洁净”门禁会阻止它在此状态下启动测试；没有为方便验证而放宽该规则。计划任务定义未修改，真实登录触发和真实 Unity 测试均 **NOT_RUN**。`admit-under-lock` 是供已持有 Mutex 的内部调用，单独调用不能证明执行权；它会拒绝没有外部持有者的情况。

主项目和 DataTest 是不同物理 Unity 项目，Mutex 键不同；本机制保证各自项目内的合作式独占，不保证两项目之间全局串行。手动 Editor、交互终端直接执行 `Unity.exe`、地图验收/Build 的临时外部启动命令仍未证明全部接入。Editor 内的架构审计、地图 Build 方法本身不创建第二个 Unity 进程，但其启动入口必须继续核对。

## 非 Unity 回归

定向门禁：`python -m unittest test_unity_guard`，24/24 PASS，约 3.9 秒；原始日志 `Tools/EngineeringAssurance/.local/unity-guard-targeted-20261016.log`。工程保障全套：`python -m unittest discover -s Tools/EngineeringAssurance -p 'test_*.py'`，55/55 PASS，约 45.1 秒；原始日志 `unity-guard-regression-final-20261016.log`。DataTest PowerShell AST 解析、配置 JSON（原文件含 UTF-8 BOM）解析均通过，原始检查记录为 `datatest-static-20261016.log`。

隔离测试覆盖真实 Windows Python 活动/已退出句柄、真实无效 PID、模拟 Unity 身份与 PID 复用、访问拒绝、未知项目路径、两个启动器争用、PowerShell 与 Python 跨语言争同一 Mutex、启动器异常退出残留、两次准入间状态变化。36408 和 26772 的现场结果另由只读原生探针记录。**未启动新的真实 Unity 进程**；活跃 Unity 同项目冲突和 DataTest 登录自动流程未做真实集成测试，不能由模拟用例推定通过。

主项目的 L2 工程保障 preflight 已保存 `Tools/EngineeringAssurance/.local/unity-guard-pid-20261016-preflight.json`，当时 `architecture=BLOCKED`；它不能替代本专项门禁结果。随后 `assure.py verify` 的原始报告 `unity-guard-pid-20261016.verify.json` 为 `identity=PASS`、`scope=FAIL`、`overall=BLOCKED`。本轮 preflight 命令误把三条路径以逗号合并传给一次 `--scope`，而脚本要求每条路径分别传一次；因此四条实际治理文件变更均被标为越界，包括获用户授权的新报告。没有通过重取基线消除这条异常。主工作树存在大量他任务修改，本轮没有清理、归因或提交。

## 当前状态与人工处理

`status` 与 `diagnose` 均输出 `UNKNOWN`，Python 退出码均为 2，`UnityLockfile=ABSENT`，原因均为 PID 26772 的两轮 `OpenProcess` 错误 5。应继续阻断 Stage02 的 Unity 启动。

最小人工步骤：在能查询该进程的 Windows 会话中，以只读方式对 **PID 26772** 再运行 `unity_guard.py diagnose --project D:\UnityProjects\RPG_Demo` 和 `Tools/EngineeringAssurance/.local/pid-rights-probe-20261016.py`，核对原生创建时间、等待信号、映像及命令行。若仍拒绝访问，先由该进程的实际持有者或管理员核对身份；不要按 PID 猜测终止，也不要删除锁文件。只有现场 `FREE`、DataTest 接入版本化并经实际触发验证、后续任务再在锁内末次准入通过时，才重新评估 Stage02 是否可恢复。

参考原生 API 语义：[OpenProcess](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-openprocess)、[WaitForSingleObject](https://learn.microsoft.com/en-us/windows/win32/api/synchapi/nf-synchapi-waitforsingleobject)、[Toolhelp 快照](https://learn.microsoft.com/en-us/windows/win32/api/tlhelp32/nf-tlhelp32-createtoolhelp32snapshot)。

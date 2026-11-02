# 2026-11-02 已完成地图与背包成果发布

## 用户授权与范围

本轮授权是让拉取者获得项目最新已完成成果，未完成内容继续本地。用户已明确23个储备怪物尚未完成，不发布其素材/动画/预制体或预览场景。商店V3.5和字体升级仍独立进行。

## 已采纳的成品与必要依赖

- 已验收地图 a41/r3 的范围继续有效；b01修正普通默认与I1专用能力隔离。
- 两份背包UI采用用户已确认通过的A1最终原字节。
- c01 Host679+独立Stamp包含这两UI及4个既有审计/制作/预览工具的必要编译输入；这些工具不等同采用怪物库，不进入正式Population。
- 旧归档R3 Recipe只修正资源显示名 `Units_Remediation_R3 -> Recipe`，不改配置、GUID、MainV1/VNext内容身份；归档R3仍按原规则拒绝。
- 现有启动器支持显式相同版本Unity路径与静态版本资源校验，并随工具提供原受审TMP精确缓存资格JSON，保持原SHA与未知差异拒绝。
- Package manifest/lock、旧manifest/pins、地图算法/人口/净空、MyTMP与正式默认/旧档规则不变。必要行尾规则逐路径固定，避免检出破坏受审SHA。

## 下游运行

先读根README：恢复锁定包依赖，使用Guard/Supervisor构建I1_PRIVATE，再运行Windows exe。默认SampleScene仍MainV1；不能直接改默认场景或开全局I1编译开关。

## 验证与剩余边界

本次在独立浅克隆、无项目Library的初始条件下执行。Unity自行恢复锁定包，未从主工程复制包缓存。产品源保持已提交c01整组；下列均为本次实际结果，不借历史结果替代。验证在当前Windows机器和锁定Editor上完成，未宣称第二台实体电脑实测。

- 工具、源码副作用、封印和资格拒绝回归：66/66，通过。
- 无项目Library冷检出：21个包meta由UPM恢复且原SHA全部匹配；源文件在两次构建/运行后保持干净。
- I1_PRIVATE Build Run `3984136dbd52412ebf8746f7c2e0494c`，DEFAULT Build Run `76567a61499e4184bac76e58ee45bf43`：各errors=0/warnings=1，自然exit0、无强制回收/残留、双锁释放。已知TMP缓存字节对按原规则复核后恢复，旧失败记录不改写。
- VNext真实恢复/保存/新进程重开：Run `1038ff9b497d4f4fa263c2830367d22f`、`71149be5dad54ab394cbb617c738f844`；MainV1同流程：`7deaa03d9c924e6db4ea524f23788870`、`7167872a17c24b93b80ae6ee3f02f888`。4进程自然exit0；8份完整世界逐字节等于原基准，不排除位置/碰撞/布局字段。
- 错误入口Run `10543e235a474a8db586ff78cf0f77da`：自然exit2、原FAILED保留，在Session/Ready之前quarantine并拒绝，作为预期反例，不算恢复成功。
- 对精确成品/工具提交的Standards与Spec独立静态审阅：各0开放项；不代表全项目或新人工验收PASS。
- 正式架构由真实干净main分支源生成：2452节点/5570边，unresolved/duplicate=0；不是将工作态预览改名成正式快照。
- 产品/工具候选提交：`f23c16e95347494577c5362dbdbbe3bb19628ed0`（24精确路径）；正式架构与本报告另作纯文档提交。远端发布/CI由发布后实际回执确认，本地运行证据不替代云端CI。

历史NUnit退出TIMEOUT、导航根因UNKNOWN和原失败回执保持，不因本次发布改为已修复。正式存档原件不读写、不迁移；相同内容MainV1旧路由与VNext新空间规则继续显式区分。

本地执行证据入口（不纳入Git）：`Artifacts/AcceptedRelease-20261102/`。

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

本文件在实施阶段建立。当前发布范围已有历史c01兼容、Windows和Main审计证据；本次新工具回归、冷检出依赖恢复与实际运行结果在下方追加，未执行前保持NOT_RUN，不把历史结果充作新验证。

- 本次工具与拒绝边界回归：PENDING。
- 本次无项目Library冷检出：PENDING。
- 本次独立Standards/Spec审阅：PENDING。
- 本次实际提交/远端CI：PENDING。

历史NUnit退出TIMEOUT、导航根因UNKNOWN和原失败回执保持，不因本次发布改为已修复。正式存档原件不读写、不迁移；相同内容MainV1旧路由与VNext新空间规则继续显式区分。

本地执行证据入口（不纳入Git）：`Artifacts/AcceptedRelease-20261102/`。

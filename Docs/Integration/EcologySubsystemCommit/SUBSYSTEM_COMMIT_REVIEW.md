# 生态分布子系统精确本地提交审查

当前状态：`READY_FOR_PRECISE_LOCAL_COMMIT`。必要业务门禁已完成；本文件随该次本地提交保存，实际SHA和最终提交动作由外部 commit-receipt.json 及Git日志确认。未合入main，远端无写操作。

## 范围、来源和真正 Git 身份

原工程物理目录仍是 `feat/tower-run-save-mvp@f9577ac`，原未提交修改、未跟踪成果、正式存档和旧Build均保留；真实main仍 `d9f60d6`。来源发现已经完成，不为本次子系统重扫33副本。

生态分支从 `f9577ac65eb5d71b6a9790a6b01f456b252d5bcc` 开始，独立于暂停的全项目候选 `6ee2300`。该父提交已经提供TowerRun、Portal、UI运行基础九项历史成果，远端备份历史已含，不误记成尚未推送。生态源码历史来自原树的 `UNSTAGED_WORKTREE_CHANGE` / `UNTRACKED_FILE`；本次开始时部分等价内容已保存在全项目混合本地提交6ee中，但尚未进入main或远端。本次重新按模块精确拆分，不继承混合提交。

冻结清单原选497路径，98路径明确排除；Spec独立挑战又发现一个完全无调用/无serialized引用的Stage1脚部适配器，进一步移出其cs/meta和新清单2pin。最终真实提交文件数与全部SHA/GUID由 FILE_AND_GUID_PROVENANCE.tsv 和提交后 Evidence/commit-receipt.json 确认，不把rename展示数混成文件数。

保留：不可变布局/Plan、普通/中立/安全区与缓冲、商人资格、构造容量/群体最低值/预算、12种原生实例及动画/对象池、Registry/目标、Home/追击/有限越界/返回、安全保护和延期伤害、配方/内容身份、实际换层/入口恢复及直接依赖。Boss只保留规划合同和内容待补标记，不宣称已有Boss资源。

SurfaceV4高程/碰撞/楼梯/根部几何是当前配方的直接运行依赖。MapStage1ProjectionPlan与SouthStairRequest被共享renderer/projector/TerrainTopology直接引用，是编译依赖，非默认/非Stage1验收。MapFootColliderCandidate无调用者，仅被清单误引用，已移出。旧Smoke/RuntimeProbe、Editor生成工具、无关Stage1诊断、HUD坐标改动、临时场景、历史Build/日志/大证据、DataTest和260退役购买资源不纳入运行提交。

## 必须修正的问题及反例

1. 旧M00149/50的池用例属于本子系统：硬编码旧R4内容清单在激活前被RecipeDefinition字节变化拒绝。当前测试选择MainV1，保留12类型/24次真实activation等断言；新分支E001真实PASS。原M001仍FAIL，未改写原XML。
2. 死亡恢复：原顺序先BindMotion后应用完整checkpoint，死亡谓词导致租约无效。真实C001/C001D红灯后，改为原全部finalizers（完整stats/inventory/shops恢复及死亡同步）→生态绑定→Ready；E001死后新冒险和两次恢复通过。没有只补血或放宽solid/live合同。
3. 部分Configure失败：只凭匹配combat barrier证明同scope未完成配置；open scope/错误owner继续拒绝，并有正负例。Dispose finally销毁holder但不吞异常。
4. 内容身份：新清单工作字节、Git blob、filtered checkout必须相同，显式 -text 固定。旧R3 `49be1618…` / R4 `0c4870c0…` 原字节保留，不改旧身份。当前新内容 `content-r1:c55fb3b4b0362e8bb59e84f4624fbb22c2f9ef96f3dcd5f61481a92dab9c9802`。
5. 楼梯测试场景残留：真实场景用例后，物理夹具还会撞别的地图；夹具现先建立自己的空场景并卸载旧SampleScene，保留原尺寸、碰撞和32通行案例；E001通过。
6. 随机floor3有限拒绝：保持每区≥3/预算/安全商人等要求。固定 -337455008/floor3真实失败→原Retry→Ready及计划=实例=Registry=Target通过。原通路重试测试只登记精确预期拒绝日志，不屏蔽未知错误、不替换漂亮Seed。
7. E002新发现：两旧重试fixture的22041322已不失败，改用E001真实失败输入；一个legacy生态-v1 fixture继承新版精确entry，现明确旧schema1/gen1/config/无ecology tuple；四原native exporter缺本轮label/真实manifest接线，现提供已有assure真实fingerprint及唯一输出label。原7FAIL保留；E003断言通过但cleanup forced，整体FAIL，详见最终表。

## 验证、独立审阅和边界

|证据|当前真实状态|用途|
|---|---|---|
|静态scanner|初次1639 nodes/3468 edges/0 unresolved；最终见ArchitectureFinal，GUID重复0，260退役文件均0|只证明静态闭包，不冒充运行|
|E001 b9849685395e452bb6adf509d2698d82|15/15，自然0，cleanup无force/残留，双锁释放|死亡/池/32楼梯/人口重试/精确入口/正式槽克隆|
|E002 d2733cce8ff64c7d99d3d29e4c16f296|95PASS/7FAIL/16既有Skip；自然2、子进程cleanup forced|真实FAIL保留，只有受影响项复验|
|E003|10/10断言，root自然0但cleanup forced，整体FAIL|7项修复定向复验|
|标准轴初审|0硬违反/0有充分依据smell|独立报告不与规格轴混排|
|规格轴初审|1 P2孤立脚部适配器，已修正，最终复核关闭|hash引用不能代替运行依赖|

REUSED_VALID_EVIDENCE只可按实际依赖SHA复用；源码/资源变动影响的用例必须重跑。E001/E002已通过且相关依赖未变的案例按逐文件核对保存；不把整套E002写成PASS。原有Ignore未新增或借机扩大。正式保存目录仍tower-run-slots/save.json，测试只写隔离根，原三槽/两侧档最终SHA需读回。runInBackground测试副作用只在精确SHA确认后恢复原0，不提交项目设置。

本任务不产生Windows Build/Player或新人工PASS，不完成B1/T13/全Seed/长期性能/Boss内容。全项目Build与main收敛等待恢复授权。源码静态架构已有核查；通用L2包装因新树未复制工具返回UNKNOWN，保留原收据，不冒称总体assure PASS。

## 恢复与回退

来源和暂停候选不改，不用reset/clean/stash/强制覆盖。独立提交后保留本分支及精确清单；撤销只能另行正常反向提交/新分支选择，不能覆盖源现场。恢复全项目审计须用户明确授权，先读MainConsolidation/HANDOFF和本任务commit receipt，再比较main/两候选真实差异，按来源与内容身份消除重复，不盲目merge或cherry-pick已等价引入的文件。

## 最终门禁和独立审阅补充

最终新内容 **content-r1:c55fb3b4b0362e8bb59e84f4624fbb22c2f9ef96f3dcd5f61481a92dab9c9802**。502个运行/测试/入口/字节策略精确路径，包含4个原路径删除（同GUID移动）；文档及出处清单另计，最终总数以commit-receipt.json为准。Runtime没有外部代码依赖；本地历史证据和原始问题答复保存在原工程及私有归档，不把这些大包放入Git。

|运行|Run ID|XML通过/总数及失败/跳过|naturalExit/exitCode/forced|整体状态|
|---|---|---|---|---|
|E001|b9849685395e452bb6adf509d2698d82|15/15，FAIL=0，SKIP=0|True/0/False|COMPLETED|
|E002|d2733cce8ff64c7d99d3d29e4c16f296|95/118，FAIL=7，SKIP=16|True/2/True|FAILED|
|E003|1cbcee2b86cf4cd393bcfc67d40d2238|10/10，FAIL=0，SKIP=0|True/0/True|FAILED|
|E004|90ea309914d14c72a96f9c0c2c33d5f4|15/15，FAIL=0，SKIP=0|False/3221225786/True|TIMEOUT|
|E005|cf2e693f70d74d6290f84a0d0d66237e|1/1，FAIL=0，SKIP=0|True/0/False|COMPLETED|
|E006|d0a290ae6aaa4ecd8dc2c68eaccb2075|9/10，FAIL=1，SKIP=0|True/2/False|FAILED|
|E007|18372a68b8f5425cb8c6a6a700afe1c0|27/27，FAIL=0，SKIP=0|True/0/False|COMPLETED|
|E008|fe050e01a3c8436b9c9f88544dd60205|1/1，FAIL=0，SKIP=0|True/0/False|COMPLETED|
|E009|0190875992ab4ecc99fb505ab6bf6d88|8/9，FAIL=1，SKIP=0|True/2/False|FAILED|
|E010|a1800436cad04393a76c5624b697070d|13/13，FAIL=0，SKIP=0|True/0/False|COMPLETED|

E010的全部Assets/Packages/ProjectSettings启动前输入与最终工作字节逐SHA相同（精确恢复任务自有runInBackground副作用后）。E003至E009只差TowerRunFlowPlayModeTests.cs一个测试文件：E009捕获死亡后新冒险随机有限容量拒绝，新增仅在实际观察到该拒绝时才使用原Retry的夹具接线，最多3次，不改生产或人口规则。E010重新验证该文件涉及的五个必要入口用例及8个最终身份/正式槽/原生池用例，见 final-input-bindings.json。E002两个测试源文件变化所涉及的全部当前用例分别在E003/E007复验；未重跑已通过的64seed corpus。E001楼梯等未变组件证据按依赖SHA复用；E002整轮不改称PASS。E006 reader误与其它场景测试混跑导致StartupChoice前提污染，E008独占新进程1/1修正该运行方式；没有改生产状态绕过断言。E009保留8PASS/1FAIL；上述夹具修正后E010 13/13收束最终内容、真实正式槽clone、原M001池、归档拒绝、死亡重启及五个必要入口门禁。

两份64seed报告均62接受/2有限拒绝/0Boss，范围是纯规划采样，不是64张完整实机或全Seed保证。原始代码15个retired Ignore保留，未新增Ignore；旧默认档测试原来缺fixture跳过，E007使用不可变正式槽副本实际通过。

使用原Supervisor公开execute及其合法treeExitGraceSeconds=30，原Guard/Mutex/Job/Unity命令校验/清理全部保持。只读owned-member观察在成功run记录到Unity.Licensing.Client、conhost及短命cmd/taskkill等自有子进程自然结束，未向任何未知PID发终止命令。E002/E003旧事件没有具体成员身份，原因不反向猜成已证实。共享Guard/Supervisor原文件未改。

**未解决但不阻止这次隔离本地保存：**E004纯EditMode断言15/15，Editor240秒退出TIMEOUT，原因未定位，不冒充生命周期PASS。本地保存的必要生态业务闭包由上述真实PlayMode及独立写读门禁承担；这不解除后续main/Build交付的退出和运行器风险，也不声称所有自动作业全绿。通用L2包装UNKNOWN保留，实际静态scanner/GUID闭包另有真实收据。

Standards轴：初审及最终delta均0硬违反/0有充分依据smell。Spec轴：初审1项P2（无caller的MapFootColliderCandidate），已移出两文件及两新pin；最终delta新增0且源码关闭。两轴报告分别保留，不混排。当前没有未解决的生态业务断言FAIL；单独保留工具生命周期限制和历史高风险。

恢复全项目审计必须先读暂停HANDOFF及本次receipt，做两候选来源/语义去重。本分支不继承混合6ee提交；不要在原脏物理目录checkout main，不自动merge/cherry-pick重复资产，不重放旧脚本或旧16档马拉松。

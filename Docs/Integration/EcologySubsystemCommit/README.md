# 生态分布子系统独立本地提交

本任务仅评估并保存生态分布子系统及其真实依赖。全项目整合保持 `PAUSED_FOR_ECOLOGY_SUBSYSTEM_COMMIT_REVIEW`，不推进本地 `main`，不写远端，不重开旧 R3/R4 兼容流水线。

- 当前实现分支：`codex/ecology-subsystem-20261024`。
- 独立工作树：`D:/.codex/worktrees/ecology-subsystem/RPG_Demo`。
- 起始/父基线：`f9577ac65eb5d71b6a9790a6b01f456b252d5bcc`，已有 TowerRun/Portal/UI 九项历史提交作为运行依赖。它们已在远端备份历史，不误记为本次新提交或此前未推送。
- 真正 `main` 保持 `d9f60d608ea617dadb0ef4ce60553f7b306a0b94`。
- 已暂停的全项目候选：`codex/main-consolidation-20261024@6ee23005edf8ded471e5d5e4b2e69b76f45fd194`，与本分支分开保留。不能把该大范围提交当作本次精确生态提交。

当前真实门禁、提交结果和恢复步骤见 [提交审查报告](SUBSYSTEM_COMMIT_REVIEW.md) 与 [HANDOFF](HANDOFF.md)。文件级提取及排除：`Evidence/scope-freeze.json`；精确最终清单由 `FILE_AND_GUID_PROVENANCE.tsv` 和提交后 `Evidence/commit-receipt.json` 绑定。

运行链为：`GameSession → TowerRunCoordinator → MapGenerationController → 地形/候选几何 → EcologyJointPlacementPlanner → 不可变生态布局及原 PlacementPlan → MapEcologyPopulationService → 原生 Prefab/Pool → Registry → 原 Restore/目标 finalizers → EcologyMapRuntime → Ready/GameplayReady`。领地、最低群体、人口预算、商人/休息安全区、实际脚部保护、追击及返回都读同一代计划。MapData 仍是高程与楼梯权威。

SurfaceV4 配方直接使用高程碰撞/楼梯资格、静态物体及根部几何；这些共享依赖需要保留。`MapStage1ProjectionPlan` 与 `MapStage1SouthStairProjectionRequest` 是现有共享 renderer/projector 的编译依赖，只保留非默认适配，不代表 Stage1 接受或阶段推进。无调用者的脚部实验适配器、旧自动验收 Smoke/Probe、Editor 生成工具、无关诊断、HUD 位置调整及历史大证据不属于本次运行提交。

既定玩法决策仍以 `RPG-Map-Generation.md` 和 `Docs/MapGeneration/Ecology` 原始权威文档为准；旧人工 PASS 保持其原构建和输入范围。B1/T13 `HIGH_RISK`、Boss `CONTENT_PENDING`、全 Seed 和长期实机表现未验证等边界保留。本次本地开发提交不等于发布或新 Main 人工验收。

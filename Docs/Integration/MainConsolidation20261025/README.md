# 本地主线整合 2026-10-25

状态：独立候选验证中，本地 main 尚未推进；远端 main 不推送。

游戏来源为已保存生态 `96371eb36a787585cf5e70141c03555419821058` 与九个父提交。旧混合 `6ee23005` 仅抽取经独立双轴审阅的工具、文档及显式启用的 QA 闭包；不复制旧玩法变体。DataTest V2 保持外部，V1 已取代，购买怪物和 Stage1 实验不回流。受保护 Settings、HUD 偏移保持原提交。

默认 Profile 的实际标识符以 `EcologyRecipeDefinition.MainV1ProfileId` 为准。十二原生怪物、场景兼容旧 Torch、严格 TRB2 身份和有限重试保留；Boss 仍缺。Stage1 未关闭，B1 高风险不因本轮整合改写。

## 检出字节契约

从旧 main 检出后快进到生态时，654 个 blob 未变化的路径仍是 CRLF，而新 `.gitattributes` 要求 LF。1220 项清单中654项失败；Git blob、`git cat-file --filters HEAD:<path>`、原生态工作树均匹配预期。本轮仅将独立工作树654路径物化为经过逐项 SHA 验证的预期字节，清单和 content-r1 身份不变。建议直接从生态/最终提交创建新工作树，或在任何 Unity 启动前运行精确清单检查；Git status 无实际 diff 不能替代字节验证。

首轮 I01：31断言13通过18失败，均为同一字节契约首项失败；自然退出2，cleanup PASS，forced=false。失败证据保留。Unity 引起的 `runInBackground` 副作用按提交内容恢复。后续结果以本轮收据为准。

## 工具与文档采用

采用的原 Guard/Supervisor 与共享源同 blob，唯一进程/锁权威保持。Hook 文件随工具保存为可执行模式，未执行安装脚本；此仓库现有 core.hooksPath 为 `.githooks`，本轮 Git 写入用命令级隔离 Hook 目录，未改变持久配置。自动图谱写入行为见 AUTO-UPDATE；Obsidian 镜像按明确目标及 SHA 清单处理。历史架构/面试资料附当前上下文，生成图谱从实际候选树重建，静态图谱不代表运行验收。

## 本轮证据

实际原始证据根：`Artifacts/MapAcceptance/MainConsolidation-20261025/`。测试断言、进程自然退出、强制清理、锁释放、输入变动、Build、Player、人工验收分别记录。当前尚无本轮人工通过。最终汇总在收尾后更新。

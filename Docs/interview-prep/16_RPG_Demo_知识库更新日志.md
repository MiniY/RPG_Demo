> 本轮整合上下文（2026-10-25）：本文原有叙述与验收数字保留为各节标明日期的历史快照。游戏主线已在候选提交 `96371eb36a787585cf5e70141c03555419821058` 保存，其父链包含九个 TowerRun/Portal/UI 提交；默认新冒险 Profile 为 `MainV1`，十二种保留原生怪物，Boss 仍待内容。旧 `ecology-v1`、UI v6 未提交、旧工作树/HEAD 等现在式叙述均不代表本轮状态。新整合 Build/Player 与人工验收只认本轮独立收据；Stage1、B1、Boss 和旧 R4 人工结论不升级。当前整合总账见 `../Integration/MainConsolidation20261025/README.md`。

# RPG_Demo 知识库更新日志

## 2026-10-12：首次系统建立

### 证据快照

- 分支：`feat/tower-run-save-mvp`
- HEAD：`7da9a7dc80733bdb35c6145bc244496589fa00e0`
- 工作树：已有未提交设置 UI v4 变更

### 新增

- 导航、项目总览、开发时间线和系统架构。
- 随机地图、战斗/AI、背包/商店、TowerRun 四组核心系统解析。
- 11 个真实问题/故障案例。
- 工程决策、面试问答、压力面试和 STAR。
- 优化路线、限时学习计划、AI 实践、术语、未来设计。
- Git/历史研究底稿与系统级证据索引。

### 重要口径

- `GameSaveService` 当前为进程内镜像，不是旧磁盘存档。
- TowerRun 保存楼层入口检查点。
- 随机地图已经整合 `SampleScene`；旧“尚未整合”文档属于历史。
- 设置 UI v4 尚未提交和最终视觉批准。
- 本次没有运行新的 Unity 全量测试，沿用的结果都标注了时点和限制。
- 大量代码由 Codex 辅助，不把仓库代码量视为个人手写或掌握证据。

## 后续记录格式

```markdown
## YYYY-MM-DD：变更主题

### 项目快照
- 分支/HEAD：
- 工作树：

### 新证据
- 源码/提交/测试/人工验收：

### 更新文档
- 文档：
- 原结论：
- 新结论：

### 保留的历史
- 哪些旧结论只在原时点成立：

### 未解决
- 证据缺口：
```

维护时只增量更新受影响文档，不整库重写；事实变化不删除历史时点。

## 2026-10-12：第一版定向质量抽检与 Obsidian 同步机制

### 新证据

- 逐项对照当前源码核验 04A～04D 的调用链、状态所有者、状态变化、失败语义和代码位置。
- 复核 Git 提交与现有 XML/日志；没有重新运行 Unity 全量测试。

### 定向修订

- 区分“Commit 前旧 Active 数据未被替换”和“TowerRun Gate 下玩家仍可继续游玩”。
- 更正玩家攻击没有独立 cooldown、命中时不复核距离的边界；与怪物攻击命中复核分开。
- 更正 `RewardSO.EnsureRewardId()` 并非严格只在编辑器调用。
- 更正 `TowerRunGateHub.cs` 位置、new-run baseline 捕获语义和 `File.Replace` 不支持时不会静默降级。
- 更正 `e7aff9f` 的历史证据范围，并降低无法由仓库证明的个人实现归属表述。
- 将“当前设置 UI v4”更新为“v4 历史阶段继续收口到未提交 v6”：采用 v6 的 6/6 回归与三分辨率截图证据，同时继续保留最终视觉确认/提交授权缺口。

### 维护机制

- 权威源：`Docs/interview-prep/`。
- Obsidian 镜像：`D:\Obsidian-1.9.14\MyObjects\Unity\RPG_Demo文档\面试复盘文档`。
- 使用 `Tools/Sync-InterviewPrepToObsidian.ps1` 做带 SHA-256 清单与冲突保护的单向增量同步。

## 2026-10-14：Monster 动画与生态 Population 增量

### 新证据

- 12 个 Monster Prefab 保持原继承和 GUID；11 个新增动画 Override 对应各类型/颜色 Sprite。Unity 动画事件测试 1/1 PASS。
- 新 `ecology-v1` 使用同一个地图放置计划和 Registry 生成 2–5 个 Monster、4–8 个三类 Mushroom；固定五 Seed PlayMode 1/1 PASS。
- Tower Run 夹具 9/9、旧地图夹具 13/13、旧奖励夹具 5/5 PASS，runner 均 exit 0。
- 最终地图 PlayMode 程序集 30 项中 29 PASS、0 FAIL、1 Skip，runner exit 0；被跳过的外部旧槽位测试已单独在隔离副本执行。
- 正式旧 `default` 槽位只读副本严格 TRB2 恢复 1/1 PASS，源文件哈希不变；该 runner 关闭停滞后被定向结束，不能记正常 exit 0。

### 定向修订与边界

- 04A 增加区域查询、配置、上限、局部聚集、确定性和位置合法性；04B 更正 Torch_Blue Prefab 路径并增加 12 种 Visual/逻辑交互；04D 增加新旧 Profile 与 TRB2 兼容。
- 人工屏幕视觉、真实 Collider 边缘体验、长期性能和独立 Player Build 尚未验收；TNT/Barrel 爆炸、独立 Hurt/Death 帧及 Merchant Population 未实现。

### 2026-10-14：Monster 人工验收反馈的定向修复

- 04B 更新 Barrel 攻击第 5 行四帧、12 个 Monster 基础伤害 10→5，以及 `PlayerHurtController` 对 Monster 攻击红闪而不击退的真实调用链；01、03、07、15 同步修正原先将玩家所有受击都描述为击退的文字。
- `.codex/TestResults/monster-acceptance-expanded-20261014.xml` 的针对性 PlayMode 5/5 PASS；人工 Game 视图观感与手感仍待用户复验，不能标作 PASS。

### 2026-10-14：旧场景 Monster 目标兼容修复与最终回归

- 完整回归揭露 `SampleScene.unity` 的历史 `Monsters/Torch_Blue` 场景实例和 `monsterTarget` 引用曾在迁移中丢失。对照 HEAD 精确恢复原父节点、Prefab 实例、fileID 和引用；只额外增加旧 Torch 初始停用 Override，确保 ecology-v1 提交前也不参与 AI、画面和 Registry。`default` 恢复时仍使用原场景实例。未修改受保护地图配置和 TRB2 格式。
- 原三项 Bugfix 在场景恢复前已独立 5/5 PASS。修复后完整 PlayMode `.codex/TestResults/monster-acceptance-final-full-r5-20261014.xml` 36/36 PASS、0 Skip，包含隔离副本的旧 TRB2 槽位恢复、生态、出口和换层回归。受控 Unity 进程已退出，数值退出码未捕获；人工视觉、手感、长期性能和独立 Player Build 仍未验证。

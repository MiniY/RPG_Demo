> 本轮整合上下文（2026-10-25）：本文原有叙述与验收数字保留为各节标明日期的历史快照。游戏主线已在候选提交 `96371eb36a787585cf5e70141c03555419821058` 保存，其父链包含九个 TowerRun/Portal/UI 提交；默认新冒险 Profile 为 `MainV1`，十二种保留原生怪物，Boss 仍待内容。旧 `ecology-v1`、UI v6 未提交、旧工作树/HEAD 等现在式叙述均不代表本轮状态。新整合 Build/Player 与人工验收只认本轮独立收据；Stage1、B1、Boss 和旧 R4 人工结论不升级。当前整合总账见 `../Integration/MainConsolidation20261025/README.md`。

# RPG_Demo 代码学习与掌握计划

> 冲刺窗口：2026-10-12 ～ 2026-10-17。  
> 本计划不自动记录“已掌握”。每项只有在实际讲解、练习、修改、排错或迁移后，才能形成能力证据。

## 1. 掌握标准

### L0：见过

能认出类名和大致用途。

### L1：能导航

能从入口定位到状态所有者和主要协作者。

### L2：能解释

能不看文档讲清正常路径、失败路径和关键不变量。

### L3：能修改

能先写/调整测试，再完成小功能或修复，不破坏边界。

### L4：理解设计

能分析取舍、边界、性能和替代方案。

### L5：能迁移

能在新场景中独立设计类似系统，并用测试证明关键不变量。

这些是评估标准，不是当前分数。本文件不预先认定任何一项已达到某级。

## 1.1 求职前优先级

### 必须掌握

1. 项目范围、AI 分工和证据边界。
2. 地图从 request 到 GameplayReady。
3. `MapData`、Plan/Commit、GenerationId。
4. TowerRun 状态机与入口检查点。
5. 背包/商店事务回滚。
6. 迷雾时序 Bug 与测试证据。
7. Monster FSM、玩家/世界伤害链。
8. EditMode/PlayMode/GPU/跨进程如何分工。

### 时间允许再学习

1. 每个噪声阈值和全部地形细节。
2. 所有 UI 运行时构建字段。
3. Shader 数学细节。
4. 每个历史提交的逐文件 diff。
5. 未实现职业/装备/任务的完整设计。

## 1.2 主要系统能力自测标准

| 系统 | L1 知道功能 | L2 理解流程 | L3 能维护 | L4 理解设计 | L5 能迁移 |
|---|---|---|---|---|---|
| 随机地图 | 知道 Seed 生成地图 | 能讲 Generate→Plan→Ready | 能加放置规则/修回归 | 能分析 Commit、Authority、性能 | 能为新项目设计可提交生成流水线 |
| 战斗/AI | 知道移动、攻击、FSM | 能走两条伤害链 | 能改状态转移/命中测试 | 能比较 FSM、行为树、寻路方案 | 能设计另一类敌人并验证 |
| 背包/商店 | 知道拾取和购买 | 能讲奖励到 UI、交易回滚 | 能加边界或新商品 | 能分析身份、事务和 UI 投影 | 能设计另一套本地经济事务 |
| TowerRun/存档 | 知道新开/恢复/切层 | 能讲 staged restore 与 Slot 写入 | 能加校验/故障测试 | 能解释 checkpoint、uncertain、迁移 | 能为新流程定义恢复边界 |
| 测试/调试 | 知道测试类型 | 能选正确测试层 | 能写回归和定位失败 | 能分析环境、flaky、证据限制 | 能建立新项目验证策略 |

每次自评必须附一项实际证据；没有证据时保持原级。

## 2. 每日统一流程

1. 10 分钟闭卷画链路。
2. 20 分钟读入口与状态所有者。
3. 20 分钟沿一个具体数据走到终点。
4. 20 分钟做一个预测/故障问题。
5. 30～60 分钟完成一个小练习。
6. 10 分钟用面试语言复述。
7. 记录证据：答案、diff、测试或调试日志。

## 3. 10 月 12 日：项目地图与证据边界

### 学习目标

- 准确区分当前 HEAD、工作树和历史分支。
- 能讲项目核心循环与当前能力边界。
- 能解释 `GameSaveService` 与 `TowerRunSlotStore` 差异。

### 阅读

- [[00_README_面试知识库导航]]
- [[01_RPG_Demo_项目全景与总体介绍]]
- [[15_RPG_Demo_证据索引与待核实问题]]
- `GameSession.cs`
- `GameSaveService.cs`
- `TowerRun/TowerRunSlotStore.cs`

### 实练

闭卷回答：

1. 从启动到 GameplayReady 涉及哪些所有者？
2. 当前哪些内容尚未提交？
3. 哪些测试数字对应 `c4cb850`，哪些对应 `7da9a7d`？

### 验收

录制或写出 90 秒介绍，必须包含一个局限和 AI 边界。

## 4. 10 月 13 日：随机地图与整合

### 学习目标

- 从 request 追到 Ready。
- 理解 Plan/Commit/Project。
- 能解释 GenerationId 与 Authority。

### 阅读链路

1. `MapGenerationController.TryGenerateMap`
2. `MapGenerationController.GenerateMap`
3. `MapIntegrationOrchestrator.Execute`
4. `RandomMapGenerator.Generate`
5. `MapPlacementPlanning`
6. 一个具体 `MonsterPlacementService`
7. `MapObjectRegistry`

### 实练 A

对一个固定 Seed 画出每个 phase 的输入/输出，指出 Commit 前后失败差异。

### 实练 B

设计规则：“出口离出生最短路径至少 40 步”。写出：

- 规则所属模块；
- 需要的数据；
- 失败原因；
- EditMode 测试；
- PlayMode 验证。

### 验收

不看源码回答 [[04A_随机地图生成与运行时集成]] 最后的四题。

## 5. 10 月 14 日：战斗、AI、对象池与奖励

### 学习目标

- 从按键讲到奖励进入背包。
- 从 Monster 感知讲到玩家死亡。
- 理解池化重置和旧 Generation 风险。

### 阅读链路

- `GameInput`
- `PlayerAction.HandleBattlePressed`
- `BaseDamageable.TakeDamage`
- `ObjectPoolManager`
- `RewardPickup`
- `PlayerInventory`
- `MonsterAIController`
- `MonsterPerceptionController`
- `MonsterAttackController`
- `PlayerDamageController`

### 实练

任选其一：

1. 写一个“对象重复启用后不会重复掉落”的测试设计。
2. 设计 `IDamageReceiver`，但不修改生产代码；列出迁移步骤。
3. 用调试日志证明一次攻击在前摇期间目标失效时不会错误命中。

### 验收

闭卷画出两条相反伤害链路，并解释为什么接收者当前分裂。

## 6. 10 月 15 日：背包、商店与事务

### 学习目标

- 明确静态配置、运行时状态、内存镜像和磁盘记录。
- 能解释一次购买的原子边界。
- 能识别 UI 是投影而非状态源。

### 阅读链路

- `RewardSO`
- `PlayerInventory.TryAdd/TryRemove`
- `ShopTradeController.TryPurchase`
- `PackageGridUI.Refresh`
- `TowerRunRuntimeStateBridge.TryCapture/TryApply`

### 实练

手工构造四个失败：

- 金币不足；
- 库存不足；
- 背包数量溢出；
- 提交失败。

对每个写出操作前后金币、商品、库存状态。

### 扩展

设计真正逐物品未读红点的数据模型，但不声称已实现。

## 7. 10 月 16 日：TowerRun 与文件持久化

### 学习目标

- 讲清 New/Restore/Death/Transition。
- 理解入口检查点。
- 理解 temp/replace/backup/hash/uncertain。

### 阅读链路

- `TowerRunCoordinator`
- `TowerFloorRuntimeController`
- `TowerRunRestoreFinalizer`
- `TowerRunRuntimeStateBridge`
- `TowerRunSlotStore.ReadSlot/TryCommit`
- `TowerRunGate/TowerRunGateHub`

### 实练 A：故障矩阵

| 崩溃点 | 正式文件 | temp | backup | 下次应怎样分类 |
|---|---|---|---|---|
| 写 temp 前 |  |  |  |  |
| 写 temp 中 |  |  |  |  |
| replace 后、复读前 |  |  |  |  |
| 正式复读不一致 |  |  |  |  |

学习者自行填写后，再与源码核对。

### 实练 B：口头解释

为什么 Hash 不是防作弊？为什么相同 Seed 仍需 Generator Version？

## 8. 10 月 17 日：综合模拟面试

### 第一轮：项目介绍

- 30 秒；
- 90 秒；
- 5 分钟。

三版都不能夸大。

### 第二轮：随机抽题

从 [[07_RPG_Demo_技术面试问答大全]] 抽 10 题，每题 2 分钟。

### 第三轮：压力追问

从 [[08_RPG_Demo_技术追问与压力面试]] 抽 5 题。

### 第四轮：现场修改设计

题目三选一：

1. 新增“精英怪必须离出生 20～50 步且与商人不冲突”。
2. 为存档增加 `buildVersion` 兼容提示。
3. 包裹改成复用 Slot，不改变现有显示排序。

要求先说不变量、测试和回滚，再写代码。

### 最终产出

- 一份 90 秒讲稿；
- 两个能真实使用的 STAR；
- 一张架构图；
- 一个实际小改动/测试证据；
- 一份“我仍不知道什么”的清单。

## 9. 间隔复习

若面试日期晚于 10 月 17 日：

- D+1：随机地图 + TowerRun。
- D+3：战斗/商店 + 两个故障案例。
- D+7：完整模拟面试。
- 面试前一天：只看导航、证据边界和短讲稿，不再大规模新增内容。

## 10. 严格评分表

每题 0～4：

| 分数 | 证据 |
|---|---|
| 0 | 不知道或结论错误 |
| 1 | 能背定义，无法联系项目 |
| 2 | 能指出项目入口和正常链路 |
| 3 | 能解释失败路径、取舍和验证 |
| 4 | 能现场修改/迁移并保持不变量 |

如果结论正确但原因错误，最高 1 分。  
如果术语混用导致机制错误，不能判为掌握。  
如果答案依赖打开文档逐句读，不算闭卷能力证据。


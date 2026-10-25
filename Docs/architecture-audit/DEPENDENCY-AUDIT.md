> 本轮整合上下文（2026-10-25）：本文原有叙述与验收数字保留为各节标明日期的历史快照。游戏主线已在候选提交 `96371eb36a787585cf5e70141c03555419821058` 保存，其父链包含九个 TowerRun/Portal/UI 提交；默认新冒险 Profile 为 `MainV1`，十二种保留原生怪物，Boss 仍待内容。旧 `ecology-v1`、UI v6 未提交、旧工作树/HEAD 等现在式叙述均不代表本轮状态。新整合 Build/Player 与人工验收只认本轮独立收据；Stage1、B1、Boss 和旧 R4 人工结论不升级。当前整合总账见 `../Integration/MainConsolidation20261025/README.md`。

# 依赖与连接审计

## 证据层级

| 关系类型 | 扫描方式 | 能证明 | 不能证明 |
|---|---|---|---|
| `has Component`、`contains child`、`serialized GUID reference`、`prefab source` | 直接解析 Scene/Prefab/`.meta` | 文件中确有挂载、层级或资产 GUID | Unity 导入成功、Prefab Override 合成结果、运行时激活 |
| `UnityEvent calls ...` | 同一序列化文件中的 `m_Target` fileID 和 `m_MethodName` | 持久化绑定目标与方法名文本存在 | 方法签名兼容、Play Mode 一定触发 |
| `source confirmed; runtime unverified` | `curated-relations.json` 的行号与代码片段重扫时逐行校验 | 这条调用/订阅在当前源码中存在 | 条件在实际玩法下满足 |
| `textual only` | `GetComponent<T>`、`new/typeof T`、`T.Instance` 的有限词法规则 | C# 文件有相关类型表达式 | 完整调用图、反射/多态实际目标 |

【已验证事实】项目 asmdef 方向是 `RPG_Demo.TowerRun → RPG_Demo.MapGeneration`；后者显式引用 `Unity.UGUI`、`Cinemachine`；默认 Assembly-CSharp 通过 `autoReferenced` 使用这两个自定义程序集（`Assets/Scripts/TowerRun/RPG_Demo.TowerRun.asmdef`、`Assets/Scripts/MapGeneration/RPG_Demo.MapGeneration.asmdef`）。未见这两个 asmdef 互相引用的编译循环。`TowerRunCoordinator` 收到 `GameplayReady` 与 `ExitUseRequested` 属于运行时事件回调，不能误报为程序集循环。

## 高影响节点

1. `MapGenerationController.GenerateMap` 同时协调准备、规划、投影、五类对象物化、玩家定位、重初始化、相机及 finalizer（`MapGenerationController.cs:35-345`）。改顺序要回归新游戏、恢复、换层三条路径。
2. `TowerRunCoordinator` 持有地图、出口、属性、背包、商店、玩家受击与正式槽位，入口由 `BindScene` 的场景查找建立（`TowerRunCoordinator.cs:97-119`）。其事件订阅在 `UnbindScene` 解除（`TowerRunCoordinator.cs:533-547`）。
3. `BaseDamageable` 是 Monster、Animal、Plant 和可摧毁物共同受击/奖励/回收逻辑的基础类（`BaseDamageable.cs:78-104,121-158`）；修改死亡顺序会影响楼层目标计数与奖励生命周期。
4. `PlayerInventory` 的 `OnInventoryChanged` 同时驱动背包格、金币 HUD、红点等（`PlayerInventory.cs:217,253`、`PackageGridUI.cs:61`、`CurrencyHudUI.cs:41`、`PackageHudButtonUI.cs:107`）。事件必须在数据提交后触发。

## Unity 资产检查

【已验证事实】扫描器依据本机 `Assets/*.meta` 建立 GUID 表，并用 `Library/PackageCache` 区分 UGUI、TMP 等包脚本；`000...e/f...` 内建 GUID 单独计数。**当前静态扫描没有未解析 GUID，不等于 Unity Editor 已确认没有 Missing Reference。** 当前四条场景持久化 UnityEvent 目标 fileID 均能在同一场景找到对应组件，方法名分别是 `HandleWeaponCategoryToggleChanged`、`HandleFoodCategoryToggleChanged`、`CloseShopPanel`、`HandleSkillCategoryToggleChanged`（`Assets/Scenes/SampleScene.unity`，具体行见图谱）。

【待确认】普通序列化 `fileID` 指向的对象是否在 Prefab Variant/Nested Prefab 合成后仍有效；Inspector 中 `monsterTarget`、Portal Prefab、Camera、UI 字段是否和运行时期望一致；Animator Controller 参数/状态和 Animation Event 是否与脚本匹配。这些需要 Unity `AssetDatabase` / `SerializedObject` 检查或 Play Mode。不能把 `fileID: 0` 一概视为缺失，因为不少字段是可选或由 `Awake` 填充。

## 发现与解释

- **场景绑定不是生成新 Prefab**：Monster/Animal/Plant 的 reservation 是 `SceneBound`，Destructible 的 reservation 是 `GenerationScoped`（`MonsterPlacementService.cs:71-76`、`AnimalPlacementService.cs:101`、`PlantPlacementService.cs:127`、`DestructiblePlacementService.cs:127`）。`MapGenerationController` 对前者重新定位、后者从池取得实例。
- **掉落生命周期跨模块**：`BaseDamageable` 生成奖励后，`GenerationScopedRewardLifetime` 将其绑定当前 RandomGenerated controller，以便新代提交时清理未拾取对象（`BaseDamageable.cs:146-177`）。这条依赖通过运行时 `FindObjectsOfType<MapGenerationController>` 寻找唯一 authority，图中应标为动态，而不是 Inspector 静态引用。
- **无用资产判定保守**：当前仅能列“未在静态 GUID/C# 规则中出现”的候选，不能证明未使用；Resource 路径、反射、运行时生成、Animation Event 和 Unity 包可能绕过当前索引。本版不自动删除任何脚本或资产。

## 复核方式

运行 `python Tools/ArchitectureAudit/scan.py --check`，打开 `viewer.html` 搜索对象或脚本，点击“上游 / 引用者”“下游 / 依赖”中的路径行号。改动前再用 Unity Editor 检查目标 Prefab 的 Inspector 和实际运行时对象身份；图谱关系数量以 `graph.json` 本次快照为准。
